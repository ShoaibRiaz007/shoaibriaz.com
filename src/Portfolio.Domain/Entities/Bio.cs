namespace Portfolio.Domain.Entities;

/// <summary>Single-row aggregate holding the site owner's identity, hero copy, about text and contact details.</summary>
public class Bio
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string YearsExperience { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LinkedIn { get; set; } = string.Empty;
    public string GitHub { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string ResumeUrl { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
}
