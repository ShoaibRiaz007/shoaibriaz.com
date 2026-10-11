using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Abstractions;
using Portfolio.Infrastructure.Persistence;
using Portfolio.Infrastructure.Security;

namespace Portfolio.Infrastructure;

public static class TelemetryDependencyInjection
{
    /// <summary>Registers the telemetry store and the visitor hasher. Call after AddInfrastructure (needs AppDbContext).</summary>
    public static IServiceCollection AddTelemetryInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ITelemetryStore, EfTelemetryStore>();
        // Singleton: it owns the in-memory daily salt, which must be shared by every request.
        services.AddSingleton<IVisitorHasher, DailySaltVisitorHasher>();
        return services;
    }
}
