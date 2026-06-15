using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NotepadMinus.Services;

/// <summary>
/// v1.5 backup / import: bundles every note in a folder into a single, human-readable
/// JSON document. Forward-compatible with v2 sidecar metadata via <see cref="BackupNote"/>'s
/// optional fields. Import never overwrites; collisions are disambiguated with " (imported)"
/// then " (2)", " (3)", … like v1's untitled-note disambiguation.
/// </summary>
public sealed class BackupService
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<int> ExportAsync(string sourceFolder, string outputJsonPath)
    {
        if (!Directory.Exists(sourceFolder)) throw new DirectoryNotFoundException(sourceFolder);

        var notes = new List<BackupNote>();
        foreach (var path in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly)
                                      .Where(p => TextFileExtensions.IsKnown(Path.GetExtension(p)))
                                      .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var fi = new FileInfo(path);
            var content = await File.ReadAllTextAsync(path);
            notes.Add(new BackupNote
            {
                Filename = fi.Name,
                Extension = fi.Extension.ToLowerInvariant(),
                CreatedAt = fi.CreationTimeUtc,
                ModifiedAt = fi.LastWriteTimeUtc,
                Content = content,
            });
        }

        var doc = new BackupDocument
        {
            ExportedAt = DateTime.UtcNow,
            ExportedBy = "notepad-- v1.5",
            SchemaVersion = CurrentSchemaVersion,
            Notes = notes,
        };

        var outDir = Path.GetDirectoryName(outputJsonPath);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        await File.WriteAllTextAsync(outputJsonPath,
            JsonSerializer.Serialize(doc, Json),
            new UTF8Encoding(false));

        return notes.Count;
    }

    /// <summary>
    /// Imports notes from a backup file into <paramref name="targetFolder"/>.
    /// Existing files are NEVER overwritten — collisions are renamed.
    /// Returns the number of notes successfully written.
    /// </summary>
    public async Task<int> ImportAsync(string backupJsonPath, string targetFolder)
    {
        if (!File.Exists(backupJsonPath)) throw new FileNotFoundException(backupJsonPath);
        Directory.CreateDirectory(targetFolder);

        BackupDocument? doc;
        var raw = await File.ReadAllTextAsync(backupJsonPath);
        doc = JsonSerializer.Deserialize<BackupDocument>(raw, Json);
        if (doc is null || doc.Notes is null) return 0;

        int wrote = 0;
        var existing = new HashSet<string>(
            Directory.EnumerateFiles(targetFolder).Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        foreach (var n in doc.Notes)
        {
            var filename = SanitizeFilename(n.Filename, n.Extension);
            if (string.IsNullOrEmpty(filename)) continue;

            var finalName = ResolveCollision(filename, existing);
            var fullPath = Path.Combine(targetFolder, finalName);
            await File.WriteAllTextAsync(fullPath, n.Content ?? string.Empty, new UTF8Encoding(false));

            try
            {
                if (n.CreatedAt != default) File.SetCreationTimeUtc(fullPath, n.CreatedAt);
                if (n.ModifiedAt != default) File.SetLastWriteTimeUtc(fullPath, n.ModifiedAt);
            }
            catch { /* timestamps are best-effort */ }

            existing.Add(finalName);
            wrote++;
        }

        return wrote;
    }

    public static string ResolveCollision(string filename, ICollection<string> existing)
    {
        if (!existing.Contains(filename)) return filename;
        var stem = Path.GetFileNameWithoutExtension(filename);
        var ext = Path.GetExtension(filename);
        var imported = $"{stem} (imported){ext}";
        if (!existing.Contains(imported)) return imported;
        int n = 2;
        while (true)
        {
            var candidate = $"{stem} (imported) ({n}){ext}";
            if (!existing.Contains(candidate)) return candidate;
            n++;
        }
    }

    private static string SanitizeFilename(string? requested, string? ext)
    {
        if (string.IsNullOrWhiteSpace(requested)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(requested.Length);
        foreach (var ch in requested)
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '-' : ch);
        var s = sb.ToString().Trim().TrimEnd('.', ' ');
        if (s.Length == 0) return string.Empty;
        if (!string.IsNullOrWhiteSpace(ext) && !s.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            var actual = ext!.StartsWith('.') ? ext : "." + ext;
            s += actual.ToLowerInvariant();
        }
        return s;
    }
}

public sealed class BackupDocument
{
    [JsonPropertyName("exportedAt")] public DateTime ExportedAt { get; set; }
    [JsonPropertyName("exportedBy")] public string ExportedBy { get; set; } = "notepad-- v1.5";
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = BackupService.CurrentSchemaVersion;
    [JsonPropertyName("notes")] public List<BackupNote> Notes { get; set; } = new();
}

public sealed class BackupNote
{
    [JsonPropertyName("filename")] public string Filename { get; set; } = string.Empty;
    [JsonPropertyName("extension")] public string Extension { get; set; } = ".txt";
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("modifiedAt")] public DateTime ModifiedAt { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}
