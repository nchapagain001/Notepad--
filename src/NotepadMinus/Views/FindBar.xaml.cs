using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NotepadMinus.Views;

/// <summary>
/// In-file Find bar. Stateless about which TextBox it targets;
/// MainWindow wires it up per active tab.
/// Modifier + last-query state persists for the app lifetime via <see cref="FindState"/>.
/// </summary>
public partial class FindBar : UserControl
{
    private readonly DispatcherTimer _debounce;
    private bool _suppressEvents;

    public event EventHandler? QueryChanged;
    public event EventHandler? NavigateNext;
    public event EventHandler? NavigatePrevious;
    public event EventHandler? CloseRequested;
    public event EventHandler? ReplaceCurrentRequested;
    public event EventHandler? ReplaceAllRequested;

    public FindBar()
    {
        InitializeComponent();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            FindState.Query = QueryBox.Text;
            QueryChanged?.Invoke(this, EventArgs.Empty);
        };
        Loaded += (_, _) => RestoreState();
    }

    public string Query => QueryBox.Text;
    public string Replacement => ReplaceBox.Text;
    public bool MatchCase => MatchCaseToggle.IsChecked == true;
    public bool WholeWord => WholeWordToggle.IsChecked == true;
    public bool UseRegex => RegexToggle.IsChecked == true;
    public bool ReplaceVisible
    {
        get => ReplaceRow.Visibility == Visibility.Visible;
        set
        {
            ReplaceRow.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            ReplaceToggle.IsChecked = value;
            ReplaceToggle.Content = value ? "⌄" : "›";
            FindState.ReplaceVisible = value;
        }
    }

    public void FocusQuery(string? prefill = null)
    {
        if (!string.IsNullOrEmpty(prefill))
        {
            _suppressEvents = true;
            QueryBox.Text = prefill;
            FindState.Query = prefill;
            _suppressEvents = false;
        }
        QueryBox.Focus();
        Keyboard.Focus(QueryBox);
        QueryBox.SelectAll();
    }

    public void SetCounter(int current, int total, bool invalidRegex, bool truncated)
    {
        if (string.IsNullOrEmpty(QueryBox.Text))
        {
            CounterText.Visibility = Visibility.Collapsed;
            SetInvalidBorder(false);
            NextButton.IsEnabled = PrevButton.IsEnabled = false;
            return;
        }

        if (invalidRegex)
        {
            CounterText.Text = "Invalid regular expression";
            CounterText.Visibility = Visibility.Visible;
            SetInvalidBorder(true);
            NextButton.IsEnabled = PrevButton.IsEnabled = false;
            return;
        }

        SetInvalidBorder(false);
        if (total == 0)
        {
            CounterText.Text = "No results";
            CounterText.Visibility = Visibility.Visible;
            SetInvalidBorder(true);
            NextButton.IsEnabled = PrevButton.IsEnabled = false;
        }
        else
        {
            var shown = current + 1;
            CounterText.Text = truncated
                ? $"{shown} of {total}+ (showing first {total})"
                : $"{shown} of {total}";
            CounterText.Visibility = Visibility.Visible;
            NextButton.IsEnabled = PrevButton.IsEnabled = total > 0;
        }
    }

    private void SetInvalidBorder(bool invalid)
    {
        QueryBorder.BorderBrush = invalid
            ? new SolidColorBrush(Color.FromArgb(180, 220, 80, 80))
            : (Brush)FindResource("SubtleBorder");
    }

    private void RestoreState()
    {
        _suppressEvents = true;
        QueryBox.Text = FindState.Query ?? string.Empty;
        ReplaceBox.Text = FindState.Replacement ?? string.Empty;
        MatchCaseToggle.IsChecked = FindState.MatchCase;
        WholeWordToggle.IsChecked = FindState.WholeWord;
        RegexToggle.IsChecked = FindState.UseRegex;
        ReplaceVisible = FindState.ReplaceVisible;
        _suppressEvents = false;
    }

    private void QueryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindState.Query = QueryBox.Text;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                NavigatePrevious?.Invoke(this, EventArgs.Empty);
            else
                NavigateNext?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void Modifier_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        FindState.MatchCase = MatchCase;
        FindState.WholeWord = WholeWord;
        FindState.UseRegex = UseRegex;
        QueryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
        => NavigatePrevious?.Invoke(this, EventArgs.Empty);

    private void NextButton_Click(object sender, RoutedEventArgs e)
        => NavigateNext?.Invoke(this, EventArgs.Empty);

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void ReplaceToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        ReplaceVisible = ReplaceToggle.IsChecked == true;
    }

    private void ReplaceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents) return;
        FindState.Replacement = ReplaceBox.Text;
    }

    private void ReplaceBox_KeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        if (e.Key == Key.Enter)
        {
            if (ctrl && alt) ReplaceAllRequested?.Invoke(this, EventArgs.Empty);
            else ReplaceCurrentRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void ReplaceOneButton_Click(object sender, RoutedEventArgs e)
        => ReplaceCurrentRequested?.Invoke(this, EventArgs.Empty);

    private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
        => ReplaceAllRequested?.Invoke(this, EventArgs.Empty);

    public void FocusReplace()
    {
        ReplaceVisible = true;
        ReplaceBox.Focus();
        Keyboard.Focus(ReplaceBox);
        ReplaceBox.SelectAll();
    }

    private DispatcherTimer? _toastTimer;
    /// <summary>Show a transient message in the counter line (e.g. "Replaced 12") for 2s.</summary>
    public void ShowToast(string message)
    {
        CounterText.Text = message;
        CounterText.Visibility = Visibility.Visible;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            // Recompute the counter from current state so we don't leave a stale message.
            QueryChanged?.Invoke(this, EventArgs.Empty);
        };
        _toastTimer.Start();
    }

    public void HandleAltShortcut(Key key)
    {
        switch (key)
        {
            case Key.C: MatchCaseToggle.IsChecked = !MatchCaseToggle.IsChecked; break;
            case Key.W: WholeWordToggle.IsChecked = !WholeWordToggle.IsChecked; break;
            case Key.R: RegexToggle.IsChecked = !RegexToggle.IsChecked; break;
        }
    }
}

/// <summary>
/// Static, process-wide persistence of the Find bar's query and modifier state.
/// Reset only when the app exits.
/// </summary>
public static class FindState
{
    public static string? Query { get; set; }
    public static string? Replacement { get; set; }
    public static bool MatchCase { get; set; }
    public static bool WholeWord { get; set; }
    public static bool UseRegex { get; set; }
    public static bool ReplaceVisible { get; set; }
}
