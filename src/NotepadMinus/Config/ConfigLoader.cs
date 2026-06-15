using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotepadMinus.Config;

/// <summary>
/// Loads <see cref="AppConfig"/> from a JSON file, applying sane defaults when keys are missing
/// or the file is absent. Writes a default file on first run so users have something to edit.
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path required", nameof(path));

        if (!File.Exists(path))
        {
            var defaults = AppConfig.CreateDefault();
            TryWrite(path, defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return AppConfig.CreateDefault();
            var parsed = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return Merge(parsed);
        }
        catch (JsonException)
        {
            // Corrupt file — don't crash; fall back to defaults silently (per "never nag").
            return AppConfig.CreateDefault();
        }
    }

    public static string ExpandPath(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return Environment.ExpandEnvironmentVariables(raw);
    }

    private static AppConfig Merge(AppConfig? parsed)
    {
        var d = AppConfig.CreateDefault();
        if (parsed is null) return d;
        parsed.Storage ??= d.Storage;
        parsed.Autosave ??= d.Autosave;
        parsed.Startup ??= d.Startup;
        parsed.Editor ??= d.Editor;

        if (string.IsNullOrWhiteSpace(parsed.Storage.NotesFolder)) parsed.Storage.NotesFolder = d.Storage.NotesFolder;
        if (string.IsNullOrWhiteSpace(parsed.Storage.ArchiveFolder)) parsed.Storage.ArchiveFolder = d.Storage.ArchiveFolder;
        if (parsed.Autosave.IntervalSeconds < 5) parsed.Autosave.IntervalSeconds = d.Autosave.IntervalSeconds;
        if (string.IsNullOrWhiteSpace(parsed.Startup.DefaultChoice)) parsed.Startup.DefaultChoice = d.Startup.DefaultChoice;
        if (string.IsNullOrWhiteSpace(parsed.Editor.FontFamily)) parsed.Editor.FontFamily = d.Editor.FontFamily;
        if (parsed.Editor.FontSize <= 0) parsed.Editor.FontSize = d.Editor.FontSize;
        if (!IsValidFont(parsed.Editor.Font)) parsed.Editor.Font = d.Editor.Font;
        if (!IsValidTheme(parsed.Editor.Theme)) parsed.Editor.Theme = d.Editor.Theme;

        return parsed;
    }

    public static bool IsValidFont(string? v)
        => string.Equals(v, "Sans", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v, "Serif", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v, "Mono", StringComparison.OrdinalIgnoreCase);

    public static bool IsValidTheme(string? v)
        => string.Equals(v, "System", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v, "Light", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v, "Sepia", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v, "Dim", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v, "Dark", StringComparison.OrdinalIgnoreCase);

    public static void Save(string path, AppConfig cfg) => TryWrite(path, cfg);

    private static void TryWrite(string path, AppConfig cfg)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, JsonOptions));
        }
        catch
        {
            // Non-fatal: app can still run with in-memory defaults.
        }
    }
}
