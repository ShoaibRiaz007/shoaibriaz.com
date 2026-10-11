using Portfolio.Application.Abstractions;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Controllers;
using Portfolio.Domain.Entities;
using Portfolio.Models;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

public class RichTextTests
{
    [Fact]
    public void Parses_HeadingsParagraphsAndBullets()
    {
        var blocks = RichText.Parse("Intro line one\nline two\n\n## Section\n- first\n- second\n\nAfter.");

        Assert.Collection(blocks,
            b => Assert.Equal("Intro line one line two", Assert.IsType<RichText.Text>(Assert.Single(Assert.IsType<RichText.Paragraph>(b).Content)).Value),
            b => Assert.Equal("Section", Assert.IsType<RichText.Heading>(b).Text),
            b => Assert.Equal(2, Assert.IsType<RichText.BulletList>(b).Items.Count),
            b => Assert.IsType<RichText.Paragraph>(b));
    }

    [Fact]
    public void Parses_BoldAndSafeLinks()
    {
        var inlines = RichText.Inlines("See **this** and [the CV](/cv), [mail](mailto:a@b.c), [site](https://x.dev).");
        Assert.Contains(inlines, i => i is RichText.Bold { Value: "this" });
        Assert.Contains(inlines, i => i is RichText.Link { Href: "/cv" });
        Assert.Contains(inlines, i => i is RichText.Link { Href: "mailto:a@b.c" });
        Assert.Contains(inlines, i => i is RichText.Link { Href: "https://x.dev" });
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public void UnsafeLinks_BecomePlainText(string href)
    {
        var inlines = RichText.Inlines($"[click]({href})");
        Assert.DoesNotContain(inlines, i => i is RichText.Link);
        Assert.Contains(inlines, i => i is RichText.Text { Value: "click" });
    }

    [Fact]
    public void Placeholders_AreFilled_AndUnknownOnesLeftVisible()
        => Assert.Equal("Kept 180 days by {typo}", RichText.Fill("Kept {retention_days} days by {typo}", new Dictionary<string, string> { ["retention_days"] = "180" }));

    [Fact]
    public void TheBuiltInPolicy_UsesOnlyKnownPlaceholders()
    {
        var values = DefaultPages.PrivacyValues(new Bio { Email = "me@x.dev", FullName = "Me" }, 180);
        var filled = RichText.Fill(DefaultPages.PrivacyBody, values);
        Assert.DoesNotMatch(@"\{[a-z_]+\}", filled);
        Assert.Contains("180 days", filled);
        Assert.Equal(values.Keys.OrderBy(k => k), DefaultPages.PrivacyPlaceholders.Select(p => p.Name).OrderBy(k => k));
    }
}

public class SitePageFacadeTests
{
    private readonly FakeRepository<SitePage> _pages = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));
    private SitePageFacade Sut() => new(_pages, _clock);

    [Fact]
    public async Task Save_CreatesThenUpdates_AndStampsTheTime()
    {
        await Sut().SaveAsync(SitePages.Privacy, "  Title ", "Body\r\nline");
        _clock.Now = _clock.Now.AddDays(1);
        await Sut().SaveAsync(SitePages.Privacy, "New", "Changed");

        var page = Assert.Single(_pages.Items);
        Assert.Equal(("New", "Changed"), (page.Title, page.Body));
        Assert.Equal(_clock.Now.UtcDateTime, page.UpdatedAt);
    }

    [Fact]
    public async Task Save_CapsLength_AndNormalisesLineEndings()
    {
        await Sut().SaveAsync(SitePages.Privacy, new string('t', 500), "a\r\nb" + new string('x', SitePageFacade.MaxBodyLength));
        var page = Assert.Single(_pages.Items);
        Assert.Equal(SitePageFacade.MaxTitleLength, page.Title.Length);
        Assert.Equal(SitePageFacade.MaxBodyLength, page.Body.Length);
        Assert.StartsWith("a\nb", page.Body);
    }

    [Fact]
    public async Task Reset_RemovesTheSavedText()
    {
        await Sut().SaveAsync(SitePages.Privacy, "T", "B");
        await Sut().ResetAsync(SitePages.Privacy);
        Assert.Null(await Sut().GetAsync(SitePages.Privacy));
    }
}

