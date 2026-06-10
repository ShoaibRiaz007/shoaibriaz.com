using Portfolio.Domain.Entities;

namespace Portfolio.Models;

/// <summary>Typed model for the public "all projects" page (replaces ViewBag).</summary>
public class ProjectsViewModel
{
    public List<Project> Projects { get; init; } = new();
    public List<string> Categories { get; init; } = new();
    public Bio Bio { get; init; } = new();
}

/// <summary>Typed model for the public project case-study page (replaces ViewBag).</summary>
public class ProjectDetailViewModel
{
    public Project Project { get; init; } = new();
    public List<ProjectMedia> Media { get; init; } = new();
    public List<Project> More { get; init; } = new();
    public Bio Bio { get; init; } = new();
}
