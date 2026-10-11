using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Facades;

namespace Portfolio.Application;

public static class TelemetryDependencyInjection
{
    /// <summary>Registers the telemetry facade. The store and visitor hasher come from Infrastructure.</summary>
    public static IServiceCollection AddTelemetryApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ITelemetryFacade, TelemetryFacade>();
        return services;
    }
}
