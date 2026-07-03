using Portfolio.Application.Abstractions;
using Portfolio.Application.Common;
using Portfolio.Application.Models;
using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;

namespace Portfolio.Application.Facades;

/// <summary>Default write-side facade orchestrating repositories, hashing, and file storage for the admin area.</summary>
public class AdminContentFacade : IAdminContentFacade
{
    private readonly IRepository<Bio> _bios;
    private readonly IRepository<Service> _services;
    private readonly IRepository<Experience> _experiences;
    private readonly IRepository<Project> _projects;
    private readonly IRepository<Skill> _skills;
    private readonly IRepository<Testimonial> _testimonials;
    private readonly IRepository<ProjectMedia> _media;
    private readonly IRepository<AdminUser> _admins;
    private readonly IPasswordHasher _hasher;
    private readonly IFileStorage _files;
    private readonly IUnitOfWork _uow;

    public AdminContentFacade(
        IRepository<Bio> bios, IRepository<Service> services, IRepository<Experience> experiences,
        IRepository<Project> projects, IRepository<Skill> skills, IRepository<Testimonial> testimonials,
        IRepository<ProjectMedia> media, IRepository<AdminUser> admins,
        IPasswordHasher hasher, IFileStorage files, IUnitOfWork uow)
    {
        _bios = bios; _services = services; _experiences = experiences; _projects = projects;
        _skills = skills; _testimonials = testimonials; _media = media; _admins = admins;
        _hasher = hasher; _files = files; _uow = uow;
    }

