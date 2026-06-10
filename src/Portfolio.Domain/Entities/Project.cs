namespace Portfolio.Domain.Entities;

/// <summary>A portfolio project / game / package, with optional case-study content.</summary>
public class Project
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TechStack { get; set; } = string.Empty;
    public string Timeline { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string ProjectUrl { get; set; } = string.Empty;
    public string GitHubUrl { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string MyRole { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
}
