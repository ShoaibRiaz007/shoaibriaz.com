using System.Security.Cryptography;

namespace Portfolio.Application.Common;

/// <summary>Computes a stable content fingerprint used to de-duplicate identical media uploads.</summary>
public static class ContentHasher
{
    public static string Sha256Hex(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    /// <summary>Hashes a stream from its current position without buffering it into memory.</summary>
    public static async Task<string> Sha256HexAsync(Stream content, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(content, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
