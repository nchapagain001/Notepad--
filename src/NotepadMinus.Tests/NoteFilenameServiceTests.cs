using System;
using NotepadMinus.Services;

namespace NotepadMinus.Tests;

public class NoteFilenameServiceTests
{
    private static readonly DateTime D = new(2026, 6, 13);

    [Fact]
    public void BuildFileName_UsesFirstLinePlusDate()
    {
        var name = NoteFilenameService.BuildFileName("Meeting with Aria", D);
        Assert.Equal("Meeting with Aria 2026-06-13.txt", name);
    }

    [Fact]
    public void BuildFileName_EmptyFirstLine_FallsBackToDateOnly()
    {
        Assert.Equal("2026-06-13.txt", NoteFilenameService.BuildFileName(null, D));
        Assert.Equal("2026-06-13.txt", NoteFilenameService.BuildFileName("", D));
        Assert.Equal("2026-06-13.txt", NoteFilenameService.BuildFileName("   \t  ", D));
        Assert.Equal("2026-06-13.txt", NoteFilenameService.BuildFileName("\r\n\r\n", D));
    }

    [Fact]
    public void BuildFileName_SkipsLeadingEmptyLines()
    {
        var name = NoteFilenameService.BuildFileName("\n\n  Real Title  \nbody", D);
        Assert.Equal("Real Title 2026-06-13.txt", name);
    }

    [Theory]
    [InlineData("path/with/slashes", "path-with-slashes 2026-06-13.txt")]
    [InlineData("colons:and|pipes?", "colons-and-pipes- 2026-06-13.txt")]
    [InlineData("trailing dots...", "trailing dots 2026-06-13.txt")]
    public void BuildFileName_StripsInvalidChars(string input, string expected)
    {
        Assert.Equal(expected, NoteFilenameService.BuildFileName(input, D));
    }

    [Fact]
    public void BuildFileName_TruncatesVeryLongFirstLines()
    {
        var longLine = new string('a', 500);
        var name = NoteFilenameService.BuildFileName(longLine, D);
        // 80 chars max title + " 2026-06-13.txt" = 80 + 15 = 95
        Assert.True(name.Length <= 95);
        Assert.EndsWith(" 2026-06-13.txt", name);
    }

    [Fact]
    public void BuildFileName_ReservedDeviceNamesAreEscaped()
    {
        var name = NoteFilenameService.BuildFileName("CON", D);
        Assert.StartsWith("_CON", name);
    }

    [Fact]
    public void Disambiguate_ReturnsOriginalWhenUnique()
    {
        var result = NoteFilenameService.DisambiguateFileName("foo 2026-06-13.txt", new[] { "other.txt" });
        Assert.Equal("foo 2026-06-13.txt", result);
    }

    [Fact]
    public void Disambiguate_AppendsCounterOnCollision()
    {
        var existing = new[] { "foo 2026-06-13.txt", "foo 2026-06-13 (2).txt" };
        var result = NoteFilenameService.DisambiguateFileName("foo 2026-06-13.txt", existing);
        Assert.Equal("foo 2026-06-13 (3).txt", result);
    }

    [Fact]
    public void Disambiguate_IsCaseInsensitive()
    {
        var existing = new[] { "FOO 2026-06-13.txt" };
        var result = NoteFilenameService.DisambiguateFileName("foo 2026-06-13.txt", existing);
        Assert.Equal("foo 2026-06-13 (2).txt", result);
    }
}
