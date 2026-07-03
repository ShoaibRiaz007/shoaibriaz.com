using System.Linq.Expressions;

namespace Portfolio.Application.Abstractions;

/// <summary>Generic persistence-ignorant repository for an aggregate. Mutating methods persist immediately.</summary>
public interface IRepository<T> where T : class
{
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    /// <summary>Filtered list — the predicate is translated to SQL, never evaluated in memory.</summary>
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    /// <summary>Filtered, ordered, bounded list — <paramref name="shape"/> (e.g. <c>q => q.OrderBy(x => x.SortOrder).Take(3)</c>)
    /// is composed onto the query and translated to SQL, so only the requested rows are ever materialized.</summary>
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>> shape, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task RemoveAsync(T entity, CancellationToken ct = default);
    /// <summary>SQL-side MAX(<paramref name="selector"/>) over rows matching <paramref name="predicate"/>; 0 if none match.</summary>
    Task<int> MaxAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default);
}
