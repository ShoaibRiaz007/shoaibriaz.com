using System.Linq.Expressions;

namespace Portfolio.Application.Abstractions;

/// <summary>Generic persistence-ignorant repository for an aggregate. Mutating methods persist immediately.</summary>
public interface IRepository<T> where T : class
{
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    /// <summary>Filtered list — the predicate is translated to SQL, never evaluated in memory.</summary>
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task RemoveAsync(T entity, CancellationToken ct = default);
}
