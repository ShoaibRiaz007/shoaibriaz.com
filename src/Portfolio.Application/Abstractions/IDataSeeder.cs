namespace Portfolio.Application.Abstractions;

/// <summary>Applies pending migrations and seeds initial content + the admin account.</summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
