using Halfempty.Nametag;
using Xunit;

namespace Tests;

public class WrapTextTests
{
    // Simple mock: each character is 10 units wide
    private static double SimpleMeasure(string text) => text.Length * 10;

    [Fact]
    public void SingleWordThatFits_ReturnsSingleLine()
    {
        var result = NametagTemplate.WrapText("Hello", maxWidth: 100, SimpleMeasure);
        
        Assert.Single(result);
        Assert.Equal("Hello", result[0]);
    }

    [Fact]
    public void MultipleWordsThatFit_ReturnsSingleLine()
    {
        var result = NametagTemplate.WrapText("Hello World", maxWidth: 200, SimpleMeasure);
        
        Assert.Single(result);
        Assert.Equal("Hello World", result[0]);
    }

    [Fact]
    public void WordsExceedWidth_WrapsToMultipleLines()
    {
        // "Hello World" = 11 chars = 110 units, maxWidth = 80
        var result = NametagTemplate.WrapText("Hello World", maxWidth: 80, SimpleMeasure);
        
        Assert.Equal(2, result.Count);
        Assert.Equal("Hello", result[0]);
        Assert.Equal("World", result[1]);
    }

    [Fact]
    public void LongSentence_WrapsCorrectly()
    {
        // Each word wraps when it would exceed 100 units
        var result = NametagTemplate.WrapText("One Two Three Four", maxWidth: 100, SimpleMeasure);
        
        // "One Two" = 7 chars = 70 units (fits)
        // "One Two Three" = 13 chars = 130 units (doesn't fit, wrap)
        // "Three Four" = 10 chars = 100 units (fits)
        Assert.Equal(2, result.Count);
        Assert.Equal("One Two", result[0]);
        Assert.Equal("Three Four", result[1]);
    }

    [Fact]
    public void SingleLongWord_StaysOnOwnLine()
    {
        // Word is longer than maxWidth but can't be split
        var result = NametagTemplate.WrapText("Supercalifragilistic", maxWidth: 50, SimpleMeasure);
        
        Assert.Single(result);
        Assert.Equal("Supercalifragilistic", result[0]);
    }

    [Fact]
    public void EmptyString_ReturnsEmptyList()
    {
        var result = NametagTemplate.WrapText("", maxWidth: 100, SimpleMeasure);
        
        Assert.Empty(result);
    }

    [Fact]
    public void SingleSpace_ReturnsEmptyList()
    {
        var result = NametagTemplate.WrapText(" ", maxWidth: 100, SimpleMeasure);
        
        // Split on space produces empty strings which get filtered
        Assert.Empty(result);
    }

    [Fact]
    public void ThreeWords_EachOnOwnLine()
    {
        // maxWidth = 50, each word is ~5 chars = 50 units
        // "Hello" = 50 units (fits exactly)
        // "Hello There" = 110 units (doesn't fit)
        var result = NametagTemplate.WrapText("Hello There World", maxWidth: 55, SimpleMeasure);
        
        Assert.Equal(3, result.Count);
        Assert.Equal("Hello", result[0]);
        Assert.Equal("There", result[1]);
        Assert.Equal("World", result[2]);
    }
}
