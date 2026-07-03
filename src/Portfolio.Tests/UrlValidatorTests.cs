using Portfolio.Application.Common;
using Xunit;

namespace Portfolio.Tests;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?query=1")]
    [InlineData("/uploads/abc123.jpg")]
    public void SanitizeOrEmpty_SafeUrl_ReturnsUnchanged(string input)
        => Assert.Equal(input, UrlValidator.SanitizeOrEmpty(input));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("//evil.com/phishing")] // protocol-relative — would inherit the page's scheme to an attacker host
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SanitizeOrEmpty_UnsafeOrBlankUrl_ReturnsEmpty(string? input)
        => Assert.Equal("", UrlValidator.SanitizeOrEmpty(input));
}
