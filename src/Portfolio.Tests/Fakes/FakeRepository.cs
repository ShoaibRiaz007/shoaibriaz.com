using System.Linq.Expressions;
using System.Reflection;
using Portfolio.Application.Abstractions;

namespace Portfolio.Tests.Fakes;

/// <summary>
/// In-memory stand-in for IRepository&lt;T&gt; so facades/services can be unit tested without a database.
/// Reads hand out copies and stored rows change only through the repository's write methods, so a facade that
/// mutates an object and forgets to save fails its test instead of passing on shared references. (Unlike EF,
/// GetByIdAsync also returns a copy rather than a tracked instance.) Added rows get an identity like the database.
/// </summary>
public class FakeRepository<T> : IRepository<T> where T : class
{
    /// <summary>The stored rows. Tests seed and assert on these directly.</summary>
    public readonly List<T> Items = new();

    /// <summary>Every entity passed to UpdateAsync / UpdateExcludingAsync, in call order.</summary>
    public readonly List<T> Updated = new();

    /// <summary>Every single-column write: (row id, property name, value), in call order.</summary>
    public readonly List<(int Id, string Property, object? Value)> FieldUpdates = new();

    private static readonly MethodInfo Clone =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo IdProperty = typeof(T).GetProperty("Id")
        ?? throw new NotSupportedException($"{typeof(T).Name} has no Id property.");

    private static T Copy(T item) => (T)Clone.Invoke(item, null)!;
    private static int IdOf(T item) => (int)IdProperty.GetValue(item)!;
    private int IndexOf(int id) => Items.FindIndex(x => IdOf(x) == id);

    /// <summary>Runs after an unfiltered ListAsync has taken its snapshot, so a test can change the table
    /// between a read and the write that follows it (a concurrent delete).</summary>
    public Action? AfterList { get; set; }

    public Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<T> snapshot = Items.Select(Copy).ToList();
        AfterList?.Invoke();
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.AsQueryable().Where(predicate).AsEnumerable().Select(Copy).ToList());

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>> shape, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(shape(Items.AsQueryable().Where(predicate)).AsEnumerable().Select(Copy).ToList());

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        var hit = Items.AsQueryable().Where(predicate).FirstOrDefault();
        return Task.FromResult(hit is null ? null : Copy(hit));
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Items.AsQueryable().Any(predicate));

    /// <summary>Runs after GetByIdAsync has taken its copy, so a test can change the table between a read and the
    /// write that follows it.</summary>
    public Action? AfterGet { get; set; }

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var i = IndexOf(id);
        var copy = i < 0 ? null : Copy(Items[i]);
        AfterGet?.Invoke();
        return Task.FromResult(copy);
    }

    public Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        if (IdOf(entity) == 0) IdProperty.SetValue(entity, Items.Count == 0 ? 1 : Items.Max(IdOf) + 1);
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    /// <summary>Like EF (a concurrency exception mapped by EfRepository), updating a deleted row throws.</summary>
    private int IndexForUpdate(T entity)
    {
        var i = IndexOf(IdOf(entity));
        return i >= 0 ? i : throw new RowNotFoundException(typeof(T).Name);
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        Items[IndexForUpdate(entity)] = Copy(entity);
        return Task.CompletedTask;
    }

    public Task UpdateExcludingAsync(T entity, IReadOnlyCollection<string> unchangedProperties, CancellationToken ct = default)
    {
        Updated.Add(entity);
        var i = IndexForUpdate(entity);
        var merged = Copy(entity);
        foreach (var name in unchangedProperties)
        {
            var prop = typeof(T).GetProperty(name)!;
            prop.SetValue(merged, prop.GetValue(Items[i]));
        }
        Items[i] = merged;
        return Task.CompletedTask;
    }

    public Task<int> UpdateFieldAsync<TProperty>(int id, string propertyName, TProperty value, CancellationToken ct = default)
    {
        FieldUpdates.Add((id, propertyName, value));
        var i = IndexOf(id);
        if (i < 0) return Task.FromResult(0);
        typeof(T).GetProperty(propertyName)!.SetValue(Items[i], value);
        return Task.FromResult(1);
    }

    public Task<int> UpdateFieldWhereAsync<TProperty>(Expression<Func<T, bool>> where, string propertyName, TProperty value,
        CancellationToken ct = default)
    {
        var match = where.Compile();
        var property = typeof(T).GetProperty(propertyName)!;
        var rows = Items.Where(match).ToList();
        foreach (var row in rows)
        {
            FieldUpdates.Add((IdOf(row), propertyName, value));
            property.SetValue(row, value);
        }
        return Task.FromResult(rows.Count);
    }

    public Task RemoveAsync(T entity, CancellationToken ct = default)
    {
        var i = IndexOf(IdOf(entity));
        if (i >= 0) Items.RemoveAt(i);
        return Task.CompletedTask;
    }

    public Task<int> MaxAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default)
    {
        var query = Items.AsQueryable().Where(predicate).Select(selector);
        return Task.FromResult(query.Any() ? query.Max() : 0);
    }

    public Task<IReadOnlyDictionary<TKey, int>> CountByAsync<TKey>(Expression<Func<T, TKey>> key, CancellationToken ct = default)
        where TKey : notnull
        => Task.FromResult<IReadOnlyDictionary<TKey, int>>(
            Items.AsQueryable().GroupBy(key).ToDictionary(g => g.Key, g => g.Count()));
}
