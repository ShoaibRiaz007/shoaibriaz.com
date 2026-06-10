namespace Portfolio.Application.Abstractions;

/// <summary>Stores and removes binary media. Framework-agnostic (no IFormFile / web types).</summary>
public interface IFileStorage
{
    /// <summary>Persists the stream and returns the public URL/path to reference it.</summary>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);

    /// <summary>Deletes a previously stored asset by its public URL. No-op if it isn't a managed asset.</summary>
    Task DeleteAsync(string publicUrl, CancellationToken ct = default);
}
