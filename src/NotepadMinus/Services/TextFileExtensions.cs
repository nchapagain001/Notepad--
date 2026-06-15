using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NotepadMinus.Services;

/// <summary>
/// v1.5 multi-extension support: which file extensions the sidebar enumerates and the
/// File → Open… dialog filters on. Every file is still treated as plain text — these
/// extensions never trigger syntax highlighting, validation, or any extension-aware
/// behavior. The list is intentionally narrow to keep "what shows in the sidebar"
/// predictable; File → Open… lets the user pick anything.
/// </summary>
public static class TextFileExtensions
{
    public static readonly IReadOnlyList<string> Known = new[]
    {
        ".txt", ".md", ".json", ".csv", ".log", ".yaml", ".yml",
        ".xml", ".ini", ".conf", ".cfg", ".env", ".tsv", ".tex",
    };

    /// <summary>Default extension for new untitled notes.</summary>
    public const string DefaultExtension = ".txt";

    public static bool IsKnown(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        return Known.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true if the file's first 4KB look like text (no NUL bytes). Used to
    /// refuse opening binary files without throwing a modal dialog. v1.5 spec §"File-Handling
    /// Rules" item 6.
    /// </summary>
    public static bool LooksLikeText(string path)
    {
        try
        {
            using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> buf = stackalloc byte[4096];
            var n = s.Read(buf);
            for (int i = 0; i < n; i++) if (buf[i] == 0) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
