using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using NotepadMinus.Config;
using NotepadMinus.Services;
using NotepadMinus.Views;

namespace NotepadMinus;

public partial class App : Application
{
    public AppConfig Config { get; private set; } = AppConfig.CreateDefault();
    public string ConfigPath { get; private set; } = string.Empty;
    public NoteRepository Repository { get; private set; } = null!;
    public SessionTracker Session { get; private set; } = null!;
    public WorkspaceStateStore Workspaces { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, ex) =>
        {
            ReportFatal(ex.Exception);
            ex.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            if (ex.ExceptionObject is Exception x) ReportFatal(x);
        };

        // Prevent WPF from auto-shutting down between the launch prompt closing and
        // MainWindow appearing. We restore default behavior once the main window is up.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Config lives in %USERPROFILE%\Documents\NotepadMinus\config.json so the user
        // can keep a single config alongside their notes, independent of which .exe they ran.
        // The app also accepts an override at %APPDATA%\NotepadMinus\config.json for users
        // who prefer the roaming-profile location.
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var docsConfigDir = Path.Combine(documents, "NotepadMinus");
        var docsConfigPath = Path.Combine(docsConfigDir, "config.json");
        var roamingConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotepadMinus",
            "config.json");

        if (File.Exists(docsConfigPath))
        {
            ConfigPath = docsConfigPath;
        }
        else if (File.Exists(roamingConfigPath))
        {
            ConfigPath = roamingConfigPath;
        }
        else
        {
            // Seed a fresh default config in Documents on first launch.
            try { Directory.CreateDirectory(docsConfigDir); } catch { }
            ConfigPath = docsConfigPath;
        }
        Config = ConfigLoader.Load(ConfigPath);

        // v1.5: apply theme before any window appears so the launch prompt is themed too.
        ThemeService.Apply(ThemeService.Parse(Config.Editor.Theme));

        var notesFolder = ConfigLoader.ExpandPath(Config.Storage.NotesFolder);
        Repository = new NoteRepository(notesFolder);

        var sessionFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotepadMinus",
            "session.json");
        Session = new SessionTracker(sessionFile);
        Workspaces = new WorkspaceStateStore(WorkspaceStateStore.DefaultPath());

        // v1.5: parse CLI args to decide between no-args (notes folder + prompt) and
        // workspace launch (specific folder/file, no prompt, restore per-folder session).
        var launch = ParseLaunch(e.Args, notesFolder);

        IReadOnlyList<string> filesToOpen;
        bool? sidebarVisibleOverride = null;
        double? sidebarWidthOverride = null;

        if (launch.Mode == LaunchMode.Workspace)
        {
            var ws = Workspaces.Load(launch.RootFolder) ?? new WorkspaceState();
            var paths = WorkspaceState.FilterExisting(ws.OpenFiles).ToList();
            if (launch.InitialFile is not null && !paths.Any(p => string.Equals(p, launch.InitialFile, StringComparison.OrdinalIgnoreCase)))
                paths.Insert(0, launch.InitialFile);
            filesToOpen = paths;
            sidebarVisibleOverride = ws.SidebarVisible;
            sidebarWidthOverride = ws.SidebarWidth;
        }
        else
        {
            var choice = LaunchChoice.StartFresh;
            if (Config.Startup.PromptOnLaunch)
            {
                var prompt = new LaunchPromptWindow();
                var ok = prompt.ShowDialog();
                choice = ok == true ? prompt.Choice : ParseDefault(Config.Startup.DefaultChoice);
            }
            else
            {
                choice = ParseDefault(Config.Startup.DefaultChoice);
            }
            filesToOpen = choice == LaunchChoice.OpenYesterday
                ? SessionTracker.FilterExisting(Session.LoadLastSession())
                : Array.Empty<string>();
        }

        // Each invocation is its own window. We use a per-folder repository so the
        // sidebar / new-tab path reflects the workspace root, not the default notes folder.
        var rootRepo = launch.Mode == LaunchMode.Workspace
            ? new NoteRepository(launch.RootFolder)
            : Repository;

        var firstLaunch = !File.Exists(ConfigPath) || (Session.IsFirstLaunch() && launch.Mode == LaunchMode.NotesFolder);

        var main = new MainWindow(Config, rootRepo, Session, Workspaces, filesToOpen, launch, firstLaunch)
        {
            SidebarVisibleOverride = sidebarVisibleOverride,
            SidebarWidthOverride = sidebarWidthOverride,
        };
        MainWindow = main;
        main.Show();

        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    public static void LaunchNewWindow(string? folderOrFile = null)
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
            };
            if (!string.IsNullOrEmpty(folderOrFile)) psi.ArgumentList.Add(folderOrFile);
            Process.Start(psi);
        }
        catch { /* never nag */ }
    }

    private static LaunchSpec ParseLaunch(string[] args, string defaultNotesFolder)
    {
        if (args is null || args.Length == 0)
            return new LaunchSpec(LaunchMode.NotesFolder, defaultNotesFolder, null);

        var arg = args[0];
        try
        {
            var full = Path.GetFullPath(arg);
            if (Directory.Exists(full)) return new LaunchSpec(LaunchMode.Workspace, full, null);
            if (File.Exists(full))
            {
                var parent = Path.GetDirectoryName(full) ?? defaultNotesFolder;
                return new LaunchSpec(LaunchMode.Workspace, parent, full);
            }
            // Treat unresolvable paths as a workspace if it has a slash, else fall back.
            if (full.IndexOfAny(new[] { '\\', '/' }) >= 0)
            {
                Directory.CreateDirectory(full);
                return new LaunchSpec(LaunchMode.Workspace, full, null);
            }
        }
        catch { }
        return new LaunchSpec(LaunchMode.NotesFolder, defaultNotesFolder, null);
    }

    private static void ReportFatal(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotepadMinus");
            Directory.CreateDirectory(dir);
            var log = Path.Combine(dir, "crash.log");
            File.AppendAllText(log, $"[{DateTime.Now:O}] {ex}\r\n\r\n");
            MessageBox.Show(ex.ToString(), "notepad-- crashed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }

    private static LaunchChoice ParseDefault(string raw)
        => string.Equals(raw, "OpenYesterday", StringComparison.OrdinalIgnoreCase)
            ? LaunchChoice.OpenYesterday
            : LaunchChoice.StartFresh;
}

public enum LaunchChoice { StartFresh, OpenYesterday }

public enum LaunchMode { NotesFolder, Workspace }

public sealed record LaunchSpec(LaunchMode Mode, string RootFolder, string? InitialFile);
