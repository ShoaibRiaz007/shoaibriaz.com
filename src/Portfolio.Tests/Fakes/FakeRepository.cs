using System.Linq.Expressions;
using Portfolio.Application.Abstractions;

namespace Portfolio.Tests.Fakes;

/// <summary>In-memory stand-in for IRepository&lt;T&gt; so facades/services can be unit tested without a database.</summary>
public class FakeRepository<T> : IRepository<T> where T : class
{
    public readonly List<T> Items = new();

    public Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.ToList());

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.AsQueryable().Where(predicate).ToList());

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>> shape, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(shape(Items.AsQueryable().Where(predicate)).ToList());

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Items.AsQueryable().Where(predicate).FirstOrDefault());

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Items.AsQueryable().Any(predicate));

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => throw new NotSupportedException("Fake repository has no identity column; use FirstOrDefaultAsync in tests.");

    public Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoveAsync(T entity, CancellationToken ct = default)
    {
        Items.Remove(entity);
        return Task.CompletedTask;
    }

    public Task<int> MaxAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default)
    {
        var query = Items.AsQueryable().Where(predicate).Select(selector);
        return Task.FromResult(query.Any() ? query.Max() : 0);
    }
}