    public async Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default) => new(
        (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio(),
        (await _services.ListAsync(ct)).OrderBy(s => s.SortOrder).ToList(),
        (await _experiences.ListAsync(ct)).OrderBy(e => e.SortOrder).ToList(),
        (await _projects.ListAsync(ct)).OrderBy(p => p.SortOrder).ToList(),
        (await _skills.ListAsync(ct)).OrderBy(s => s.SortOrder).ToList(),
        (await _testimonials.ListAsync(ct)).OrderBy(t => t.SortOrder).ToList());

    // ---- Bio ----
    public async Task<Bio> GetBioAsync(CancellationToken ct = default)
        => (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio();

    public async Task SaveBioAsync(Bio model, CancellationToken ct = default)
    {
        model.LinkedIn = UrlValidator.SanitizeOrEmpty(model.LinkedIn);
        model.GitHub = UrlValidator.SanitizeOrEmpty(model.GitHub);
        model.Website = UrlValidator.SanitizeOrEmpty(model.Website);
        model.ResumeUrl = UrlValidator.SanitizeOrEmpty(model.ResumeUrl);
        model.ProfileImageUrl = UrlValidator.SanitizeOrEmpty(model.ProfileImageUrl);

        var bio = (await _bios.ListAsync(ct)).FirstOrDefault();
        if (bio is null) { await _bios.AddAsync(model, ct); return; }
        bio.FullName = model.FullName; bio.Headline = model.Headline; bio.Tagline = model.Tagline;
        bio.About = model.About; bio.Location = model.Location; bio.YearsExperience = model.YearsExperience;
        bio.Email = model.Email; bio.Phone = model.Phone; bio.LinkedIn = model.LinkedIn;
        bio.GitHub = model.GitHub; bio.Website = model.Website; bio.ResumeUrl = model.ResumeUrl;
        bio.ProfileImageUrl = model.ProfileImageUrl;
        await _bios.UpdateAsync(bio, ct);
    }

    // ---- Services ----
    public Task<Service?> GetServiceAsync(int id, CancellationToken ct = default) => _services.GetByIdAsync(id, ct);
    public async Task SaveServiceAsync(Service s, CancellationToken ct = default)
    { if (s.Id == 0) await _services.AddAsync(s, ct); else await _services.UpdateAsync(s, ct); }
    public async Task DeleteServiceAsync(int id, CancellationToken ct = default)
    { var s = await _services.GetByIdAsync(id, ct); if (s is not null) await _services.RemoveAsync(s, ct); }

    // ---- Projects ----
    public Task<Project?> GetProjectAsync(int id, CancellationToken ct = default) => _projects.GetByIdAsync(id, ct);
    public async Task SaveProjectAsync(Project p, CancellationToken ct = default)
    {
        p.Slug = string.IsNullOrWhiteSpace(p.Slug) ? SlugGenerator.Generate(p.Title) : p.Slug.ToLowerInvariant();
        p.Slug = await EnsureUniqueSlugAsync(p.Slug, p.Id, ct);
        p.ProjectUrl = UrlValidator.SanitizeOrEmpty(p.ProjectUrl);
        p.GitHubUrl = UrlValidator.SanitizeOrEmpty(p.GitHubUrl);
        p.ImageUrl = UrlValidator.SanitizeOrEmpty(p.ImageUrl);
        if (p.Id == 0) await _projects.AddAsync(p, ct); else await _projects.UpdateAsync(p, ct);
    }
    public async Task DeleteProjectAsync(int id, CancellationToken ct = default)
    { var p = await _projects.GetByIdAsync(id, ct); if (p is not null) await _projects.RemoveAsync(p, ct); }

    private const int MaxSlugSuffixAttempts = 1000;

    /// <summary>Slugs carry a unique index; suffix duplicates ("foo-2") rather than failing the save.</summary>
    private async Task<string> EnsureUniqueSlugAsync(string slug, int ownId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) slug = "project";
        var candidate = slug;
        var i = 2;
        while (await _projects.AnyAsync(x => x.Slug == candidate && x.Id != ownId, ct))
        {
            if (i > MaxSlugSuffixAttempts)
                candidate = $"{slug}-{Guid.NewGuid():N}";
            else
                candidate = $"{slug}-{i}";
            i++;
        }
        return candidate;
    }

    // ---- Experiences ----
    public Task<Experience?> GetExperienceAsync(int id, CancellationToken ct = default) => _experiences.GetByIdAsync(id, ct);
    public async Task SaveExperienceAsync(Experience e, CancellationToken ct = default)
    { if (e.Id == 0) await _experiences.AddAsync(e, ct); else await _experiences.UpdateAsync(e, ct); }
    public async Task DeleteExperienceAsync(int id, CancellationToken ct = default)
    { var e = await _experiences.GetByIdAsync(id, ct); if (e is not null) await _experiences.RemoveAsync(e, ct); }

    // ---- Skills ----
    public Task<Skill?> GetSkillAsync(int id, CancellationToken ct = default) => _skills.GetByIdAsync(id, ct);
    public async Task SaveSkillAsync(Skill s, CancellationToken ct = default)
    { if (s.Id == 0) await _skills.AddAsync(s, ct); else await _skills.UpdateAsync(s, ct); }
    public async Task DeleteSkillAsync(int id, CancellationToken ct = default)
    { var s = await _skills.GetByIdAsync(id, ct); if (s is not null) await _skills.RemoveAsync(s, ct); }

    // ---- Testimonials ----
    public Task<Testimonial?> GetTestimonialAsync(int id, CancellationToken ct = default) => _testimonials.GetByIdAsync(id, ct);
    public async Task SaveTestimonialAsync(Testimonial t, CancellationToken ct = default)
    { if (t.Id == 0) await _testimonials.AddAsync(t, ct); else await _testimonials.UpdateAsync(t, ct); }
    public async Task DeleteTestimonialAsync(int id, CancellationToken ct = default)
    { var t = await _testimonials.GetByIdAsync(id, ct); if (t is not null) await _testimonials.RemoveAsync(t, ct); }

    // ---- Project media ----
    public async Task<IReadOnlyList<ProjectMedia>> GetProjectMediaAsync(int projectId, CancellationToken ct = default)
        => (await _media.ListAsync(m => m.ProjectId == projectId, ct)).OrderBy(m => m.SortOrder).ToList();

    public async Task<MediaResult> UploadMediaAsync(int projectId, Stream content, string fileName, string caption, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(projectId, ct);
        if (project is null) return new(false, "Project not found.");

        // Never buffer the whole upload in memory: sniff 16 bytes, then hash and persist by
        // streaming. IFormFile streams are buffered and seekable; spool to a temp file otherwise.
        Stream stream = content;
        try
        {
            if (!content.CanSeek)
            {
                stream = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite,
                    FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await content.CopyToAsync(stream, ct);
            }

            if (stream.Length == 0) return new(false, "No file selected.");

            stream.Position = 0;
            var header = new byte[16];
            var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            var check = FileSignatureValidator.Validate(header.AsSpan(0, read).ToArray(), fileName);
            if (!check.Ok) return new(false, check.Message);

            // De-duplicate by content: if an identical file was already uploaded, reuse its stored
            // asset instead of uploading a copy.
            stream.Position = 0;
            var contentHash = await ContentHasher.Sha256HexAsync(stream, ct);
            var existing = await _media.FirstOrDefaultAsync(
                m => m.ContentHash != "" && m.ContentHash == contentHash, ct);

            string url;
            string message;
            if (existing is not null)
            {
                url = existing.Url;
                message = "Identical media already existed — reused the existing file (no duplicate upload).";
            }
            else
            {
                stream.Position = 0;
                try
                {
                    url = await _files.SaveAsync(stream, fileName, ct);
                }
                catch (InvalidOperationException ex)
                {
                    // Storage quota (or other storage-level) rejection — surface as a normal failure
                    // result rather than a 500, matching every other validation failure in this method.
                    return new(false, ex.Message);
                }
                message = "Media uploaded.";
            }

            var nextOrder = await _media.MaxAsync(m => m.ProjectId == projectId, m => m.SortOrder, ct) + 1;

            // Media row + project cover update commit or roll back together.
            await _uow.ExecuteAsync(async token =>
            {
                await _media.AddAsync(new ProjectMedia
                {
                    ProjectId = projectId, MediaType = check.MediaType, Url = url,
                    Caption = caption, SortOrder = nextOrder, ContentHash = contentHash
                }, token);

                if (check.MediaType == "image" && string.IsNullOrWhiteSpace(project.ImageUrl))
                {
                    project.ImageUrl = url;
                    await _projects.UpdateAsync(project, token);
                }
            }, ct);

            return new(true, message);
        }
        finally
        {
            if (!ReferenceEquals(stream, content)) await stream.DisposeAsync();
        }
    }

    public async Task<MediaResult> AddEmbedAsync(int projectId, string embedUrl, string caption, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(projectId, ct);
        if (project is null) return new(false, "Project not found.");

        var normalized = EmbedUrlNormalizer.Normalize(embedUrl);
        if (normalized is null) return new(false, "Could not parse that video URL. Paste a YouTube or Vimeo link.");

        var nextOrder = await _media.MaxAsync(m => m.ProjectId == projectId, m => m.SortOrder, ct) + 1;
        await _media.AddAsync(new ProjectMedia
        {
            ProjectId = projectId, MediaType = "embed", Url = normalized,
            Caption = caption, SortOrder = nextOrder
        }, ct);
        return new(true, "Video embed added.");
    }

    public async Task DeleteMediaAsync(int id, CancellationToken ct = default)
    {
        var media = await _media.GetByIdAsync(id, ct);
        if (media is null) return;

        // Only delete the underlying stored asset if no other media row still references the same URL
        // (a de-duplicated file may be shared across projects).
        var sharedByOthers = await _media.AnyAsync(m => m.Id != media.Id && m.Url == media.Url, ct);

        // Row first, file second: an orphaned file is harmless; a row pointing at a deleted file is not.
        await _media.RemoveAsync(media, ct);
        if (!sharedByOthers)
            await _files.DeleteAsync(media.Url, ct);
    }

    // ---- Account ----
    public async Task<string?> ChangePasswordAsync(string username, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _admins.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null || !_hasher.Verify(currentPassword, user.PasswordHash)) return null;
        user.PasswordHash = _hasher.Hash(newPassword);
        // Rotating the stamp invalidates every other outstanding session for this account.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _admins.UpdateAsync(user, ct);
        return user.SecurityStamp;
    }
}
