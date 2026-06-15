using NotepadMinus.Services;

namespace NotepadMinus.Tests;

public class NoteContentTests
{
    [Fact]
    public void Split_EmptyOrNull_ReturnsEmptyPair()
    {
        Assert.Equal((string.Empty, string.Empty), NoteContent.Split(null));
        Assert.Equal((string.Empty, string.Empty), NoteContent.Split(string.Empty));
    }

    [Fact]
    public void Split_SingleLineNoNewline_GoesIntoHeader()
    {
        var (h, b) = NoteContent.Split("only a header");
        Assert.Equal("only a header", h);
        Assert.Equal(string.Empty, b);
    }

    [Fact]
    public void Split_LfNewline_HeaderAndBody()
    {
        var (h, b) = NoteContent.Split("Title\nline 2\nline 3");
        Assert.Equal("Title", h);
        Assert.Equal("line 2\nline 3", b);
    }

    [Fact]
    public void Split_CrLfNewline_TrimsCrFromHeader()
    {
        var (h, b) = NoteContent.Split("Title\r\nbody");
        Assert.Equal("Title", h);
        Assert.Equal("body", b);
    }

    [Fact]
    public void Split_EmptyHeader_Preserved()
    {
        var (h, b) = NoteContent.Split("\nbody only");
        Assert.Equal(string.Empty, h);
        Assert.Equal("body only", b);
    }

    [Fact]
    public void Join_BothNonEmpty_UsesPlatformNewline()
    {
        var joined = NoteContent.Join("Title", "body");
        Assert.Equal("Title" + System.Environment.NewLine + "body", joined);
    }

    [Fact]
    public void Join_EmptyBody_ReturnsHeaderOnly()
    {
        Assert.Equal("Title", NoteContent.Join("Title", string.Empty));
        Assert.Equal("Title", NoteContent.Join("Title", null));
    }

    [Fact]
    public void RoundTrip_PreservesHeaderAndBody()
    {
        var cases = new[]
        {
            ("Title", "first body line\nsecond"),
            ("",      "body only"),
            ("only header", ""),
            ("Title", ""),
        };
        foreach (var (h, b) in cases)
        {
            var joined = NoteContent.Join(h, b);
            var (h2, b2) = NoteContent.Split(joined);
            Assert.Equal(h, h2);
            Assert.Equal(b, b2);
        }
    }
}
