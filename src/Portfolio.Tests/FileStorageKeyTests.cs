using CloudinaryDotNet;
using Portfolio.Infrastructure.Storage;
using Xunit;

namespace Portfolio.Tests;

/// <summary>File keys decide whether a replaced Bio file or a removed gallery item may be deleted: only this app's
/// own uploads, compared however their URL is spelled.</summary>
public class FileStorageKeyTests
{
    private static readonly CloudinaryFileStorage Cloud = new(new Cloudinary(new Account("mycloud", "key", "secret")));

    [Fact]
    public void Cloudinary_SameAsset_HasOneKey_WithOrWithoutVersionOrQuery()
    {
        var key = Cloud.FileKey("https://res.cloudinary.com/mycloud/image/upload/v1712345678/portfolio/abc123.jpg");
        Assert.NotNull(key);
        Assert.Equal(key, Cloud.FileKey("https://res.cloudinary.com/mycloud/image/upload/portfolio/abc123.png?x=1"));
        Assert.Equal(key, Cloud.FileKey("https://res.cloudinary.com/mycloud/image/upload/w_300/v9/portfolio/abc123.jpg"));
        Assert.NotEqual(key, Cloud.FileKey("https://res.cloudinary.com/mycloud/video/upload/v1/portfolio/abc123.mp4"));
    }

    [Fact]
    public void Cloudinary_RawKeepsItsExtension()
        => Assert.NotEqual(Cloud.FileKey("https://res.cloudinary.com/mycloud/raw/upload/v1/portfolio/cv.pdf"),
            Cloud.FileKey("https://res.cloudinary.com/mycloud/raw/upload/v1/portfolio/cv.docx"));

    [Theory]
    [InlineData("https://res.cloudinary.com/othercloud/image/upload/v1/portfolio/abc123.jpg")] // another account
    [InlineData("https://res.cloudinary.com/mycloud/image/upload/v1/elsewhere/abc123.jpg")]   // not our upload folder
    [InlineData("https://example.com/mycloud/image/upload/v1/portfolio/abc123.jpg")]          // not Cloudinary
    [InlineData("/uploads/abc123.jpg")]
    [InlineData("")]
    public void Cloudinary_NotOurUpload_HasNoKey(string url) => Assert.Null(Cloud.FileKey(url));

    [Fact]
    public async Task Cloudinary_RefusesToDeleteAnotherAccountsAsset()
        // Returns before any API call (none could succeed with these fake credentials).
        => await Cloud.DeleteAsync("https://res.cloudinary.com/othercloud/image/upload/v1/portfolio/abc123.jpg");

    [Fact]
    public void Local_KeyIsTheStoredFileName_UnderThePrefixOnly()
    {
        var local = new LocalFileStorage(new FileStorageOptions(Path.GetTempPath(), "/uploads"));
        Assert.Equal(local.FileKey("/uploads/a1.png"), local.FileKey("/uploads/A1.png?v=2"));
        Assert.Null(local.FileKey("/images/a1.png"));
        Assert.Null(local.FileKey("https://example.com/uploads/a1.png"));
    }
}
