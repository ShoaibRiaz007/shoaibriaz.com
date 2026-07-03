using Portfolio.Application.Abstractions;

namespace Portfolio.Infrastructure.Storage;

/// <summary>Configuration for the local file store. Supplied by the composition root (Web).
/// <paramref name="MaxTotalBytes"/> bounds the uploads directory's total size so repeated uploads
/// can't exhaust disk space; default 2 GiB.</summary>
public record FileStorageOptions(string RootPath, string PublicPrefix, long MaxTotalBytes = 2L * 1024 * 1024 * 1024);

/// <summary>Saves uploads to a local directory and exposes them under a public URL prefix.</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;

    public LocalFileStorage(FileStorageOptions options) => _options = options;

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_options.RootPath);

        var currentSize = new DirectoryInfo(_options.RootPath).EnumerateFiles().Sum(f => f.Length);
        if (currentSize + content.Length > _options.MaxTotalBytes)
            throw new InvalidOperationException(
                $"Uploads storage quota exceeded ({_options.MaxTotalBytes / (1024 * 1024)} MB). Delete unused media before uploading more.");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_options.RootPath, fileName);

        await using (var stream = File.Create(fullPath))
            await content.CopyToAsync(stream, ct);

        return $"{_options.PublicPrefix.TrimEnd('/')}/{fileName}";
    }

    public Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)) return Task.CompletedTask;

        var prefix = _options.PublicPrefix.TrimEnd('/') + "/";
        if (!publicUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Task.CompletedTask;

        var fileName = Path.GetFileName(publicUrl); // strips any path, prevents traversal
        var fullPath = Path.Combine(_options.RootPath, fileName);

        // Defence in depth: ensure the resolved path stays inside the uploads root. The trailing
        // separator matters — a bare prefix check would accept a sibling like "uploads-evil".
        var root = Path.GetFullPath(_options.RootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(fullPath);
        if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(resolved))
            File.Delete(resolved);

        return Task.CompletedTask;
    }
}