public class ProjectInsightsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 15, 0, 0, TimeSpan.Zero);
    private readonly FakeTelemetryStore _store = new();
    private TelemetryFacade Sut() => new(_store, new FakeVisitorHasher(), new FakeClock(Now));

    private void View(string path, string visitor, int daysAgo = 0, string source = "Direct", string? device = "Desktop")
        => _store.PageViews.Add(new PageView { Id = _store.PageViews.Count + 1, OccurredAt = Now.UtcDateTime.AddDays(-daysAgo), Path = path, VisitorHash = visitor, Source = source, Device = device });

    [Fact]
    public async Task ProjectViews_CountPagesAndVisitors_PerSlug()
    {
        View("/project/alpha", "a"); View("/project/alpha", "a"); View("/project/Alpha", "b");
        View("/project/beta", "c"); View("/projects", "d"); View("/project/alpha", "e", daysAgo: 40);

        var views = await Sut().GetProjectViewsAsync(30);

        Assert.Equal(new ProjectViews("alpha", 3, 2), views["alpha"]);
        Assert.Equal(1, views["beta"].Views);
        Assert.False(views.ContainsKey(""));
        Assert.Equal(2, views.Count);
    }

    [Fact]
    public void ProjectRows_RankByViews_AndKeepUnviewedProjects()
    {
        var projects = new[] { new Project { Id = 1, Title = "A", Slug = "a" }, new Project { Id = 2, Title = "B", Slug = "b" }, new Project { Id = 3, Title = "C", Slug = "c" } };
        var rows = ActivityViewModel.ProjectRows(projects, new Dictionary<string, ProjectViews> { ["b"] = new("b", 9, 4), ["c"] = new("c", 2, 2) });

        Assert.Equal(["B", "C", "A"], rows.Select(r => r.Title));
        Assert.Equal(2, rows[0].Position);   // where it sits on the site
        Assert.Equal(0, rows[2].Views);
    }

    [Fact]
    public void ProjectsList_ShowsViewsWhenGiven()
    {
        var views = new Dictionary<string, ProjectViews> { ["a"] = new("a", 1200, 1) };
        Assert.Equal("1,200 · 1 visitor", AdminOverview.ViewsCell(views, "A"));
        Assert.Equal("0", AdminOverview.ViewsCell(views, "missing"));
    }

    [Fact]
    public async Task RecentVisits_ArePaged()
    {
        for (var i = 0; i < 30; i++) View($"/p{i}", $"v{i}");

        var first = await Sut().GetActivityAsync(7);
        var second = await Sut().GetActivityAsync(7, table: new TableQuery(VisitsPage: 2));
        var beyond = await Sut().GetActivityAsync(7, table: new TableQuery(VisitsPage: 99));

        Assert.Equal(TelemetryFacade.TablePageSize, first.RecentVisits.Count);
        Assert.Equal(new TablePage(1, 2), first.VisitsPage);
        Assert.Equal(30 - TelemetryFacade.TablePageSize, second.RecentVisits.Count);
        Assert.Empty(first.RecentVisits.Select(v => v.Id).Intersect(second.RecentVisits.Select(v => v.Id)));
        Assert.Equal(2, beyond.VisitsPage.Number);   // clamped to the last page
    }

    [Fact]
    public async Task Search_FiltersTheTables_ButNotTheTotals()
    {
        View("/project/alpha", "a", source: "LinkedIn"); View("/cv", "b"); View("/", "c", device: "Mobile");
        _store.LoginAttempts.Add(new LoginAttempt { Id = 1, OccurredAt = Now.UtcDateTime, Username = "admin", Succeeded = true, IpAddress = "203.0.113.5" });
        _store.LoginAttempts.Add(new LoginAttempt { Id = 2, OccurredAt = Now.UtcDateTime, Username = "", Succeeded = false, FailureReason = "invalid credentials" });

        var r = await Sut().GetActivityAsync(7, table: new TableQuery(VisitsSearch: " LINKEDIN ", LoginsSearch: "failed"));

        Assert.Equal(3, r.PageViews);
        Assert.Equal("/project/alpha", Assert.Single(r.RecentVisits).Path);
        Assert.Equal(1, r.VisitsMatched);
        Assert.Equal("linkedin", r.VisitsSearch);
        Assert.Equal(2, Assert.Single(r.LoginAttempts).Id);
        Assert.Equal("Mobile", Assert.Single((await Sut().GetActivityAsync(7, table: new TableQuery(VisitsSearch: "mobile"))).RecentVisits).Device);
        Assert.Single((await Sut().GetActivityAsync(7, table: new TableQuery(LoginsSearch: "203.0.113"))).LoginAttempts);
    }
}

