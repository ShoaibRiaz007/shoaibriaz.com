using Portfolio.Domain.Abstractions;

namespace Portfolio.Domain.Entities;

/// <summary>A degree or course in the education timeline.</summary>
public class Education : IOrderedContent
{
    public int Id { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    /// <summary>Years only: degrees are shown as "2014 – 2018", month precision adds nothing.</summary>
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
