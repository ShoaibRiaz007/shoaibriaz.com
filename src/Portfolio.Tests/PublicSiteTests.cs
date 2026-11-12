using Microsoft.AspNetCore.Http;
using Portfolio.Domain.Entities;
using Portfolio.Models;
using Xunit;

namespace Portfolio.Tests;

public class PublicSiteTests
{
    private static Bio Named(string? name) => new() { FullName = name! };

    // The header rendered "M.." for "M. Shoaib Riaz": abbreviated tokens must not become the brand mark.
    [Theory]
    [InlineData("M. Shoaib Riaz", "Shoaib")]
    [InlineData("Shoaib Riaz", "Shoaib")]
    [InlineData("  Dr.  Jane   Doe ", "Jane")]
    [InlineData("Cher", "Cher")]
    [InlineData("M.", "M")]
    [InlineData("", "Portfolio")]
    [InlineData("   ", "Portfolio")]
    [InlineData(null, "Portfolio")]
    public void FirstName_SkipsAbbreviatedTokens(string? name, string expected)
        => Assert.Equal(expected, PublicSite.FirstName(Named(name)));

    [Theory]
    [InlineData("M. Shoaib Riaz", "SR")]
    [InlineData("shoaib riaz", "SR")]
    [InlineData("M. J. Smith", "S")]
    [InlineData("Cher", "C")]
    [InlineData("A. B.", "AB")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Initials_SkipAbbreviatedTokens(string? name, string expected)
        => Assert.Equal(expected, PublicSite.Initials(Named(name)));

    [Fact]
    public void AreaSlug_RoundTripsEveryArea()
    {
        foreach (var area in PublicSite.Areas)
            Assert.Equal(area, PublicSite.AreaFromSlug(PublicSite.AreaSlug(area)));
        Assert.Equal(PublicSite.Areas.Length, PublicSite.Areas.Select(PublicSite.AreaSlug).Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("all")]
    [InlineData("nope")]
    public void AreaFromSlug_UnknownMeansAll(string? slug) => Assert.Null(PublicSite.AreaFromSlug(slug));

    [Fact]
    public void AreaFromSlug_IsCaseInsensitive() => Assert.Equal(PublicSite.Areas[2], PublicSite.AreaFromSlug(" XR "));

    [Fact]
    public void Variant_IsStableAndInRange()
    {
        Assert.Equal(PublicSite.Variant("ai-diagram-generation-powerpoint"), PublicSite.Variant("ai-diagram-generation-powerpoint"));
        var picks = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "", null }.Select(s => PublicSite.Variant(s)).ToList();
        Assert.All(picks, v => Assert.InRange(v, 0, 3));
        Assert.True(picks.Distinct().Count() > 1);
    }

    private static List<Project> Projects(params string[] slugs) => slugs.Select(s => new Project { Slug = s, Title = s }).ToList();

    [Fact]
    public void Neighbours_WrapAtBothEnds()
    {
        var list = Projects("a", "b", "c");
        Assert.Equal(("c", "b"), Slugs(PublicSite.Neighbours(list, "a")));
        Assert.Equal(("a", "c"), Slugs(PublicSite.Neighbours(list, "b")));
        Assert.Equal(("b", "a"), Slugs(PublicSite.Neighbours(list, "c")));
    }

    [Fact]
    public void Neighbours_NoneForSingleOrMissing()
    {
        Assert.Equal((null, null), Slugs(PublicSite.Neighbours(Projects("a"), "a")));
        Assert.Equal((null, null), Slugs(PublicSite.Neighbours(Projects("a", "b"), "zzz")));
    }

    // Production (Linux) emitted <loc>/projects</loc> and canonical "/": there Uri.TryCreate("/projects",
    // UriKind.Absolute) succeeds as file:///projects, so a site path must never be mistaken for a URL.
    [Theory]
    [InlineData("/projects", "https://example.com/projects")]
    [InlineData("/", "https://example.com/")]
    [InlineData("projects", "https://example.com/projects")]
    [InlineData("/uploads/me.png", "https://example.com/uploads/me.png")]
    [InlineData("https://res.cloudinary.com/x/me.png", "https://res.cloudinary.com/x/me.png")]
    [InlineData("//cdn.example.org/me.png", "https://cdn.example.org/me.png")] // protocol-relative: another host
    [InlineData("", "https://example.com")]
    [InlineData(null, "https://example.com")]
    public void Absolute_ResolvesSitePathsAgainstTheOrigin(string? pathOrUrl, string expected)
        => Assert.Equal(expected, PublicSite.Absolute("https://example.com", pathOrUrl));

    [Theory]
    [InlineData("https://shoaibriaz.com/about", "https://shoaibriaz.com")]
    [InlineData("/somewhere", "http://request.test")] // a path is not an origin (file:// on Linux)
    [InlineData("", "http://request.test")]
    public void Origin_UsesOnlyAnHttpWebsite(string website, string expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        request.Host = new HostString("request.test");
        Assert.Equal(expected, PublicSite.Origin(new Bio { Website = website }, request));
    }

    [Theory]
    [InlineData("/css/site-v2.css", true)]
    [InlineData("/world/assets/index-abc.js", true)]
    [InlineData("/uploads/missing", true)]
    [InlineData("/wp-login.php", true)]
    [InlineData("/favicon.png", true)]
    [InlineData("/no-such-page", false)]
    [InlineData("/project/some-slug", false)]
    [InlineData("/worldly", false)]
    public void IsStaticAssetPath_SkipsTheDesigned404ForFiles(string path, bool expected)
        => Assert.Equal(expected, PublicSite.IsStaticAssetPath(new PathString(path)));

    private static (string?, string?) Slugs((Project? Previous, Project? Next) n) => (n.Previous?.Slug, n.Next?.Slug);

    // The home hero leads with the first part and rotates the rest, and the structured data's job title is the
    // first part too: both read the same separators.
    [Theory]
    [InlineData("Team Lead · C# / .NET · AI/LLM", new[] { "Team Lead", "C# / .NET", "AI/LLM" })]
    [InlineData("Team Lead • C# / .NET", new[] { "Team Lead", "C# / .NET" })]
    [InlineData("Team Lead | C# / .NET", new[] { "Team Lead", "C# / .NET" })]
    [InlineData(" Team Lead ", new[] { "Team Lead" })]
    [InlineData("", new string[0])]
    [InlineData(null, new string[0])]
    public void HeadlineParts_SplitsOnEverySeparator(string? headline, string[] expected)
    {
        Assert.Equal(expected, PublicSite.HeadlineParts(headline));
        Assert.Equal(expected.FirstOrDefault(), PublicSite.HeadlineTitle(headline));
    }

    // The home stats strip shows only metrics led by a number, split into the figure and the words after it.
    [Theory]
    [InlineData("70–80% faster level production", "70–80%", "faster level production")]
    [InlineData("70 – 80% faster", "70 – 80%", "faster")]
    [InlineData("70 - 80% faster", "70 - 80%", "faster")]
    [InlineData("70 — 80% faster", "70 — 80%", "faster")]
    [InlineData("70 –80% faster", "70 –80%", "faster")]
    [InlineData("70% faster\nlevel production", "70%", "faster\nlevel production")]
    [InlineData("~70% faster", "~70%", "faster")]
    [InlineData("$2M saved", "$2M", "saved")]
    [InlineData("1,200+ users", "1,200+", "users")]
    [InlineData("  3x faster", "3x", "faster")]
    [InlineData("12", "12", "")]
    public void ProofFigure_SplitsTheLeadingFigure(string metric, string figure, string label)
        => Assert.Equal((figure, label), PublicSite.ProofFigure(metric));

    [Theory]
    [InlineData("Claude-powered diagrams inside PowerPoint")]
    [InlineData("JSON-RPC 2.0 over named pipes")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(null)]
    public void ProofFigure_NullWhenNotLedByANumber(string? metric) => Assert.Null(PublicSite.ProofFigure(metric));

    [Fact]
    public void Describe_LeadsWithFullNameAndIncludesLocation()
    {
        var bio = new Bio
        {
            FullName = "Shoaib Riaz",
            Tagline = "I build AI-powered .NET software.",
            Location = "Lahore, Punjab, Pakistan"
        };
        var desc = PublicSite.Describe(bio);
        Assert.StartsWith("Shoaib Riaz — ", desc);
        Assert.EndsWith("Based in Lahore, Pakistan.", desc);
        Assert.InRange(desc.Length, 1, PublicSite.DescriptionMax);
    }

    [Fact]
    public void Describe_LongTaglineTruncatesCleanlyWithinMax()
    {
        var bio = new Bio
        {
            FullName = "Shoaib Riaz",
            Tagline = "I build AI-powered .NET software, from backend APIs to desktop add-ins and real-time Unity clients, and lead the team that ships it across the world.",
            Location = "Lahore, Punjab, Pakistan"
        };
        var desc = PublicSite.Describe(bio);
        Assert.StartsWith("Shoaib Riaz — ", desc);
        Assert.EndsWith("Based in Lahore, Pakistan.", desc);
        Assert.True(desc.Length <= PublicSite.DescriptionMax);
        Assert.Contains("…", desc);
    }

    [Fact]
    public void Describe_HandlesMissingNameOrLocationOrTagline()
    {
        Assert.Equal("Portfolio.", PublicSite.Describe(new Bio()));
        Assert.Equal("Portfolio. Based in Lahore, Pakistan.", PublicSite.Describe(new Bio { Location = "Lahore, Pakistan" }));
        Assert.Equal("Jane Doe — Portfolio.", PublicSite.Describe(new Bio { FullName = "Jane Doe" }));
    }

    [Fact]
    public void StructuredData_IncludesDisambiguatingDescription()
    {
        var bio = new Bio
        {
            FullName = "Shoaib Riaz",
            Headline = "Team Lead · .NET",
            Location = "Lahore, Pakistan"
        };
        var facts = new PersonFacts("Team Lead", "Lucen Software", new List<string>(), new List<string>());
        var json = PublicSite.StructuredData(bio, "https://shoaibriaz.com", "https://shoaibriaz.com", null, facts, true);
        Assert.Contains("disambiguatingDescription", json);
        Assert.Contains("Team Lead at Lucen Software, based in Lahore, Pakistan", json);
    }

    [Fact]
    public void SeedContent_Projects_HaveRichCaseStudyContent()
    {
        var projects = Portfolio.Infrastructure.Persistence.SeedContent.Projects();
        Assert.NotEmpty(projects);
        foreach (var p in projects)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Title), $"Project {p.Slug} missing Title");
            Assert.False(string.IsNullOrWhiteSpace(p.Summary), $"Project {p.Slug} missing Summary");
            Assert.False(string.IsNullOrWhiteSpace(p.Description), $"Project {p.Slug} missing Description");
            Assert.False(string.IsNullOrWhiteSpace(p.TechStack), $"Project {p.Slug} missing TechStack");
            Assert.False(string.IsNullOrWhiteSpace(p.Challenge), $"Project {p.Slug} missing Challenge");
            Assert.False(string.IsNullOrWhiteSpace(p.Solution), $"Project {p.Slug} missing Solution");
            Assert.False(string.IsNullOrWhiteSpace(p.Outcome), $"Project {p.Slug} missing Outcome");
            Assert.False(string.IsNullOrWhiteSpace(p.Highlights), $"Project {p.Slug} missing Highlights");
            Assert.True(p.Summary.Length <= 250, $"Project {p.Slug} summary too long: {p.Summary.Length}");
        }
    }
}

