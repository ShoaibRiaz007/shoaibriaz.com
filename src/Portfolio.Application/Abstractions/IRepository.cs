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
    /// <summary>Throws <see cref="RowNotFoundException"/> when the row no longer exists.</summary>
    Task UpdateAsync(T entity, CancellationToken ct = default);
    /// <summary>Writes every column except <paramref name="unchangedProperties"/>, in one statement, so values
    /// owned by another write path (e.g. list position) are never overwritten with a stale copy.
    /// Throws <see cref="RowNotFoundException"/> when the row no longer exists.</summary>
    Task UpdateExcludingAsync(T entity, IReadOnlyCollection<string> unchangedProperties, CancellationToken ct = default);
    /// <summary>Sets one column on one row in a single UPDATE (no read, no other columns touched).
    /// Returns the number of rows affected: 0 when the id doesn't exist.</summary>
    Task<int> UpdateFieldAsync<TProperty>(int id, string propertyName, TProperty value, CancellationToken ct = default);
    /// <summary>Sets one column on every row matching <paramref name="where"/> in a single UPDATE, so the condition
    /// is checked at write time rather than read first. Returns the number of rows affected.</summary>
    Task<int> UpdateFieldWhereAsync<TProperty>(Expression<Func<T, bool>> where, string propertyName, TProperty value,
        CancellationToken ct = default);
    Task RemoveAsync(T entity, CancellationToken ct = default);
    /// <summary>SQL-side MAX(<paramref name="selector"/>) over rows matching <paramref name="predicate"/>; 0 if none match.</summary>
    Task<int> MaxAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default);
    /// <summary>SQL-side GROUP BY <paramref name="key"/> with COUNT(*): row counts per key value, no rows loaded.
    /// Key values with no rows are absent.</summary>
    Task<IReadOnlyDictionary<TKey, int>> CountByAsync<TKey>(Expression<Func<T, TKey>> key, CancellationToken ct = default)
        where TKey : notnull;
}
