using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Entities;

namespace Portfolio.Infrastructure.Persistence;

/// <summary>EF Core implementation of the telemetry store. Reads are no-tracking; composed queries run as SQL.</summary>
public class EfTelemetryStore : ITelemetryStore
{
    private readonly AppDbContext _db;

    public EfTelemetryStore(AppDbContext db) => _db = db;

    public async Task AddPageViewAsync(PageView view, CancellationToken ct = default)
    {
        _db.PageViews.Add(view);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddLoginAttemptAsync(LoginAttempt attempt, CancellationToken ct = default)
    {
        _db.LoginAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<T>> QueryPageViewsAsync<T>(Func<IQueryable<PageView>, IQueryable<T>> query, CancellationToken ct = default)
        => await query(_db.PageViews.AsNoTracking()).ToListAsync(ct);

    public async Task<IReadOnlyList<T>> QueryLoginAttemptsAsync<T>(Func<IQueryable<LoginAttempt>, IQueryable<T>> query, CancellationToken ct = default)
        => await query(_db.LoginAttempts.AsNoTracking()).ToListAsync(ct);

    public Task<int> CountPageViewsAsync<T>(Func<IQueryable<PageView>, IQueryable<T>> query, CancellationToken ct = default)
        => query(_db.PageViews.AsNoTracking()).CountAsync(ct);

    public Task<int> CountLoginAttemptsAsync(Expression<Func<LoginAttempt, bool>> predicate, CancellationToken ct = default)
        => _db.LoginAttempts.AsNoTracking().CountAsync(predicate, ct);

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
        => await _db.PageViews.Where(v => v.OccurredAt < cutoffUtc).ExecuteDeleteAsync(ct)
           + await _db.LoginAttempts.Where(a => a.OccurredAt < cutoffUtc).ExecuteDeleteAsync(ct);
}
