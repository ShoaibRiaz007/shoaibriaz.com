using Portfolio.Domain.Abstractions;

namespace Portfolio.Domain.Entities;

/// <summary>A role in the work-history timeline. Consecutive roles at the same company are
/// grouped on the public site (promotions); Company is compared trimmed and case-insensitively.</summary>
public class Experience : IOrderedContent
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    /// <summary>Month precision; the day is always 1. Required by the admin form; null only on legacy rows
    /// created before dates existed, which keep their hand-written Period.</summary>
    public DateOnly? StartDate { get; set; }
    /// <summary>Null means "Present".</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Display text ("Jun 2025 - Dec 2025"). Regenerated from the dates on save; kept as a
    /// column so legacy rows without dates still render.</summary>
    public string Period { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;

    public bool IsCurrent => StartDate is not null && EndDate is null;
}
