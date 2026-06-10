using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Application.Abstractions;
using Portfolio.Infrastructure.Persistence;
using Portfolio.Infrastructure.Security;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers persistence, security, storage and seeding. Values are supplied by the composition root.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        FileStorageOptions fileStorageOptions,
        AdminSeedOptions adminSeedOptions,
        CloudinaryOptions? cloudinaryOptions = null)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        // Open-generic repository for every aggregate.
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton(adminSeedOptions);
        services.AddScoped<IDataSeeder, DataSeeder>();

        // IFileStorage is polymorphic: Cloudinary when configured, otherwise local disk.
        services.AddSingleton(fileStorageOptions);
        if (cloudinaryOptions is { IsConfigured: true })
        {
            var account = new Account(cloudinaryOptions.CloudName, cloudinaryOptions.ApiKey, cloudinaryOptions.ApiSecret);
            var cloudinary = new Cloudinary(account) { Api = { Secure = true } };
            services.AddSingleton(cloudinary);
            services.AddScoped<IFileStorage, CloudinaryFileStorage>();
        }
        else
        {
            services.AddScoped<IFileStorage, LocalFileStorage>();
        }

        return services;
    }
}
