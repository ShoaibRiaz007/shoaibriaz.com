using Portfolio.Application.Common;
using Xunit;

namespace Portfolio.Tests;

public class EmbedUrlNormalizerTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/embed/dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/76979871", "https://player.vimeo.com/video/76979871")]
    public void Normalize_RecognisedUrl_RebuildsEmbedUrl(string input, string expected)
        => Assert.Equal(expected, EmbedUrlNormalizer.Normalize(input));

    [Theory]
    [InlineData("https://evil-youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com.evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://example.com/not-a-video")]
    [InlineData("")]
    [InlineData(null)]
    public void Normalize_UnrecognisedOrSpoofedHost_ReturnsNull(string? input)
        => Assert.Null(EmbedUrlNormalizer.Normalize(input));

    [Fact]
    public void Normalize_NeverEchoesRawQueryStringBackVerbatim()
    {
        // The id is extracted and validated by regex, then the URL is rebuilt from scratch —
        // an attempt to smuggle extra content through the query string must not survive.
        var result = EmbedUrlNormalizer.Normalize("https://www.youtube.com/watch?v=abc123\"><script>alert(1)</script>");
        Assert.Null(result);
    }
}
