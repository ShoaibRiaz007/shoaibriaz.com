namespace Portfolio.Application.Common;

/// <summary>Guards admin-entered link/image URLs the same way EmbedUrlNormalizer guards video embeds:
/// only absolute http/https URLs are accepted, blocking javascript:/data: URI injection into href/src.</summary>
public static class UrlValidator
{
    /// <summary>Returns the trimmed URL if it is a safe absolute http/https address, or a site-relative
    /// path (e.g. "/uploads/foo.jpg", as produced by local file storage) — otherwise "".
    /// A single leading slash is required (not "//", which browsers treat as protocol-relative
    /// and would let a stored value point at an attacker-controlled host).</summary>
    public static string SanitizeOrEmpty(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        url = url.Trim();

        if (url.StartsWith('/') && !url.StartsWith("//")) return url;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? url
            : "";
    }
}
