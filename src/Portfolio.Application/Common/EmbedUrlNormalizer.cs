using System.Text.RegularExpressions;

namespace Portfolio.Application.Common;

/// <summary>Converts a YouTube/Vimeo watch URL into an embeddable iframe URL. Returns null if unrecognised.
/// The output is always rebuilt from the extracted, validated video id — never the caller's raw URL.</summary>
public static class EmbedUrlNormalizer
{
    private static readonly Regex YouTubeId = new(@"^[A-Za-z0-9_-]{6,20}$", RegexOptions.Compiled);
    private static readonly Regex VimeoId = new(@"^\d{1,15}$", RegexOptions.Compiled);

    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        try
        {
            var uri = new Uri(url.StartsWith("http://") || url.StartsWith("https://") ? url : "https://" + url);
            var host = uri.Host.ToLowerInvariant();

            if (host == "youtu.be")
                return YouTube(uri.AbsolutePath.Trim('/'));

            if (IsHost(host, "youtube.com"))
            {
                if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal))
                    return YouTube(uri.AbsolutePath["/embed/".Length..].Trim('/'));
                return YouTube(ParseQueryValue(uri.Query, "v"));
            }

            if (IsHost(host, "vimeo.com"))
            {
                var id = uri.AbsolutePath.Trim('/').Split('/').LastOrDefault();
                return id is not null && VimeoId.IsMatch(id) ? $"https://player.vimeo.com/video/{id}" : null;
            }
        }
        catch (UriFormatException) { return null; }
        return null;
    }

    /// <summary>Exact-label host match: "youtube.com" or "*.youtube.com", but never "evil-youtube.com".</summary>
    private static bool IsHost(string host, string domain)
        => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

    private static string? YouTube(string? id)
        => !string.IsNullOrEmpty(id) && YouTubeId.IsMatch(id) ? $"https://www.youtube.com/embed/{id}" : null;

    private static string? ParseQueryValue(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == key) return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }
}
