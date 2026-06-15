using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NotepadMinus.Config;
using NotepadMinus.Models;
using NotepadMinus.Services;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;
using MenuItem = System.Windows.Controls.MenuItem;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using System.Windows.Documents;

namespace NotepadMinus;

public partial class MainWindow : FluentWindow
{
    private readonly AppConfig _config;
    private NoteRepository _repo;
    private readonly SessionTracker _session;
    private readonly WorkspaceStateStore _workspaces;
    private readonly LaunchSpec _launch;
    private readonly bool _firstLaunch;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly ObservableCollection<NoteTab> _openTabs = new();
    private readonly ObservableCollection<SidebarItem> _sidebarItems = new();
    private readonly Stack<ClosedTabSnapshot> _recentlyClosed = new();

    private readonly Dictionary<NoteTab, TextBox> _bodyEditors = new();
    private readonly Dictionary<NoteTab, TextBox> _gutters = new();
    private readonly Dictionary<NoteTab, Views.FindBar> _findBars = new();
    private readonly Dictionary<NoteTab, MatchHighlightAdorner> _findAdorners = new();
    private readonly Dictionary<NoteTab, FindService.Result> _findResults = new();
    private readonly Dictionary<NoteTab, int> _findCurrentIndex = new();

    private bool _sidebarVisible = false;
    private double _sidebarLastWidth = 280;
    private double _editorFontSize = 13;
    private FontService.Choice _font = FontService.Choice.Mono;
    private ThemeService.Theme _theme = ThemeService.Theme.System;

    public bool? SidebarVisibleOverride { get; set; }
    public double? SidebarWidthOverride { get; set; }

