using Portfolio.Application.Models;
using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;
using Portfolio.Models;

namespace Portfolio.Api;

/// <summary>Public profile. Phone is deliberately absent: no public page shows it, so the API must not either.</summary>
public record ProfileDto(
    string FullName, string Headline, string Tagline, string[] About, string Location, string YearsExperience,
    string Email, string LinkedIn, string GitHub, string Website, string ResumeUrl, string ProfileImageUrl);

public record RoleDto(
    string Role, string EmploymentType, string Location, DateOnly? Start, DateOnly? End, bool IsCurrent,
    string Period, string[] Highlights);

public record CompanyDto(string Company, DateOnly? Start, DateOnly? End, string Period, IReadOnlyList<RoleDto> Roles);

public record EducationDto(string Institution, string Degree, string FieldOfStudy, int? StartYear, int? EndYear, string Description);

public record CertificationDto(string Name, string Issuer, DateOnly? IssuedOn, string CredentialId, string CredentialUrl);

public record SkillDto(string Name, string Category, int Level);

public record ServiceDto(string Title, string Description, string Icon);

public record TestimonialDto(string Author, string AuthorTitle, string Quote);

public record ProjectDto(
    string Slug, string Title, string Summary, string Category, string[] TechStack, string Timeline,
    string ImageUrl, string ProjectUrl, string GitHubUrl, string Metric, bool IsFeatured);

public record MediaDto(string Type, string Url, string Caption);

public record ProjectDetailDto(
    ProjectDto Project, string[] Description, string[] Challenge, string[] Solution, string[] Outcome,
    string MyRole, string[] Highlights, IReadOnlyList<MediaDto> Media);

public record SiteDto(
    ProfileDto Profile, IReadOnlyList<CompanyDto> Experience, IReadOnlyList<EducationDto> Education,
    IReadOnlyList<CertificationDto> Certifications, IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<ServiceDto> Services, IReadOnlyList<ProjectDto> Projects, IReadOnlyList<TestimonialDto> Testimonials);

/// <summary>Entity → DTO mapping. Newline-delimited admin text becomes arrays so clients never re-parse it.</summary>
public static class ApiModels
{
    public static SiteDto ToDto(SiteData s) => new(
        ToDto(s.Bio),
        ExperienceGrouping.GroupConsecutive(s.Experiences).Select(ToDto).ToList(),
        s.Educations.Select(e => new EducationDto(e.Institution, e.Degree, e.FieldOfStudy, e.StartYear, e.EndYear, e.Description)).ToList(),
        s.Certifications.Select(c => new CertificationDto(c.Name, c.Issuer, c.IssuedOn, c.CredentialId, c.CredentialUrl)).ToList(),
        s.Skills.Select(k => new SkillDto(k.Name, k.Category, k.Level)).ToList(),
        s.Services.Select(v => new ServiceDto(v.Title, v.Description, v.Icon)).ToList(),
        s.Projects.Select(ToDto).ToList(),
        s.Testimonials.Select(t => new TestimonialDto(t.Author, t.AuthorTitle, t.Quote)).ToList());

    public static ProfileDto ToDto(Bio b) => new(
        b.FullName, b.Headline, b.Tagline, PublicSite.Lines(b.About), b.Location, b.YearsExperience,
        b.Email, b.LinkedIn, b.GitHub, b.Website, b.ResumeUrl, b.ProfileImageUrl);

    public static CompanyDto ToDto(CompanyGroup g) => new(
        g.Company, g.StartDate, g.EndDate, g.Period,
        g.Roles.Select(r => new RoleDto(r.Role, r.EmploymentType, r.Location, r.StartDate, r.EndDate, r.IsCurrent,
            r.Period, PublicSite.Lines(r.Description))).ToList());

    public static ProjectDto ToDto(Project p) => new(
        p.Slug, p.Title, p.Summary, p.Category, PublicSite.Csv(p.TechStack), p.Timeline,
        p.ImageUrl, p.ProjectUrl, p.GitHubUrl, p.Metric, p.IsFeatured);

    public static ProjectDetailDto ToDto(ProjectDetail d) => new(
        ToDto(d.Project), PublicSite.Lines(d.Project.Description), PublicSite.Lines(d.Project.Challenge), PublicSite.Lines(d.Project.Solution),
        PublicSite.Lines(d.Project.Outcome), d.Project.MyRole, PublicSite.Lines(d.Project.Highlights),
        d.Media.Select(m => new MediaDto(m.MediaType, m.Url, m.Caption)).ToList());
}
