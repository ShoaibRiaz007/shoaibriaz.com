using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Abstractions;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Persistence;
using Xunit;

namespace Portfolio.Tests;

/// <summary>The column-scoped writes against a real relational provider (ExecuteUpdate needs one; EF InMemory
/// can't run it). SQLite in-memory: one open connection is the database; each context is a separate request.</summary>
public sealed class EfRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public EfRepositoryTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
        db.Projects.Add(new Project { Id = 1, Title = "Old", Slug = "old", SortOrder = 3, IsPublished = false });
        db.Projects.Add(new Project { Id = 2, Title = "Other", Slug = "other", SortOrder = 1, ImageUrl = "/uploads/b.png" });
        db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private Project Stored(int id)
    {
        using var db = NewContext();
        return db.Projects.AsNoTracking().Single(p => p.Id == id);
    }

    [Fact]
    public async Task UpdateExcluding_WritesTheForm_ButNotTheExcludedColumns()
    {
        await using var db = NewContext();
        var staleForm = new Project { Id = 1, Title = "Renamed", Slug = "old", SortOrder = 1, IsPublished = true };

        await new EfRepository<Project>(db).UpdateExcludingAsync(staleForm,
            new[] { nameof(IOrderedContent.SortOrder), nameof(IOrderedContent.IsPublished) });

        var stored = Stored(1);
        Assert.Equal(("Renamed", 3, false), (stored.Title, stored.SortOrder, stored.IsPublished));
    }

    [Fact]
    public async Task UpdateExcluding_DeletedRow_ThrowsRowNotFound()
    {
        await using var db = NewContext();
        await Assert.ThrowsAsync<RowNotFoundException>(() => new EfRepository<Project>(db)
            .UpdateExcludingAsync(new Project { Id = 99, Title = "Gone", Slug = "gone" }, Array.Empty<string>()));
    }

    [Fact]
    public async Task UpdateField_SetsOnlyThatColumn_AndReportsMissingRows()
    {
        await using var db = NewContext();
        var repo = new EfRepository<Project>(db);

        Assert.Equal(1, await repo.UpdateFieldAsync(1, nameof(Project.ImageUrl), "/uploads/cover.png"));
        Assert.Equal(1, await repo.UpdateFieldAsync(1, nameof(IOrderedContent.IsPublished), true));
        Assert.Equal(0, await repo.UpdateFieldAsync(99, nameof(IOrderedContent.IsPublished), true));

        var stored = Stored(1);
        Assert.Equal(("/uploads/cover.png", true, "Old", 3), (stored.ImageUrl, stored.IsPublished, stored.Title, stored.SortOrder));
        Assert.Equal("/uploads/b.png", Stored(2).ImageUrl);
    }

    private static Portfolio.Application.Facades.AdminContentFacade Facade(AppDbContext db, Fakes.FakeFileStorage? files = null) => new(
        new EfRepository<Bio>(db), new EfRepository<Service>(db), new EfRepository<Experience>(db),
        new EfRepository<Project>(db), new EfRepository<Skill>(db), new EfRepository<Testimonial>(db),
        new EfRepository<Education>(db), new EfRepository<Certification>(db), new EfRepository<ProjectMedia>(db),
        new EfRepository<AdminUser>(db), new Portfolio.Infrastructure.Security.PasswordHasher(),
        files ?? new Fakes.FakeFileStorage(), new EfUnitOfWork(db));

    [Fact]
    public async Task DeleteMedia_FileInUseCheck_TranslatesAndMatchesOtherSpellings()
    {
        await using (var seed = NewContext())
        {
            seed.ProjectMedia.Add(new ProjectMedia { Id = 1, ProjectId = 1, Url = "/uploads/b.png?v=2" }); // the cover's file
            seed.ProjectMedia.Add(new ProjectMedia { Id = 2, ProjectId = 1, Url = "/uploads/c.png" });
            await seed.SaveChangesAsync();
        }
        var files = new Fakes.FakeFileStorage();
        await using var db = NewContext();

        await Facade(db, files).DeleteMediaAsync(1);
        await Facade(db, files).DeleteMediaAsync(2);

        Assert.Equal(new[] { "/uploads/c.png" }, files.Deleted);
    }

    [Fact]
    public async Task AdminSidebarCounts_TranslateForEveryList()
    {
        await using var db = NewContext();
        var facade = Facade(db);

        var counts = await facade.GetContentCountsAsync();

        var projects = counts.Single(c => c.Kind == Portfolio.Application.Models.ContentKind.Projects);
        Assert.Equal((1, 1), (projects.Live, projects.Hidden));
        Assert.All(counts.Where(c => c.Kind != Portfolio.Application.Models.ContentKind.Projects),
            c => Assert.Equal((0, 0), (c.Live, c.Hidden)));
    }

    [Fact]
    public async Task CountBy_GroupsInSql()
    {
        await using var db = NewContext();
        var counts = await new EfRepository<Project>(db).CountByAsync(p => p.IsPublished);
        Assert.Equal(1, counts[true]);
        Assert.Equal(1, counts[false]);
    }
}
