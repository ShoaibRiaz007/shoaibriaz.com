using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;

namespace Portfolio.Infrastructure.Persistence;

/// <summary>Admin bootstrap credentials supplied by configuration (no hardcoded fallback for the password).</summary>
public record AdminSeedOptions(string Username, string? Password);

/// <summary>Applies migrations and seeds initial content + the admin account. Idempotent.</summary>
public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly AdminSeedOptions _admin;

    public DataSeeder(AppDbContext db, IPasswordHasher hasher, AdminSeedOptions admin)
    {
        _db = db; _hasher = hasher; _admin = admin;
    }

    // Arbitrary fixed key for the advisory lock namespace used to serialize migrate+seed across
    // concurrently-starting instances. Any int64 works as long as it's stable across deployments.
    private const long MigrationLockKey = 851_203_411;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Postgres advisory lock, held for the connection's lifetime: if two instances start at once
        // against the same database, the second blocks here instead of racing the first on DDL/seed
        // inserts. Session-scoped, so it's automatically released when the connection closes even on
        // a crash.
        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using (var lockCmd = connection.CreateCommand())
            {
                lockCmd.CommandText = "SELECT pg_advisory_lock(@key)";
                var param = lockCmd.CreateParameter();
                param.ParameterName = "key";
                param.Value = MigrationLockKey;
                lockCmd.Parameters.Add(param);
                await lockCmd.ExecuteNonQueryAsync(ct);
            }

            await SeedInternalAsync(ct);
        }
        finally
        {
            await using var unlockCmd = connection.CreateCommand();
            unlockCmd.CommandText = "SELECT pg_advisory_unlock(@key)";
            var param = unlockCmd.CreateParameter();
            param.ParameterName = "key";
            param.Value = MigrationLockKey;
            unlockCmd.Parameters.Add(param);
            await unlockCmd.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private async Task SeedInternalAsync(CancellationToken ct)
    {
        await _db.Database.MigrateAsync(ct);

        // ---- Admin account ----
        if (!await _db.AdminUsers.AnyAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(_admin.Password))
                throw new InvalidOperationException(
                    "No admin account exists and no admin password is configured. " +
                    "Set 'Admin:Password' (user-secrets) or the 'Admin__Password' environment variable before first run.");

            _db.AdminUsers.Add(new AdminUser
            {
                Username = string.IsNullOrWhiteSpace(_admin.Username) ? "admin" : _admin.Username,
                PasswordHash = _hasher.Hash(_admin.Password),
                SecurityStamp = Guid.NewGuid().ToString("N")
            });
        }

        // Backfill stamps for accounts created before security stamps existed.
        foreach (var u in await _db.AdminUsers.Where(u => u.SecurityStamp == "").ToListAsync(ct))
            u.SecurityStamp = Guid.NewGuid().ToString("N");

        // Fresh databases only: each table is seeded when empty, so admin edits are never overwritten.
        // Existing databases get new content through scripts/, not here.
        if (!await _db.Bios.AnyAsync(ct)) _db.Bios.Add(SeedContent.Bio());
        if (!await _db.Services.AnyAsync(ct)) _db.Services.AddRange(SeedContent.Services());
        if (!await _db.Experiences.AnyAsync(ct)) _db.Experiences.AddRange(SeedContent.Experiences());
        if (!await _db.Educations.AnyAsync(ct)) _db.Educations.AddRange(SeedContent.Educations());
        if (!await _db.Certifications.AnyAsync(ct)) _db.Certifications.AddRange(SeedContent.Certifications());
        if (!await _db.Skills.AnyAsync(ct)) _db.Skills.AddRange(SeedContent.Skills());

        if (!await _db.Projects.AnyAsync(ct))
        {
            _db.Projects.AddRange(SeedContent.Projects());

            // InvoGames titles: the curated few are public, the rest are seeded hidden (kept for the record).
            var hiddenOrder = SeedContent.GamesSortStart + SeedContent.CuratedGameSlugs.Count;
            foreach (var game in ProjectSeedData.InvoGamesProjects())
            {
                var curated = SeedContent.CuratedGameSlugs.ToList().IndexOf(game.Slug);
                game.IsFeatured = false;
                game.IsPublished = curated >= 0;
                game.SortOrder = curated >= 0 ? SeedContent.GamesSortStart + curated : hiddenOrder++;
                _db.Projects.Add(game);
            }
        }

        if (!await _db.Testimonials.AnyAsync(ct))
        {
            _db.Testimonials.Add(new Testimonial
            {
                Author = "Abel Chocano Gómez",
                AuthorTitle = "Co-Founder at Gamersfy / Winnder.com",
                SortOrder = 1,
                Quote =
                    "Working with Shoaib on WINNDER projects has been a very positive experience. He has proven to be a highly skilled professional, " +
                    "with a strong ability to understand product needs and translate them accurately into each project. He is very reliable when it comes to " +
                    "meeting deadlines and has a great ability to interpret what the client is looking for and bring it to life effectively in every delivery. " +
                    "Without a doubt, a well-rounded professional who adds value from day one and truly a pleasure to work with."
            });
        }

        await _db.SaveChangesAsync(ct);

        // Replace the old placeholder contact email on databases seeded before it was fixed.
        foreach (var bio in await _db.Bios.Where(b => b.Email == "shoaib.riaz@example.com").ToListAsync(ct))
            bio.Email = "riazshoaib17@gmail.com";

        // Backfill slugs on rows created before slugs existed. Projects are seeded only into an empty table
        // (above), so a project the admin deletes stays deleted.
        foreach (var p in await _db.Projects.Where(p => p.Slug.Trim() == "").ToListAsync(ct))
            p.Slug = SlugGenerator.Generate(p.Title);

        await _db.SaveChangesAsync(ct);
    }
}
