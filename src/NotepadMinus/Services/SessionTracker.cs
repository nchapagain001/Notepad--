using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NotepadMinus.Services;

/// <summary>
/// Persists the set of file paths that were open when the app last shut down.
/// This is v1's simple definition of "yesterday's tabs": whatever was on screen
/// at the last close. Storage lives outside the user-visible notes folder so a
/// file-watching user never sees it.
/// </summary>
public sealed class SessionTracker
{
    private readonly string _sessionFilePath;

    public SessionTracker(string sessionFilePath)
    {
        _sessionFilePath = sessionFilePath ?? throw new ArgumentNullException(nameof(sessionFilePath));
    }

    public IReadOnlyList<string> LoadLastSession()
    {
        var s = LoadState();
        if (s?.OpenFiles is null) return Array.Empty<string>();
        return s.OpenFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>True if no prior session has ever been recorded — i.e. first run.</summary>
    public bool IsFirstLaunch() => !File.Exists(_sessionFilePath);

    /// <summary>Loads the last-saved sidebar-visible preference. Null when never set.</summary>
    public bool? LoadSidebarVisible() => LoadState()?.SidebarVisible;

    private SessionState? LoadState()
    {
        if (!File.Exists(_sessionFilePath)) return null;
        try
        {
            var json = File.ReadAllText(_sessionFilePath);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<SessionState>(json);
        }
        catch
        {
            return null;
        }
    }

    public void SaveSession(IEnumerable<string> openFilePaths, bool? sidebarVisible = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(_sessionFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var existing = LoadState();
            var state = new SessionState
            {
                SavedAtUtc = DateTime.UtcNow,
                OpenFiles = openFilePaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                SidebarVisible = sidebarVisible ?? existing?.SidebarVisible,
            };
            File.WriteAllText(_sessionFilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Non-fatal — losing yesterday's tabs is annoying but never crashes the app.
        }
    }

    /// <summary>
    /// Returns only the file paths that still exist on disk. Used when restoring
    /// "Open Yesterday's" so a deleted file doesn't error out.
    /// </summary>
    public static IReadOnlyList<string> FilterExisting(IEnumerable<string> paths)
        => paths.Where(File.Exists).ToArray();

    private sealed class SessionState
    {
        public DateTime SavedAtUtc { get; set; }
        public List<string>? OpenFiles { get; set; }
        public bool? SidebarVisible { get; set; }
    }
}
