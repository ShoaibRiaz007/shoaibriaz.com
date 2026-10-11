using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Controllers;
using Portfolio.Domain.Abstractions;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Security;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

public class ListStateTests
{
    private readonly FakeRepository<Bio> _bios = new();
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<Service> _services = new();
    private readonly FakeRepository<Experience> _experiences = new();
    private readonly FakeRepository<ProjectMedia> _media = new();
    private readonly FakeFileStorage _files = new();

    private AdminContentFacade Sut() => new(
        _bios, _services, _experiences, _projects, new FakeRepository<Skill>(),
        new FakeRepository<Testimonial>(), new FakeRepository<Education>(), new FakeRepository<Certification>(),
        _media, new FakeRepository<AdminUser>(), new PasswordHasher(), _files, new FakeUnitOfWork());

    [Fact]
    public async Task StaleServiceForm_KeepsOrderAndVisibility()
    {
        _services.Items.Add(new Service { Id = 1, Title = "Old", SortOrder = 4, IsPublished = false });
        await Sut().SaveServiceAsync(new Service { Id = 1, Title = "New", SortOrder = 1, IsPublished = true });
        var s = _services.Items.Single();
        Assert.Equal(("New", 4, false), (s.Title, s.SortOrder, s.IsPublished));
    }

    [Fact]
    public async Task StaleExperienceForm_KeepsOrderAndVisibility()
    {
        _experiences.Items.Add(new Experience { Id = 1, Role = "Old", SortOrder = 4, IsPublished = false, StartDate = new DateOnly(2020, 1, 1) });
        await Sut().SaveExperienceAsync(new Experience { Id = 1, Role = "New", SortOrder = 1, IsPublished = true, StartDate = new DateOnly(2020, 1, 1) });
        var e = _experiences.Items.Single();
        Assert.Equal(("New", 4, false), (e.Role, e.SortOrder, e.IsPublished));
    }

    [Fact]
    public async Task NewRow_CanBeCreatedHidden()
    {
        var draft = new Project { Title = "Draft", IsPublished = false };
        await Sut().SaveProjectAsync(draft);
        Assert.False(_projects.Items.Single().IsPublished);
    }

    [Fact]
    public async Task Reorder_WritesOnlySortOrder_ForEveryRow_InIdOrder()
    {
        _projects.Items.Add(new Project { Id = 3, Title = "C", SortOrder = 3 });
        _projects.Items.Add(new Project { Id = 1, Title = "A", SortOrder = 1 });
        _projects.Items.Add(new Project { Id = 2, Title = "B", SortOrder = 2 });

        await Sut().ReorderAsync(ContentKind.Projects, new[] { 3, 1, 2 });

        // Rows already in place are still written (stale "already right" skips let overlapping reorders duplicate).
        Assert.Equal(new[] { 1, 2, 3 }, _projects.FieldUpdates.Select(u => u.Id));
        Assert.All(_projects.FieldUpdates, u => Assert.Equal(nameof(IOrderedContent.SortOrder), u.Property));
        Assert.Empty(_projects.Updated); // never a whole-row write
        Assert.Equal(new[] { 3, 1, 2 }, _projects.Items.OrderBy(p => p.SortOrder).Select(p => p.Id));
    }

