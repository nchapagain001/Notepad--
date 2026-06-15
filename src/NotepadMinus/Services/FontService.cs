using System;

namespace NotepadMinus.Services;

/// <summary>
/// v1.5 font choice. The user picks one of three high-level options (Sans / Serif / Mono)
/// in View → Font; each maps to a concrete font family applied to the header band and the
/// body of every tab. No per-document variation — "one font within a document" still holds.
/// </summary>
public static class FontService
{
    public enum Choice { Sans, Serif, Mono }

    public static Choice Parse(string? raw)
        => raw?.ToLowerInvariant() switch
        {
            "sans" => Choice.Sans,
            "serif" => Choice.Serif,
            _ => Choice.Mono,
        };

    public static string ToConfigString(Choice c) => c switch
    {
        Choice.Sans => "Sans",
        Choice.Serif => "Serif",
        _ => "Mono",
    };

    public static string FamilyFor(Choice c) => c switch
    {
        Choice.Sans => "Segoe UI",
        Choice.Serif => "Cambria",
        _ => "Consolas",
    };
}
