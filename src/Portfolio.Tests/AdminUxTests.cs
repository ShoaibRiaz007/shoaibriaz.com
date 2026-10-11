using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Controllers;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Security;
using Portfolio.Models;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

/// <summary>Admin redesign: per-section list pages, in-place visibility toggles, dashboard stats and checklist.</summary>
public class AdminUxControllerTests
{
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<Skill> _skills = new();

    private AdminController Sut(bool ajax = false)
    {
        var facade = new AdminContentFacade(
            new FakeRepository<Bio>(), new FakeRepository<Service>(), new FakeRepository<Experience>(), _projects, _skills,
            new FakeRepository<Testimonial>(), new FakeRepository<Education>(), new FakeRepository<Certification>(),
            new FakeRepository<ProjectMedia>(), new FakeRepository<AdminUser>(), new PasswordHasher(), new FakeFileStorage(),
            new FakeUnitOfWork());
        var http = new DefaultHttpContext();
        if (ajax) http.Request.Headers.XRequestedWith = "XMLHttpRequest";
        return new AdminController(facade, new NullOutputCacheStore(), new Portfolio.Caching.NotFoundCache())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NullTempDataProvider()),
        };
    }

    [Fact]
    public async Task List_WithBindingError_IsNotFound()
    {
        var c = Sut();
        c.ModelState.AddModelError("kind", "The value 'nope' is not valid.");
        Assert.IsType<NotFoundResult>(await c.List(ContentKind.Projects, default));
    }

    [Fact]
    public async Task ProjectsList_ShowsViewCounts_AndStillWorksWhenTheyFail()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "A", Slug = "a" });
        var store = new FakeTelemetryStore();
        store.PageViews.Add(new PageView { Id = 1, OccurredAt = DateTime.UtcNow, Path = "/project/a", VisitorHash = "v", Source = "Direct" });
        var telemetry = new TelemetryFacade(store, new FakeVisitorHasher(), TimeProvider.System);

        var model = Assert.IsType<ContentListViewModel>(Assert.IsType<ViewResult>(await Sut().List(ContentKind.Projects, default, telemetry)).Model);
        Assert.Contains("Views (30 days)", model.Columns);
        var row = Assert.Single(model.Rows);
        Assert.Equal("1 · 1 visitor", row.Cells[^1]);
        Assert.DoesNotContain("visitor", row.SearchText);   // the count is shown, not matched by the search
        Assert.StartsWith("a ", row.SearchText);

        store.FailQueries = true;
        Assert.IsType<ViewResult>(await Sut().List(ContentKind.Projects, default, telemetry));
    }

    [Fact]
    public async Task List_UndefinedKind_IsNotFound()
        => Assert.IsType<NotFoundResult>(await Sut().List((ContentKind)99, default));

    [Fact]
    public async Task List_ShowsThatKindsRows_InAdminOrder_AndSharesTheSnapshotWithTheSidebar()
    {
        _skills.Items.Add(new Skill { Id = 1, Name = "B", SortOrder = 2 });
        _skills.Items.Add(new Skill { Id = 2, Name = "A", SortOrder = 1, IsPublished = false });
        var c = Sut();

        var view = Assert.IsType<ViewResult>(await c.List(ContentKind.Skills, default));

        var model = Assert.IsType<ContentListViewModel>(view.Model);
        Assert.Equal(ContentKind.Skills, model.Kind);
        Assert.Equal(new[] { 2, 1 }, model.Rows.Select(r => r.Id));
        Assert.False(model.Rows[0].IsPublished);
        Assert.Equal("EditSkill", model.EditAction);
        Assert.IsType<AdminDashboard>(c.HttpContext.Items[AdminController.DashboardItemKey]);
    }

    [Fact]
    public async Task SetPublished_FromScript_Returns204_WithoutAFlashMessage()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "A" });
        var c = Sut(ajax: true);

        Assert.IsType<NoContentResult>(await c.SetPublished(ContentKind.Projects, 1, false, default));

        Assert.False(_projects.Items.Single().IsPublished);
        Assert.False(c.TempData.ContainsKey("Message")); // would otherwise pop up on the next page load
    }

    [Fact]
    public async Task SetPublished_FormPost_RedirectsToItsList_WithAMessage()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "A", IsPublished = false });
        var c = Sut();

        var redirect = Assert.IsType<RedirectToActionResult>(await c.SetPublished(ContentKind.Projects, 1, true, default));

        Assert.Equal(nameof(AdminController.List), redirect.ActionName);
        Assert.Equal("projects", redirect.RouteValues!["kind"]);
        Assert.NotNull(c.TempData["Message"]);
        Assert.False(c.TempData.ContainsKey(AdminController.MessageTypeKey)); // a success toast, not an error
    }

    [Fact]
    public async Task SetPublished_MissingRow_IsNotFound_EvenFromScript()
        => Assert.IsType<NotFoundResult>(await Sut(ajax: true).SetPublished(ContentKind.Projects, 42, true, default));

    [Fact]
    public async Task DeleteSkill_ReturnsToTheSkillsList()
    {
        _skills.Items.Add(new Skill { Id = 7, Name = "Go" });
        var redirect = Assert.IsType<RedirectToActionResult>(await Sut().DeleteSkill(7, default));
        Assert.Equal(nameof(AdminController.List), redirect.ActionName);
        Assert.Equal("skills", redirect.RouteValues!["kind"]);
    }

    [Fact]
    public async Task FailedUpload_IsFlaggedAsAnError()
    {
        var c = Sut();
        await c.UploadResume(null, default);
        Assert.NotNull(c.TempData["Message"]);
        Assert.Equal("error", c.TempData[AdminController.MessageTypeKey]);
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

public class AdminOverviewTests
{
    private static AdminDashboard Dashboard(Bio? bio = null, IReadOnlyList<Project>? projects = null,
        IReadOnlyList<Testimonial>? testimonials = null, IReadOnlyList<Skill>? skills = null) => new(
        bio ?? new Bio(), Array.Empty<Service>(), Array.Empty<Experience>(), projects ?? Array.Empty<Project>(),
        skills ?? Array.Empty<Skill>(), testimonials ?? Array.Empty<Testimonial>(), Array.Empty<Education>(),
        Array.Empty<Certification>());

    private static ChecklistItem Check(AdminDashboard d, string label) => AdminOverview.Checklist(d).Single(c => c.Label == label);

    [Fact]
    public void Stats_CountLiveAndHiddenPerSection()
    {
        var d = Dashboard(skills: new[]
        {
            new Skill { Id = 1 }, new Skill { Id = 2 }, new Skill { Id = 3, IsPublished = false },
        });
        var skills = AdminOverview.Stats(d).Single(s => s.Section.Kind == ContentKind.Skills);
        Assert.Equal((2, 1, 3), (skills.Live, skills.Hidden, skills.Total));
        Assert.Equal(Enum.GetValues<ContentKind>().Length, AdminOverview.Stats(d).Count); // every list has a card
    }

    [Fact]
    public void EmptyProfile_FlagsPhotoAndCv_WithLinksToTheUploadSection()
    {
        var photo = Check(Dashboard(), "Profile photo");
        var cv = Check(Dashboard(), "CV (PDF)");
        Assert.False(photo.Done);
        Assert.False(cv.Done);
        Assert.Equal(("EditBio", "files"), (photo.FixAction, photo.FixFragment));
        Assert.True(Check(Dashboard(new Bio { ProfileImageUrl = "/uploads/me.png" }), "Profile photo").Done);
    }

    [Fact]
    public void FeaturedProjects_CountOnlyLiveOnes_AgainstTheHomePageMinimum()
    {
        var projects = new[]
        {
            new Project { Id = 1, IsFeatured = true }, new Project { Id = 2, IsFeatured = true },
            new Project { Id = 3, IsFeatured = true, IsPublished = false },
        };
        Assert.False(Check(Dashboard(projects: projects), "Featured projects").Done);
        projects[2].IsPublished = true;
        Assert.True(Check(Dashboard(projects: projects), "Featured projects").Done);
    }

    // More featured than the home page shows: still done, and the detail names how many it shows.
    [Fact]
    public void FeaturedProjects_BeyondTheHomeCount_SayHowManyShow()
    {
        var projects = Enumerable.Range(1, AdminOverview.HomeFeaturedProjects + 1)
            .Select(i => new Project { Id = i, IsFeatured = true }).ToArray();
        var item = Check(Dashboard(projects: projects), "Featured projects");
        Assert.True(item.Done);
        Assert.Contains($"first {AdminOverview.HomeFeaturedProjects}", item.Detail);
        Assert.DoesNotContain("first", Check(Dashboard(projects: projects[..AdminOverview.HomeFeaturedProjects]), "Featured projects").Detail);
    }

    [Fact]
    public void OneProjectMissingAnImage_LinksStraightToItsForm_SeveralLinkToTheList()
    {
        var one = new[] { new Project { Id = 4, Title = "Bare", Metric = "x" }, new Project { Id = 5, ImageUrl = "/a.png", Metric = "y" } };
        var item = Check(Dashboard(projects: one), "Project cover images");
        Assert.False(item.Done);
        Assert.Equal("EditProject", item.FixAction);
        Assert.Equal("4", item.FixRoute!["id"]);

        var two = new[] { new Project { Id = 4 }, new Project { Id = 5 }, new Project { Id = 6, IsPublished = false } };
        var many = Check(Dashboard(projects: two), "Project cover images");
        Assert.Equal("List", many.FixAction);
        Assert.Contains("2 live projects", many.Detail); // the hidden one doesn't count
    }

    [Fact]
    public void List_BuildsColumnsAndRowsForEveryKind()
    {
        foreach (var kind in Enum.GetValues<ContentKind>())
        {
            var list = AdminOverview.List(Dashboard(), kind);
            Assert.Equal(kind, list.Kind);
            Assert.NotEmpty(list.Columns);
            Assert.StartsWith("Edit", list.EditAction);
            Assert.StartsWith("Delete", list.DeleteAction);
        }
    }
}
