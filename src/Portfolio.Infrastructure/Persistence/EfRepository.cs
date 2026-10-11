using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Abstractions;

namespace Portfolio.Infrastructure.Persistence;

/// <summary>EF Core implementation of the generic repository. Mutations persist immediately.</summary>
public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _db;
    private readonly DbSet<T> _set;

    public EfRepository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default)
        => await _set.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>> shape, CancellationToken ct = default)
        => await shape(_set.AsNoTracking().Where(predicate)).ToListAsync(ct);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => _set.AsNoTracking().AnyAsync(predicate, ct);

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _set.FindAsync(new object?[] { id }, ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _set.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _set.Update(entity);
        await SaveUpdateAsync(ct);
    }

    public async Task UpdateExcludingAsync(T entity, IReadOnlyCollection<string> unchangedProperties, CancellationToken ct = default)
    {
        var entry = _set.Update(entity);
        foreach (var name in unchangedProperties) entry.Property(name).IsModified = false;
        await SaveUpdateAsync(ct);
    }

    /// <summary>No entity has a concurrency token, so EF reports a concurrency conflict only when the UPDATE
    /// matched no row: the row was deleted after the caller read it.</summary>
    private async Task SaveUpdateAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new RowNotFoundException(typeof(T).Name, ex); }
    }

    public Task<int> UpdateFieldAsync<TProperty>(int id, string propertyName, TProperty value, CancellationToken ct = default)
        => _set.Where(e => EF.Property<int>(e, "Id") == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => EF.Property<TProperty>(e, propertyName), value), ct);

    public Task<int> UpdateFieldWhereAsync<TProperty>(Expression<Func<T, bool>> where, string propertyName, TProperty value,
        CancellationToken ct = default)
        => _set.Where(where).ExecuteUpdateAsync(s => s.SetProperty(e => EF.Property<TProperty>(e, propertyName), value), ct);

    public async Task RemoveAsync(T entity, CancellationToken ct = default)
    {
        _set.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> MaxAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(predicate).Select(selector);
        return await query.AnyAsync(ct) ? await query.MaxAsync(ct) : 0;
    }

    public async Task<IReadOnlyDictionary<TKey, int>> CountByAsync<TKey>(Expression<Func<T, TKey>> key, CancellationToken ct = default)
        where TKey : notnull
        => await _set.AsNoTracking().GroupBy(key)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}
