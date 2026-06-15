using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NotepadMinus.Services;

/// <summary>
/// Builds safe, sortable filenames of the form "&lt;first line&gt; YYYY-MM-DD.txt".
/// Falls back to just "YYYY-MM-DD.txt" when the first line is empty/whitespace.
/// Disambiguates collisions with " (2)", " (3)", etc.
/// </summary>
public static class NoteFilenameService
{
    private const int MaxTitleChars = 80;

    public static string BuildFileName(string? firstLine, DateTime date)
        => BuildFileName(firstLine, date, ".txt");

    public static string BuildFileName(string? firstLine, DateTime date, string? extension)
    {
        var ext = NormalizeExtension(extension);
        var datePart = date.ToString("yyyy-MM-dd");
        var title = Sanitize(firstLine);
        return string.IsNullOrEmpty(title)
            ? $"{datePart}{ext}"
            : $"{title} {datePart}{ext}";
    }

    private static string NormalizeExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return ".txt";
        var e = ext.Trim();
        if (!e.StartsWith('.')) e = "." + e;
        return e.ToLowerInvariant();
    }

    /// <summary>
    /// Given the desired filename and the existing files in the folder,
    /// returns a unique filename by appending " (2)", " (3)", … before ".txt"
    /// when needed. Existing-file matching is case-insensitive.
    /// </summary>
    public static string DisambiguateFileName(string desiredFileName, System.Collections.Generic.IEnumerable<string> existingFileNames)
    {
        var existing = new System.Collections.Generic.HashSet<string>(
            existingFileNames.Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(desiredFileName)) return desiredFileName;

        var stem = Path.GetFileNameWithoutExtension(desiredFileName);
        var ext = Path.GetExtension(desiredFileName);
        var n = 2;
        while (true)
        {
            var candidate = $"{stem} ({n}){ext}";
            if (!existing.Contains(candidate)) return candidate;
            n++;
        }
    }

    public static string Sanitize(string? firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine)) return string.Empty;

        // Take first non-empty line only.
        var line = firstLine.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;

        if (line.Length == 0) return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(line.Length);
        foreach (var ch in line)
        {
            if (Array.IndexOf(invalid, ch) >= 0) { sb.Append('-'); continue; }
            // Strip control chars too.
            if (char.IsControl(ch)) continue;
            sb.Append(ch);
        }

        // Collapse runs of whitespace and trim leading/trailing dots/spaces (Windows-hostile).
        var collapsed = string.Join(' ',
            sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        collapsed = collapsed.Trim().TrimEnd('.', ' ');

        if (collapsed.Length > MaxTitleChars) collapsed = collapsed[..MaxTitleChars].TrimEnd();

        // Windows reserved device names.
        var reserved = new[] { "CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
                               "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9" };
        if (reserved.Any(r => string.Equals(r, collapsed, StringComparison.OrdinalIgnoreCase)))
            collapsed = "_" + collapsed;

        return collapsed;
    }
}
