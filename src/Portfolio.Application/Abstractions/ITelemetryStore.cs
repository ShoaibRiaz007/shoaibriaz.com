using System.Linq.Expressions;
using Portfolio.Domain.Entities;

namespace Portfolio.Application.Abstractions;

/// <summary>Persistence for visit and login telemetry. Queries are composed by the caller and translated to SQL
/// (GroupBy/Count run in the database), the same way <see cref="IRepository{T}"/> composes its shape argument.</summary>
public interface ITelemetryStore
{
    Task AddPageViewAsync(PageView view, CancellationToken ct = default);
    Task AddLoginAttemptAsync(LoginAttempt attempt, CancellationToken ct = default);

    Task<IReadOnlyList<T>> QueryPageViewsAsync<T>(Func<IQueryable<PageView>, IQueryable<T>> query, CancellationToken ct = default);
    Task<IReadOnlyList<T>> QueryLoginAttemptsAsync<T>(Func<IQueryable<LoginAttempt>, IQueryable<T>> query, CancellationToken ct = default);

    /// <summary>SQL-side COUNT(*) over whatever <paramref name="query"/> returns (e.g. a Distinct projection).</summary>
    Task<int> CountPageViewsAsync<T>(Func<IQueryable<PageView>, IQueryable<T>> query, CancellationToken ct = default);
    Task<int> CountLoginAttemptsAsync(Expression<Func<LoginAttempt, bool>> predicate, CancellationToken ct = default);

    /// <summary>Deletes page views and login attempts that occurred before <paramref name="cutoffUtc"/>; returns rows removed.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);
}