public class NotFoundRouteTests
{
    // Re-execution keeps the request's method, so a GET-only target turned a POST to a missing URL into a 405.
    [Fact]
    public void NotFoundPage_AcceptsAnyMethod()
    {
        var action = typeof(Portfolio.Controllers.HomeController).GetMethod(nameof(Portfolio.Controllers.HomeController.NotFoundPage))!;
        Assert.Empty(action.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true));
        Assert.Contains(action.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), true),
            a => ((Microsoft.AspNetCore.Mvc.RouteAttribute)a).Template == "/not-found");
    }

    [Theory]
    [InlineData(nameof(Portfolio.Controllers.HomeController.Sitemap), "/sitemap.xml")]
    [InlineData(nameof(Portfolio.Controllers.HomeController.Robots), "/robots.txt")]
    public void SeoEndpoints_AcceptBothGetAndHead(string actionName, string expectedRoute)
    {
        var action = typeof(Portfolio.Controllers.HomeController).GetMethod(actionName)!;
        var getAttr = Assert.Single(action.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.HttpGetAttribute>());
        var headAttr = Assert.Single(action.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpHeadAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.HttpHeadAttribute>());
        Assert.Equal(expectedRoute, getAttr.Template);
        Assert.Equal(expectedRoute, headAttr.Template);
    }

    [Fact]
    public void SeedContent_Bio_UsesWwwCanonicalOrigin()
        => Assert.Equal("https://www.shoaibriaz.com", Portfolio.Infrastructure.Persistence.SeedContent.Bio().Website);

    [Theory]
    [InlineData("?v=abc123", true)]
    [InlineData("?v=", false)]
    [InlineData("", false)]
    public void StaticAssetCache_AppliesImmutableCachingOnlyWhenVersioned(string query, bool expectedHeader)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);

        Portfolio.Hosting.StaticAssetCache.Apply(context);

        if (expectedHeader)
        {
            Assert.Equal(Portfolio.Hosting.StaticAssetCache.ImmutableHeaderValue, context.Response.Headers.CacheControl.ToString());
        }
        else
        {
            Assert.True(string.IsNullOrEmpty(context.Response.Headers.CacheControl));
        }
    }
}
