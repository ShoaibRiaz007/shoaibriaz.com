using System.Net;
using System.Text;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Caching;
using Portfolio.Controllers;
using Portfolio.Domain.Entities;
using Portfolio.Hosting;
using Portfolio.Infrastructure.Security;
using Portfolio.Infrastructure.Storage;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

/// <summary>Concurrent edits, file identity and cache freshness on the admin write paths.</summary>
public class WritePathHardeningTests
{
    private readonly FakeRepository<Bio> _bios = new();
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<ProjectMedia> _media = new();
    private readonly FakeFileStorage _files = new();

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };

    private AdminContentFacade Sut() => new(
        _bios, new FakeRepository<Service>(), new FakeRepository<Experience>(), _projects, new FakeRepository<Skill>(),
        new FakeRepository<Testimonial>(), new FakeRepository<Education>(), new FakeRepository<Certification>(),
        _media, new FakeRepository<AdminUser>(), new PasswordHasher(), _files, new FakeUnitOfWork());

    private AdminController Controller(NotFoundCache? notFound = null)
    {
        var http = new DefaultHttpContext();
        return new AdminController(Sut(), new NullOutputCacheStore(), notFound ?? new NotFoundCache())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NullTempDataProvider()),
        };
    }

    // ---- Project cover ----

    [Fact]
    public async Task FirstImage_DoesNotReplaceACoverSetDuringTheUpload()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "P", Slug = "p" });
        _files.OnSave = _ => _projects.Items[0].ImageUrl = "https://example.com/chosen.png"; // set by hand mid-upload

        Assert.True((await Sut().UploadMediaAsync(1, new MemoryStream(Png), "a.png", "")).Success);

        Assert.Equal("https://example.com/chosen.png", _projects.Items.Single().ImageUrl);
    }

    [Fact]
    public async Task FirstImage_FillsABlankCover()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "P", Slug = "p", ImageUrl = "  " });

        await Sut().UploadMediaAsync(1, new MemoryStream(Png), "a.png", "");

        Assert.Equal(_files.Saved.Single(), _projects.Items.Single().ImageUrl);
    }

    // ---- Bio uploads ----

    [Fact]
    public async Task BioUpload_WhenTheProfileRowVanishesMidUpload_FailsAndDropsTheNewFile()
    {
        _bios.Items.Add(new Bio { Id = 1, FullName = "Me", ProfileImageUrl = "/uploads/old.png" });
        _bios.AfterList = () => _bios.Items.Clear(); // deleted between the upload's read and its write

        var result = await Sut().UploadProfileImageAsync(new MemoryStream(Png), "me.png");

        Assert.False(result.Success);
        Assert.Equal(_files.Saved, _files.Deleted); // the new file is removed; the old one is left alone
    }

    [Fact]
    public async Task BioUpload_ReportsAFailedCleanupOfTheReplacedFile()
    {
        _bios.Items.Add(new Bio { Id = 1, FullName = "Me", ProfileImageUrl = "/uploads/old.png" });
        _files.OnDelete = _ => throw new InvalidOperationException("storage offline");

        var result = await Sut().UploadProfileImageAsync(new MemoryStream(Png), "me.png");

        Assert.True(result.Success);
        Assert.True(result.CleanupFailed);
    }

    [Fact]
    public async Task EditBio_FormRenderedBeforeAnUpload_KeepsTheUploadedPhoto()
    {
        // The form showed old.png; an upload in another tab then stored new.png.
        _bios.Items.Add(new Bio { Id = 1, FullName = "Me", ProfileImageUrl = "/uploads/new.png" });
        var posted = new Bio { Id = 1, FullName = "Me again", ProfileImageUrl = "/uploads/old.png" };

        await Controller().EditBio(posted, originalProfileImageUrl: "/uploads/old.png", originalResumeUrl: "", default);

        var stored = _bios.Items.Single();
        Assert.Equal(("Me again", "/uploads/new.png"), (stored.FullName, stored.ProfileImageUrl));
    }

    // ---- File identity ----

    [Fact]
    public async Task DeletingMedia_KeepsAFileTheBioLinksToWithDifferentCase()
    {
        _projects.Items.Add(new Project { Id = 1, Title = "P", Slug = "p" });
        _media.Items.Add(new ProjectMedia { Id = 7, ProjectId = 1, MediaType = "image", Url = "/uploads/abc123.png" });
        _bios.Items.Add(new Bio { Id = 1, FullName = "Me", ProfileImageUrl = "/uploads/ABC123.png" });

        await Sut().DeleteMediaAsync(7);

        Assert.Empty(_files.Deleted);
    }

    [Fact]
    public void Cloudinary_TransformationWithoutVersion_IsTheSameFile()
    {
        var cloud = new CloudinaryFileStorage(new Cloudinary(new Account("mycloud", "key", "secret")));
        Assert.Equal(
            cloud.FileKey("https://res.cloudinary.com/mycloud/image/upload/v123/portfolio/abc.jpg"),
            cloud.FileKey("https://res.cloudinary.com/mycloud/image/upload/w_300,c_fill/portfolio/abc.jpg"));
    }

    [Fact]
    public async Task LocalStorage_DeletesAFileReferencedWithAQueryString()
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "x.png");
            await File.WriteAllBytesAsync(file, Png);
            var storage = new LocalFileStorage(new FileStorageOptions(root, "/uploads"));

            await storage.DeleteAsync("/uploads/x.png?v=2");

            Assert.False(File.Exists(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- Stable order for equal positions ----

    [Fact]
    public async Task EqualPositions_ListInIdOrder_OnTheSiteAndInTheAdmin()
    {
        _projects.Items.Add(new Project { Id = 5, Title = "B", Slug = "b", SortOrder = 1 });
        _projects.Items.Add(new Project { Id = 3, Title = "A", Slug = "a", SortOrder = 1 });
        var site = new PublicContentFacade(
            _bios, new FakeRepository<Service>(), new FakeRepository<Experience>(), _projects, new FakeRepository<Skill>(),
            new FakeRepository<Testimonial>(), new FakeRepository<Education>(), new FakeRepository<Certification>(), _media);

        Assert.Equal(new[] { 3, 5 }, (await site.GetProjectsAsync()).Projects.Select(p => p.Id));
        Assert.Equal(new[] { 3, 5 }, (await Sut().GetDashboardAsync()).Projects.Select(p => p.Id));
    }

    [Fact]
    public async Task Reorder_RowsLeftOut_KeepIdOrderForEqualPositions()
    {
        _projects.Items.Add(new Project { Id = 9, Title = "C", Slug = "c", SortOrder = 2 });
        _projects.Items.Add(new Project { Id = 5, Title = "B", Slug = "b", SortOrder = 1 });
        _projects.Items.Add(new Project { Id = 3, Title = "A", Slug = "a", SortOrder = 1 });

        await Sut().ReorderAsync(ContentKind.Projects, new[] { 9 });

        Assert.Equal(new[] { 9, 3, 5 }, _projects.Items.OrderBy(p => p.SortOrder).Select(p => p.Id));
    }
}

/// <summary>The 404 page's shared data: loaded once per window, never stale after an admin change.</summary>
public class NotFoundCacheTests
{
    private static NotFoundData Data(string name) => new(new Bio { FullName = name }, Array.Empty<Project>());

    [Fact]
    public async Task ConcurrentMisses_LoadOnce()
    {
        var cache = new NotFoundCache();
        var loads = 0;
        var gate = new TaskCompletionSource();
        async Task<NotFoundData> Load(CancellationToken _) { Interlocked.Increment(ref loads); await gate.Task; return Data("a"); }

        var requests = Enumerable.Range(0, 10).Select(_ => cache.GetAsync(Load, default)).ToList();
        gate.SetResult();
        await Task.WhenAll(requests);

        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Invalidate_ForcesAFreshLoad()
    {
        var cache = new NotFoundCache();
        Assert.Equal("old", (await cache.GetAsync(_ => Task.FromResult(Data("old")), default)).Bio.FullName);

        cache.Invalidate();

        Assert.Equal("new", (await cache.GetAsync(_ => Task.FromResult(Data("new")), default)).Bio.FullName);
    }

    [Fact]
    public async Task ALoadThatStartedBeforeAnInvalidation_IsNotCached()
    {
        var cache = new NotFoundCache();
        await cache.GetAsync(_ => { cache.Invalidate(); return Task.FromResult(Data("stale")); }, default);

        Assert.Equal("fresh", (await cache.GetAsync(_ => Task.FromResult(Data("fresh")), default)).Bio.FullName);
    }

    [Fact]
    public async Task AdminContentChange_InvalidatesIt()
    {
        var cache = new NotFoundCache();
        await cache.GetAsync(_ => Task.FromResult(Data("before")), default);
        var projects = new FakeRepository<Project>();
        projects.Items.Add(new Project { Id = 1, Title = "P", Slug = "p" });
        var facade = new AdminContentFacade(
            new FakeRepository<Bio>(), new FakeRepository<Service>(), new FakeRepository<Experience>(), projects,
            new FakeRepository<Skill>(), new FakeRepository<Testimonial>(), new FakeRepository<Education>(),
            new FakeRepository<Certification>(), new FakeRepository<ProjectMedia>(), new FakeRepository<AdminUser>(),
            new PasswordHasher(), new FakeFileStorage(), new FakeUnitOfWork());
        var http = new DefaultHttpContext();
        var controller = new AdminController(facade, new NullOutputCacheStore(), cache)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NullTempDataProvider()),
        };

        await controller.SetPublished(ContentKind.Projects, 1, false, default);

        Assert.Equal("after", (await cache.GetAsync(_ => Task.FromResult(Data("after")), default)).Bio.FullName);
    }
}

