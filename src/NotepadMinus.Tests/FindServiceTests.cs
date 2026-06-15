using NotepadMinus.Services;
using Xunit;

namespace NotepadMinus.Tests;

public class FindServiceTests
{
    [Fact]
    public void EmptyQuery_ReturnsEmpty()
    {
        var r = FindService.Find("hello", "", false, false, false);
        Assert.True(r.IsValid);
        Assert.Empty(r.Matches);
    }

    [Fact]
    public void EmptyText_ReturnsEmpty()
    {
        var r = FindService.Find("", "x", false, false, false);
        Assert.True(r.IsValid);
        Assert.Empty(r.Matches);
    }

    [Fact]
    public void Literal_CaseInsensitive_Default()
    {
        var r = FindService.Find("Hello hello HELLO", "hello", caseSensitive: false, wholeWord: false, useRegex: false);
        Assert.True(r.IsValid);
        Assert.Equal(3, r.Matches.Count);
        Assert.Equal(0, r.Matches[0].Start);
        Assert.Equal(5, r.Matches[0].Length);
    }

    [Fact]
    public void Literal_CaseSensitive_OnlyExactCase()
    {
        var r = FindService.Find("Hello hello HELLO", "hello", caseSensitive: true, wholeWord: false, useRegex: false);
        Assert.Single(r.Matches);
        Assert.Equal(6, r.Matches[0].Start);
    }

    [Fact]
    public void WholeWord_RejectsSubstring()
    {
        var r = FindService.Find("cat cats catalog cat", "cat", false, wholeWord: true, false);
        Assert.Equal(2, r.Matches.Count);
        Assert.Equal(0, r.Matches[0].Start);
        Assert.Equal(17, r.Matches[1].Start);
    }

    [Fact]
    public void Literal_EscapesRegexMetaCharacters()
    {
        // '.' should match a literal dot, not any char, when useRegex is false.
        var r = FindService.Find("a.b axb a.b", ".", false, false, useRegex: false);
        Assert.Equal(2, r.Matches.Count);
    }

    [Fact]
    public void Regex_DotMatchesAnyCharByDefault()
    {
        // Regex on: '.' is the wildcard.
        var r = FindService.Find("a.b axb", "a.b", false, false, useRegex: true);
        Assert.Equal(2, r.Matches.Count);
    }

    [Fact]
    public void Regex_CaptureGroupsWork()
    {
        var r = FindService.Find("foo123 bar456", @"(\w+)(\d+)", false, false, true);
        Assert.Equal(2, r.Matches.Count);
        Assert.Equal(0, r.Matches[0].Start);
        Assert.Equal(6, r.Matches[0].Length);
    }

    [Fact]
    public void Regex_Invalid_ReturnsInvalidNotThrow()
    {
        var r = FindService.Find("anything", "[unclosed", false, false, useRegex: true);
        Assert.False(r.IsValid);
        Assert.Empty(r.Matches);
    }

    [Fact]
    public void Regex_WholeWord_WrapsWithBoundaries()
    {
        var r = FindService.Find("foo123 foobar", @"\w+", false, wholeWord: true, useRegex: true);
        Assert.Equal(2, r.Matches.Count);
        Assert.Equal("foo123", "foo123 foobar".Substring(r.Matches[0].Start, r.Matches[0].Length));
        Assert.Equal("foobar", "foo123 foobar".Substring(r.Matches[1].Start, r.Matches[1].Length));
    }

    [Fact]
    public void NextIndex_WrapsToStart()
    {
        var ms = new[] { new FindService.Match(0, 1), new FindService.Match(10, 1), new FindService.Match(20, 1) };
        Assert.Equal(2, FindService.NextIndex(ms, 15));
        Assert.Equal(0, FindService.NextIndex(ms, 25)); // wrap
        Assert.Equal(0, FindService.NextIndex(ms, 0));
        Assert.Equal(1, FindService.NextIndex(ms, 1));
    }

