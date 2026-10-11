using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Application.Facades;
using Portfolio.Controllers;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Security;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

/// <summary>The gallery actions on a project: cover, order, captions, and what removing the cover does.</summary>
public class ProjectMediaTests
{
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<ProjectMedia> _media = new();
    private readonly FakeFileStorage _files = new();
    private readonly AdminContentFacade _sut;

    public ProjectMediaTests()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "P", Slug = "p", ImageUrl = "/uploads/a.jpg" });
        _projects.Items.Add(new Project { Id = 2, Title = "Other", Slug = "other" });
        _media.Items.AddRange(
        [
            new ProjectMedia { Id = 10, ProjectId = 1, MediaType = "image", Url = "/uploads/a.jpg", SortOrder = 1 },
            new ProjectMedia { Id = 11, ProjectId = 1, MediaType = "video", Url = "/uploads/v.mp4", SortOrder = 2 },
            new ProjectMedia { Id = 12, ProjectId = 1, MediaType = "image", Url = "/uploads/b.jpg", SortOrder = 3 },
            new ProjectMedia { Id = 20, ProjectId = 2, MediaType = "image", Url = "/uploads/c.jpg", SortOrder = 1 },
        ]);
        _sut = new AdminContentFacade(
            new FakeRepository<Bio>(), new FakeRepository<Service>(), new FakeRepository<Experience>(), _projects,
            new FakeRepository<Skill>(), new FakeRepository<Testimonial>(), new FakeRepository<Education>(),
            new FakeRepository<Certification>(), _media, new FakeRepository<AdminUser>(),
            new PasswordHasher(), _files, new FakeUnitOfWork());
    }

    private Project P1 => _projects.Items.Single(p => p.Id == 1);
    private int[] Order(int projectId) => _media.Items.Where(m => m.ProjectId == projectId).OrderBy(m => m.SortOrder).Select(m => m.Id).ToArray();

    [Fact]
    public async Task SetCover_UsesTheImage()
    {
        Assert.True((await _sut.SetCoverAsync(1, 12)).Success);
        Assert.Equal("/uploads/b.jpg", P1.ImageUrl);
    }

    [Theory]
    [InlineData(11)]   // a video can't be a cover
    [InlineData(20)]   // another project's image
    [InlineData(99)]   // gone
    public async Task SetCover_RefusesWhatIsntAnImageOfThisProject(int mediaId)
    {
        Assert.False((await _sut.SetCoverAsync(1, mediaId)).Success);
        Assert.Equal("/uploads/a.jpg", P1.ImageUrl);
    }

    [Fact]
    public async Task Move_ShiftsOnePlace_AndRenumbers()
    {
        Assert.True(await _sut.MoveMediaAsync(1, 12, -1));
        Assert.Equal([10, 12, 11], Order(1));
        Assert.Equal([1, 2, 3], _media.Items.Where(m => m.ProjectId == 1).OrderBy(m => m.SortOrder).Select(m => m.SortOrder));
    }

    [Fact]
    public async Task Move_PastTheEnds_StaysPut_AndOtherProjectsAreUntouched()
    {
        Assert.True(await _sut.MoveMediaAsync(1, 10, -1));
        Assert.True(await _sut.MoveMediaAsync(1, 12, 1));
        Assert.Equal([10, 11, 12], Order(1));
        Assert.False(await _sut.MoveMediaAsync(1, 20, -1));   // belongs to project 2
        Assert.Equal([20], Order(2));
    }

    [Fact]
    public async Task Caption_IsTrimmedAndCapped_AndOnlyForThisProject()
    {
        Assert.True(await _sut.UpdateMediaCaptionAsync(1, 11, "  Gameplay demo  "));
        Assert.Equal("Gameplay demo", _media.Items.Single(m => m.Id == 11).Caption);

        Assert.True(await _sut.UpdateMediaCaptionAsync(1, 11, new string('x', 600)));
        Assert.Equal(AdminContentFacade.MaxCaptionLength, _media.Items.Single(m => m.Id == 11).Caption.Length);

        Assert.False(await _sut.UpdateMediaCaptionAsync(1, 20, "nope"));
        Assert.Equal("", _media.Items.Single(m => m.Id == 20).Caption);
    }

    [Fact]
    public async Task RemovingTheCoverInTheMiddle_MakesTheImageAfterItTheCover()
    {
        _media.Items.Add(new ProjectMedia { Id = 13, ProjectId = 1, MediaType = "image", Url = "/uploads/d.jpg", SortOrder = 4 });
        await _sut.SetCoverAsync(1, 12);

        await _sut.DeleteMediaAsync(12);

        Assert.Equal("/uploads/d.jpg", P1.ImageUrl);   // the one after it, not the first image
    }

    [Fact]
    public async Task RemovingTheLastImage_WhenItIsTheCover_UsesTheOneBefore()
    {
        await _sut.SetCoverAsync(1, 12);
        await _sut.DeleteMediaAsync(12);
        Assert.Equal("/uploads/a.jpg", P1.ImageUrl);
    }

    [Fact]
    public async Task RemovingAnImage_NeverTouchesACoverThatIsAnotherImage()
    {
        await _sut.SetCoverAsync(1, 12);   // the cover is not the first image
        await _sut.DeleteMediaAsync(10);
        Assert.Equal("/uploads/b.jpg", P1.ImageUrl);
    }

    [Fact]
    public async Task RemovingTheCover_MatchesItByFile_NotExactText()
    {
        P1.ImageUrl = "/uploads/A.jpg?v=2";   // the same file, linked by hand
        await _sut.DeleteMediaAsync(10);
        Assert.Equal("/uploads/b.jpg", P1.ImageUrl);
    }

    [Fact]
    public async Task SetCover_OnAnImageRemovedMeanwhile_PutsThePreviousCoverBack()
    {
        _media.AfterGet = () => { _media.Items.RemoveAll(m => m.Id == 12); _media.AfterGet = null; };

        var result = await _sut.SetCoverAsync(1, 12);

        Assert.False(result.Success);
        Assert.Equal("/uploads/a.jpg", P1.ImageUrl);
    }

    [Fact]
    public async Task Caption_CutAtTheLimit_KeepsWholeCharacters()
    {
        await _sut.UpdateMediaCaptionAsync(1, 11, new string('x', AdminContentFacade.MaxCaptionLength - 1) + "😀");
        var caption = _media.Items.Single(m => m.Id == 11).Caption;
        Assert.False(char.IsHighSurrogate(caption[^1]));
    }

    [Fact]
    public async Task UploadingAFileAlreadyInTheGallery_AddsNothing()
    {
        Assert.False((await _sut.UploadMediaAsync(2, new MemoryStream([.. Png, 7]), "x.png", "")).Skipped);
        Assert.True((await _sut.UploadMediaAsync(2, new MemoryStream([.. Png, 7]), "x.png", "")).Skipped);
        Assert.Equal(2, _media.Items.Count(m => m.ProjectId == 2));   // c.jpg plus one copy
        Assert.Single(_files.Saved);
    }

    [Fact]
    public async Task ASaveThatDidntTouchTheCover_KeepsTheStoredOne()
    {
        var staleForm = new Project { Id = 1, Title = "Renamed", Slug = "p", ImageUrl = "/uploads/a.jpg" };
        await _sut.SetCoverAsync(1, 12);   // changed in the gallery after the form loaded

        await _sut.SaveProjectAsync(staleForm, keepStoredCover: true);

        Assert.Equal("Renamed", P1.Title);
        Assert.Equal("/uploads/b.jpg", P1.ImageUrl);
    }

    [Fact]
    public async Task RemovingTheCover_MakesTheNextImageTheCover_AndDeletesTheFile()
    {
        await _sut.DeleteMediaAsync(10);

        Assert.Equal("/uploads/b.jpg", P1.ImageUrl);
        Assert.Contains("/uploads/a.jpg", _files.Deleted);
    }

    [Fact]
    public async Task RemovingTheLastImage_ClearsTheCover()
    {
        await _sut.DeleteMediaAsync(10);
        await _sut.DeleteMediaAsync(12);

        Assert.Equal("", P1.ImageUrl);
    }

    [Fact]
    public async Task RemovingAnotherImage_KeepsTheCover()
    {
        await _sut.DeleteMediaAsync(12);
        Assert.Equal("/uploads/a.jpg", P1.ImageUrl);
    }

    [Fact]
    public void UploadSummary_CountsAndNamesEachProblem()
    {
        Assert.Contains("3", AdminController.UploadSummary(3, 3, []));
        Assert.DoesNotContain("files", AdminController.UploadSummary(1, 1, []));   // singular
        Assert.DoesNotContain("0 of", AdminController.UploadSummary(1, 0, ["a.exe: no"]));
        var partial = AdminController.UploadSummary(3, 2, ["notes.txt: not allowed", "big.mov: too large"]);
        Assert.Contains("2 of 3", partial);
        Assert.Contains("notes.txt", partial);
        Assert.Contains("big.mov", partial);
        Assert.NotEqual(AdminController.UploadSummary(1, 0, ["a.exe: no"]), AdminController.UploadSummary(2, 1, ["a.exe: no"]));
    }

    // ---- Controller ----

    private sealed class CountingCache : IOutputCacheStore
    {
        public int Evictions;
        public ValueTask EvictByTagAsync(string tag, CancellationToken ct) { Evictions++; return ValueTask.CompletedTask; }
        public ValueTask<byte[]?> GetAsync(string key, CancellationToken ct) => ValueTask.FromResult<byte[]?>(null);
        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private readonly CountingCache _cache = new();

    private AdminController Controller()
    {
        var http = new DefaultHttpContext();
        return new AdminController(_sut, _cache, new Portfolio.Caching.NotFoundCache())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NullTempDataProvider()),
        };
    }

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };

    private static IFormFile File(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", name);

    [Fact]
    public async Task CreatingAProject_GoesToItsMediaStep()
    {
        var result = Assert.IsType<RedirectToActionResult>(await Controller().EditProject(new Project { Title = "New one" }, null, CancellationToken.None));

        Assert.Equal(nameof(AdminController.EditProject), result.ActionName);
        Assert.Equal("media", result.Fragment);
        var id = Assert.IsType<int>(result.RouteValues!["id"]);
        Assert.Equal(_projects.Items.Single(p => p.Title == "New one").Id, id);
        Assert.NotEqual(0, id);
    }

    [Fact]
    public async Task SavingWithAnUntouchedCoverLink_KeepsTheGallerysCover()
    {
        await _sut.SetCoverAsync(1, 12);
        await Controller().EditProject(new Project { Id = 1, Title = "P", Slug = "p", ImageUrl = "/uploads/a.jpg" }, "/uploads/a.jpg", CancellationToken.None);
        Assert.Equal("/uploads/b.jpg", P1.ImageUrl);

        // A link the admin actually changed is saved.
        await Controller().EditProject(new Project { Id = 1, Title = "P", Slug = "p", ImageUrl = "https://cdn.example/x.png" }, "/uploads/b.jpg", CancellationToken.None);
        Assert.Equal("https://cdn.example/x.png", P1.ImageUrl);
    }

    [Fact]
    public async Task Move_IsOneStepWhateverIsPosted()
    {
        await Controller().MoveMedia(1, 10, 50, CancellationToken.None);
        Assert.Equal([11, 10, 12], Order(1));
    }

    [Fact]
    public async Task UploadingSeveralFiles_SavesTheGoodOnes_ReportsTheBad_AndRefreshesTheSite()
    {
        var files = new List<IFormFile>
        {
            File("one.png", [.. Png, 1]),
            File("notes.txt", "just text"u8.ToArray()),
            File("two.png", [.. Png, 2]),
        };

        await Controller().UploadMedia(2, files, CancellationToken.None);

        Assert.Equal(3, _media.Items.Count(m => m.ProjectId == 2));   // c.jpg plus the two images
        Assert.Equal(1, _cache.Evictions);
    }

    [Fact]
    public async Task AFileThatFailsToSave_DoesntStopTheOthers_AndTheSiteIsRefreshed()
    {
        _files.OnSave = name => { if (name == "two.png") throw new IOException("disk"); };

        await Controller().UploadMedia(2, [File("one.png", [.. Png, 1]), File("two.png", [.. Png, 2]), File("three.png", [.. Png, 3])], CancellationToken.None);

        Assert.Equal(3, _media.Items.Count(m => m.ProjectId == 2));   // c.jpg, one, three
        Assert.Equal(1, _cache.Evictions);
    }

    [Fact]
    public async Task ReUploadingAGalleryImage_MakesItTheCover_WhenThereIsNone_AndRefreshesTheSite()
    {
        await _sut.UploadMediaAsync(2, new MemoryStream([.. Png, 7]), "x.png", "");
        var copy = _media.Items.Last(m => m.ProjectId == 2);
        _projects.Items.Single(p => p.Id == 2).ImageUrl = null;

        await Controller().UploadMedia(2, [File("x.png", [.. Png, 7])], CancellationToken.None);

        Assert.Equal(copy.Url, _projects.Items.Single(p => p.Id == 2).ImageUrl);
        Assert.Equal(1, _cache.Evictions);
    }

    [Fact]
    public async Task ReUploadingAGalleryImage_LeavesAnExistingCover()
    {
        await _sut.UploadMediaAsync(1, new MemoryStream([.. Png, 8]), "y.png", "");
        P1.ImageUrl = "/uploads/a.jpg";
        Assert.True((await _sut.UploadMediaAsync(1, new MemoryStream([.. Png, 8]), "y.png", "")).Skipped);
        Assert.Equal("/uploads/a.jpg", P1.ImageUrl);
    }

    [Fact]
    public async Task UploadingOnlyBadFiles_DoesntRefreshTheSite()
    {
        await Controller().UploadMedia(2, [File("notes.txt", "just text"u8.ToArray())], CancellationToken.None);
        Assert.Equal(0, _cache.Evictions);
    }
}
