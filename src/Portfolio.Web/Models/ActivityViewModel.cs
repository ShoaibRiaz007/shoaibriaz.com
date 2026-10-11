using System.Globalization;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Application.Telemetry;

namespace Portfolio.Models;

/// <summary>The admin Activity page: the report plus the geometry of its server-rendered SVG chart.</summary>
public class ActivityViewModel
{
    /// <summary>Chart units per day; bars are drawn in a viewBox of (days × this) by 100 and stretched to fit.</summary>
    public const int SlotWidth = 10;

    /// <summary>Projects listed in the Projects panel's table; the rest are behind "Show the other N".</summary>
    public const int TopProjects = 10;

    public ActivityViewModel(ActivityReport report, IReadOnlyList<ProjectActivityRow>? projects = null)
    {
        Report = report;
        Projects = projects ?? [];
        PeakViews = report.Daily.Count == 0 ? 0 : report.Daily.Max(d => d.Views);
        Bars = report.Daily.Select((d, i) => new ChartBar(d, i * SlotWidth, Height(d.Views), Height(d.Visitors))).ToList();
    }

    public ActivityReport Report { get; }

    /// <summary>Every project, most viewed first, with its position in the site's order.</summary>
    public IReadOnlyList<ProjectActivityRow> Projects { get; }
    public int ProjectPeakViews => Projects.Count == 0 ? 0 : Projects.Max(p => p.Views);

    /// <summary>Joins the projects (in their site order) with their page views; unviewed projects get zeros.</summary>
    public static IReadOnlyList<ProjectActivityRow> ProjectRows(IEnumerable<Portfolio.Domain.Entities.Project> projects,
        IReadOnlyDictionary<string, ProjectViews> views)
        => projects
            .Select((p, i) => views.TryGetValue((p.Slug ?? "").ToLowerInvariant(), out var v)
                ? new ProjectActivityRow(p.Id, p.Title, p.IsPublished, i + 1, v.Views, v.Visitors)
                : new ProjectActivityRow(p.Id, p.Title, p.IsPublished, i + 1, 0, 0))
            .OrderByDescending(r => r.Views).ThenByDescending(r => r.Visitors).ThenBy(r => r.Position)
            .ToList();
    public IReadOnlyList<int> Ranges => TelemetryFacade.Ranges;
    public int PeakViews { get; }
    public IReadOnlyList<ChartBar> Bars { get; }
    public int ChartWidth => Math.Max(1, Bars.Count) * SlotWidth;

    /// <summary>Five or more failed sign-ins in the last hour gets a warning banner.</summary>
    public bool ShowLoginWarning => Report.FailedLoginsLastHour >= 5;

    public BarList Sources => new("Top sources", Report.Sources.Select(r => new BarRow(r.Label, r.Count)).ToList(), Report.Arrivals);
    public BarList Countries => new("Top countries", Report.Countries.Select(c => new BarRow(c.Name, c.Count, c.Flag)).ToList(), Report.PageViews);
    public BarList Pages => new("Top pages", Report.Pages.Select(r => new BarRow(r.Label, r.Count)).ToList(), Report.PageViews);
    public BarList Devices => new("Devices", Report.Devices.Select(r => new BarRow(r.Label, r.Count)).ToList(), Report.PageViews);
    public BarList Referrers => new("Top referrers", Report.ReferrerHosts.Select(r => new BarRow(r.Label, r.Count)).ToList(), Report.Arrivals);
    public BarList Campaigns => new("UTM campaigns", Report.Campaigns.Select(r => new BarRow(r.Label, r.Count)).ToList(), Report.Arrivals);

    // Bars share one scale (peak views = full height); anything non-zero stays visible.
    private double Height(int count) => count == 0 || PeakViews == 0 ? 0 : Math.Max(2, count * 96.0 / PeakViews);

    /// <summary>"just now", "5 min ago", "3 h ago", "today 01:05" (12 h or older, same day), "yesterday 14:05",
    /// then "9 Oct 14:05" (all UTC).</summary>
    public static string Ago(DateTime utc, DateTime now)
    {
        var age = now - utc;
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min ago";
        if (age < TimeSpan.FromHours(12)) return $"{(int)age.TotalHours} h ago";
        if (utc.Date == now.Date) return "today " + utc.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (utc.Date == now.Date.AddDays(-1)) return "yesterday " + utc.ToString("HH:mm", CultureInfo.InvariantCulture);
        return utc.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Full timestamp for a title/tooltip.</summary>
    public static string Absolute(DateTime utc) => utc.ToString("ddd d MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public static string Day(DateOnly day) => day.ToString("d MMM", CultureInfo.InvariantCulture);

    public static string Number(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"Chrome on Windows" from a stored User-Agent.</summary>
    public static string BrowserOs(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";
        var ua = UserAgents.Parse(userAgent);
        return $"{ua.Browser} on {ua.Os}";
    }
}

public record ChartBar(DailyVisits Day, int X, double ViewsHeight, double VisitorsHeight);

public record BarRow(string Label, int Count, string? Flag = null);

/// <summary>One "top N" panel. Bar widths are relative to the leader; the percentage is of <see cref="Total"/>: all
/// page views, or arrivals for the panels about where visits came from.</summary>
public record BarList(string Heading, IReadOnlyList<BarRow> Rows, int Total)
{
    public int Max => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);
    public int WidthPercent(BarRow row) => Max == 0 ? 0 : Math.Max(2, row.Count * 100 / Max);
    public string Share(BarRow row) => Total == 0 ? "" : $"{Math.Round(row.Count * 100.0 / Total, MidpointRounding.AwayFromZero)}%";
}

/// <summary>One project on the Activity page: where it sits in the site's order and how often its page was opened.</summary>
public record ProjectActivityRow(int Id, string Title, bool IsPublished, int Position, int Views, int Visitors);

/// <summary>One Activity table's search box and pager. <see cref="Keep"/> holds the other query values to carry along.</summary>
public record TableToolsModel(string Anchor, string Label, string Placeholder, string PageParam, string SearchParam,
    string? Search, Portfolio.Application.Models.TablePage Page, IReadOnlyDictionary<string, string?> Keep)
{
    /// <summary>This page of the Activity page with the given page and search for this table (null leaves it out).</summary>
    public string Href(int? page, string? search)
    {
        var query = new Dictionary<string, string?>(Keep) { [PageParam] = page is > 1 ? page.Value.ToString(CultureInfo.InvariantCulture) : null, [SearchParam] = search };
        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Admin/Activity",
            query.Where(kv => !string.IsNullOrEmpty(kv.Value))) + "#" + Anchor;
    }
}
