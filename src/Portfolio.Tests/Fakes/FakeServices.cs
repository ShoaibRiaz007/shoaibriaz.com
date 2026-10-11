using Portfolio.Application.Abstractions;

namespace Portfolio.Tests.Fakes;

/// <summary>Records saves/deletes instead of touching disk or Cloudinary.</summary>
public class FakeFileStorage : IFileStorage
{
    public readonly List<string> Saved = new();
    public readonly List<string> Deleted = new();

    /// <summary>Runs inside DeleteAsync before recording, so a test can observe state at delete time or throw.</summary>
    public Action<string>? OnDelete { get; set; }

    /// <summary>Runs inside SaveAsync before storing, so a test can make storage fail (quota, Cloudinary error).</summary>
    public Action<string>? OnSave { get; set; }

    /// <summary>Like LocalFileStorage: only "/uploads/..." files are ours; the query string and letter case don't matter.</summary>
    public string? FileKey(string publicUrl)
        => publicUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ? publicUrl.Split('?')[0].ToLowerInvariant() : null;

    public Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        OnSave?.Invoke(originalFileName);
        var url = $"/uploads/{Saved.Count + 1}{Path.GetExtension(originalFileName)}";
        Saved.Add(url);
        return Task.FromResult(url);
    }

    public Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        OnDelete?.Invoke(publicUrl);
        Deleted.Add(publicUrl);
        return Task.CompletedTask;
    }
}

/// <summary>Runs the operation inline; transactional behaviour is covered by the real database.</summary>
public class FakeUnitOfWork : IUnitOfWork
{
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default) => operation(ct);
}
