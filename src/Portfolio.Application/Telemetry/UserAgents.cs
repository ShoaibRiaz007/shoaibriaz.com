using System.Text.RegularExpressions;

namespace Portfolio.Application.Telemetry;

/// <summary>Coarse device / browser / OS from a User-Agent string. Deliberately lightweight: the Activity page only
/// needs a split, not exact versions, and storing names alone keeps rows less fingerprintable.</summary>
public record UserAgentInfo(string Device, string Browser, string Os);

public static partial class UserAgents
{
    public const string Mobile = "Mobile";
    public const string Tablet = "Tablet";
    public const string Desktop = "Desktop";

    // Crawlers, link unfurlers, monitors, headless browsers and HTTP libraries. "(?<!cu)bot" spares CUBOT phones.
    [GeneratedRegex(@"(?<!cu)bot|crawl|spider|slurp|preview|headless|lighthouse|pagespeed|gtmetrix|curl|wget|python|go-http|java/|okhttp|axios|node-fetch|undici|libwww|httpclient|http_request|scrapy|facebookexternalhit|embedly|whatsapp|telegram|discord|slack|monitor|uptime|pingdom|phantomjs|selenium|puppeteer|playwright|dataprovider|ahrefs|semrush|mediapartners|feedfetcher|zgrab|masscan|nuclei|postman|insomnia",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BotPattern();

    [GeneratedRegex(@"ipad|tablet|kindle|silk/|playbook|android(?!.*mobile)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TabletPattern();

    [GeneratedRegex(@"mobi|iphone|ipod|android|windows phone|blackberry|opera mini", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MobilePattern();

    /// <summary>True for automated clients. A missing User-Agent counts as automated: every real browser sends one.</summary>
    public static bool IsBot(string? userAgent)
        => string.IsNullOrWhiteSpace(userAgent) || BotPattern().IsMatch(userAgent);

    public static UserAgentInfo Parse(string? userAgent)
    {
        var ua = userAgent ?? string.Empty;
        var device = TabletPattern().IsMatch(ua) ? Tablet : MobilePattern().IsMatch(ua) ? Mobile : Desktop;
        return new UserAgentInfo(device, BrowserOf(ua), OsOf(ua));
    }

    // Order matters: Edge and Opera also say "Chrome", Chrome also says "Safari".
    private static string BrowserOf(string ua)
        => Has(ua, "Edg/") || Has(ua, "EdgA/") || Has(ua, "EdgiOS/") ? "Edge"
         : Has(ua, "OPR/") || Has(ua, "Opera") ? "Opera"
         : Has(ua, "SamsungBrowser/") ? "Samsung Internet"
         : Has(ua, "Firefox/") || Has(ua, "FxiOS/") ? "Firefox"
         : Has(ua, "CriOS/") || Has(ua, "Chrome/") ? "Chrome"
         : Has(ua, "Safari/") ? "Safari"
         : "Other";

    // iOS says "like Mac OS X", so it is checked before macOS.
    private static string OsOf(string ua)
        => Has(ua, "Windows") ? "Windows"
         : Has(ua, "iPhone") || Has(ua, "iPad") || Has(ua, "iPod") ? "iOS"
         : Has(ua, "Android") ? "Android"
         : Has(ua, "CrOS") ? "ChromeOS"
         : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "macOS"
         : Has(ua, "Linux") ? "Linux"
         : "Other";

    private static bool Has(string ua, string token) => ua.Contains(token, StringComparison.OrdinalIgnoreCase);
}
