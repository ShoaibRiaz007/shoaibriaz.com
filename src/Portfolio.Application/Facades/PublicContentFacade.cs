using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;
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
    private readonly IRepository<ProjectMedia> _media;

    public PublicContentFacade(
        IRepository<Bio> bios, IRepository<Service> services, IRepository<Experience> experiences,
        IRepository<Project> projects, IRepository<Skill> skills, IRepository<Testimonial> testimonials,
        IRepository<ProjectMedia> media)
    {
        _bios = bios; _services = services; _experiences = experiences;
        _projects = projects; _skills = skills; _testimonials = testimonials; _media = media;
    }

    public async Task<HomeData> GetHomeAsync(int prominentCount = 6, CancellationToken ct = default)
    {
        var bio = (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio();
        var services = (await _services.ListAsync(ct)).OrderBy(s => s.SortOrder).ToList();
        var experiences = (await _experiences.ListAsync(ct)).OrderBy(e => e.SortOrder).ToList();
        var skills = (await _skills.ListAsync(ct)).OrderBy(s => s.SortOrder).ToList();
        var testimonials = (await _testimonials.ListAsync(ct)).OrderBy(t => t.SortOrder).ToList();
        var allProjects = (await _projects.ListAsync(ct)).OrderBy(p => p.SortOrder).ToList();

        var prominent = allProjects.Where(p => p.IsFeatured).Take(prominentCount).ToList();
        if (prominent.Count < 3) prominent = allProjects.Take(prominentCount).ToList();

        return new HomeData(bio, services, experiences, prominent, allProjects.Count, skills, testimonials);
    }

    public async Task<ProjectsData> GetProjectsAsync(CancellationToken ct = default)
    {
        var projects = (await _projects.ListAsync(ct))
            .OrderByDescending(p => p.IsFeatured).ThenBy(p => p.SortOrder)
            .ToList();
        var categories = projects
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => p.Category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        var bio = (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio();
        return new ProjectsData(projects, categories, bio);
    }

    public async Task<ProjectDetail?> GetProjectDetailAsync(string slug, CancellationToken ct = default)
    {
        // Slugs are normalized to lowercase on save (AdminContentFacade.SaveProjectAsync / SlugGenerator),
        // so a direct comparison is enough and avoids a non-sargable ToLower() in the generated SQL.
        var lowered = slug.ToLowerInvariant();
        var project = await _projects.FirstOrDefaultAsync(p => p.Slug == lowered, ct);
        if (project is null) return null;

        var media = (await _media.ListAsync(m => m.ProjectId == project.Id, ct))
            .OrderBy(m => m.SortOrder)
            .ToList();

        var more = await _projects.ListAsync(
            p => p.Id != project.Id,
            q => q.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.SortOrder).Take(3),
            ct);

        var bio = (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio();
        return new ProjectDetail(project, media, more, bio);
    }
}
