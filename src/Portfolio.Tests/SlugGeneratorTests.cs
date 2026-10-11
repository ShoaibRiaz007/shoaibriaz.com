using Portfolio.Domain.Services;
using Xunit;

namespace Portfolio.Tests;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Trim Me  ", "trim-me")]
    [InlineData("C# / .NET Project!!", "c-net-project")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    [InlineData("--leading-and-trailing--", "leading-and-trailing")]
    public void Generate_ProducesUrlSafeLowercaseSlug(string input, string expected)
        => Assert.Equal(expected, SlugGenerator.Generate(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Generate_BlankInput_ReturnsEmpty(string? input)
        => Assert.Equal("", SlugGenerator.Generate(input!));
}
