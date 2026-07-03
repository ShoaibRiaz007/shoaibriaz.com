using Portfolio.Application.Common;
using Xunit;

namespace Portfolio.Tests;

public class FileSignatureValidatorTests
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly byte[] GarbageHeader = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

    [Fact]
    public void Validate_MatchingPngContentAndExtension_Succeeds()
    {
        var result = FileSignatureValidator.Validate(PngHeader, "photo.png");
        Assert.True(result.Ok);
        Assert.Equal("image", result.MediaType);
    }

    [Fact]
    public void Validate_ExtensionSpoofing_ContentIsPngButExtensionIsMp4_Fails()
    {
        // An attacker renaming a script or unexpected payload to look like an allowed video type,
        // while the real bytes are something else entirely, must be rejected.
        var result = FileSignatureValidator.Validate(PngHeader, "payload.mp4");
        Assert.False(result.Ok);
    }

    [Fact]
    public void Validate_DisallowedExtension_Fails()
    {
        var result = FileSignatureValidator.Validate(JpegHeader, "shell.svg");
        Assert.False(result.Ok);
    }

    [Fact]
    public void Validate_AllowedExtensionButUnrecognisedContent_Fails()
    {
        var result = FileSignatureValidator.Validate(GarbageHeader, "photo.png");
        Assert.False(result.Ok);
    }

    [Fact]
    public void Validate_ExtensionCaseInsensitive_Succeeds()
    {
        var result = FileSignatureValidator.Validate(JpegHeader, "PHOTO.JPG");
        Assert.True(result.Ok);
    }
}
