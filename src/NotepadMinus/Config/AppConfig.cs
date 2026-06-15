using System;
using System.Collections.Generic;

namespace NotepadMinus.Config;

public sealed class AppConfig
{
    public StorageConfig Storage { get; set; } = new();
    public AutosaveConfig Autosave { get; set; } = new();
    public StartupConfig Startup { get; set; } = new();
    public EditorConfig Editor { get; set; } = new();

    public static AppConfig CreateDefault() => new();
}

public sealed class StorageConfig
{
    public string NotesFolder { get; set; } = @"%USERPROFILE%\Documents\NotepadMinus\notes";
    public string ArchiveFolder { get; set; } = @"%USERPROFILE%\Documents\NotepadMinus\notes\_archive";
}

public sealed class AutosaveConfig
{
    public int IntervalSeconds { get; set; } = 10;
}

public sealed class StartupConfig
{
    public bool PromptOnLaunch { get; set; } = true;
    public string DefaultChoice { get; set; } = "StartFresh";
}

public sealed class EditorConfig
{
    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 12;
    public bool WordWrap { get; set; } = false;
    public bool ShowLineNumber { get; set; } = false;
    public bool ShowSidebar { get; set; } = false;

    /// <summary>v1.5: "Sans" | "Serif" | "Mono". Maps to a concrete font family at runtime.</summary>
    public string Font { get; set; } = "Mono";

    /// <summary>v1.5: "System" | "Light" | "Sepia" | "Dim" | "Dark".</summary>
    public string Theme { get; set; } = "System";
}
