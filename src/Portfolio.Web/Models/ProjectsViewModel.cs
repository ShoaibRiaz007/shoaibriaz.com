using Portfolio.Domain.Entities;

namespace Portfolio.Models;

/// <summary>Typed model for the public projects page (published projects only; replaces ViewBag).</summary>
public class ProjectsViewModel : IPublicPage
{
    public List<Project> Projects { get; init; } = new();
    public List<string> Categories { get; init; } = new();
    /// <summary>Area picked via ?area=, or null for all. Applied server-side so the chips work without JS.</summary>
    public string? Area { get; init; }
    public Bio Bio { get; init; } = new();
    public PageMeta Meta { get; init; } = new("", "", "/projects");
}

/// <summary>Typed model for the public project case-study page (replaces ViewBag).</summary>
public class ProjectDetailViewModel : IPublicPage
{
    public Project Project { get; init; } = new();
    public List<ProjectMedia> Media { get; init; } = new();
    public List<Project> More { get; init; } = new();
    public Project? Previous { get; init; }
    public Project? Next { get; init; }
    public Bio Bio { get; init; } = new();
    public PageMeta Meta { get; init; } = new("", "", "/");
}

/// <summary>One video in a project gallery, with the title screen readers and the player use.</summary>
public record VideoFrameModel(Portfolio.Domain.Entities.ProjectMedia Media, string Title);
