using System.Text;

namespace Portfolio.Application.Telemetry;

/// <summary>Classifies where a visit came from, and cleans referrer URLs before they are stored.</summary>
public static class TrafficSources
{
    public const string Direct = "Direct";
    /// <summary>A click from one of the site's own pages: not an arrival, so it is left out of the sources.</summary>
    public const string Internal = "Internal";
    public const string Other = "Other";
    public const string Email = "Email";

    // Matched against the referrer host (www. stripped) and its parent domains. Webmail comes first so
    // mail.google.com counts as Email rather than Google.
    private static readonly (string Domain, string Source)[] Hosts =
    {
        ("mail.google.com", Email), ("outlook.live.com", Email), ("outlook.office.com", Email),
        ("outlook.office365.com", Email), ("mail.yahoo.com", Email), ("mail.proton.me", Email),
        ("linkedin.com", "LinkedIn"), ("lnkd.in", "LinkedIn"),
        ("bing.com", "Bing"),
        ("github.com", "GitHub"), ("github.io", "GitHub"),
        ("t.co", "X/Twitter"), ("twitter.com", "X/Twitter"), ("x.com", "X/Twitter"),
        ("facebook.com", "Facebook"), ("fb.com", "Facebook"), ("fb.me", "Facebook"),
        ("reddit.com", "Reddit"), ("redd.it", "Reddit"),
        ("youtube.com", "YouTube"), ("youtu.be", "YouTube"),
    };

    // Android apps send android-app://<package>/ as the referrer.
    private static readonly Dictionary<string, string> AndroidApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["com.linkedin.android"] = "LinkedIn",
        ["com.google.android.gm"] = Email,
        ["com.google.android.googlequicksearchbox"] = "Google",
        ["com.twitter.android"] = "X/Twitter",
        ["com.facebook.katana"] = "Facebook",
        ["com.reddit.frontpage"] = "Reddit",
        ["com.google.android.youtube"] = "YouTube",
    };

    // utm_source values people commonly type for the same places.
    private static readonly Dictionary<string, string> UtmAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["linkedin"] = "LinkedIn", ["li"] = "LinkedIn", ["lnkd"] = "LinkedIn",
        ["google"] = "Google", ["bing"] = "Bing", ["github"] = "GitHub", ["gh"] = "GitHub",
        ["twitter"] = "X/Twitter", ["x"] = "X/Twitter", ["t.co"] = "X/Twitter",
        ["facebook"] = "Facebook", ["fb"] = "Facebook", ["reddit"] = "Reddit",
        ["youtube"] = "YouTube", ["yt"] = "YouTube",
        ["email"] = Email, ["e-mail"] = Email, ["mail"] = Email, ["newsletter"] = Email, ["gmail"] = Email, ["outlook"] = Email,
    };

    public const int MaxSourceLength = 40;

    /// <summary>The visit's source. A utm_source, when present, wins over the referrer.</summary>
    /// <param name="referrerHost">Host from <see cref="CleanReferrer"/> (already www-stripped), or null.</param>
    public static string Classify(string? referrerHost, string? utmSource)
    {
        if (!string.IsNullOrWhiteSpace(utmSource))
        {
            var utm = utmSource.Trim();
            return UtmAliases.TryGetValue(utm, out var alias) ? alias : Truncate(utm, MaxSourceLength);
        }
        if (string.IsNullOrEmpty(referrerHost)) return Direct;
        if (AndroidApps.TryGetValue(referrerHost, out var app)) return app;

        var host = referrerHost.ToLowerInvariant();
        foreach (var (domain, source) in Hosts)
            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal))
                return source;
        // google.com, google.co.uk, google.com.pk, ...
        var labels = host.Split('.');
        if (Array.IndexOf(labels, "google") is var i && i >= 0 && i < labels.Length - 1) return "Google";
        return Other;
    }

    /// <summary>True when the referrer is one of the site's own pages.</summary>
    public static bool IsSameSite(string? referrer, string? siteHost)
        => !string.IsNullOrEmpty(siteHost)
           && Uri.TryCreate(referrer?.Trim(), UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
           && uri.Host.Equals(siteHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>Parses a referrer into (host without "www.", URL without query/fragment except utm_* parameters).
    /// Returns nulls for empty, unparseable or same-site referrers (an internal navigation isn't a source).</summary>
    public static (string? Host, string? Url) CleanReferrer(string? referrer, string? siteHost, int maxHost = 200, int maxUrl = 500)
    {
        if (string.IsNullOrWhiteSpace(referrer) || !Uri.TryCreate(referrer.Trim(), UriKind.Absolute, out var uri))
            return (null, null);

        if (uri.Scheme == "android-app")
        {
            var package = Truncate(uri.Host.ToLowerInvariant(), maxHost);
            return package.Length == 0 ? (null, null) : (package, Truncate($"android-app://{package}/", maxUrl));
        }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return (null, null);

        var host = uri.Host.ToLowerInvariant();
        if (!string.IsNullOrEmpty(siteHost) && host.Equals(siteHost, StringComparison.OrdinalIgnoreCase)) return (null, null);
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];

        var url = new StringBuilder($"{uri.Scheme}://{uri.Authority.ToLowerInvariant()}{uri.AbsolutePath}");
        var utm = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)).ToList();
        if (utm.Count > 0) url.Append('?').Append(string.Join('&', utm));
        return (Truncate(host, maxHost), Truncate(url.ToString(), maxUrl));
    }

    /// <summary>Cuts to at most <paramref name="max"/> UTF-16 units without splitting a surrogate pair (a lone
    /// surrogate isn't valid UTF-8, so Npgsql would refuse to write the row).</summary>
    internal static string Truncate(string value, int max)
    {
        if (value.Length <= max) return value;
        var end = max > 0 && char.IsHighSurrogate(value[max - 1]) ? max - 1 : max;
        return value[..end];
    }
}