    [Fact]
    public void PreviousIndex_WrapsToEnd()
    {
        var ms = new[] { new FindService.Match(0, 1), new FindService.Match(10, 1), new FindService.Match(20, 1) };
        Assert.Equal(1, FindService.PreviousIndex(ms, 15));
        Assert.Equal(2, FindService.PreviousIndex(ms, -1)); // wrap
        Assert.Equal(2, FindService.PreviousIndex(ms, 0));
    }

    [Fact]
    public void NextIndex_EmptyList_ReturnsMinusOne()
    {
        Assert.Equal(-1, FindService.NextIndex(System.Array.Empty<FindService.Match>(), 0));
    }

    [Fact]
    public void Multiline_Pattern_MatchesAcrossLines_WithMultilineFlag()
    {
        // ^ should match line start by default (RegexOptions.Multiline is enabled).
        var r = FindService.Find("line1\nline2\nline3", "^line", false, false, useRegex: true);
        Assert.Equal(3, r.Matches.Count);
    }

    [Fact]
    public void ReplaceAll_Literal_ReplacesEveryOccurrence()
    {
        var r = FindService.ReplaceAll("foo bar foo baz foo", "foo", "qux", false, false, false);
        Assert.True(r.IsValid);
        Assert.Equal(3, r.ReplacedCount);
        Assert.Equal("qux bar qux baz qux", r.NewText);
    }

    [Fact]
    public void ReplaceAll_CaseSensitive_OnlyExactCase()
    {
        var r = FindService.ReplaceAll("Foo foo FOO", "foo", "x", caseSensitive: true, false, false);
        Assert.Equal(1, r.ReplacedCount);
        Assert.Equal("Foo x FOO", r.NewText);
    }

    [Fact]
    public void ReplaceAll_WholeWord_Bounded()
    {
        var r = FindService.ReplaceAll("cat cats cat", "cat", "dog", false, wholeWord: true, false);
        Assert.Equal(2, r.ReplacedCount);
        Assert.Equal("dog cats dog", r.NewText);
    }

    [Fact]
    public void ReplaceAll_Regex_BackreferencesWork()
    {
        // Swap groups via $2 $1
        var r = FindService.ReplaceAll("alice smith, bob jones", @"(\w+) (\w+)", "$2 $1", false, false, true);
        Assert.Equal(2, r.ReplacedCount);
        Assert.Equal("smith alice, jones bob", r.NewText);
    }

    [Fact]
    public void ReplaceAll_InvalidRegex_ReturnsInvalid_OriginalTextUnchanged()
    {
        var r = FindService.ReplaceAll("hello", "[unclosed", "x", false, false, true);
        Assert.False(r.IsValid);
        Assert.Equal("hello", r.NewText);
        Assert.Equal(0, r.ReplacedCount);
    }

    [Fact]
    public void ReplaceAll_EmptyQuery_NoOp()
    {
        var r = FindService.ReplaceAll("hello", "", "x", false, false, false);
        Assert.True(r.IsValid);
        Assert.Equal(0, r.ReplacedCount);
        Assert.Equal("hello", r.NewText);
    }

    [Fact]
    public void ReplaceOne_Literal_ReturnsReplacementVerbatim()
    {
        var s = FindService.ReplaceOne("foo", "foo", "BAR", false, false, false);
        Assert.Equal("BAR", s);
    }

    [Fact]
    public void ReplaceOne_Regex_ExpandsBackreferences()
    {
        var s = FindService.ReplaceOne("alice smith", @"(\w+) (\w+)", "$2 $1", false, false, true);
        Assert.Equal("smith alice", s);
    }

    [Fact]
    public void ReplaceOne_InvalidRegex_ReturnsNull()
    {
        var s = FindService.ReplaceOne("anything", "[unclosed", "x", false, false, true);
        Assert.Null(s);
    }
}
