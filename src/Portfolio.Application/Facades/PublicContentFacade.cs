using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;
using Portfolio.Domain.Abstractions;
using Portfolio.Domain.Entities;

namespace Portfolio.Application.Facades;

/// <summary>Default read-side facade composing the repositories into page-shaped aggregates.</summary>
public class PublicContentFacade : IPublicContentFacade
{
    private readonly IRepository<Bio> _bios;
    private readonly IRepository<Service> _services;
    private readonly IRepository<Experience> _experiences;
    private readonly IRepository<Project> _projects;
    private readonly IRepository<Skill> _skills;
    private readonly IRepository<Testimonial> _testimonials;
    private readonly IRepository<Education> _educations;
    private readonly IRepository<Certification> _certifications;
    private readonly IRepository<ProjectMedia> _media;

    public PublicContentFacade(
        IRepository<Bio> bios, IRepository<Service> services, IRepository<Experience> experiences,
        IRepository<Project> projects, IRepository<Skill> skills, IRepository<Testimonial> testimonials,
        IRepository<Education> educations, IRepository<Certification> certifications,
        IRepository<ProjectMedia> media)
    {
        _bios = bios; _services = services; _experiences = experiences;
        _projects = projects; _skills = skills; _testimonials = testimonials;
        _educations = educations; _certifications = certifications; _media = media;
    }

    /// <summary>Home page highlights need at least this many featured projects; below that the home page
    /// shows the first projects in list order instead (the admin checklist warns about it).</summary>
    public const int MinFeaturedProjects = 3;

    /// <summary>Published rows only, in admin order — filtered and sorted in SQL. Id breaks SortOrder ties
    /// (two rows appended at once can share a position) so the order is stable between requests.</summary>
    private static Task<IReadOnlyList<T>> PublishedAsync<T>(IRepository<T> repo, CancellationToken ct)
        where T : class, IOrderedContent
        => repo.ListAsync(x => x.IsPublished, q => q.OrderBy(x => x.SortOrder).ThenBy(x => x.Id), ct);

    private async Task<Bio> BioAsync(CancellationToken ct) => (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio();

    public Task<Bio> GetBioAsync(CancellationToken ct = default) => BioAsync(ct);

    public async Task<HomeData> GetHomeAsync(int prominentCount = 6, CancellationToken ct = default)
    {
        var bio = await BioAsync(ct);
        var services = await PublishedAsync(_services, ct);
        var experiences = await PublishedAsync(_experiences, ct);
        var skills = await PublishedAsync(_skills, ct);
        var testimonials = await PublishedAsync(_testimonials, ct);
        var educations = await PublishedAsync(_educations, ct);
        var certifications = await PublishedAsync(_certifications, ct);
        var allProjects = await PublishedAsync(_projects, ct);

        var prominent = allProjects.Where(p => p.IsFeatured).Take(prominentCount).ToList();
        if (prominent.Count < MinFeaturedProjects) prominent = allProjects.Take(prominentCount).ToList();

        return new HomeData(bio, services, experiences, prominent, allProjects.Count, skills, testimonials,
            educations, certifications);
    }

    public async Task<ProjectsData> GetProjectsAsync(CancellationToken ct = default)
    {
        // Admin order (SortOrder) everywhere, so a drag on the dashboard is exactly what visitors see;
        // IsFeatured picks what the home page highlights, it doesn't reorder lists.
        var projects = await PublishedAsync(_projects, ct);
        var categories = projects
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => p.Category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        return new ProjectsData(projects, categories, await BioAsync(ct));
    }

    public async Task<ProjectDetail?> GetProjectDetailAsync(string slug, CancellationToken ct = default)
    {
        // One query serves the page, its prev/next links and "More": all of them follow admin order.
        // Slugs are normalized to lowercase on save (AdminContentFacade.SaveProjectAsync / SlugGenerator).
        var lowered = slug.ToLowerInvariant();
        var projects = await PublishedAsync(_projects, ct);
        var project = projects.FirstOrDefault(p => p.Slug == lowered);
        if (project is null) return null;

        var media = await _media.ListAsync(
            m => m.ProjectId == project.Id, q => q.OrderBy(m => m.SortOrder).ThenBy(m => m.Id), ct);
        var more = projects.Where(p => p.Id != project.Id).Take(3).ToList();

        return new ProjectDetail(project, media, more, await BioAsync(ct), projects);
    }

    public async Task<NotFoundData> GetNotFoundAsync(int suggestionCount = 3, CancellationToken ct = default)
    {
        // Bounded: featured projects first, then the rest, each in admin order.
        var suggestions = await _projects.ListAsync(
            p => p.IsPublished,
            q => q.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.SortOrder).ThenBy(p => p.Id).Take(suggestionCount),
            ct);
        return new NotFoundData(await BioAsync(ct), suggestions);
    }

    public async Task<SiteData> GetSiteAsync(CancellationToken ct = default) => new(
        await BioAsync(ct),
        await PublishedAsync(_services, ct),
        await PublishedAsync(_experiences, ct),
        await PublishedAsync(_educations, ct),
        await PublishedAsync(_certifications, ct),
        await PublishedAsync(_skills, ct),
        await PublishedAsync(_projects, ct),
        await PublishedAsync(_testimonials, ct));
}
