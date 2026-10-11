using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Caching;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Services;
using Portfolio.Hosting;
using Portfolio.Models;

namespace Portfolio.Controllers;

public class HomeController : Controller
{
    private readonly IPublicContentFacade _content;
    private readonly NotFoundCache _notFound;
    private readonly IConfiguration _config;
    private readonly ISitePageFacade _pages;

    public HomeController(IPublicContentFacade content, NotFoundCache notFound, IConfiguration config, ISitePageFacade pages)
    {
        _content = content; _notFound = notFound; _config = config; _pages = pages;
    }

    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var data = await _content.GetHomeAsync(6, ct);
        var name = data.Bio.FullName;
        return View(new HomeViewModel
        {
            Bio = data.Bio,
            Services = data.Services.ToList(),
            ExperienceGroups = ExperienceGrouping.GroupConsecutive(data.Experiences),
            Projects = data.ProminentProjects.ToList(),
            Skills = data.Skills.ToList(),
            Testimonials = data.Testimonials.ToList(),
            Educations = data.Educations.ToList(),
            Certifications = data.Certifications.ToList(),
            TotalProjects = data.TotalProjects,
            Person = PersonFacts.From(data.Experiences, data.Skills, data.Educations),
            Meta = new PageMeta(
                string.IsNullOrWhiteSpace(data.Bio.Headline) ? name : $"{name} — {data.Bio.Headline}",
                PublicSite.Describe(data.Bio),
                "/", Type: "profile"),
        });
    }

    [HttpGet("/projects")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Projects(string? area, CancellationToken ct)
    {
        var data = await _content.GetProjectsAsync(ct);
        return View(new ProjectsViewModel
        {
            Projects = data.Projects.ToList(),
            Categories = data.Categories.ToList(),
            Area = PublicSite.AreaFromSlug(area),
            Bio = data.Bio,
            Meta = new PageMeta(
                $"Work — {data.Bio.FullName}",
                PublicSite.Truncate($"Selected work by {data.Bio.FullName}"
                    + (PublicSite.Place(data.Bio.Location) is { } place ? $" ({place})" : "")
                    + ": AI/LLM features, .NET backends and desktop add-ins, and Unity/XR products.", PublicSite.DescriptionMax),
                "/projects"),
        });
    }

    [HttpGet("/project/{slug}")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Project(string slug, CancellationToken ct)
    {
        var detail = await _content.GetProjectDetailAsync(slug, ct);
        if (detail is null) return await NotFoundPageAsync(ct);

        var p = detail.Project;
        // Prev/next follow the same admin order as the projects page.
        var (previous, next) = PublicSite.Neighbours(detail.Projects, p.Slug);
        // The share preview uses the same cover as the page and the project card (none: the profile photo).
        var cover = string.IsNullOrWhiteSpace(p.ImageUrl) ? null : p.ImageUrl;
        return View(new ProjectDetailViewModel
        {
            Project = p,
            Media = detail.Media.ToList(),
            More = detail.More.ToList(),
            Previous = previous,
            Next = next,
            Bio = detail.Bio,
            Meta = new PageMeta(
                $"{p.Title} — {detail.Bio.FullName}",
                PublicSite.Truncate(string.IsNullOrWhiteSpace(p.Summary) ? p.Description : p.Summary, 160),
                $"/project/{p.Slug}", cover, "article"),
        });
    }

    /// <summary>"/projects/{slug}" is an easy guess for a case study; send it to the canonical URL.</summary>
    [HttpGet("/projects/{slug}")]
    public IActionResult ProjectAlias(string slug) => RedirectPermanent($"/project/{Uri.EscapeDataString(slug)}");

    [HttpGet("/cv")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Cv(CancellationToken ct)
    {
        var site = await _content.GetSiteAsync(ct);
        return View(new CvViewModel
        {
            Site = site,
            ExperienceGroups = ExperienceGrouping.GroupConsecutive(site.Experiences),
            Person = PersonFacts.From(site.Experiences, site.Skills, site.Educations),
            Meta = new PageMeta($"CV — {site.Bio.FullName}", PublicSite.Describe(site.Bio), "/cv"),
        });
    }

    [HttpGet("/privacy")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Privacy(CancellationToken ct)
    {
        var bio = await _content.GetBioAsync(ct);
        var saved = await _pages.GetAsync(SitePages.Privacy, ct);
        var values = DefaultPages.PrivacyValues(bio, TelemetryRetentionService.RetentionDays(_config));
        var (title, body) = DefaultPages.Privacy(saved);
        return View(new PrivacyViewModel
        {
            Bio = bio,
            Title = title,
            Blocks = RichText.Parse(body, values),
            UpdatedAt = saved?.UpdatedAt,
            Meta = new PageMeta($"Privacy — {bio.FullName}",
                $"The privacy policy for {bio.FullName}'s site: what it records about visits and how long it keeps it.",
                "/privacy"),
        });
    }

    /// <summary>Target of the status-code re-execute for unmatched public URLs. Any method: re-execution keeps
    /// the original one, and a POST to a missing URL must still get this page rather than a 405.</summary>
    [Route("/not-found")]
    public Task<IActionResult> NotFoundPage(CancellationToken ct) => NotFoundPageAsync(ct);

    private async Task<IActionResult> NotFoundPageAsync(CancellationToken ct)
    {
        var data = await _notFound.GetAsync(token => _content.GetNotFoundAsync(3, token), ct);
        var bio = data.Bio;
        var requested = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath ?? Request.Path.Value ?? "/";
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound", new NotFoundViewModel
        {
            Bio = bio,
            RequestedPath = requested,
            Suggestions = data.Suggestions.ToList(),
            Meta = new PageMeta($"Page not found — {bio.FullName}", "That page doesn't exist.", requested, NoIndex: true),
        });
    }

    [HttpGet("/sitemap.xml")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var data = await _content.GetProjectsAsync(ct);
        var origin = PublicSite.Origin(data.Bio, Request);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var paths = new[] { "/", "/projects", "/cv", "/privacy" }.Concat(data.Projects.Select(p => $"/project/{p.Slug}"));
        var xml = new XElement(ns + "urlset",
            paths.Select(path => new XElement(ns + "url", new XElement(ns + "loc", PublicSite.Absolute(origin, path)))));
        return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + xml, "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Robots(CancellationToken ct)
    {
        var origin = PublicSite.Origin((await _content.GetProjectsAsync(ct)).Bio, Request);
        // The admin area is behind login anyway; keeping crawlers out avoids indexing the login page.
        return Content($"User-agent: *\nDisallow: /Admin\nDisallow: /Account\n\nSitemap: {origin}/sitemap.xml\n", "text/plain", Encoding.UTF8);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
