using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotepadMinus.Services;

/// <summary>
/// v1.5 per-folder state persistence. Records, per workspace folder, the tabs that were
/// open at last close, the sidebar width, and the sidebar collapsed/expanded state.
/// Storage lives in <c>%APPDATA%\notepad-minus\workspaces.json</c> — never inside the
/// workspace folder itself, so `notepad--` never pollutes user folders with metadata.
/// </summary>
public sealed class WorkspaceStateStore
{
    private readonly string _path;

    public WorkspaceStateStore(string filePath) { _path = filePath; }

    public static string DefaultPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "notepad-minus", "workspaces.json");

    public WorkspaceState? Load(string folderPath)
    {
        var all = LoadAll();
        var key = Normalize(folderPath);
        return all.Workspaces.TryGetValue(key, out var s) ? s : null;
    }

    public void Save(string folderPath, WorkspaceState state)
    {
        var all = LoadAll();
        var key = Normalize(folderPath);
        all.Workspaces[key] = state;
        TryWrite(all);
    }

    private WorkspacesFile LoadAll()
    {
        try
        {
            if (!File.Exists(_path)) return new WorkspacesFile();
            var raw = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(raw)) return new WorkspacesFile();
            return JsonSerializer.Deserialize<WorkspacesFile>(raw) ?? new WorkspacesFile();
        }
        catch
        {
            return new WorkspacesFile();
        }
    }

    private void TryWrite(WorkspacesFile data)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* losing state is annoying but never fatal */ }
    }

    public static string Normalize(string folderPath)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath)).ToLowerInvariant();
}

public sealed class WorkspacesFile
{
    [JsonPropertyName("workspaces")]
    public Dictionary<string, WorkspaceState> Workspaces { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkspaceState
{
    [JsonPropertyName("openFiles")] public List<string> OpenFiles { get; set; } = new();
    [JsonPropertyName("activeTab")] public int ActiveTabIndex { get; set; }
    [JsonPropertyName("sidebarWidth")] public double SidebarWidth { get; set; } = 280;
    [JsonPropertyName("sidebarVisible")] public bool SidebarVisible { get; set; } = false;
    [JsonPropertyName("savedAt")] public DateTime SavedAtUtc { get; set; }

    public static IReadOnlyList<string> FilterExisting(IEnumerable<string> paths)
        => paths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).ToArray();
}
