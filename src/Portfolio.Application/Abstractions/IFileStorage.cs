namespace Portfolio.Application.Abstractions;

/// <summary>Stores and removes binary media. Framework-agnostic (no IFormFile / web types).</summary>
public interface IFileStorage
{
    /// <summary>Persists the stream and returns the public URL/path to reference it.</summary>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);

    /// <summary>Deletes a previously stored asset by its public URL. No-op if it isn't a managed asset.</summary>
    Task DeleteAsync(string publicUrl, CancellationToken ct = default);

    /// <summary>Identity of a file this store itself uploaded, the same however its URL is spelled (Cloudinary
    /// version segment, extension, query string); null for any other URL. Callers delete a file only when it
    /// has a key and no record still points at a URL with the same key.</summary>
    string? FileKey(string publicUrl);
}
