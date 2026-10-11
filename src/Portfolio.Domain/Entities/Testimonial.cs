using Portfolio.Domain.Abstractions;

namespace Portfolio.Domain.Entities;

/// <summary>A recommendation / testimonial from a client or colleague.</summary>
public class Testimonial : IOrderedContent
{
    public int Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public string AuthorTitle { get; set; } = string.Empty;
    public string Quote { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
