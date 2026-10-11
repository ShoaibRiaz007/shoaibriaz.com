using Portfolio.Domain.Entities;

namespace Portfolio.Application.Models;

/// <summary>What the browser beacon reports. Every field is untrusted and is validated/truncated before storage.</summary>
public record BeaconInput(
    string? Path,
    string? Referrer = null,
    string? TimeZone = null,
    string? Language = null,
    int? ScreenWidth = null,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? UtmCampaign = null);

/// <summary>Request facts the server sees alongside a beacon. <see cref="ClientIp"/> is only ever hashed.</summary>
public record VisitContext(string? ClientIp, string? UserAgent, string? AcceptLanguage, string? SiteHost);

/// <summary>A sign-in attempt to record. There is deliberately no password field.</summary>
public record LoginAttemptInput(
    string? Username, bool Succeeded, string? FailureReason, string? IpAddress, string? UserAgent, string? AcceptLanguage);

public static class LoginFailureReasons
{
    public const string InvalidCredentials = "invalid credentials";
    public const string Locked = "locked";
    public const string RateLimited = "rate limited";
}

/// <summary>Everything the admin Activity page shows for one range.</summary>
public record ActivityReport
{
    public int Days { get; init; }
    public DateTime GeneratedAt { get; init; }
    public int PageViews { get; init; }
    /// <summary>Visitor ids reset every UTC day, so this is unique visitors per day, summed over the range.</summary>
    public int UniqueVisitors { get; init; }
    public int IslandVisits { get; init; }
    /// <summary>Page views that started a visit (not a click from another of the site's pages).</summary>
    public int Arrivals { get; init; }
    public int FailedLogins24h { get; init; }
    public int FailedLoginsLastHour { get; init; }
    public IReadOnlyList<DailyVisits> Daily { get; init; } = [];
    public IReadOnlyList<LabelCount> Sources { get; init; } = [];
    public IReadOnlyList<CountryCount> Countries { get; init; } = [];
    public IReadOnlyList<LabelCount> Pages { get; init; } = [];
    public IReadOnlyList<LabelCount> Devices { get; init; } = [];
    public IReadOnlyList<LabelCount> ReferrerHosts { get; init; } = [];
    public IReadOnlyList<LabelCount> Campaigns { get; init; } = [];
    public IReadOnlyList<PageView> RecentVisits { get; init; } = [];
    public IReadOnlyList<LoginAttempt> LoginAttempts { get; init; } = [];
    /// <summary>Which page of each table this is, and how many there are (at least 1).</summary>
    public TablePage VisitsPage { get; init; } = new(1, 1);
    public TablePage LoginsPage { get; init; } = new(1, 1);
    /// <summary>The searches the tables were filtered by (cleaned), and how many rows matched.</summary>
    public string? VisitsSearch { get; init; }
    public int VisitsMatched { get; init; }
    public string? LoginsSearch { get; init; }
    public int LoginsMatched { get; init; }
}

/// <summary>One UTC day; days without visits are included with zeros.</summary>
public record DailyVisits(DateOnly Day, int Views, int Visitors);

public record LabelCount(string Label, int Count);

public record CountryCount(string Code, string Name, string Flag, int Count);

/// <summary>Dashboard teaser.</summary>
public record VisitorSummary(int Days, int PageViews, int UniqueVisitors);

/// <summary>Visits to one project's page (/project/{slug}) in a range: page views and distinct daily visitors.</summary>
public record ProjectViews(string Slug, int Views, int Visitors);

/// <summary>Paging and search for the Activity tables. Searches match part of a page path, source, country, device,
/// browser or campaign (visits) and part of a username, IP address or failure reason, or "success"/"failed" (sign-ins),
/// ignoring case.</summary>
public record TableQuery(int VisitsPage = 1, string? VisitsSearch = null, int LoginsPage = 1, string? LoginsSearch = null);

/// <summary>A page of a table: its number (from 1) and the page count.</summary>
public record TablePage(int Number, int Count)
{
    public bool HasNewer => Number > 1;
    public bool HasOlder => Number < Count;
}
