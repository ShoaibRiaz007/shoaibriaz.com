using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Portfolio.Api;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Security;
using Portfolio.ModelBinding;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

public class PublicContentFacadePublishedTests
{
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<Experience> _experiences = new();
    private readonly FakeRepository<Skill> _skills = new();

    private PublicContentFacade Sut() => new(
        new FakeRepository<Bio>(), new FakeRepository<Service>(), _experiences, _projects, _skills,
        new FakeRepository<Testimonial>(), new FakeRepository<Education>(), new FakeRepository<Certification>(),
        new FakeRepository<ProjectMedia>());

    public PublicContentFacadePublishedTests()
    {
        _projects.Items.Add(new Project { Id = 1, Slug = "live", Title = "Live", IsFeatured = true, SortOrder = 1 });
        _projects.Items.Add(new Project { Id = 2, Slug = "draft", Title = "Draft", IsFeatured = true, SortOrder = 2, IsPublished = false });
        _experiences.Items.Add(new Experience { Id = 1, Role = "Shown", SortOrder = 1 });
        _experiences.Items.Add(new Experience { Id = 2, Role = "Hidden", SortOrder = 2, IsPublished = false });
        _skills.Items.Add(new Skill { Id = 1, Name = "Hidden skill", IsPublished = false });
    }

    [Fact]
    public async Task Site_ReturnsOnlyPublishedRows()
    {
        var site = await Sut().GetSiteAsync();
        Assert.Equal(new[] { "live" }, site.Projects.Select(p => p.Slug));
        Assert.Equal(new[] { "Shown" }, site.Experiences.Select(e => e.Role));
        Assert.Empty(site.Skills);
    }

    [Fact]
    public async Task Home_And_ProjectsPage_ExcludeHiddenProjects()
    {
        var home = await Sut().GetHomeAsync();
        Assert.DoesNotContain(home.ProminentProjects, p => p.Slug == "draft");
        Assert.Equal(1, home.TotalProjects);
        Assert.DoesNotContain((await Sut().GetProjectsAsync()).Projects, p => p.Slug == "draft");
    }

    [Fact]
    public async Task ProjectDetail_CarriesThePublishedListForPrevNext_AndMoreExcludesItself()
    {
        _projects.Items.Add(new Project { Id = 3, Slug = "third", Title = "Third", SortOrder = 3 });
        var detail = await Sut().GetProjectDetailAsync("LIVE");
        Assert.NotNull(detail);
        Assert.Equal(new[] { "live", "third" }, detail!.Projects.Select(p => p.Slug));
        Assert.Equal(new[] { "third" }, detail.More.Select(p => p.Slug));
    }

    [Fact]
    public async Task NotFound_SuggestsFeaturedFirst_PublishedOnly_Bounded()
    {
        _projects.Items.Add(new Project { Id = 3, Slug = "plain", SortOrder = 0 });
        _projects.Items.Add(new Project { Id = 4, Slug = "plain-2", SortOrder = 5 });
        _projects.Items.Add(new Project { Id = 5, Slug = "plain-3", SortOrder = 6 });
        var data = await Sut().GetNotFoundAsync(3);
        Assert.Equal(new[] { "live", "plain", "plain-2" }, data.Suggestions.Select(p => p.Slug));
    }

    [Fact]
    public async Task HiddenProjectDetail_IsNotFound()
    {
        Assert.Null(await Sut().GetProjectDetailAsync("draft"));
        var live = await Sut().GetProjectDetailAsync("live");
        Assert.NotNull(live);
        Assert.DoesNotContain(live!.More, p => p.Slug == "draft");
    }
}

public class AdminContentFacadeGuardTests
{
    private readonly FakeRepository<Bio> _bios = new();
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<Experience> _experiences = new();
    private readonly FakeRepository<ProjectMedia> _media = new();
    private readonly FakeFileStorage _files = new();

    private AdminContentFacade Sut() => new(
        _bios, new FakeRepository<Service>(), _experiences, _projects, new FakeRepository<Skill>(),
        new FakeRepository<Testimonial>(), new FakeRepository<Education>(), new FakeRepository<Certification>(),
        _media, new FakeRepository<AdminUser>(), new PasswordHasher(), _files, new FakeUnitOfWork());

    private static MemoryStream Pdf() => new(Encoding.ASCII.GetBytes("%PDF-1.7 rest of file"));

    [Fact]
    public async Task EditingAStaleForm_KeepsDashboardOrderAndVisibility()
    {
        // The form was opened while the row was first and visible; since then it was dragged last and hidden.
        _projects.Items.Add(new Project { Id = 1, Title = "A", Slug = "a", SortOrder = 3, IsPublished = false });
        var staleForm = new Project { Id = 1, Title = "A (renamed)", Slug = "a", SortOrder = 1, IsPublished = true };

        await Sut().SaveProjectAsync(staleForm);

        var stored = _projects.Items.Single();
        Assert.Equal("A (renamed)", stored.Title);
        Assert.Equal(3, stored.SortOrder);
        Assert.False(stored.IsPublished);
    }

    [Fact]
    public async Task Reorder_UnlistedRowsKeepTheirRelativeOrder_AndDuplicateIdsAreIgnored()
    {
        // Insertion order deliberately differs from SortOrder, so only the SortOrder tiebreak orders 4 and 5.
        _projects.Items.Add(new Project { Id = 1, SortOrder = 1 });
        _projects.Items.Add(new Project { Id = 5, SortOrder = 4 });
        _projects.Items.Add(new Project { Id = 2, SortOrder = 2 });
        _projects.Items.Add(new Project { Id = 4, SortOrder = 3 });

        await Sut().ReorderAsync(ContentKind.Projects, new[] { 2, 2, 1 });

        Assert.Equal(new[] { 2, 1, 4, 5 }, _projects.Items.OrderBy(p => p.SortOrder).Select(p => p.Id));
        Assert.Equal(4, _projects.Items.Select(p => p.SortOrder).Distinct().Count());
    }

