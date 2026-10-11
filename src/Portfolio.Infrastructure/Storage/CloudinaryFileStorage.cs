using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Portfolio.Application.Abstractions;

namespace Portfolio.Infrastructure.Storage;

/// <summary>Cloudinary credentials, supplied by the composition root from config (user-secrets / env).</summary>
public record CloudinaryOptions(string? CloudName, string? ApiKey, string? ApiSecret)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret);
}

/// <summary>Stores media on Cloudinary (images, videos, and PDF documents) and returns the secure delivery URL.</summary>
public class CloudinaryFileStorage : IFileStorage
{
    private static readonly string[] VideoExts = { ".mp4", ".webm", ".ogg", ".mov" };
    private const string Folder = "portfolio";

    private readonly Cloudinary _cloudinary;

    public CloudinaryFileStorage(Cloudinary cloudinary) => _cloudinary = cloudinary;

    public string? FileKey(string publicUrl)
        => TryParse(publicUrl, out var resourceType, out var publicId) ? $"{resourceType}:{publicId}" : null;

    /// <summary>Parses a delivery URL of a file this app uploaded: this account's cloud, under the upload
    /// folder. URL shape: /{cloud}/{resourceType}/upload/[transformations/][v123/]{folder}/{publicId}.{ext}.
    /// Anything else (another account's asset, a hand-pasted link) is not ours to delete.</summary>
    private bool TryParse(string? publicUrl, out ResourceType resourceType, out string publicId)
    {
        resourceType = ResourceType.Image;
        publicId = "";
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri)
            || !uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase)) return false;

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var uploadIdx = Array.IndexOf(segments, "upload");
        if (uploadIdx != 2 || uploadIdx + 1 >= segments.Length
            || !segments[0].Equals(_cloudinary.Api.Account.Cloud, StringComparison.OrdinalIgnoreCase)) return false;

        resourceType = segments[1] switch
        {
            "video" => ResourceType.Video,
            "raw" => ResourceType.Raw,
            _ => ResourceType.Image,
        };

        // Everything between "upload" and the folder is delivery options (transformations, a version), present
        // or not in any combination; the public id starts at the folder.
        var idParts = segments.Skip(uploadIdx + 1).ToList();
        var folder = idParts.IndexOf(Folder);
        if (folder < 0 || folder + 1 >= idParts.Count) return false;
        idParts.RemoveRange(0, folder);
        // Image/video public ids exclude the extension; raw public ids include it.
        if (resourceType != ResourceType.Raw)
            idParts[^1] = Path.GetFileNameWithoutExtension(idParts[^1]);
        publicId = string.Join('/', idParts);
        return true;
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var file = new FileDescription(originalFileName, content);

        if (ext == ".pdf")
        {
            // Raw, not image: Cloudinary blocks PDF delivery through the image pipeline on
            // default accounts, which would leave the public "Download CV" link returning 401.
            var result = await _cloudinary.UploadAsync(new RawUploadParams { File = file, Folder = Folder }, "raw", ct);
            if (result.Error is not null) throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
            return result.SecureUrl.ToString();
        }
        if (VideoExts.Contains(ext))
        {
            var result = await _cloudinary.UploadAsync(new VideoUploadParams { File = file, Folder = Folder }, ct);
            if (result.Error is not null) throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
            return result.SecureUrl.ToString();
        }
        else
        {
            var result = await _cloudinary.UploadAsync(new ImageUploadParams { File = file, Folder = Folder }, ct);
            if (result.Error is not null) throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
            return result.SecureUrl.ToString();
        }
    }

    public async Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        // Never delete by a loosely matched id: only this account's uploads (another cloud's URL is refused).
        if (!TryParse(publicUrl, out var resourceType, out var publicId)) return;

        var result = await _cloudinary.DeleteResourcesAsync(new DelResParams
        {
            PublicIds = new List<string> { publicId },
            ResourceType = resourceType
        }, ct);
        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
    }
}
