using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NotepadMinus.Services;

/// <summary>
/// Extracts unstructured #hashtags from note body text. v1 search treats these as
/// plain substrings in the file content; this helper exists for filtering and tests.
/// </summary>
public static class HashtagParser
{
    // A hashtag is '#' followed by 1+ letters/digits/underscores/hyphens, and must
    // start at the beginning of the string or after whitespace/punctuation that is
    // not itself part of a tag. We deliberately keep this simple.
    private static readonly Regex TagRegex = new(
        @"(?<![\w#])#([A-Za-z0-9_][A-Za-z0-9_\-]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Parse(string? body)
    {
        if (string.IsNullOrEmpty(body)) return Array.Empty<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (Match m in TagRegex.Matches(body))
        {
            var tag = m.Groups[1].Value;
            if (seen.Add(tag)) result.Add(tag);
        }
        return result;
    }

    public static bool ContainsTag(string? body, string tag)
    {
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(tag)) return false;
        var needle = tag.StartsWith('#') ? tag[1..] : tag;
        foreach (var t in Parse(body))
            if (string.Equals(t, needle, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
