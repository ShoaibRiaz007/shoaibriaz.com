namespace Portfolio.Domain.Entities;

/// <summary>A role in the work-history timeline.</summary>
public class Experience
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
