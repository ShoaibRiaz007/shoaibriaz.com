using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;
using Portfolio.Application.Telemetry;
using Portfolio.Domain.Entities;
using L = Portfolio.Application.Telemetry.TelemetryLimits;

namespace Portfolio.Application.Facades;

public partial class TelemetryFacade : ITelemetryFacade
{
    /// <summary>The ranges the Activity page offers; anything else falls back to the first.</summary>
    public static readonly IReadOnlyList<int> Ranges = [7, 30, 90];

    private const int TopCount = 8;
    /// <summary>Rows per page of the recent-visits and sign-in tables.</summary>
    public const int TablePageSize = 25;

    // The site's own non-public areas never carry the beacon; a report for one is forged or stale.
    private static readonly string[] IgnoredPathPrefixes = ["/admin", "/account", "/api"];

    private readonly ITelemetryStore _store;
    private readonly IVisitorHasher _hasher;
    private readonly TimeProvider _clock;

    public TelemetryFacade(ITelemetryStore store, IVisitorHasher hasher, TimeProvider clock)
    {
        _store = store; _hasher = hasher; _clock = clock;
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public async Task<bool> RecordPageViewAsync(BeaconInput input, VisitContext context, CancellationToken ct = default)
    {
        if (UserAgents.IsBot(context.UserAgent)) return false;
        var path = CleanPath(input.Path);
        if (path is null) return false;

        var (referrerHost, referrer) = TrafficSources.CleanReferrer(input.Referrer, context.SiteHost, L.ReferrerHost, L.Referrer);
        var utmSource = Clean(input.UtmSource, L.Utm);
        var timeZone = Matches(input.TimeZone, TimeZonePattern(), L.TimeZone);
        var language = Matches(input.Language, LanguagePattern(), L.Language);
        var country = TimeZoneCountries.CountryOf(timeZone)
                      ?? Countries.FromLanguageTag(language)
                      ?? Countries.FromLanguageTag(context.AcceptLanguage);
        var agent = UserAgents.Parse(context.UserAgent);

        await _store.AddPageViewAsync(new PageView
        {
            OccurredAt = UtcNow,
            Path = path,
            ReferrerHost = referrerHost,
            Referrer = referrer,
            Source = TrafficSources.IsSameSite(input.Referrer, context.SiteHost)
                ? TrafficSources.Internal
                : TrafficSources.Classify(referrerHost, utmSource),
            UtmSource = utmSource,
            UtmMedium = Clean(input.UtmMedium, L.Utm),
            UtmCampaign = Clean(input.UtmCampaign, L.Utm),
            Country = country,
            CountryName = country is null ? null : TrafficSources.Truncate(Countries.Name(country), L.CountryName),
            TimeZone = timeZone,
            Language = language,
            Device = agent.Device,
            Browser = agent.Browser,
            Os = agent.Os,
            ScreenWidth = input.ScreenWidth is > 0 and <= 20_000 ? input.ScreenWidth : null,
            VisitorHash = _hasher.Hash(context.ClientIp, context.UserAgent),
            IsIsland = path.Equals("/world", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/world/", StringComparison.OrdinalIgnoreCase),
        }, ct);
        return true;
    }

    public Task RecordLoginAttemptAsync(LoginAttemptInput input, CancellationToken ct = default)
        => _store.AddLoginAttemptAsync(new LoginAttempt
        {
            OccurredAt = UtcNow,
            Username = Clean(input.Username, L.Username) ?? string.Empty,
            Succeeded = input.Succeeded,
            FailureReason = input.Succeeded ? null : Clean(input.FailureReason, L.FailureReason),
            IpAddress = Clean(input.IpAddress, L.IpAddress),
            Country = Countries.FromLanguageTag(input.AcceptLanguage),
            UserAgent = Clean(input.UserAgent, L.UserAgent),
        }, ct);

    public const int MaxSearchLength = 100;

    public async Task<ActivityReport> GetActivityAsync(int days, CancellationToken ct = default, TableQuery? table = null)
    {
        table ??= new TableQuery();
        var vq = SearchTerm(table.VisitsSearch);
        var lq = SearchTerm(table.LoginsSearch);
        days = NormalizeRange(days);
        var now = UtcNow;
        var today = DateOnly.FromDateTime(now);
        var from = RangeStart(today, days);
        var dayAgo = now.AddHours(-24);
        var hourAgo = now.AddHours(-1);

        // Sequential awaits: the store shares one DbContext per request, which allows a single query at a time.
        var pageViews = await _store.CountPageViewsAsync(q => q.Where(v => v.OccurredAt >= from), ct);
        var visitors = await _store.CountPageViewsAsync(q => q.Where(v => v.OccurredAt >= from).Select(v => v.VisitorHash).Distinct(), ct);
        var island = await _store.CountPageViewsAsync(q => q.Where(v => v.OccurredAt >= from && v.IsIsland), ct);
        var arrivals = await _store.CountPageViewsAsync(q => q.Where(v => v.OccurredAt >= from && v.Source != TrafficSources.Internal), ct);
        var failed24h = await _store.CountLoginAttemptsAsync(a => !a.Succeeded && a.OccurredAt >= dayAgo, ct);
        var failedHour = await _store.CountLoginAttemptsAsync(a => !a.Succeeded && a.OccurredAt >= hourAgo, ct);

        var dailyRows = await _store.QueryPageViewsAsync(q => q
            .Where(v => v.OccurredAt >= from)
            .GroupBy(v => v.OccurredAt.Date)
            .Select(g => new { Day = g.Key, Views = g.Count(), Visitors = g.Select(v => v.VisitorHash).Distinct().Count() }), ct);
        var byDay = dailyRows.ToDictionary(r => DateOnly.FromDateTime(r.Day), r => r);
        var daily = Enumerable.Range(0, days)
            .Select(i => today.AddDays(i - days + 1))
            .Select(d => byDay.TryGetValue(d, out var r) ? new DailyVisits(d, r.Views, r.Visitors) : new DailyVisits(d, 0, 0))
            .ToList();

        var countries = (await TopAsync(from, v => v.Country, ct))
            .Select(c => new CountryCount(c.Label, Countries.Name(c.Label), Countries.Flag(c.Label), c.Count))
            .ToList();

        // Out-of-range page numbers (an old link after the data shrank) land on the nearest real page.
        var visitRows = vq is null ? pageViews : await _store.CountPageViewsAsync(q => MatchVisits(q.Where(v => v.OccurredAt >= from), vq), ct);
        var loginRows = await _store.CountLoginAttemptsAsync(MatchLogins(from, lq), ct);
        var visits = Page(table.VisitsPage, visitRows);
        var logins = Page(table.LoginsPage, loginRows);

        return new ActivityReport
        {
            Days = days,
            GeneratedAt = now,
            PageViews = pageViews,
            UniqueVisitors = visitors,
            IslandVisits = island,
            Arrivals = arrivals,
            FailedLogins24h = failed24h,
            FailedLoginsLastHour = failedHour,
            Daily = daily,
            Sources = await TopAsync(from, v => v.Source, ct, arrivalsOnly: true),
            Countries = countries,
            Pages = await TopAsync(from, v => v.Path, ct),
            Devices = await TopAsync(from, v => v.Device, ct),
            ReferrerHosts = await TopAsync(from, v => v.ReferrerHost, ct),
            Campaigns = await TopAsync(from, v => v.UtmCampaign, ct),
            RecentVisits = await _store.QueryPageViewsAsync(q => MatchVisits(q.Where(v => v.OccurredAt >= from), vq)
                .OrderByDescending(v => v.OccurredAt).ThenByDescending(v => v.Id)
                .Skip((visits.Number - 1) * TablePageSize).Take(TablePageSize), ct),
            LoginAttempts = await _store.QueryLoginAttemptsAsync(q => q.Where(MatchLogins(from, lq))
                .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
                .Skip((logins.Number - 1) * TablePageSize).Take(TablePageSize), ct),
            VisitsPage = visits,
            LoginsPage = logins,
            VisitsSearch = vq,
            VisitsMatched = visitRows,
            LoginsSearch = lq,
            LoginsMatched = loginRows,
        };
    }

    public async Task<VisitorSummary> GetVisitorSummaryAsync(int days, CancellationToken ct = default)
    {
        var from = RangeStart(DateOnly.FromDateTime(UtcNow), days);
        var views = await _store.CountPageViewsAsync(q => q.Where(v => v.OccurredAt >= from), ct);
        var visitors = await _store.CountPageViewsAsync(q => q.Where(v => v.OccurredAt >= from).Select(v => v.VisitorHash).Distinct(), ct);
        return new VisitorSummary(days, views, visitors);
    }

    /// <summary>Lower-cased, trimmed and capped; null when there is nothing to search for.</summary>
    private static string? SearchTerm(string? raw)
    {
        var term = Clean(raw, MaxSearchLength)?.ToLowerInvariant();
        return string.IsNullOrEmpty(term) ? null : term;
    }

    // Case-insensitive "contains" that EF translates (lower(column) LIKE '%term%'); stored columns are short.
    private static IQueryable<PageView> MatchVisits(IQueryable<PageView> q, string? term)
        => term is null ? q : q.Where(v =>
            v.Path.ToLower().Contains(term) || v.Source.ToLower().Contains(term)
            || (v.Country != null && v.Country.ToLower().Contains(term)) || (v.CountryName != null && v.CountryName.ToLower().Contains(term))
            || (v.Device != null && v.Device.ToLower().Contains(term)) || (v.Browser != null && v.Browser.ToLower().Contains(term))
            || (v.UtmCampaign != null && v.UtmCampaign.ToLower().Contains(term)));

    private static Expression<Func<LoginAttempt, bool>> MatchLogins(DateTime from, string? term)
        => term is null
            ? a => a.OccurredAt >= from
            : a => a.OccurredAt >= from && (a.Username.ToLower().Contains(term)
                || (a.IpAddress != null && a.IpAddress.ToLower().Contains(term))
                || (a.FailureReason != null && a.FailureReason.ToLower().Contains(term))
                || (term == "success" && a.Succeeded) || (term == "failed" && !a.Succeeded));

    private static TablePage Page(int requested, int rows)
    {
        var count = Math.Max(1, (rows + TablePageSize - 1) / TablePageSize);
        return new TablePage(Math.Clamp(requested, 1, count), count);
    }

    /// <summary>The path prefix of a project page.</summary>
    public const string ProjectPathPrefix = "/project/";

    public async Task<IReadOnlyDictionary<string, ProjectViews>> GetProjectViewsAsync(int days, CancellationToken ct = default)
    {
        var from = RangeStart(DateOnly.FromDateTime(UtcNow), days);
        // Paths are stored as visited, and routing ignores case, so /Project/Foo/ is the same page as /project/foo.
        // One row per (path, visitor) pair; the variants are then merged per slug so a visitor counts once.
        var rows = await _store.QueryPageViewsAsync(q => q
            .Where(v => v.OccurredAt >= from && v.Path.ToLower().StartsWith(ProjectPathPrefix))
            .GroupBy(v => new { Path = v.Path.ToLower(), v.VisitorHash })
            .Select(g => new { g.Key.Path, g.Key.VisitorHash, Views = g.Count() }), ct);
        return rows
            .GroupBy(r => r.Path[ProjectPathPrefix.Length..].Trim('/'))
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => new ProjectViews(g.Key, g.Sum(r => r.Views), g.Select(r => r.VisitorHash).Distinct().Count()));
    }

    public static int NormalizeRange(int days) => Ranges.Contains(days) ? days : Ranges[0];

    /// <summary>Midnight UTC at the start of a <paramref name="days"/>-day range ending today.</summary>
    private static DateTime RangeStart(DateOnly today, int days)
        => today.AddDays(1 - days).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>Most frequent non-empty values of one column in the range, counted in the database;
    /// <paramref name="arrivalsOnly"/> leaves out clicks inside the site.</summary>
    private async Task<IReadOnlyList<LabelCount>> TopAsync(DateTime from, Expression<Func<PageView, string?>> column, CancellationToken ct,
        bool arrivalsOnly = false)
    {
        var rows = await _store.QueryPageViewsAsync(q => q
            .Where(v => v.OccurredAt >= from && (!arrivalsOnly || v.Source != TrafficSources.Internal))
            .Select(column)
            .Where(value => value != null && value != "")
            .GroupBy(value => value)
            .Select(g => new { Label = g.Key!, Count = g.Count() })
            .OrderByDescending(r => r.Count).ThenBy(r => r.Label)
            .Take(TopCount), ct);
        return rows.Select(r => new LabelCount(r.Label, r.Count)).ToList();
    }

    /// <summary>A site path: must start with a single "/", loses its query/fragment, no control characters or
    /// whitespace. Null when unusable or when it points into the admin, account or API areas.</summary>
    public static string? CleanPath(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw[0] != '/' || raw.StartsWith("//", StringComparison.Ordinal)) return null;
        var cut = raw.IndexOfAny(['?', '#']);
        var path = cut >= 0 ? raw[..cut] : raw;
        if (path.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))) return null;
        if (IgnoredPathPrefixes.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)
                                         || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
            return null;
        return TrafficSources.Truncate(path, L.Path);
    }

    /// <summary>Trimmed, control characters and unpaired surrogates removed, truncated; null when nothing is left.</summary>
    public static string? Clean(string? raw, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var kept = new System.Text.StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (char.IsControl(c)) continue;
            if (char.IsHighSurrogate(c) && i + 1 < raw.Length && char.IsLowSurrogate(raw[i + 1])) { kept.Append(c).Append(raw[++i]); continue; }
            if (char.IsSurrogate(c)) continue;
            kept.Append(c);
        }
        var text = kept.ToString().Trim();
        return text.Length == 0 ? null : TrafficSources.Truncate(text, max);
    }

    private static string? Matches(string? raw, Regex pattern, int max)
    {
        var text = raw?.Trim();
        return !string.IsNullOrEmpty(text) && text.Length <= max && pattern.IsMatch(text) ? text : null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_+\-/]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TimeZonePattern();

    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex LanguagePattern();
}