    [Fact]
    public async Task SetPublished_WritesOnlyTheFlag()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "A" });
        Assert.True(await Sut().SetPublishedAsync(ContentKind.Projects, 1, false));
        Assert.Equal((1, nameof(IOrderedContent.IsPublished), (object?)false), _projects.FieldUpdates.Single());
        Assert.Empty(_projects.Updated);
    }

    [Fact]
    public async Task UploadPhoto_KeepsOldFile_UsedAsAProjectCover()
    {
        _bios.Items.Add(new Bio { Id = 1, ProfileImageUrl = "/uploads/cover.png" });
        _projects.Items.Add(new Project { Id = 1, ImageUrl = "/uploads/cover.png" });
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };

        await Sut().UploadProfileImageAsync(new MemoryStream(png), "me.png");

        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public async Task UploadResume_KeepsOldFile_StillUsedByTheOtherBioField()
    {
        _bios.Items.Add(new Bio { Id = 1, ResumeUrl = "/uploads/same.pdf", ProfileImageUrl = "/uploads/same.pdf" });
        await Sut().UploadResumeAsync(new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 x")), "cv.pdf");
        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public async Task DeleteMedia_KeepsFile_UsedAsTheProfilePhoto()
    {
        _bios.Items.Add(new Bio { Id = 1, ProfileImageUrl = "/uploads/x.png" });
        _media.Items.Add(new ProjectMedia { Id = 7, ProjectId = 1, Url = "/uploads/x.png" });

        await Sut().DeleteMediaAsync(7);

        Assert.Empty(_media.Items);
        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public async Task DeleteMedia_DeletesFile_WhenNothingElseUsesIt()
    {
        _media.Items.Add(new ProjectMedia { Id = 7, ProjectId = 1, Url = "/uploads/x.png" });
        await Sut().DeleteMediaAsync(7);
        Assert.Equal(new[] { "/uploads/x.png" }, _files.Deleted);
    }
}

public class PublicOrderingTests
{
    [Fact]
    public async Task Projects_FollowAdminOrder_NotFeaturedFirst()
    {
        var projects = new FakeRepository<Project>();
        projects.Items.Add(new Project { Id = 1, Slug = "plain", SortOrder = 1 });
        projects.Items.Add(new Project { Id = 2, Slug = "featured", SortOrder = 2, IsFeatured = true });
        var sut = new PublicContentFacade(new FakeRepository<Bio>(), new FakeRepository<Service>(), new FakeRepository<Experience>(),
            projects, new FakeRepository<Skill>(), new FakeRepository<Testimonial>(), new FakeRepository<Education>(),
            new FakeRepository<Certification>(), new FakeRepository<ProjectMedia>());

        Assert.Equal(new[] { "plain", "featured" }, (await sut.GetSiteAsync()).Projects.Select(p => p.Slug));
        Assert.Equal(new[] { "plain", "featured" }, (await sut.GetProjectsAsync()).Projects.Select(p => p.Slug));
    }
}

public class AdminControllerRuleTests
{
    [Fact]
    public void BioForm_UntouchedUrlField_IsLeftOut_EditedFieldIsSaved()
    {
        var posted = new Bio { ProfileImageUrl = "/uploads/old.png", ResumeUrl = "https://example.com/new.pdf" };
        Assert.Equal(new[] { nameof(Bio.ProfileImageUrl) },
            AdminController.UntouchedFileUrlColumns(posted, "/uploads/old.png", "/uploads/cv.pdf"));
    }

    [Fact]
    public void BioForm_UntouchedResumeUrl_IsLeftOut()
    {
        var posted = new Bio { ProfileImageUrl = "https://example.com/me.png", ResumeUrl = "/uploads/cv.pdf" };
        Assert.Equal(new[] { nameof(Bio.ResumeUrl) },
            AdminController.UntouchedFileUrlColumns(posted, "/uploads/old.png", "/uploads/cv.pdf"));
        // A missing original (old form) counts as "", so a blank field stays untouched rather than clearing the URL.
        Assert.Equal(new[] { nameof(Bio.ProfileImageUrl), nameof(Bio.ResumeUrl) },
            AdminController.UntouchedFileUrlColumns(new Bio(), null, null));
    }

    [Theory]
    [InlineData(0, false, false, "", true)]                  // new role without a start month
    [InlineData(5, false, true, "Jul 2022 - Present", true)] // legacy row given only an end month
    [InlineData(5, false, false, "", true)]                  // existing row with nothing at all
    [InlineData(5, false, false, "2019 – 2020", false)]      // legacy row left untouched
    [InlineData(0, true, false, "", false)]                  // normal new role
    public void ExperienceDates(int id, bool hasStart, bool hasEnd, string period, bool expectError)
    {
        var model = new Experience
        {
            Id = id, Period = period,
            StartDate = hasStart ? new DateOnly(2022, 7, 1) : null,
            EndDate = hasEnd ? new DateOnly(2026, 5, 1) : null,
        };
        var state = new ModelStateDictionary();
        AdminController.ValidateExperienceDates(model, state);
        Assert.Equal(expectError, state.ErrorCount > 0);
    }

    [Fact]
    public void EndBeforeStart_IsRejected()
    {
        var state = new ModelStateDictionary();
        AdminController.ValidateExperienceDates(
            new Experience { StartDate = new DateOnly(2025, 6, 1), EndDate = new DateOnly(2024, 1, 1) }, state);
        Assert.True(state.ContainsKey(nameof(Experience.EndDate)));
    }

    // A missing or misspelled kind binds to ContentKind's zero value (Projects); the model-state error that
    // [BindRequired]/the enum converter records must stop the action before it touches any list.
    private static AdminController ControllerWithBindingError()
    {
        var c = new AdminController(null!, null!, null!);
        c.ModelState.AddModelError("kind", "The kind field is required.");
        return c;
    }

    [Fact]
    public async Task Reorder_WithBindingError_IsBadRequest()
        => Assert.IsType<BadRequestResult>(await ControllerWithBindingError().Reorder(ContentKind.Projects, new[] { 1 }, default));

    [Fact]
    public async Task SetPublished_WithBindingError_IsBadRequest()
        => Assert.IsType<BadRequestResult>(await ControllerWithBindingError().SetPublished(ContentKind.Projects, 1, false, default));
}
