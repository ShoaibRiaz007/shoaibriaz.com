namespace Portfolio.Domain.Entities;

/// <summary>A recommendation / testimonial from a client or colleague.</summary>
public class Testimonial
{
    public int Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public string AuthorTitle { get; set; } = string.Empty;
    public string Quote { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
