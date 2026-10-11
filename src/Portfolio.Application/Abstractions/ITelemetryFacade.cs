using Portfolio.Application.Models;

namespace Portfolio.Application.Abstractions;

/// <summary>Cookie-free visitor analytics and the admin login-attempt log.</summary>
public interface ITelemetryFacade
{
    /// <summary>Validates, enriches and stores one page view. Returns false when it was ignored (bot, bad path).</summary>
    Task<bool> RecordPageViewAsync(BeaconInput input, VisitContext context, CancellationToken ct = default);

    Task RecordLoginAttemptAsync(LoginAttemptInput input, CancellationToken ct = default);

    /// <summary>The Activity report for the last <paramref name="days"/> UTC days (today included). The recent-visits and sign-in tables are paged (<paramref name="table"/>) and can
    /// be searched; the totals and charts always cover the whole range.</summary>
    Task<ActivityReport> GetActivityAsync(int days, CancellationToken ct = default, TableQuery? table = null);

    Task<VisitorSummary> GetVisitorSummaryAsync(int days, CancellationToken ct = default);

    /// <summary>Views of each project page in the last <paramref name="days"/> days, keyed by slug (lower case).
    /// Projects nobody opened are absent.</summary>
    Task<IReadOnlyDictionary<string, ProjectViews>> GetProjectViewsAsync(int days, CancellationToken ct = default);
}
