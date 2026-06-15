using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotepadMinus.Services;

/// <summary>
/// Reads and writes notes as plain-text files in a single flat folder.
/// v1.5 expands the supported extensions beyond .txt; see <see cref="TextFileExtensions"/>.
/// All filenames are managed through <see cref="NoteFilenameService"/>.
/// </summary>
public sealed class NoteRepository
{
    private readonly string _notesFolder;

    public NoteRepository(string notesFolder)
    {
        if (string.IsNullOrWhiteSpace(notesFolder)) throw new ArgumentException("Folder required", nameof(notesFolder));
        _notesFolder = notesFolder;
        Directory.CreateDirectory(_notesFolder);
    }

    public string NotesFolder => _notesFolder;

    public IReadOnlyList<NoteFile> List()
    {
        if (!Directory.Exists(_notesFolder)) return Array.Empty<NoteFile>();
        return new DirectoryInfo(_notesFolder)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(f => TextFileExtensions.IsKnown(f.Extension))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new NoteFile(f.FullName, f.Name, f.LastWriteTimeUtc))
            .ToArray();
    }

    public async Task<string> ReadAsync(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Reads a file and returns text + detected encoding + detected line-ending convention.</summary>
    public static async Task<FileText> ReadWithMetaAsync(string filePath)
    {
        if (!File.Exists(filePath)) return new FileText(string.Empty, new UTF8Encoding(false), "\r\n");
        byte[] raw = await File.ReadAllBytesAsync(filePath);
        var enc = DetectEncoding(raw, out int bomLen);
        var text = enc.GetString(raw, bomLen, raw.Length - bomLen);
        var nl = text.Contains("\r\n") ? "\r\n" : (text.Contains('\n') ? "\n" : "\r\n");
        return new FileText(text, enc, nl);
    }

    public async Task WriteAsync(string filePath, string content)
        => await WriteAsync(filePath, content, new UTF8Encoding(false));

    public async Task WriteAsync(string filePath, string content, Encoding encoding)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        // Atomic-ish write: temp + replace to avoid corrupting files on crash.
        var tmp = filePath + ".tmp";
        await File.WriteAllTextAsync(tmp, content ?? string.Empty, encoding);
        try
        {
            if (File.Exists(filePath)) File.Replace(tmp, filePath, null);
            else File.Move(tmp, filePath);
        }
        catch
        {
            // Fall back to a plain overwrite if Replace failed (e.g., cross-volume).
            File.Copy(tmp, filePath, overwrite: true);
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public void Rename(string oldPath, string newPath)
    {
        if (!File.Exists(oldPath)) return;
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;
        var dir = Path.GetDirectoryName(newPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.Move(oldPath, newPath);
    }

    public IEnumerable<string> ExistingFileNames()
    {
        if (!Directory.Exists(_notesFolder)) return Array.Empty<string>();
        return Directory.EnumerateFiles(_notesFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(p => TextFileExtensions.IsKnown(Path.GetExtension(p)))
            .Select(Path.GetFileName)!;
    }

    private static Encoding DetectEncoding(byte[] raw, out int bomLen)
    {
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) { bomLen = 3; return new UTF8Encoding(true); }
        if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE) { bomLen = 2; return Encoding.Unicode; }
        if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF) { bomLen = 2; return Encoding.BigEndianUnicode; }
        bomLen = 0;
        return new UTF8Encoding(false);
    }
}

public sealed record NoteFile(string FullPath, string FileName, DateTime LastWriteTimeUtc);

public sealed record FileText(string Text, Encoding Encoding, string LineEnding);
