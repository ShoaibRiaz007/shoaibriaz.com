using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Abstractions;

namespace Portfolio.Infrastructure.Persistence;

/// <summary>Wraps an operation in a database transaction via the provider's execution strategy,
/// so multi-step facade operations (e.g. media upload + project update) commit or roll back together.</summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public EfUnitOfWork(AppDbContext db) => _db = db;

    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(ct, async token =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(token);
            await operation(token);
            await tx.CommitAsync(token);
        });
    }
}
