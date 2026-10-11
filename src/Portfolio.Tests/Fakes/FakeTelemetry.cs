using System.Linq.Expressions;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Entities;

namespace Portfolio.Tests.Fakes;

/// <summary>In-memory telemetry store: composed queries run as LINQ-to-objects over the stored rows.</summary>
public class FakeTelemetryStore : ITelemetryStore
{
    public readonly List<PageView> PageViews = new();
    public readonly List<LoginAttempt> LoginAttempts = new();
    private int _lastPageViewId, _lastLoginAttemptId;   // like identity columns: never reused after a delete

    public Task AddPageViewAsync(PageView view, CancellationToken ct = default)
    {
        view.Id = ++_lastPageViewId;
        PageViews.Add(view);
        return Task.CompletedTask;
    }

    public Task AddLoginAttemptAsync(LoginAttempt attempt, CancellationToken ct = default)
    {
        attempt.Id = ++_lastLoginAttemptId;
        LoginAttempts.Add(attempt);
        return Task.CompletedTask;
    }

    /// <summary>Makes page-view queries throw, as an unreachable database would.</summary>
    public bool FailQueries { get; set; }

    public Task<IReadOnlyList<T>> QueryPageViewsAsync<T>(Func<IQueryable<PageView>, IQueryable<T>> query, CancellationToken ct = default)
        => FailQueries ? throw new InvalidOperationException("store unavailable")
            : Task.FromResult<IReadOnlyList<T>>(query(PageViews.AsQueryable()).ToList());

    public Task<IReadOnlyList<T>> QueryLoginAttemptsAsync<T>(Func<IQueryable<LoginAttempt>, IQueryable<T>> query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(query(LoginAttempts.AsQueryable()).ToList());

    public Task<int> CountPageViewsAsync<T>(Func<IQueryable<PageView>, IQueryable<T>> query, CancellationToken ct = default)
        => Task.FromResult(query(PageViews.AsQueryable()).Count());

    public Task<int> CountLoginAttemptsAsync(Expression<Func<LoginAttempt, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(LoginAttempts.AsQueryable().Count(predicate));

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
        => Task.FromResult(PageViews.RemoveAll(v => v.OccurredAt < cutoffUtc) + LoginAttempts.RemoveAll(a => a.OccurredAt < cutoffUtc));
}

/// <summary>A clock tests can set and advance.</summary>
public class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; }

    public FakeClock(DateTimeOffset now) => Now = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Deterministic stand-in: the same (IP, User-Agent) pair always maps to the same opaque "visitor-N".</summary>
public class FakeVisitorHasher : IVisitorHasher
{
    private readonly Dictionary<string, string> _ids = new();

    public string Hash(string? clientIp, string? userAgent)
    {
        var key = $"{clientIp}|{userAgent}";
        if (!_ids.TryGetValue(key, out var id)) _ids[key] = id = $"visitor-{_ids.Count + 1}";
        return id;
    }
}
