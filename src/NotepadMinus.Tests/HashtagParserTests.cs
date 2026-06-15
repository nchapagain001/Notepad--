using System.Linq;
using NotepadMinus.Services;

namespace NotepadMinus.Tests;

public class HashtagParserTests
{
    [Fact]
    public void EmptyBody_ReturnsNothing()
    {
        Assert.Empty(HashtagParser.Parse(null));
        Assert.Empty(HashtagParser.Parse(string.Empty));
    }

    [Fact]
    public void ParsesBasicHashtags()
    {
        var tags = HashtagParser.Parse("Talked about #design and #frontend today.").ToArray();
        Assert.Equal(new[] { "design", "frontend" }, tags);
    }

    [Fact]
    public void DeduplicatesCaseInsensitively()
    {
        var tags = HashtagParser.Parse("#Foo #foo #FOO #bar").ToArray();
        Assert.Equal(new[] { "Foo", "bar" }, tags);
    }

    [Fact]
    public void IgnoresHashesInMiddleOfWords()
    {
        var tags = HashtagParser.Parse("issue#123 and email a#b are not tags but #tag is").ToArray();
        Assert.Equal(new[] { "tag" }, tags);
    }

    [Fact]
    public void HashAloneIsNotATag()
    {
        Assert.Empty(HashtagParser.Parse("# just a hash"));
        Assert.Empty(HashtagParser.Parse("##doubled"));
    }

    [Fact]
    public void AllowsHyphensAndUnderscores()
    {
        var tags = HashtagParser.Parse("#client-a #q3_review").ToArray();
        Assert.Equal(new[] { "client-a", "q3_review" }, tags);
    }

    [Fact]
    public void ContainsTag_WorksWithAndWithoutHash()
    {
        var body = "follow up on #design";
        Assert.True(HashtagParser.ContainsTag(body, "design"));
        Assert.True(HashtagParser.ContainsTag(body, "#design"));
        Assert.True(HashtagParser.ContainsTag(body, "DESIGN"));
        Assert.False(HashtagParser.ContainsTag(body, "other"));
    }
}