    public MainWindow(AppConfig config,
                      NoteRepository repo,
                      SessionTracker session,
                      WorkspaceStateStore workspaces,
                      IReadOnlyList<string> filesToOpen,
                      LaunchSpec launch,
                      bool firstLaunch)
    {
        _config = config;
        _repo = repo;
        _session = session;
        _workspaces = workspaces;
        _launch = launch;
        _firstLaunch = firstLaunch;

        InitializeComponent();

        Tabs.ItemsSource = _openTabs;
        NotesList.ItemsSource = _sidebarItems;

        _editorFontSize = _config.Editor.FontSize > 0 ? _config.Editor.FontSize : 13;
        WordWrapMenuItem.IsChecked = _config.Editor.WordWrap;
        LineNumberMenuItem.IsChecked = _config.Editor.ShowLineNumber;
        _font = FontService.Parse(_config.Editor.Font);
        _theme = ThemeService.Parse(_config.Editor.Theme);
        SyncThemeMenu();
        SyncFontMenu();

        NotesFolderLabel.Text = (launch.Mode == LaunchMode.Workspace ? "Workspace: " : "Notes: ") + _repo.NotesFolder;
        if (launch.Mode == LaunchMode.Workspace) Title = $"notepad-- — {Path.GetFileName(_repo.NotesFolder)}";

        // Sidebar visibility: workspace overrides win; otherwise honor the last-session value
        // (LoadSidebarVisible) or fall back to the config default (Editor.ShowSidebar).
        var savedVisible = SidebarVisibleOverride ?? _session.LoadSidebarVisible();
        _sidebarVisible = savedVisible ?? _config.Editor.ShowSidebar;
        if (SidebarWidthOverride is double w && w > 50) _sidebarLastWidth = w;
        ApplySidebarVisibility(initial: true);

        DataObject.AddPastingHandler(this, OnAnyTextBoxPasting);
        PreviewKeyDown += MainWindow_PreviewKeyDown;

        _autosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(5, _config.Autosave.IntervalSeconds))
        };
        _autosaveTimer.Tick += async (_, _) => await SaveAllDirtyAsync();

        ThemeService.Changed += _ => RefreshAppearanceAllEditors();

        Loaded += async (_, _) =>
        {
            await RefreshSidebarAsync();
            await OpenInitialTabsAsync(filesToOpen);
            _autosaveTimer.Start();
            FocusActiveBodyEditor();
        };

        Closing += MainWindow_Closing;
    }

    // ----- shutdown -----

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _autosaveTimer.Stop();
        try { SaveAllDirtyOnClose(); } catch { }

        var paths = _openTabs
            .Select(t => t.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();

        if (_launch.Mode == LaunchMode.Workspace)
        {
            try
            {
                var width = SidebarColumn.Width.IsAbsolute && SidebarColumn.Width.Value > 0
                    ? SidebarColumn.Width.Value
                    : _sidebarLastWidth;
                _workspaces.Save(_launch.RootFolder, new WorkspaceState
                {
                    OpenFiles = paths,
                    ActiveTabIndex = Tabs.SelectedIndex,
                    SidebarWidth = width,
                    SidebarVisible = _sidebarVisible,
                    SavedAtUtc = DateTime.UtcNow,
                });
            }
            catch { }
        }
        else
        {
            try { _session.SaveSession(paths, _sidebarVisible); } catch { }
        }
    }

    private void SaveAllDirtyOnClose()
    {
        foreach (var tab in _openTabs.ToArray())
        {
            if (!tab.IsDirty) continue;
            if (string.IsNullOrEmpty(tab.FilePath)
                && string.IsNullOrWhiteSpace(tab.Text))
                continue;
            try { SaveTabSync(tab); } catch { }
        }
    }

    private void SaveTabSync(NoteTab tab)
    {
        var targetPath = ResolveTargetPath(tab);
        if (!string.IsNullOrEmpty(tab.FilePath)
            && !string.Equals(tab.FilePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            _repo.Rename(tab.FilePath, targetPath);
        }
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(targetPath, tab.ToFileContent(), tab.Encoding);
        tab.FilePath = targetPath;
        tab.MarkClean();
    }

    // ----- initial tab loading -----

    private async Task OpenInitialTabsAsync(IReadOnlyList<string> filesToOpen)
    {
        if (filesToOpen.Count == 0) { AddBlankTab(); return; }

        foreach (var path in filesToOpen)
        {
            await OpenPathIntoNewTabAsync(path);
        }

        if (_openTabs.Count == 0) AddBlankTab();
        Tabs.SelectedIndex = 0;
    }

    private async Task OpenPathIntoNewTabAsync(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            if (!TextFileExtensions.LooksLikeText(path))
            {
                MessageBox.Show(this,
                    "This file doesn't look like text — opening canceled.",
                    "notepad--",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var meta = await NoteRepository.ReadWithMetaAsync(path);
            var tab = new NoteTab
            {
                FilePath = path,
                CreatedDate = File.GetCreationTime(path),
                Encoding = meta.Encoding,
                LineEnding = meta.LineEnding,
                IsForeign = IsForeign(path),
            };
            tab.LoadFromFileContent(meta.Text);
            _openTabs.Add(tab);
        }
        catch { }
    }

    private bool IsForeign(string path)
    {
        if (_launch.Mode != LaunchMode.Workspace) return false;
        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
            return !string.Equals(
                WorkspaceStateStore.Normalize(parent),
                WorkspaceStateStore.Normalize(_launch.RootFolder),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private NoteTab AddBlankTab()
    {
        var tab = new NoteTab
        {
            CreatedDate = DateTime.Now,
            Extension = TextFileExtensions.DefaultExtension,
        };
        _openTabs.Add(tab);
        Tabs.SelectedItem = tab;
        return tab;
    }

    // ----- sidebar -----

    private async Task RefreshSidebarAsync()
    {
        await Task.Yield();
        var files = _repo.List();
        _sidebarItems.Clear();
        foreach (var f in files)
            _sidebarItems.Add(new SidebarItem(f.FullPath, f.LastWriteTimeUtc));
        ApplySidebarFilter();
    }

    private void ApplySidebarFilter()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_sidebarItems);
        if (view is null) return;
        var query = SearchBox?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(query)) { view.Filter = null; return; }

        view.Filter = obj =>
        {
            if (obj is not SidebarItem item) return false;
            if (item.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;

            try
            {
                var content = File.ReadAllText(item.FullPath);
                if (content.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
                if (query.StartsWith('#') && HashtagParser.ContainsTag(content, query)) return true;
            }
            catch { }
            return false;
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySidebarFilter();

    private async void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NotesList.SelectedItem is not SidebarItem item) return;
        await OpenOrFocusFileAsync(item.FullPath);
    }

    private async Task OpenOrFocusFileAsync(string path)
    {
        var existing = _openTabs.FirstOrDefault(t =>
            !string.IsNullOrEmpty(t.FilePath) &&
            string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            Tabs.SelectedItem = existing;
            FocusActiveBodyEditor();
            return;
        }
        await OpenPathIntoNewTabAsync(path);
        if (_openTabs.Count > 0) Tabs.SelectedItem = _openTabs[^1];
        FocusActiveBodyEditor();
    }

    // ----- auto-save -----

    private async Task SaveAllDirtyAsync()
    {
        foreach (var tab in _openTabs.ToArray())
        {
            if (!tab.IsDirty) continue;
            if (string.IsNullOrEmpty(tab.FilePath)
                && string.IsNullOrWhiteSpace(tab.Text))
                continue;
            await SaveTabAsync(tab);
        }
        await RefreshSidebarAsync();
    }

    private async Task SaveTabAsync(NoteTab tab)
    {
        var targetPath = ResolveTargetPath(tab);
        try
        {
            if (!string.IsNullOrEmpty(tab.FilePath) &&
                !string.Equals(tab.FilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                _repo.Rename(tab.FilePath, targetPath);
            }
            await _repo.WriteAsync(targetPath, tab.ToFileContent(), tab.Encoding);
            tab.FilePath = targetPath;
            tab.MarkClean();
        }
        catch { }
    }

    private string ResolveTargetPath(NoteTab tab)
    {
        // Foreign files (outside the sidebar root) always save in place.
        if (tab.IsForeign && !string.IsNullOrEmpty(tab.FilePath)) return tab.FilePath;

        var ext = string.IsNullOrEmpty(tab.Extension) ? TextFileExtensions.DefaultExtension : tab.Extension;
        // Pass the whole tab Text so Sanitize picks the first non-empty line — that way a
        // note that starts with one or more blank lines still gets a meaningful filename.
        var desiredName = NoteFilenameService.BuildFileName(tab.Text, tab.CreatedDate, ext);

        if (string.IsNullOrEmpty(tab.FilePath))
        {
            var unique = NoteFilenameService.DisambiguateFileName(desiredName, _repo.ExistingFileNames());
            return Path.Combine(_repo.NotesFolder, unique);
        }

        // Existing file: keep the current name unless the derived name is different
        // (e.g., the user edited the header). Never auto-rename foreign files.
        var currentName = Path.GetFileName(tab.FilePath);
        if (string.Equals(currentName, desiredName, StringComparison.OrdinalIgnoreCase))
            return tab.FilePath;

        var others = _repo.ExistingFileNames()
            .Where(n => !string.Equals(n, currentName, StringComparison.OrdinalIgnoreCase));
        var unique2 = NoteFilenameService.DisambiguateFileName(desiredName, others);
        return Path.Combine(Path.GetDirectoryName(tab.FilePath) ?? _repo.NotesFolder, unique2);
    }

    // ----- File menu handlers -----

    private void NewTab_Click(object sender, RoutedEventArgs e) { AddBlankTab(); FocusActiveBodyEditor(); }
    private void NewWindow_Click(object sender, RoutedEventArgs e) => App.LaunchNewWindow();
    private void CloseTab_Click(object sender, RoutedEventArgs e) => CloseCurrentTab();
    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is NoteTab tab) CloseTab(tab);
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open file",
            Filter = "Text files (*.txt;*.md;*.json;*.csv;*.log;*.yaml;*.yml;*.xml;*.ini;*.conf;*.cfg;*.env;*.tsv;*.tex)|*.txt;*.md;*.json;*.csv;*.log;*.yaml;*.yml;*.xml;*.ini;*.conf;*.cfg;*.env;*.tsv;*.tex|All files (*.*)|*.*",
            InitialDirectory = _repo.NotesFolder,
        };
        if (dlg.ShowDialog(this) != true) return;
        await OpenOrFocusFileAsync(dlg.FileName);
    }

    private void ReopenClosedTab_Click(object sender, RoutedEventArgs e) => ReopenLastClosed();

    private async void ReopenLastClosed()
    {
        if (_recentlyClosed.Count == 0) return;
        var s = _recentlyClosed.Pop();
        if (!string.IsNullOrEmpty(s.FilePath) && File.Exists(s.FilePath))
        {
            await OpenOrFocusFileAsync(s.FilePath);
            return;
        }
        // Restore an unsaved tab from its in-memory snapshot.
        var tab = new NoteTab { CreatedDate = s.CreatedDate, Extension = s.Extension };
        tab.LoadFromFileContent(s.Text);
        // LoadFromFileContent calls MarkClean → mark dirty so autosave persists it.
        tab.IsDirty = true;
        _openTabs.Add(tab);
        Tabs.SelectedItem = tab;
        FocusActiveBodyEditor();
    }

    private async void ExportNotes_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export notes",
            Filter = "JSON backup (*.json)|*.json",
            FileName = $"notepad-minus-backup-{DateTime.Now:yyyy-MM-dd}.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            await SaveAllDirtyAsync();
            await new BackupService().ExportAsync(_repo.NotesFolder, dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Export failed: " + ex.Message, "notepad--", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportNotes_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import notes",
            Filter = "JSON backup (*.json)|*.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            await new BackupService().ImportAsync(dlg.FileName, _repo.NotesFolder);
            await RefreshSidebarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Import failed: " + ex.Message, "notepad--", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RevealFolder_Click(object sender, RoutedEventArgs e)
    {
        await SaveAllDirtyAsync();
        try { Process.Start(new ProcessStartInfo { FileName = _repo.NotesFolder, UseShellExecute = true }); }
        catch { }
    }

    private async void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (!string.IsNullOrEmpty(app.ConfigPath)) await OpenOrFocusFileAsync(app.ConfigPath);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    // ----- Edit menu / Find -----

    private void Find_Click(object sender, RoutedEventArgs e) => OpenFindBar();
    private void Replace_Click(object sender, RoutedEventArgs e) => OpenReplaceBar();

    private void FindBarControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Views.FindBar bar) return;
        var tab = bar.DataContext as NoteTab;
        if (tab is null) return;
        _findBars[tab] = bar;
        bar.QueryChanged += (_, _) => RunFind(tab);
        bar.NavigateNext += (_, _) => NavigateMatch(tab, +1);
        bar.NavigatePrevious += (_, _) => NavigateMatch(tab, -1);
        bar.CloseRequested += (_, _) => CloseFindBar(tab);
        bar.ReplaceCurrentRequested += (_, _) => ReplaceCurrent(tab);
        bar.ReplaceAllRequested += (_, _) => ReplaceAll(tab);
    }

    private void FindBarControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Views.FindBar bar) return;
        var tab = bar.DataContext as NoteTab;
        if (tab is null) return;
        RemoveAdorner(tab);
        _findBars.Remove(tab);
        _findResults.Remove(tab);
        _findCurrentIndex.Remove(tab);
    }

    private void OpenFindBar()
    {
        if (Tabs.SelectedItem is not NoteTab tab) return;
        if (!_findBars.TryGetValue(tab, out var bar) || !_bodyEditors.TryGetValue(tab, out var editor)) return;

        var prefill = editor.SelectionLength > 0 && !editor.SelectedText.Contains('\n')
            ? editor.SelectedText
            : null;
        bar.Visibility = Visibility.Visible;
        bar.FocusQuery(prefill);
        RepositionFindBarIfCoveringCaret(tab);
        RunFind(tab);
    }

    private void OpenReplaceBar()
    {
        if (Tabs.SelectedItem is not NoteTab tab) return;
        if (!_findBars.TryGetValue(tab, out var bar) || !_bodyEditors.TryGetValue(tab, out var editor)) return;

        var prefill = editor.SelectionLength > 0 && !editor.SelectedText.Contains('\n')
            ? editor.SelectedText
            : null;
        bar.Visibility = Visibility.Visible;
        bar.ReplaceVisible = true;
        // If the bar was just opened, prefill query from selection; otherwise leave it.
        bar.FocusQuery(prefill);
        RepositionFindBarIfCoveringCaret(tab);
        RunFind(tab);
    }

    private void ReplaceCurrent(NoteTab tab)
    {
        if (!_findBars.TryGetValue(tab, out var bar) || !_bodyEditors.TryGetValue(tab, out var editor)) return;
        if (string.IsNullOrEmpty(bar.Query)) return;
        if (!_findResults.TryGetValue(tab, out var result) || !result.IsValid || result.Matches.Count == 0) return;

        // VS Code semantics: if the current selection equals the current match, replace it
        // and move to the next; otherwise, just move to the next match without replacing.
        var current = _findCurrentIndex.TryGetValue(tab, out var c) ? c : 0;
        if (current < 0 || current >= result.Matches.Count) return;
        var m = result.Matches[current];

        var selectionMatchesCurrent = editor.SelectionStart == m.Start && editor.SelectionLength == m.Length;
        if (!selectionMatchesCurrent)
        {
            // Just select it; user can press Replace again to commit.
            SelectMatch(tab, current);
            return;
        }

        var matched = editor.Text.Substring(m.Start, m.Length);
        var replacement = FindService.ReplaceOne(matched, bar.Query, bar.Replacement,
            bar.MatchCase, bar.WholeWord, bar.UseRegex) ?? string.Empty;

        // Single undo unit.
        editor.BeginChange();
        try
        {
            editor.Select(m.Start, m.Length);
            editor.SelectedText = replacement;
            editor.Select(m.Start + replacement.Length, 0);
        }
        finally { editor.EndChange(); }

        // Re-run find against new text so indices stay correct.
        RunFindAndAdvance(tab, m.Start + replacement.Length);
    }

    private void ReplaceAll(NoteTab tab)
    {
        if (!_findBars.TryGetValue(tab, out var bar) || !_bodyEditors.TryGetValue(tab, out var editor)) return;
        if (string.IsNullOrEmpty(bar.Query)) return;

        var caret = editor.CaretIndex;
        var result = FindService.ReplaceAll(editor.Text ?? string.Empty, bar.Query, bar.Replacement,
            bar.MatchCase, bar.WholeWord, bar.UseRegex);

        if (!result.IsValid)
        {
            bar.SetCounter(0, 0, invalidRegex: true, truncated: false);
            return;
        }
        if (result.ReplacedCount == 0)
        {
            bar.SetCounter(0, 0, invalidRegex: false, truncated: false);
            return;
        }

        editor.BeginChange();
        try
        {
            editor.Text = result.NewText;
            editor.CaretIndex = Math.Min(caret, editor.Text.Length);
        }
        finally { editor.EndChange(); }

        RunFind(tab);
        // Calm inline feedback: append replaced count to counter.
        if (_findBars.TryGetValue(tab, out var bar2) && _findResults.TryGetValue(tab, out var newResult))
        {
            var current = _findCurrentIndex.TryGetValue(tab, out var c) ? c : 0;
            bar2.SetCounter(current, newResult.Matches.Count, false, newResult.Truncated);
            bar2.ShowToast($"Replaced {result.ReplacedCount}");
        }
    }

    /// <summary>Re-run Find after an edit and place the current match at the first match at or after <paramref name="cursor"/>.</summary>
    private void RunFindAndAdvance(NoteTab tab, int cursor)
    {
        if (!_findBars.TryGetValue(tab, out var bar) || !_bodyEditors.TryGetValue(tab, out var editor)) return;
        var result = FindService.Find(editor.Text ?? string.Empty, bar.Query, bar.MatchCase, bar.WholeWord, bar.UseRegex);
        _findResults[tab] = result;
        if (!result.IsValid || result.Matches.Count == 0)
        {
            bar.SetCounter(0, result.Matches.Count, !result.IsValid, result.Truncated);
            RemoveAdorner(tab);
            return;
        }
        var idx = FindService.NextIndex(result.Matches, cursor);
        if (idx < 0) idx = 0;
        _findCurrentIndex[tab] = idx;
        bar.SetCounter(idx, result.Matches.Count, false, result.Truncated);
        SelectMatch(tab, idx);
        EnsureAdorner(tab, result, idx);
    }

    private void CloseFindBar(NoteTab tab)
    {
        if (_findBars.TryGetValue(tab, out var bar))
            bar.Visibility = Visibility.Collapsed;
        RemoveAdorner(tab);
        _findResults.Remove(tab);
        _findCurrentIndex.Remove(tab);
        if (_bodyEditors.TryGetValue(tab, out var editor))
            Dispatcher.BeginInvoke(new Action(() => editor.Focus()), DispatcherPriority.Background);
    }

    private void RunFind(NoteTab tab)
    {
        if (!_findBars.TryGetValue(tab, out var bar) || !_bodyEditors.TryGetValue(tab, out var editor)) return;
        var text = editor.Text ?? string.Empty;
        var result = FindService.Find(text, bar.Query, bar.MatchCase, bar.WholeWord, bar.UseRegex);
        _findResults[tab] = result;

        if (!result.IsValid)
        {
            bar.SetCounter(0, 0, invalidRegex: true, truncated: false);
            RemoveAdorner(tab);
            return;
        }
        if (result.Matches.Count == 0)
        {
            bar.SetCounter(0, 0, invalidRegex: false, truncated: false);
            RemoveAdorner(tab);
            return;
        }

        // Pick the first match at or after the caret.
        var cursor = editor.SelectionLength > 0 ? editor.SelectionStart : editor.CaretIndex;
        var idx = 0;
        for (int i = 0; i < result.Matches.Count; i++)
        {
            if (result.Matches[i].Start >= cursor) { idx = i; break; }
        }
        _findCurrentIndex[tab] = idx;
        bar.SetCounter(idx, result.Matches.Count, false, result.Truncated);
        SelectMatch(tab, idx);
        EnsureAdorner(tab, result, idx);
    }

    private void NavigateMatch(NoteTab tab, int direction)
    {
        if (!_findResults.TryGetValue(tab, out var result) || result.Matches.Count == 0) return;
        var current = _findCurrentIndex.TryGetValue(tab, out var c) ? c : 0;
        var next = direction > 0
            ? (current + 1) % result.Matches.Count
            : (current - 1 + result.Matches.Count) % result.Matches.Count;
        _findCurrentIndex[tab] = next;
        if (_findBars.TryGetValue(tab, out var bar))
            bar.SetCounter(next, result.Matches.Count, false, result.Truncated);
        SelectMatch(tab, next);
        if (_findAdorners.TryGetValue(tab, out var ad))
        {
            ad.CurrentIndex = next;
            ad.InvalidateVisual();
        }
        RepositionFindBarIfCoveringCaret(tab);
    }

    private void SelectMatch(NoteTab tab, int index)
    {
        if (!_findResults.TryGetValue(tab, out var result) || !_bodyEditors.TryGetValue(tab, out var editor)) return;
        if (index < 0 || index >= result.Matches.Count) return;
        var m = result.Matches[index];
        try
        {
            editor.Select(m.Start, m.Length);
            var line = editor.GetLineIndexFromCharacterIndex(m.Start);
            if (line >= 0) editor.ScrollToLine(line);
        }
        catch { /* TextBox may not be ready yet — ignore. */ }
    }

    private void EnsureAdorner(NoteTab tab, FindService.Result result, int currentIndex)
    {
        if (!_bodyEditors.TryGetValue(tab, out var editor)) return;
        var layer = AdornerLayer.GetAdornerLayer(editor);
        if (layer is null) return;
        if (!_findAdorners.TryGetValue(tab, out var ad))
        {
            ad = new MatchHighlightAdorner(editor);
            layer.Add(ad);
            _findAdorners[tab] = ad;
            // Re-render the overlay when the user scrolls so highlights track the viewport.
            editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => ad.InvalidateVisual()));
        }
        ad.Matches = result.Matches;
        ad.CurrentIndex = currentIndex;
        ad.InvalidateVisual();
    }

    private void RemoveAdorner(NoteTab tab)
    {
        if (!_findAdorners.TryGetValue(tab, out var ad)) return;
        if (_bodyEditors.TryGetValue(tab, out var editor))
        {
            var layer = AdornerLayer.GetAdornerLayer(editor);
            layer?.Remove(ad);
        }
        _findAdorners.Remove(tab);
    }

    private void RepositionFindBarIfCoveringCaret(NoteTab tab)
    {
        if (!_findBars.TryGetValue(tab, out var bar) || !_bodyEditors.TryGetValue(tab, out var editor)) return;
        try
        {
            var caretRect = editor.GetRectFromCharacterIndex(editor.CaretIndex);
            if (caretRect.IsEmpty) return;
            // Bar's height area at the top ~ 60px. If caret line is in the top 80px of the viewport,
            // drop bar to bottom; otherwise pin to top.
            var atTop = caretRect.Top < 80;
            bar.VerticalAlignment = atTop ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            bar.Margin = atTop ? new Thickness(0, 0, 16, 16) : new Thickness(0, 8, 16, 0);
        }
        catch { /* ignore */ }
    }

    // ----- View menu -----

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => ToggleSidebar();
    private void ToggleSidebar()
    {
        _sidebarVisible = !_sidebarVisible;
        ApplySidebarVisibility(initial: false);
    }

    private void ApplySidebarVisibility(bool initial)
    {
        if (_sidebarVisible)
        {
            SidebarColumn.Width = new GridLength(_sidebarLastWidth);
            SidebarPanel.Visibility = Visibility.Visible;
        }
        else
        {
            if (!initial && SidebarColumn.Width.IsAbsolute && SidebarColumn.Width.Value > 0)
                _sidebarLastWidth = SidebarColumn.Width.Value;
            SidebarColumn.Width = new GridLength(0);
            SidebarPanel.Visibility = Visibility.Collapsed;
        }
        SidebarMenuItem.IsChecked = _sidebarVisible;
    }

    private void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        var wrap = WordWrapMenuItem.IsChecked;
        foreach (var editor in _bodyEditors.Values)
        {
            editor.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            editor.HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        }
    }

    private void LineNumber_Click(object sender, RoutedEventArgs e)
    {
        foreach (var g in _gutters.Values) ApplyGutterVisibility(g);
        UpdateCaretLabel();
    }

    private void ApplyGutterVisibility(TextBox gutter)
    {
        var show = LineNumberMenuItem.IsChecked;
        gutter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Zoom(+1);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Zoom(-1);
    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        _editorFontSize = _config.Editor.FontSize > 0 ? _config.Editor.FontSize : 13;
        ApplyZoom();
    }

    private void Zoom(int delta)
    {
        _editorFontSize = Math.Clamp(_editorFontSize + delta, 8, 36);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        foreach (var editor in _bodyEditors.Values) editor.FontSize = _editorFontSize;
        foreach (var g in _gutters.Values) g.FontSize = _editorFontSize;
    }

    // ----- Theme menu -----

    private void ThemeSystem_Click(object sender, RoutedEventArgs e) => SetTheme(ThemeService.Theme.System);
    private void ThemeLight_Click(object sender, RoutedEventArgs e) => SetTheme(ThemeService.Theme.Light);
    private void ThemeSepia_Click(object sender, RoutedEventArgs e) => SetTheme(ThemeService.Theme.Sepia);
    private void ThemeDim_Click(object sender, RoutedEventArgs e) => SetTheme(ThemeService.Theme.Dim);
    private void ThemeDark_Click(object sender, RoutedEventArgs e) => SetTheme(ThemeService.Theme.Dark);

    private void SetTheme(ThemeService.Theme t)
    {
        _theme = t;
        ThemeService.Apply(t);
        SyncThemeMenu();
        PersistConfig();
    }

    private void SyncThemeMenu()
    {
        ThemeSystemItem.IsChecked = _theme == ThemeService.Theme.System;
        ThemeLightItem.IsChecked  = _theme == ThemeService.Theme.Light;
        ThemeSepiaItem.IsChecked  = _theme == ThemeService.Theme.Sepia;
        ThemeDimItem.IsChecked    = _theme == ThemeService.Theme.Dim;
        ThemeDarkItem.IsChecked   = _theme == ThemeService.Theme.Dark;
    }

    // ----- Font menu -----

    private void FontSans_Click(object sender, RoutedEventArgs e) => SetFont(FontService.Choice.Sans);
    private void FontSerif_Click(object sender, RoutedEventArgs e) => SetFont(FontService.Choice.Serif);
    private void FontMono_Click(object sender, RoutedEventArgs e) => SetFont(FontService.Choice.Mono);

    private void SetFont(FontService.Choice c)
    {
        _font = c;
        SyncFontMenu();
        RefreshAppearanceAllEditors();
        PersistConfig();
    }

    private void SyncFontMenu()
    {
        FontSansItem.IsChecked  = _font == FontService.Choice.Sans;
        FontSerifItem.IsChecked = _font == FontService.Choice.Serif;
        FontMonoItem.IsChecked  = _font == FontService.Choice.Mono;
    }

    private void PersistConfig()
    {
        try
        {
            _config.Editor.Font = FontService.ToConfigString(_font);
            _config.Editor.Theme = _theme.ToString();
            var app = (App)Application.Current;
            if (!string.IsNullOrEmpty(app.ConfigPath)) ConfigLoader.Save(app.ConfigPath, _config);
        }
        catch { }
    }

    // ----- editor registration -----

    private void BodyEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is NoteTab tab) _bodyEditors[tab] = tb;
        ApplyAppearance(tb, body: true);
        // Sync the gutter scroll with this editor's scroll.
        tb.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(BodyEditor_ScrollChanged));
        if (tb.DataContext is NoteTab t2 && _gutters.TryGetValue(t2, out var g))
        {
            UpdateGutter(tb, g);
            ApplyGutterVisibility(g);
        }
    }

    private void BodyEditor_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is NoteTab tab && _bodyEditors.TryGetValue(tab, out var existing) && existing == tb)
            _bodyEditors.Remove(tab);
        tb.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(BodyEditor_ScrollChanged));
    }

    private void LineGutter_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is NoteTab tab)
        {
            _gutters[tab] = tb;
            if (_bodyEditors.TryGetValue(tab, out var ed)) UpdateGutter(ed, tb);
            ApplyGutterVisibility(tb);
            ApplyAppearance(tb, body: true); // match the body font/size
        }
    }

    private void LineGutter_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is NoteTab tab
            && _gutters.TryGetValue(tab, out var existing) && existing == tb)
            _gutters.Remove(tab);
    }

    private void BodyEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is NoteTab tab && _gutters.TryGetValue(tab, out var g))
            UpdateGutter(tb, g);
    }

    private void BodyEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is NoteTab tab && _gutters.TryGetValue(tab, out var g)
            && Math.Abs(g.VerticalOffset - tb.VerticalOffset) > 0.5)
        {
            g.ScrollToVerticalOffset(tb.VerticalOffset);
        }
    }

    private void BodyEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
        Zoom(e.Delta > 0 ? +1 : -1);
        e.Handled = true;
    }

    private static void UpdateGutter(TextBox editor, TextBox gutter)
    {
        var text = editor.Text ?? string.Empty;
        int lines = 1;
        for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
        var sb = new StringBuilder(lines * 4);
        for (int i = 1; i <= lines; i++)
        {
            sb.Append(i);
            if (i < lines) sb.Append('\n');
        }
        gutter.Text = sb.ToString();
    }

    private void BodyEditor_SelectionChanged(object sender, RoutedEventArgs e) => UpdateCaretLabel();

    private void RefreshAppearanceAllEditors()
    {
        foreach (var ed in _bodyEditors.Values) ApplyAppearance(ed, body: true);
        foreach (var g in _gutters.Values) ApplyAppearance(g, body: true);
    }

    private void ApplyAppearance(TextBox editor, bool body)
    {
        editor.FontFamily = new FontFamily(FontService.FamilyFor(_font));
        if (body)
        {
            editor.FontSize = _editorFontSize;
            var wrap = WordWrapMenuItem.IsChecked;
            editor.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            editor.HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        }
    }

    // ----- keyboard shortcuts -----

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

        // Alt+C/W/R toggles Find modifiers when the bar is visible.
        if (alt && !ctrl && Tabs.SelectedItem is NoteTab activeTab
            && _findBars.TryGetValue(activeTab, out var fb) && fb.Visibility == Visibility.Visible)
        {
            if (e.Key is Key.C or Key.W or Key.R)
            {
                fb.HandleAltShortcut(e.Key);
                e.Handled = true;
                return;
            }
        }

        // F3 / Shift+F3 navigate when Find is open.
        if (e.Key == Key.F3 && Tabs.SelectedItem is NoteTab navTab
            && _findBars.TryGetValue(navTab, out var nb) && nb.Visibility == Visibility.Visible)
        {
            NavigateMatch(navTab, shift ? -1 : +1);
            e.Handled = true;
            return;
        }

        if (!ctrl) return;

        switch (e.Key)
        {
            case Key.T when shift: ReopenLastClosed(); e.Handled = true; break;
            case Key.T: AddBlankTab(); FocusActiveBodyEditor(); e.Handled = true; break;
            case Key.N: App.LaunchNewWindow(); e.Handled = true; break;
            case Key.W: CloseCurrentTab(); e.Handled = true; break;
            case Key.O: OpenFile_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.S: _ = SaveAllDirtyAsync(); e.Handled = true; break;
            case Key.F: OpenFindBar(); e.Handled = true; break;
            case Key.H: OpenReplaceBar(); e.Handled = true; break;
            case Key.B: ToggleSidebar(); e.Handled = true; break;
            case Key.OemPlus or Key.Add: Zoom(+1); e.Handled = true; break;
            case Key.OemMinus or Key.Subtract: Zoom(-1); e.Handled = true; break;
            case Key.D0 or Key.NumPad0: ZoomReset_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.OemComma: OpenConfig_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.Tab when shift: CycleTab(-1); e.Handled = true; break;
            case Key.Tab: CycleTab(+1); e.Handled = true; break;
            case Key.D1: JumpToTab(0); e.Handled = true; break;
            case Key.D2: JumpToTab(1); e.Handled = true; break;
            case Key.D3: JumpToTab(2); e.Handled = true; break;
            case Key.D4: JumpToTab(3); e.Handled = true; break;
            case Key.D5: JumpToTab(4); e.Handled = true; break;
            case Key.D6: JumpToTab(5); e.Handled = true; break;
            case Key.D7: JumpToTab(6); e.Handled = true; break;
            case Key.D8: JumpToTab(7); e.Handled = true; break;
            case Key.D9: JumpToTab(_openTabs.Count - 1); e.Handled = true; break;
        }
    }

    private void CycleTab(int delta)
    {
        if (_openTabs.Count == 0) return;
        var i = Tabs.SelectedIndex < 0 ? 0 : Tabs.SelectedIndex;
        i = ((i + delta) % _openTabs.Count + _openTabs.Count) % _openTabs.Count;
        Tabs.SelectedIndex = i;
        FocusActiveBodyEditor();
    }

    private void JumpToTab(int index)
    {
        if (index < 0 || index >= _openTabs.Count) return;
        Tabs.SelectedIndex = index;
        FocusActiveBodyEditor();
    }

    // ----- sidebar context menu actions -----

    private SidebarItem? CtxItem(object sender)
    {
        if (sender is MenuItem mi)
        {
            var ctx = mi.Parent as ContextMenu ?? (mi.CommandParameter as ContextMenu);
            if (ctx?.PlacementTarget is ListBoxItem lbi && lbi.DataContext is SidebarItem si) return si;
        }
        return NotesList.SelectedItem as SidebarItem;
    }

    private async void Ctx_Open(object sender, RoutedEventArgs e)
    {
        if (CtxItem(sender) is SidebarItem si) await OpenOrFocusFileAsync(si.FullPath);
    }

    private void Ctx_Reveal(object sender, RoutedEventArgs e)
    {
        if (CtxItem(sender) is not SidebarItem si) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{si.FullPath}\"") { UseShellExecute = true }); }
        catch { }
    }

    private void Ctx_CopyPath(object sender, RoutedEventArgs e)
    {
        if (CtxItem(sender) is SidebarItem si)
        {
            try { Clipboard.SetText(si.FullPath); } catch { }
        }
    }

    private void Ctx_CopyName(object sender, RoutedEventArgs e)
    {
        if (CtxItem(sender) is SidebarItem si)
        {
            try { Clipboard.SetText(Path.GetFileName(si.FullPath)); } catch { }
        }
    }

    private async void Ctx_Rename(object sender, RoutedEventArgs e)
    {
        if (CtxItem(sender) is not SidebarItem si) return;
        var current = Path.GetFileName(si.FullPath);
        var dlg = new RenameDialog(current) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var newName = dlg.NewName.Trim();
        if (string.IsNullOrEmpty(newName) || newName == current) return;
        try
        {
            var newPath = Path.Combine(Path.GetDirectoryName(si.FullPath)!, newName);
            File.Move(si.FullPath, newPath);
            // If the renamed file is open in a tab, update its FilePath.
            var tab = _openTabs.FirstOrDefault(t =>
                string.Equals(t.FilePath, si.FullPath, StringComparison.OrdinalIgnoreCase));
            if (tab is not null) tab.FilePath = newPath;
            await RefreshSidebarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Rename failed: " + ex.Message, "notepad--", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Ctx_Delete(object sender, RoutedEventArgs e)
    {
        if (CtxItem(sender) is not SidebarItem si) return;
        // Close any open tab pointing at this file first.
        var tab = _openTabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, si.FullPath, StringComparison.OrdinalIgnoreCase));
        if (tab is not null) { tab.MarkClean(); _openTabs.Remove(tab); }
        RecycleBin.Send(si.FullPath);
        await RefreshSidebarAsync();
    }

    // ----- helpers -----

    private void CloseCurrentTab()
    {
        if (Tabs.SelectedItem is NoteTab tab) CloseTab(tab);
    }

    private async void CloseTab(NoteTab tab)
    {
        // v1.5: synchronous save on tab close to avoid the 60s autosave window.
        var hasContent = !string.IsNullOrWhiteSpace(tab.Text);
        if (tab.IsDirty && (hasContent || !string.IsNullOrEmpty(tab.FilePath)))
        {
            try { SaveTabSync(tab); } catch { }
        }
        _recentlyClosed.Push(new ClosedTabSnapshot(tab.FilePath, tab.Text, tab.CreatedDate, tab.Extension));
        _openTabs.Remove(tab);
        if (_openTabs.Count == 0) AddBlankTab();
        await RefreshSidebarAsync();
    }

    private TextBox? GetActiveBodyEditor()
        => Tabs.SelectedItem is NoteTab tab && _bodyEditors.TryGetValue(tab, out var ed) ? ed : null;

    private TextBox? GetActiveHeaderEditor() => null; // header band removed in v1.5.1

    private void FocusActiveBodyEditor()
        => Dispatcher.BeginInvoke(new Action(() => GetActiveBodyEditor()?.Focus()), DispatcherPriority.Background);

    private void ToggleFindBar(bool show)
    {
        if (Tabs.SelectedItem is not NoteTab tab) return;
        if (show) OpenFindBar();
        else CloseFindBar(tab);
    }

    private void UpdateCaretLabel()
    {
        if (!LineNumberMenuItem.IsChecked) { CaretLabel.Text = string.Empty; return; }
        var editor = GetActiveBodyEditor();
        if (editor is null) { CaretLabel.Text = string.Empty; return; }
        var caret = editor.CaretIndex;
        var line = editor.GetLineIndexFromCharacterIndex(caret) + 1;
        var col = caret - editor.GetCharacterIndexFromLineIndex(line - 1) + 1;
        CaretLabel.Text = $"Ln {line}, Col {col}";
    }

    private void OnAnyTextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.SourceDataObject is null) return;
        if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            var text = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText);
            var dao = new DataObject();
            dao.SetData(DataFormats.UnicodeText, text);
            e.DataObject = dao;
            e.FormatToApply = DataFormats.UnicodeText;
        }
        else if (e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            var text = (string)e.SourceDataObject.GetData(DataFormats.Text);
            var dao = new DataObject();
            dao.SetData(DataFormats.Text, text);
            e.DataObject = dao;
            e.FormatToApply = DataFormats.Text;
        }
        else
        {
            e.CancelCommand();
        }
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent is null) return null;
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == name) return t;
            var deeper = FindChild<T>(child, name);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    private sealed record ClosedTabSnapshot(string? FilePath, string Text, DateTime CreatedDate, string Extension);
}