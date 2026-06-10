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
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(T entity, CancellationToken ct = default)
    {
        _set.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
