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
}
