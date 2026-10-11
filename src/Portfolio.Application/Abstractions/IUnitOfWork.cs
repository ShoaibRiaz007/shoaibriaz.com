namespace Portfolio.Application.Abstractions;

/// <summary>Runs a multi-step persistence operation atomically (single transaction).</summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
