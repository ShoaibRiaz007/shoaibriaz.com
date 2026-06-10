using Portfolio.Application.Models;
using Portfolio.Domain.Entities;

namespace Portfolio.Application.Abstractions;

/// <summary>Write-side facade for the admin area: CRUD over all content plus media management.</summary>
public interface IAdminContentFacade
{
    Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default);

    // Bio
    Task<Bio> GetBioAsync(CancellationToken ct = default);
    Task SaveBioAsync(Bio bio, CancellationToken ct = default);

    // Services
    Task<Service?> GetServiceAsync(int id, CancellationToken ct = default);
    Task SaveServiceAsync(Service service, CancellationToken ct = default);
    Task DeleteServiceAsync(int id, CancellationToken ct = default);

    // Projects
    Task<Project?> GetProjectAsync(int id, CancellationToken ct = default);
    Task SaveProjectAsync(Project project, CancellationToken ct = default);
    Task DeleteProjectAsync(int id, CancellationToken ct = default);

    // Experiences
    Task<Experience?> GetExperienceAsync(int id, CancellationToken ct = default);
    Task SaveExperienceAsync(Experience experience, CancellationToken ct = default);
    Task DeleteExperienceAsync(int id, CancellationToken ct = default);

    // Skills
    Task<Skill?> GetSkillAsync(int id, CancellationToken ct = default);
    Task SaveSkillAsync(Skill skill, CancellationToken ct = default);
    Task DeleteSkillAsync(int id, CancellationToken ct = default);

    // Testimonials
    Task<Testimonial?> GetTestimonialAsync(int id, CancellationToken ct = default);
    Task SaveTestimonialAsync(Testimonial testimonial, CancellationToken ct = default);
    Task DeleteTestimonialAsync(int id, CancellationToken ct = default);

    // Project media
    Task<IReadOnlyList<ProjectMedia>> GetProjectMediaAsync(int projectId, CancellationToken ct = default);
    Task<MediaResult> UploadMediaAsync(int projectId, Stream content, string fileName, string caption, CancellationToken ct = default);
    Task<MediaResult> AddEmbedAsync(int projectId, string embedUrl, string caption, CancellationToken ct = default);
    Task DeleteMediaAsync(int id, CancellationToken ct = default);

    // Account
    /// <summary>Returns the new security stamp on success (so the caller can re-issue its own cookie), null on failure.</summary>
    Task<string?> ChangePasswordAsync(string username, string currentPassword, string newPassword, CancellationToken ct = default);
}
