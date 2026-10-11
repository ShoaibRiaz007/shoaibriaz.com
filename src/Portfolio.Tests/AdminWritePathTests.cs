using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Controllers;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Security;
using Portfolio.Models;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

/// <summary>Admin writes touch only the columns they own, deleted rows and files are handled, and guards hold.</summary>
public class AdminWritePathTests
{
    private readonly FakeRepository<Bio> _bios = new();
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<Service> _services = new();
    private readonly FakeRepository<Skill> _skills = new();
    private readonly FakeRepository<Certification> _certifications = new();
    private readonly FakeRepository<ProjectMedia> _media = new();
    private readonly FakeFileStorage _files = new();

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };
    private static MemoryStream Pdf() => new(Encoding.ASCII.GetBytes("%PDF-1.7 rest of file"));

    private AdminContentFacade Sut() => new(
        _bios, _services, new FakeRepository<Experience>(), _projects, _skills,
        new FakeRepository<Testimonial>(), new FakeRepository<Education>(), _certifications,
        _media, new FakeRepository<AdminUser>(), new PasswordHasher(), _files, new FakeUnitOfWork());

    private AdminController Controller()
    {
        var http = new DefaultHttpContext();
        return new AdminController(Sut(), new NullOutputCacheStore(), new Portfolio.Caching.NotFoundCache())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NullTempDataProvider()),
        };
    }

    // ---- Bio: column-scoped writes ----

    [Fact]
    public async Task SaveBio_LeavesUntouchedUrlColumnsAlone()
    {
        // An upload replaced the photo after this form was rendered.
        _bios.Items.Add(new Bio { Id = 1, FullName = "Old", ProfileImageUrl = "/uploads/new.png", ResumeUrl = "/uploads/cv.pdf" });
        var form = new Bio { FullName = "New", ProfileImageUrl = "/uploads/old.png", ResumeUrl = "/uploads/cv.pdf" };

        await Sut().SaveBioAsync(form, new[] { nameof(Bio.ProfileImageUrl), nameof(Bio.ResumeUrl) });

        var stored = _bios.Items.Single();
        Assert.Equal(("New", "/uploads/new.png", "/uploads/cv.pdf"), (stored.FullName, stored.ProfileImageUrl, stored.ResumeUrl));
    }

    [Fact]
    public async Task Upload_WritesOnlyItsUrlColumn()
    {
        _bios.Items.Add(new Bio { Id = 1, FullName = "Name", ResumeUrl = "/uploads/old.pdf" });

        Assert.True((await Sut().UploadResumeAsync(Pdf(), "cv.pdf")).Success);

        Assert.Equal((1, nameof(Bio.ResumeUrl), (object?)_files.Saved.Single()), _bios.FieldUpdates.Single());
        Assert.Empty(_bios.Updated); // never a whole-row write that could revert a text edit
    }

    [Fact]
    public async Task Upload_WithNoBioRow_CreatesOne()
    {
        Assert.True((await Sut().UploadProfileImageAsync(new MemoryStream(Png), "me.png")).Success);
        Assert.Equal(_files.Saved.Single(), _bios.Items.Single().ProfileImageUrl);
        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public async Task Upload_StorageFailure_IsAFailedResult_AndChangesNothing()
    {
        _bios.Items.Add(new Bio { Id = 1, ResumeUrl = "/uploads/old.pdf" });
        _files.OnSave = _ => throw new InvalidOperationException("quota exceeded");

        var result = await Sut().UploadResumeAsync(Pdf(), "cv.pdf");

        Assert.False(result.Success);
        Assert.Equal("/uploads/old.pdf", _bios.Items.Single().ResumeUrl);
        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public async Task Upload_KeepsOldFile_WhenAGalleryItemUsesItUnderAnotherSpelling()
    {
        _bios.Items.Add(new Bio { Id = 1, ProfileImageUrl = "/uploads/g.png?v=2" });
        _media.Items.Add(new ProjectMedia { Id = 1, ProjectId = 1, Url = "/uploads/g.png" });

        await Sut().UploadProfileImageAsync(new MemoryStream(Png), "me.png");

        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public async Task Upload_NeverDeletesAFileThisAppDidNotUpload()
    {
        _bios.Items.Add(new Bio { Id = 1, ProfileImageUrl = "https://example.com/me.png" });
        await Sut().UploadProfileImageAsync(new MemoryStream(Png), "me.png");
        Assert.Empty(_files.Deleted);
    }

    // ---- Project media: cover ----

    [Fact]
    public async Task FirstImage_SetsOnlyTheCoverColumn_SoAHideDuringTheUploadSurvives()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "P", Slug = "p" });
        _files.OnSave = _ => _projects.Items[0].IsPublished = false; // hidden from the list mid-upload

        Assert.True((await Sut().UploadMediaAsync(1, new MemoryStream(Png), "a.png", "")).Success);

        var stored = _projects.Items.Single();
        Assert.Equal((_files.Saved.Single(), false), (stored.ImageUrl, stored.IsPublished));
        Assert.Empty(_projects.Updated);
    }

    // ---- Deleted rows ----

    [Fact]
    public async Task SavingADeletedRow_Throws_RowNotFound()
        => await Assert.ThrowsAsync<RowNotFoundException>(() => Sut().SaveServiceAsync(new Service { Id = 5, Title = "Gone" }));

    [Fact]
    public async Task EditForm_ForADeletedRow_ReturnsToTheListWithAnError()
    {
        var c = Controller();
        var redirect = Assert.IsType<RedirectToActionResult>(await c.EditService(new Service { Id = 5, Title = "Gone" }, default));
        Assert.Equal(nameof(AdminController.List), redirect.ActionName);
        Assert.Equal("error", c.TempData[AdminController.MessageTypeKey]);
    }

    [Fact]
    public async Task ValidationError_OnAnExistingRow_ShowsItsStoredVisibility()
    {
        _skills.Items.Add(new Skill { Id = 3, Name = "Go", IsPublished = false });
        var c = Controller();
        c.ModelState.AddModelError(nameof(Skill.Name), "Required.");

        var view = Assert.IsType<ViewResult>(await c.EditSkill(new Skill { Id = 3, Name = "" }, default)); // binds IsPublished = true

        Assert.False(Assert.IsType<Skill>(view.Model).IsPublished);
    }

    // ---- Guards ----

    [Fact]
    public async Task Certification_CredentialUrl_IsSanitised()
    {
        await Sut().SaveCertificationAsync(new Certification { Name = "C", CredentialUrl = "javascript:alert(1)" });
        Assert.Equal("", _certifications.Items.Single().CredentialUrl);
    }

    [Fact]
    public async Task UndefinedKind_IsRejected_ByReorderAndSetPublished()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Sut().ReorderAsync((ContentKind)99, new[] { 1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Sut().SetPublishedAsync((ContentKind)99, 1, true));
    }

    [Fact]
    public async Task Reorder_WithNoIds_IsBadRequest()
    {
        _projects.Items.Add(new Project { Id = 1, SortOrder = 1 });
        Assert.IsType<BadRequestResult>(await Controller().Reorder(ContentKind.Projects, Array.Empty<int>(), default));
        Assert.Empty(_projects.FieldUpdates);
    }

    [Fact]
    public void SetPublished_RequiresTheFlag()
    {
        // Without [BindRequired] a request missing isPublished binds false and silently hides the item.
        var parameter = typeof(AdminController).GetMethod(nameof(AdminController.SetPublished))!
            .GetParameters().Single(p => p.Name == "isPublished");
        Assert.True(parameter.IsDefined(typeof(BindRequiredAttribute), false));
    }

    [Fact]
    public async Task Education_EndYearBeforeStartYear_IsRejected()
    {
        var c = Controller();
        Assert.IsType<ViewResult>(await c.EditEducation(new Education { Institution = "U", StartYear = 2020, EndYear = 2019 }, default));
        Assert.True(c.ModelState.ContainsKey(nameof(Education.EndYear)));
    }

    [Fact]
    public void ExperienceDates_InTheSameMonth_AreNotEndBeforeStart()
    {
        // The binder accepts full dates; both save as June 2025.
        var state = new ModelStateDictionary();
        AdminController.ValidateExperienceDates(
            new Experience { StartDate = new DateOnly(2025, 6, 20), EndDate = new DateOnly(2025, 6, 5) }, state);
        Assert.Equal(0, state.ErrorCount);
    }

    // ---- Sidebar counts ----

    [Fact]
    public async Task ContentCounts_AreLiveAndHiddenPerList()
    {
        _skills.Items.Add(new Skill { Id = 1 });
        _skills.Items.Add(new Skill { Id = 2, IsPublished = false });
        _skills.Items.Add(new Skill { Id = 3, IsPublished = false });

        var counts = await Sut().GetContentCountsAsync();

        Assert.Equal(Enum.GetValues<ContentKind>().Length, counts.Count);
        var skills = AdminOverview.Stats(counts).Single(s => s.Section.Kind == ContentKind.Skills);
        Assert.Equal((1, 2), (skills.Live, skills.Hidden));
    }

    private sealed class NullOutputCacheStore : IOutputCacheStore
    {
        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);
        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
