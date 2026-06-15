using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NotepadMinus.Services;

/// <summary>
/// Pure search logic for the in-file Find bar (v1.5.5). UI-agnostic and unit-tested.
/// Returns an <see cref="IsValid"/> flag so the bar can show "Invalid regular expression"
/// without raising or crashing.
/// </summary>
public static class FindService
{
    /// <summary>Hard cap on matches returned, per productv1.5 Find prompt. Navigation still
    /// works at the limit; viewport-only highlighting handles the visual side.</summary>
    public const int MaxMatches = 10_000;

    public readonly record struct Match(int Start, int Length);

    public sealed record Result(bool IsValid, IReadOnlyList<Match> Matches, bool Truncated)
    {
        public static readonly Result Empty = new(true, Array.Empty<Match>(), false);
        public static readonly Result Invalid = new(false, Array.Empty<Match>(), false);
    }

    public static Result Find(string? text, string? query, bool caseSensitive, bool wholeWord, bool useRegex)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return Result.Empty;

        Regex regex;
        try
        {
            regex = BuildRegex(query!, caseSensitive, wholeWord, useRegex);
        }
        catch (ArgumentException)
        {
            // RegexParseException is an ArgumentException subclass — this single catch covers
            // both malformed patterns and bad option combinations.
            return Result.Invalid;
        }

        var matches = new List<Match>();
        var truncated = false;
        var search = regex.Matches(text!);
        foreach (System.Text.RegularExpressions.Match m in search)
        {
            if (m.Length == 0)
            {
                // Avoid infinite loops on zero-length matches (e.g. ^, $, lookarounds).
                continue;
            }
            if (matches.Count >= MaxMatches)
            {
                truncated = true;
                break;
            }
            matches.Add(new Match(m.Index, m.Length));
        }
        return new Result(true, matches, truncated);
    }

    private static Regex BuildRegex(string query, bool caseSensitive, bool wholeWord, bool useRegex)
    {
        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        if (!caseSensitive) options |= RegexOptions.IgnoreCase;

        var pattern = useRegex ? query : Regex.Escape(query);
        if (wholeWord) pattern = $@"\b(?:{pattern})\b";

        // Compiled gets us much faster repeated searches on large files; capped by .NET internally.
        return new Regex(pattern, options | RegexOptions.Compiled, TimeSpan.FromSeconds(2));
    }

    /// <summary>Index of the next match after the current cursor position, wrapping to the start.</summary>
    public static int NextIndex(IReadOnlyList<Match> matches, int cursor)
    {
        if (matches.Count == 0) return -1;
        for (int i = 0; i < matches.Count; i++)
            if (matches[i].Start >= cursor) return i;
        return 0; // wrap
    }

    /// <summary>Index of the previous match before the current cursor position, wrapping to the end.</summary>
    public static int PreviousIndex(IReadOnlyList<Match> matches, int cursor)
    {
        if (matches.Count == 0) return -1;
        for (int i = matches.Count - 1; i >= 0; i--)
            if (matches[i].Start < cursor) return i;
        return matches.Count - 1; // wrap
    }

    /// <summary>
    /// Compute the replacement string for a single match's text. In literal mode this is
    /// just <paramref name="replacement"/> verbatim. In regex mode, $1/${name} backrefs
    /// are expanded against the original pattern. Returns null when the regex is invalid.
    /// </summary>
    public static string? ReplaceOne(string matchedText, string query, string replacement,
        bool caseSensitive, bool wholeWord, bool useRegex)
    {
        if (!useRegex) return replacement ?? string.Empty;
        try
        {
            var regex = BuildRegex(query, caseSensitive, wholeWord, useRegex: true);
            // Single replacement against the exact matched substring.
            return regex.Replace(matchedText, replacement ?? string.Empty, 1);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public readonly record struct ReplaceAllResult(bool IsValid, string NewText, int ReplacedCount);

    /// <summary>
    /// Replace every match of <paramref name="query"/> in <paramref name="text"/> with
    /// <paramref name="replacement"/>. In regex mode, substitutions ($1, ${name}, etc.)
    /// are honored. Returns IsValid=false (and the original text) when the regex is malformed.
    /// </summary>
    public static ReplaceAllResult ReplaceAll(string? text, string? query, string? replacement,
        bool caseSensitive, bool wholeWord, bool useRegex)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return new ReplaceAllResult(true, text ?? string.Empty, 0);

        Regex regex;
        try
        {
            regex = BuildRegex(query!, caseSensitive, wholeWord, useRegex);
        }
        catch (ArgumentException)
        {
            return new ReplaceAllResult(false, text!, 0);
        }

        var count = 0;
        string Eval(System.Text.RegularExpressions.Match m)
        {
            if (m.Length == 0) return m.Value; // mirror Find: skip zero-length matches
            count++;
            return useRegex ? m.Result(replacement ?? string.Empty) : (replacement ?? string.Empty);
        }

        var newText = regex.Replace(text!, Eval);
        return new ReplaceAllResult(true, newText, count);
    }
}