public class UploadSkipTests
{
    [Fact]
    public void Summary_SaysWhenFilesWereAlreadyThere()
    {
        Assert.Contains("already", AdminController.UploadSummary(1, 0, [], skipped: 1));
        Assert.DoesNotContain("uploaded", AdminController.UploadSummary(2, 0, [], skipped: 2));
        var mixed = AdminController.UploadSummary(3, 2, [], skipped: 1);
        Assert.Contains("2 files uploaded", mixed);
        Assert.Contains("already", mixed);
    }
}

public class ReviewFollowUpTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 15, 0, 0, TimeSpan.Zero);
    private readonly FakeTelemetryStore _store = new();
    private TelemetryFacade Sut() => new(_store, new FakeVisitorHasher(), new FakeClock(Now));

    private void View(string path, string visitor, string? country = null, string? browser = null, string? campaign = null)
        => _store.PageViews.Add(new PageView { Id = _store.PageViews.Count + 1, OccurredAt = Now.UtcDateTime, Path = path, VisitorHash = visitor,
            Source = "Direct", Country = country, Browser = browser, UtmCampaign = campaign });

    private void Login(int id, bool ok)
        => _store.LoginAttempts.Add(new LoginAttempt { Id = id, OccurredAt = Now.UtcDateTime.AddMinutes(-id), Username = "admin", Succeeded = ok });

    [Fact]
    public async Task ProjectViews_MergeCaseAndSlashVariants_AndCountAVisitorOnce()
    {
        View("/project/foo", "a"); View("/Project/Foo/", "a"); View("/PROJECT/foo", "b");

        var foo = (await Sut().GetProjectViewsAsync(30))["foo"];

        Assert.Equal(new ProjectViews("foo", 3, 2), foo);
    }

    [Fact]
    public async Task SignIns_ArePaged_AndSearchableBySuccess()
    {
        for (var i = 1; i <= 30; i++) Login(i, ok: i % 3 == 0);

        var second = await Sut().GetActivityAsync(7, table: new TableQuery(LoginsPage: 2));
        var success = await Sut().GetActivityAsync(7, table: new TableQuery(LoginsSearch: "success"));

        Assert.Equal(30 - TelemetryFacade.TablePageSize, second.LoginAttempts.Count);
        Assert.Equal(26, second.LoginAttempts[0].Id);
        Assert.Equal(10, success.LoginsMatched);
        Assert.All(success.LoginAttempts, a => Assert.True(a.Succeeded));
    }

    [Theory]
    [InlineData("pk")]
    [InlineData("firefox")]
    [InlineData("launch")]
    public async Task VisitSearch_CoversCountryBrowserAndCampaign(string term)
    {
        View("/", "a", country: "PK", browser: "Firefox", campaign: "launch-week"); View("/cv", "b");
        Assert.Equal("/", Assert.Single((await Sut().GetActivityAsync(7, table: new TableQuery(VisitsSearch: term))).RecentVisits).Path);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task PageNumbersBelowOne_ShowTheFirstPage(int page)
        => Assert.Equal(1, (await Sut().GetActivityAsync(7, table: new TableQuery(VisitsPage: page))).VisitsPage.Number);

    [Fact]
    public void ASavedPolicy_IsShown_AndABlankOneFallsBack()
    {
        Assert.Equal(("Mine", "My text"), DefaultPages.Privacy(new SitePage { Title = "Mine", Body = "My text" }));
        Assert.Equal((DefaultPages.PrivacyTitle, DefaultPages.PrivacyBody), DefaultPages.Privacy(new SitePage { Title = " ", Body = "" }));
        Assert.Equal((DefaultPages.PrivacyTitle, DefaultPages.PrivacyBody), DefaultPages.Privacy(null));
    }

    [Fact]
    public void HeadingsTheAdminWrote_AreKept_EvenWithoutText()
    {
        var blocks = RichText.Parse("# Policy\n## Intro\nText {email}\n\nMore.\n## Contact", new Dictionary<string, string> { ["email"] = "" });
        Assert.Equal(["Policy", "Intro", "Contact"], blocks.OfType<RichText.Heading>().Select(h => h.Text));
        Assert.Equal("More.", Assert.IsType<RichText.Text>(Assert.Single(Assert.Single(blocks.OfType<RichText.Paragraph>()).Content)).Value);
    }

    [Fact]
    public void HashLines_CountAsText_WhenDroppingEmptyValues()
    {
        var blocks = RichText.Parse("## Contact\nEmail {email}\n#hashtag note\n\n#1 way: {email}", new Dictionary<string, string> { ["email"] = "" });
        // "#hashtag" and "#1" aren't headings: the first keeps the section (and its heading), the second is dropped.
        Assert.Equal("Contact", Assert.IsType<RichText.Heading>(Assert.Single(blocks, b => b is RichText.Heading)).Text);
        Assert.Equal("#hashtag note", Assert.IsType<RichText.Text>(Assert.Single(Assert.Single(blocks.OfType<RichText.Paragraph>()).Content)).Value);
    }

    [Fact]
    public void ABlankName_ReadsAsTheSiteOwner()
        => Assert.Equal("the site owner", DefaultPages.PrivacyValues(new Bio { FullName = "" }, 1)["name"]);

    [Fact]
    public void WithNoEmail_TheContactLineAndItsHeadingAreLeftOut()
    {
        var blocks = RichText.Parse(DefaultPages.PrivacyBody, DefaultPages.PrivacyValues(new Bio { Email = "", FullName = "Me" }, 180));

        Assert.DoesNotContain(blocks, b => b is RichText.Heading { Text: "Questions" });
        Assert.DoesNotContain(blocks.OfType<RichText.Paragraph>().SelectMany(p => p.Content), i => i is RichText.Link { Href: "mailto:" });
        Assert.Contains(blocks, b => b is RichText.Heading { Text: "Signing in" });
        Assert.Contains(RichText.Parse(DefaultPages.PrivacyBody, DefaultPages.PrivacyValues(new Bio { Email = "me@x.dev" }, 180)),
            b => b is RichText.Heading { Text: "Questions" });
    }
}

public class PrivacyAdminTests
{
    private sealed class CountingCache : Microsoft.AspNetCore.OutputCaching.IOutputCacheStore
    {
        public List<string> Tags = [];
        public ValueTask EvictByTagAsync(string tag, CancellationToken ct) { Tags.Add(tag); return ValueTask.CompletedTask; }
        public ValueTask<byte[]?> GetAsync(string key, CancellationToken ct) => ValueTask.FromResult<byte[]?>(null);
        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public async Task Saving_StoresTheText_RefreshesTheSite_AndCountsLineBreaksAsStored()
    {
        var pages = new FakeRepository<SitePage>();
        var cache = new CountingCache();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var c = new PrivacyAdminController(new SitePageFacade(pages, TimeProvider.System), null!, cache,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new NullTempDataProvider()),
        };
        // Over the limit as posted (CRLF), under it as stored (LF).
        var body = string.Join("\r\n", Enumerable.Repeat(new string('x', 99), 200));

        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(await c.PrivacyPolicy(new EditPrivacyViewModel { Title = "T", Body = body }, default));

        Assert.Equal(body.Replace("\r\n", "\n"), Assert.Single(pages.Items).Body);
        Assert.Equal(["content"], cache.Tags);
    }
}
