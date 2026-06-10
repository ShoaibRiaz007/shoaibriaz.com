using Microsoft.Extensions.DependencyInjection;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Facades;

namespace Portfolio.Application;

public static class DependencyInjection
{
    /// <summary>Registers the application-layer facades. Repositories/hasher/storage come from Infrastructure.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPublicContentFacade, PublicContentFacade>();
        services.AddScoped<IAdminContentFacade, AdminContentFacade>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
