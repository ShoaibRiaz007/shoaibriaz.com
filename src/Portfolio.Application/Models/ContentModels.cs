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
    IReadOnlyList<Testimonial> Testimonials);

/// <summary>The full projects index page: all projects plus the distinct category list.</summary>
public record ProjectsData(
    IReadOnlyList<Project> Projects,
    IReadOnlyList<string> Categories,
    Bio Bio);

/// <summary>A single project's case-study page.</summary>
public record ProjectDetail(
    Project Project,
    IReadOnlyList<ProjectMedia> Media,
    IReadOnlyList<Project> More,
    Bio Bio);

/// <summary>All content collections for the admin dashboard.</summary>
public record AdminDashboard(
    Bio Bio,
    IReadOnlyList<Service> Services,
    IReadOnlyList<Experience> Experiences,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<Testimonial> Testimonials);

/// <summary>Outcome of a media upload/embed attempt.</summary>
public record MediaResult(bool Success, string Message);