/// <summary>The public 404 pipeline, end to end on a test server: only real misses become the 404 page.</summary>
public class PublicNotFoundPagesTests
{
    private static async Task<HttpClient> ClientAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UsePublicNotFoundPages();
        app.UseRouting(); // after the status-code pages, as in Program.cs, so a re-executed request is routed
        app.Map("/not-found", (HttpContext ctx) => { ctx.Response.StatusCode = 404; return "designed 404"; });
        app.MapGet("/limited", () => Results.StatusCode(StatusCodes.Status429TooManyRequests));
        app.MapPost("/form", () => Results.BadRequest());
        app.MapGet("/api/missing", () => Results.NotFound());
        await app.StartAsync();
        return app.GetTestClient();
    }

    [Fact]
    public async Task AMissingPage_GetsTheDesigned404()
    {
        var res = await (await ClientAsync()).GetAsync("/no-such-page");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("designed 404", await res.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("GET", "/limited", HttpStatusCode.TooManyRequests)]
    [InlineData("POST", "/form", HttpStatusCode.BadRequest)]
    public async Task OtherErrors_KeepTheirStatusAndAreNotRenderedAs404(string method, string path, HttpStatusCode expected)
    {
        var res = await (await ClientAsync()).SendAsync(new HttpRequestMessage(new System.Net.Http.HttpMethod(method), path));
        Assert.Equal(expected, res.StatusCode);
        Assert.NotEqual("designed 404", await res.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/css/missing.css")]
    [InlineData("/world/missing.js")]
    [InlineData("/api/missing")]
    public async Task StaticFilesAndTheApi_GetAPlain404(string path)
    {
        var res = await (await ClientAsync()).GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.NotEqual("designed 404", await res.Content.ReadAsStringAsync());
    }
}
