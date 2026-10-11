using Portfolio.Domain.Entities;

namespace Portfolio.Application.Models;

/// <summary>Everything the public home page needs, in one aggregate.</summary>
public record HomeData(
    Bio Bio,
    IReadOnlyList<Service> Services,
    IReadOnlyList<Experience> Experiences,
    IReadOnlyList<Project> ProminentProjects,
    int TotalProjects,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<Testimonial> Testimonials,
    IReadOnlyList<Education> Educations,
    IReadOnlyList<Certification> Certifications);

/// <summary>Every published content collection in one aggregate: feeds the CV page, the JSON API and the 3D world.</summary>
public record SiteData(
    Bio Bio,
    IReadOnlyList<Service> Services,
    IReadOnlyList<Experience> Experiences,
    IReadOnlyList<Education> Educations,
    IReadOnlyList<Certification> Certifications,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Testimonial> Testimonials);

/// <summary>The public projects index page: published projects plus their distinct category list.</summary>
public record ProjectsData(
    IReadOnlyList<Project> Projects,
    IReadOnlyList<string> Categories,
    Bio Bio);

/// <summary>A single project's case-study page. <paramref name="Projects"/> is every published project in
/// admin order (the page's prev/next links).</summary>
public record ProjectDetail(
    Project Project,
    IReadOnlyList<ProjectMedia> Media,
    IReadOnlyList<Project> More,
    Bio Bio,
    IReadOnlyList<Project> Projects);

/// <summary>The 404 page: the owner's identity and a few suggested projects.</summary>
public record NotFoundData(Bio Bio, IReadOnlyList<Project> Suggestions);

/// <summary>All content collections for the admin dashboard.</summary>
public record AdminDashboard(
    Bio Bio,
    IReadOnlyList<Service> Services,
    IReadOnlyList<Experience> Experiences,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<Testimonial> Testimonials,
    IReadOnlyList<Education> Educations,
    IReadOnlyList<Certification> Certifications);

/// <summary>Live and hidden row counts for one admin list (sidebar badges).</summary>
public record ContentCount(ContentKind Kind, int Live, int Hidden);

/// <summary>Outcome of a media upload/embed attempt.</summary>
/// <summary>Outcome of an upload. <paramref name="CleanupFailed"/>: the new file is live but the replaced one
/// couldn't be removed from storage.</summary>
public record MediaResult(bool Success, string Message, bool CleanupFailed = false, bool Skipped = false);
