using System;

namespace NotepadMinus.Services;

/// <summary>
/// Splits a note's raw text into a single-line header (the first line) and a body
/// (everything after the first newline). Round-trip safe: <see cref="Join"/> followed
/// by <see cref="Split"/> returns the same pair.
/// </summary>
public static class NoteContent
{
    public static (string Header, string Body) Split(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return (string.Empty, string.Empty);

        var idx = raw.IndexOf('\n');
        if (idx < 0)
        {
            return (raw.TrimEnd('\r'), string.Empty);
        }

        var header = raw[..idx].TrimEnd('\r');
        var body = raw[(idx + 1)..];
        return (header, body);
    }

    public static string Join(string? header, string? body) => Join(header, body, Environment.NewLine);

    public static string Join(string? header, string? body, string? newLine)
    {
        header ??= string.Empty;
        body ??= string.Empty;
        newLine = string.IsNullOrEmpty(newLine) ? Environment.NewLine : newLine;

        // No body → just the header (no trailing newline).
        if (body.Length == 0) return header;

        return header + newLine + body;
    }
}
