using Intentionality.Core;

namespace Intentionality.Tests;

public class DistractionRulesTests
{
    [Theory]
    [InlineData("chrome", "Funny Cats - YouTube", DistractionRules.YouTube)]
    [InlineData("msedge", "YouTube", DistractionRules.YouTube)]
    [InlineData("firefox", "reddit: the front page of the internet", DistractionRules.Reddit)]
    [InlineData("brave", "r/programming - Reddit", DistractionRules.Reddit)]
    [InlineData("chrome", "r/AskReddit", DistractionRules.Reddit)]
    [InlineData("Spotify", "Song Name", DistractionRules.Spotify)]
    [InlineData("spotify.exe", "Spotify", DistractionRules.Spotify)]
    public void Match_KnownDistractions(string process, string title, string expected)
    {
        Assert.Equal(expected, DistractionRules.Match(process, title));
    }

    [Theory]
    [InlineData("chrome", "Stack Overflow - Where Developers Learn")]
    [InlineData("code", "YouTube - VS Code")]
    [InlineData("explorer", "Reddit folder")]
    [InlineData(null, "YouTube")]
    [InlineData("chrome", "")]
    public void Match_NonDistractions_ReturnsNull(string? process, string title)
    {
        Assert.Null(DistractionRules.Match(process, title));
    }

    [Fact]
    public void DisplayName_MapsKnownRules()
    {
        Assert.Equal("YouTube", DistractionRules.DisplayName(DistractionRules.YouTube));
        Assert.Equal("Reddit", DistractionRules.DisplayName(DistractionRules.Reddit));
        Assert.Equal("Spotify", DistractionRules.DisplayName(DistractionRules.Spotify));
    }
}
