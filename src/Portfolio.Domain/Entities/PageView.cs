namespace Portfolio.Domain.Entities;

/// <summary>One anonymous page view reported by the site's cookie-free beacon (or the 3D island).
/// Holds no IP address or other direct identifier: <see cref="VisitorHash"/> is a one-way hash keyed by a salt
/// that rotates daily and is never stored, so visitors can be counted per day but not followed across days.</summary>
public class PageView
{
    public int Id { get; set; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }

    public string Path { get; set; } = string.Empty;

    /// <summary>Referring host without "www." (null for direct visits).</summary>
    public string? ReferrerHost { get; set; }

    /// <summary>Referring URL with its query string removed (utm_* parameters excepted) and no fragment.</summary>
    public string? Referrer { get; set; }

    /// <summary>Where the visit came from: LinkedIn, Google, …, Direct, Other, or the utm_source when one was given.</summary>
    public string Source { get; set; } = string.Empty;

    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }

    /// <summary>ISO 3166-1 alpha-2, derived from the browser's time zone (or its language region as a fallback).</summary>
    public string? Country { get; set; }
    public string? CountryName { get; set; }

    /// <summary>IANA time zone reported by the browser, e.g. "Asia/Karachi".</summary>
    public string? TimeZone { get; set; }
    public string? Language { get; set; }

    /// <summary>"Mobile" | "Tablet" | "Desktop"</summary>
    public string? Device { get; set; }
    public string? Browser { get; set; }
    public string? Os { get; set; }
    public int? ScreenWidth { get; set; }

    public string VisitorHash { get; set; } = string.Empty;

    /// <summary>True for views of the 3D island (/world/).</summary>
    public bool IsIsland { get; set; }
}
