using Portfolio.Domain.Abstractions;

namespace Portfolio.Domain.Entities;

/// <summary>A capability / offering shown in the "What I Do" section.</summary>
public class Service : IOrderedContent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
