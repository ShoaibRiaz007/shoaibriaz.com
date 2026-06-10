using Portfolio.Application.Abstractions;

namespace Portfolio.Hosting;

/// <summary>Applies migrations and seeds content once at startup, in its own DI scope.</summary>
public class MigrateAndSeedHostedService : IHostedService
{
    private readonly IServiceProvider _services;

    public MigrateAndSeedHostedService(IServiceProvider services) => _services = services;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
