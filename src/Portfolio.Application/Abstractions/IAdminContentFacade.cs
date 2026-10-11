using Portfolio.Application.Models;
using Portfolio.Domain.Entities;

namespace Portfolio.Application.Abstractions;

/// <summary>Write-side facade for the admin area: CRUD over all content plus media management. Saving an existing
/// row that was deleted meanwhile throws <see cref="RowNotFoundException"/>.</summary>
public interface IAdminContentFacade
{
    Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default);
    /// <summary>Live/hidden counts per list, from count-only queries (the sidebar on pages that don't load the lists).</summary>
    Task<IReadOnlyList<ContentCount>> GetContentCountsAsync(CancellationToken ct = default);

    // Bio
    Task<Bio> GetBioAsync(CancellationToken ct = default);
    /// <summary>Saves the form's columns except <paramref name="keepStoredColumns"/> (URL fields the admin left
    /// untouched), so a form rendered before an upload can't put back the URL of a replaced, deleted file.</summary>
    Task SaveBioAsync(Bio bio, IReadOnlyCollection<string> keepStoredColumns, CancellationToken ct = default);
    Task<MediaResult> UploadProfileImageAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<MediaResult> UploadResumeAsync(Stream content, string fileName, CancellationToken ct = default);

    // Services
    Task<Service?> GetServiceAsync(int id, CancellationToken ct = default);
    Task SaveServiceAsync(Service service, CancellationToken ct = default);
    Task DeleteServiceAsync(int id, CancellationToken ct = default);

    // Projects
    Task<Project?> GetProjectAsync(int id, CancellationToken ct = default);
    /// <summary>Saves the edit form. <paramref name="keepStoredCover"/> leaves the cover column as stored: the form
    /// didn't change it, and the gallery may have since (a stale tab must not restore a removed cover).</summary>
    Task SaveProjectAsync(Project project, CancellationToken ct = default, bool keepStoredCover = false);
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

    // Education
    Task<Education?> GetEducationAsync(int id, CancellationToken ct = default);
    Task SaveEducationAsync(Education education, CancellationToken ct = default);
    Task DeleteEducationAsync(int id, CancellationToken ct = default);

    // Certifications
    Task<Certification?> GetCertificationAsync(int id, CancellationToken ct = default);
    Task SaveCertificationAsync(Certification certification, CancellationToken ct = default);
    Task DeleteCertificationAsync(int id, CancellationToken ct = default);

    // Ordering & visibility (every list)
    /// <summary>Rewrites SortOrder to match <paramref name="orderedIds"/> (1-based). Ids not listed keep
    /// their relative order after the listed ones, so a stale admin tab can't drop rows.</summary>
    Task ReorderAsync(ContentKind kind, IReadOnlyList<int> orderedIds, CancellationToken ct = default);
    /// <summary>Returns false if the row does not exist.</summary>
    Task<bool> SetPublishedAsync(ContentKind kind, int id, bool isPublished, CancellationToken ct = default);

    // Project media
    Task<IReadOnlyList<ProjectMedia>> GetProjectMediaAsync(int projectId, CancellationToken ct = default);
    Task<MediaResult> UploadMediaAsync(int projectId, Stream content, string fileName, string caption, CancellationToken ct = default);
    Task<MediaResult> AddEmbedAsync(int projectId, string embedUrl, string caption, CancellationToken ct = default);
    /// <summary>Removes a gallery item. When it was the project's cover, the image after it in the gallery becomes the
    /// cover (or the one before, if it was the last image; none if it was the only one).</summary>
    Task DeleteMediaAsync(int id, CancellationToken ct = default);
    /// <summary>Makes a gallery image the project's cover.</summary>
    Task<MediaResult> SetCoverAsync(int projectId, int mediaId, CancellationToken ct = default);
    /// <summary>Moves a gallery item <paramref name="offset"/> places (negative = earlier). False if it isn't there.</summary>
    Task<bool> MoveMediaAsync(int projectId, int mediaId, int offset, CancellationToken ct = default);
    /// <summary>False if the item isn't in that project's gallery.</summary>
    Task<bool> UpdateMediaCaptionAsync(int projectId, int mediaId, string caption, CancellationToken ct = default);

    // Account
    /// <summary>Returns the new security stamp on success (so the caller can re-issue its own cookie), null on failure.</summary>
    Task<string?> ChangePasswordAsync(string username, string currentPassword, string newPassword, CancellationToken ct = default);
}
