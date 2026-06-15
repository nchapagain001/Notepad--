using System.IO;
using NotepadMinus.Services;
using Xunit;

namespace NotepadMinus.Tests;

public class TextFileExtensionsTests
{
    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".TXT", true)]
    [InlineData(".md", true)]
    [InlineData(".json", true)]
    [InlineData(".csv", true)]
    [InlineData(".log", true)]
    [InlineData(".yaml", true)]
    [InlineData(".yml", true)]
    [InlineData(".exe", false)]
    [InlineData(".png", false)]
    [InlineData(".", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKnown_DetectsTextExtensions(string? ext, bool expected)
        => Assert.Equal(expected, TextFileExtensions.IsKnown(ext));

    [Fact]
    public void LooksLikeText_TrueForUtf8File()
    {
        var p = Path.GetTempFileName();
        try
        {
            File.WriteAllText(p, "hello world\nplain text here");
            Assert.True(TextFileExtensions.LooksLikeText(p));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void LooksLikeText_FalseForBinaryFile()
    {
        var p = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(p, new byte[] { 0x00, 0x01, 0x02, 0x00, 0xFF });
            Assert.False(TextFileExtensions.LooksLikeText(p));
        }
        finally { File.Delete(p); }
    }
}