    [Fact]
    public async Task NewRow_WithExplicitSortOrder_KeepsIt()
    {
        var p = new Project { Title = "Seeded", SortOrder = 7 };
        await Sut().SaveProjectAsync(p);
        Assert.Equal(7, p.SortOrder);
    }

    [Fact]
    public async Task SaveExperience_NormalisesEndDateToTheMonth()
    {
        var role = new Experience { Role = "Dev", Company = "X", StartDate = new DateOnly(2023, 8, 17), EndDate = new DateOnly(2025, 6, 30) };
        await Sut().SaveExperienceAsync(role);
        Assert.Equal(new DateOnly(2025, 6, 1), role.EndDate);
        Assert.Equal("Aug 2023 - Jun 2025", role.Period);
    }

    [Fact]
    public async Task UploadResume_DeletesOldFileOnlyAfterBioPointsAtNewOne()
    {
        _bios.Items.Add(new Bio { Id = 1, ResumeUrl = "/uploads/old.pdf" });
        string? urlAtDeleteTime = null;
        _files.OnDelete = _ => urlAtDeleteTime = _bios.Items.Single().ResumeUrl;

        await Sut().UploadResumeAsync(Pdf(), "cv.pdf");

        Assert.Equal(_files.Saved.Single(), urlAtDeleteTime);
    }

    [Fact]
    public async Task UploadResume_OverTheLimit_IsRejectedBeforeStorage()
    {
        _bios.Items.Add(new Bio { Id = 1 });
        var big = new byte[10 * 1024 * 1024 + 1];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(big, 0);

        var result = await Sut().UploadResumeAsync(new MemoryStream(big), "cv.pdf");

        Assert.False(result.Success);
        Assert.Empty(_files.Saved);
    }

    [Fact]
    public async Task UploadPhoto_DoesNotDeleteAFileAProjectStillUses()
    {
        _bios.Items.Add(new Bio { Id = 1, ProfileImageUrl = "/uploads/shared.png" });
        _media.Items.Add(new ProjectMedia { Id = 1, ProjectId = 1, Url = "/uploads/shared.png" });
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };

        var result = await Sut().UploadProfileImageAsync(new MemoryStream(png), "me.png");

        Assert.True(result.Success, result.Message);
        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public async Task UploadResume_OldFileCleanupFailure_StillReportsSuccess()
    {
        _bios.Items.Add(new Bio { Id = 1, ResumeUrl = "/uploads/old.pdf" });
        var deleteAttempted = false;
        _files.OnDelete = _ => { deleteAttempted = true; throw new InvalidOperationException("Cloudinary delete failed: rate limited"); };

        var result = await Sut().UploadResumeAsync(Pdf(), "cv.pdf");

        Assert.True(deleteAttempted);
        Assert.True(result.Success);
        Assert.True(result.CleanupFailed); // the admin is told the old file is still in storage
        Assert.Equal(_files.Saved.Single(), _bios.Items.Single().ResumeUrl);
    }

    [Fact]
    public async Task UploadPhoto_Gif_IsRejected()
    {
        _bios.Items.Add(new Bio { Id = 1 });
        var gif = Encoding.ASCII.GetBytes("GIF89a\0\0\0\0\0\0\0\0\0\0");

        var result = await Sut().UploadProfileImageAsync(new MemoryStream(gif), "me.gif");

        Assert.False(result.Success);
        Assert.Empty(_files.Saved);
    }
}

public class MonthDateOnlyModelBinderTests
{
    private static async Task<(ModelBindingResult Result, ModelStateDictionary State)> BindAsync(Type type, string? value)
    {
        var values = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        if (value is not null) values["d"] = value;
        var context = new DefaultModelBindingContext
        {
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(type),
            ModelName = "d",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, new QueryCollection(values), CultureInfo.InvariantCulture),
        };
        await new MonthDateOnlyModelBinder().BindModelAsync(context);
        return (context.Result, context.ModelState);
    }

    [Theory]
    [InlineData("2025-06", 2025, 6, 1)]
    [InlineData("2025-06-14", 2025, 6, 14)]
    public async Task Binds_MonthAndFullDate(string raw, int y, int m, int d)
    {
        var (result, _) = await BindAsync(typeof(DateOnly?), raw);
        Assert.True(result.IsModelSet);
        Assert.Equal(new DateOnly(y, m, d), result.Model);
    }

    [Fact]
    public async Task Empty_BindsNull_ForNullable()
    {
        var (result, state) = await BindAsync(typeof(DateOnly?), "");
        Assert.True(result.IsModelSet);
        Assert.Null(result.Model);
        Assert.Equal(0, state.ErrorCount); // IsValid stays false until validation runs
    }

    [Fact]
    public async Task Empty_IsAnError_ForNonNullable()
    {
        var (result, state) = await BindAsync(typeof(DateOnly), "");
        Assert.False(result.IsModelSet);
        Assert.Equal(1, state.ErrorCount);
    }

    [Fact]
    public async Task Garbage_IsAnError()
    {
        var (result, state) = await BindAsync(typeof(DateOnly?), "June 2025");
        Assert.False(result.IsModelSet);
        Assert.Equal(1, state.ErrorCount);
    }
}

public class PublicApiShapeTests
{
    [Fact]
    public void ProfileDto_DoesNotExposePhone()
        => Assert.Null(typeof(ProfileDto).GetProperty("Phone"));
}
