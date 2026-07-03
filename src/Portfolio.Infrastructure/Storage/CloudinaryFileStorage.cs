using System.Text.RegularExpressions;
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

/// <summary>Stores media on Cloudinary (images and videos) and returns the secure delivery URL.</summary>
public class CloudinaryFileStorage : IFileStorage
{
    private static readonly string[] VideoExts = { ".mp4", ".webm", ".ogg", ".mov" };
    private const string Folder = "portfolio";

    private readonly Cloudinary _cloudinary;

    public CloudinaryFileStorage(Cloudinary cloudinary) => _cloudinary = cloudinary;

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var file = new FileDescription(originalFileName, content);

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
        if (string.IsNullOrWhiteSpace(publicUrl) || !publicUrl.Contains("res.cloudinary.com")) return;

        // URL shape: /{cloud}/{resourceType}/upload/[v123/]{folder/}{publicId}.{ext}
        var segments = new Uri(publicUrl).AbsolutePath.Trim('/').Split('/');
        var uploadIdx = Array.IndexOf(segments, "upload");
        if (segments.Length < 3 || uploadIdx <= 0 || uploadIdx + 1 >= segments.Length) return;

        var resourceType = segments[uploadIdx - 1] == "video" ? ResourceType.Video : ResourceType.Image;

        var idParts = segments.Skip(uploadIdx + 1).ToList();
        if (idParts.Count > 0 && Regex.IsMatch(idParts[0], @"^v\d+$")) idParts.RemoveAt(0); // drop version
        if (idParts.Count == 0) return;
        idParts[^1] = Path.GetFileNameWithoutExtension(idParts[^1]); // drop extension from final segment
        var publicId = string.Join('/', idParts);

        var result = await _cloudinary.DeleteResourcesAsync(new DelResParams
        {
            PublicIds = new List<string> { publicId },
            ResourceType = resourceType
        }, ct);
        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
    }
}
