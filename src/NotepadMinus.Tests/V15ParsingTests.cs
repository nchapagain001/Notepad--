using System;
using NotepadMinus.Services;
using Xunit;

namespace NotepadMinus.Tests;

public class NoteFilenameServiceExtTests
{
    [Fact]
    public void BuildFileName_DefaultsToTxt() =>
        Assert.Equal("hello 2026-06-13.txt",
            NoteFilenameService.BuildFileName("hello", new DateTime(2026, 6, 13)));

    [Theory]
    [InlineData(".md",   "hello 2026-06-13.md")]
    [InlineData("md",    "hello 2026-06-13.md")]
    [InlineData(".JSON", "hello 2026-06-13.json")]
    [InlineData("",      "hello 2026-06-13.txt")]
    [InlineData(null,    "hello 2026-06-13.txt")]
    public void BuildFileName_HonorsExtension(string? ext, string expected)
        => Assert.Equal(expected, NoteFilenameService.BuildFileName("hello", new DateTime(2026, 6, 13), ext));

    [Fact]
    public void BuildFileName_EmptyTitleFallback_RespectsExtension()
        => Assert.Equal("2026-06-13.json",
            NoteFilenameService.BuildFileName(null, new DateTime(2026, 6, 13), ".json"));
}

public class ThemeFontParseTests
{
    [Theory]
    [InlineData("Sans",  FontService.Choice.Sans)]
    [InlineData("serif", FontService.Choice.Serif)]
    [InlineData("MONO",  FontService.Choice.Mono)]
    [InlineData("",      FontService.Choice.Mono)]
    [InlineData(null,    FontService.Choice.Mono)]
    public void FontService_Parse(string? raw, FontService.Choice expected)
        => Assert.Equal(expected, FontService.Parse(raw));

    [Theory]
    [InlineData(FontService.Choice.Sans,  "Segoe UI")]
    [InlineData(FontService.Choice.Serif, "Cambria")]
    [InlineData(FontService.Choice.Mono,  "Consolas")]
    public void FontService_FamilyFor(FontService.Choice c, string expected)
        => Assert.Equal(expected, FontService.FamilyFor(c));

    [Theory]
    [InlineData("system", ThemeService.Theme.System)]
    [InlineData("Light",  ThemeService.Theme.Light)]
    [InlineData("SEPIA",  ThemeService.Theme.Sepia)]
    [InlineData("dim",    ThemeService.Theme.Dim)]
    [InlineData("Dark",   ThemeService.Theme.Dark)]
    [InlineData(null,     ThemeService.Theme.System)]
    [InlineData("xyz",    ThemeService.Theme.System)]
    public void ThemeService_Parse(string? raw, ThemeService.Theme expected)
        => Assert.Equal(expected, ThemeService.Parse(raw));
}
