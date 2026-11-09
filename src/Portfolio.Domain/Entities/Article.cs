namespace Portfolio.Domain.Entities;

/// <summary>A technical architecture article or topic cluster guide for search authority and inbound leads.</summary>
public class Article
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TechStack { get; set; } = string.Empty;
    public string PillarSlug { get; set; } = string.Empty;
    public string ReadingTime { get; set; } = string.Empty;
    public DateOnly PublishedDate { get; set; }
    public bool IsPillar { get; set; }
    public string TargetKeyword { get; set; } = string.Empty;
    public IReadOnlyList<string> Highlights { get; set; } = [];
}
