namespace Portfolio.Domain.Entities;

/// <summary>A technical skill, grouped by category, with a proficiency level for the bar chart.</summary>
public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Level { get; set; } = 80;
    public int SortOrder { get; set; }
}
