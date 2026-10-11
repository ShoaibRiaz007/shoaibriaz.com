using Portfolio.Application.Abstractions;
using Portfolio.Application.Common;
using Portfolio.Application.Models;
using Portfolio.Domain.Abstractions;
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
    private readonly IRepository<Education> _educations;
    private readonly IRepository<Certification> _certifications;
    private readonly IRepository<ProjectMedia> _media;
    private readonly IRepository<AdminUser> _admins;
    private readonly IPasswordHasher _hasher;
    private readonly IFileStorage _files;
    private readonly IUnitOfWork _uow;

    public AdminContentFacade(
        IRepository<Bio> bios, IRepository<Service> services, IRepository<Experience> experiences,
        IRepository<Project> projects, IRepository<Skill> skills, IRepository<Testimonial> testimonials,
        IRepository<Education> educations, IRepository<Certification> certifications,
        IRepository<ProjectMedia> media, IRepository<AdminUser> admins,
        IPasswordHasher hasher, IFileStorage files, IUnitOfWork uow)
    {
        _bios = bios; _services = services; _experiences = experiences; _projects = projects;
        _skills = skills; _testimonials = testimonials; _educations = educations; _certifications = certifications;
        _media = media; _admins = admins;
        _hasher = hasher; _files = files; _uow = uow;
    }

    public async Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default) => new(
        (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio(),
        InListOrder(await _services.ListAsync(ct)),
        InListOrder(await _experiences.ListAsync(ct)),
        InListOrder(await _projects.ListAsync(ct)),
        InListOrder(await _skills.ListAsync(ct)),
        InListOrder(await _testimonials.ListAsync(ct)),
        InListOrder(await _educations.ListAsync(ct)),
        InListOrder(await _certifications.ListAsync(ct)));

    /// <summary>Admin order; Id breaks SortOrder ties (two rows appended at once can share a position), so the
    /// list and the public site agree and the order doesn't change between requests.</summary>
    private static IReadOnlyList<T> InListOrder<T>(IEnumerable<T> rows) where T : IOrderedContent
        => rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToList();

    public async Task<IReadOnlyList<ContentCount>> GetContentCountsAsync(CancellationToken ct = default) => new[]
    {
        await CountAsync(ContentKind.Projects, _projects, ct),
        await CountAsync(ContentKind.Experience, _experiences, ct),
        await CountAsync(ContentKind.Education, _educations, ct),
        await CountAsync(ContentKind.Certifications, _certifications, ct),
        await CountAsync(ContentKind.Skills, _skills, ct),
        await CountAsync(ContentKind.Services, _services, ct),
        await CountAsync(ContentKind.Testimonials, _testimonials, ct),
    };

    private static async Task<ContentCount> CountAsync<T>(ContentKind kind, IRepository<T> repo, CancellationToken ct)
        where T : class, IOrderedContent
    {
        var byVisibility = await repo.CountByAsync(x => x.IsPublished, ct);
        return new(kind, byVisibility.GetValueOrDefault(true), byVisibility.GetValueOrDefault(false));
    }

    // ---- Bio ----
    public async Task<Bio> GetBioAsync(CancellationToken ct = default)
        => (await _bios.ListAsync(ct)).FirstOrDefault() ?? new Bio();

    public async Task SaveBioAsync(Bio model, IReadOnlyCollection<string> keepStoredColumns, CancellationToken ct = default)
    {
        model.LinkedIn = UrlValidator.SanitizeOrEmpty(model.LinkedIn);
        model.GitHub = UrlValidator.SanitizeOrEmpty(model.GitHub);
        model.Website = UrlValidator.SanitizeOrEmpty(model.Website);
        model.ResumeUrl = UrlValidator.SanitizeOrEmpty(model.ResumeUrl);
        model.ProfileImageUrl = UrlValidator.SanitizeOrEmpty(model.ProfileImageUrl);

        var bio = (await _bios.ListAsync(ct)).FirstOrDefault();
        if (bio is null) { await _bios.AddAsync(model, ct); return; }
        // No read-modify-write of the URL columns: an upload committing meanwhile keeps its new URL.
        model.Id = bio.Id;
        await _bios.UpdateExcludingAsync(model, keepStoredColumns, ct);
    }

    private const long MaxProfileImageBytes = 5L * 1024 * 1024;
    private const long MaxResumeBytes = 10L * 1024 * 1024;

    public Task<MediaResult> UploadProfileImageAsync(Stream content, string fileName, CancellationToken ct = default)
        => ReplaceBioAssetAsync(content, fileName, MaxProfileImageBytes,
            header =>
            {
                var check = FileSignatureValidator.Validate(header, fileName);
                return check.Ok && (check.MediaType != "image" || check.Format == "gif")
                    ? check with { Ok = false, Message = "The profile photo must be a JPG, PNG or WebP image." }
                    : check;
            },
            nameof(Bio.ProfileImageUrl), "Profile photo updated.", ct);

    public Task<MediaResult> UploadResumeAsync(Stream content, string fileName, CancellationToken ct = default)
        => ReplaceBioAssetAsync(content, fileName, MaxResumeBytes,
            header => FileSignatureValidator.ValidatePdf(header, fileName),
            nameof(Bio.ResumeUrl), "CV uploaded.", ct);

    /// <summary>Validates, stores and swaps a single-file Bio asset (photo / CV) in the <paramref name="urlColumn"/>
    /// column only, so a text edit saved meanwhile is neither lost nor reverted. The previous file is deleted
    /// only after the Bio row points at the new one, so a failure never leaves a broken link, and only when it
    /// is one of this app's uploads that nothing else still uses (Bio URLs are editable, so they can point at
    /// gallery files or external links).</summary>
    private async Task<MediaResult> ReplaceBioAssetAsync(
        Stream content, string fileName, long maxBytes, Func<byte[], FileSignatureValidator.Result> validate,
        string urlColumn, string successMessage, CancellationToken ct)
    {
        var (stream, owned) = await EnsureSeekableAsync(content, ct);
        try
        {
            if (stream.Length == 0) return new(false, "No file selected.");
            if (stream.Length > maxBytes) return new(false, $"File is too large (max {maxBytes / (1024 * 1024)} MB).");

            var check = validate(await ReadHeaderAsync(stream, ct));
            if (!check.Ok) return new(false, check.Message);

            stream.Position = 0;
            string url;
            try { url = await _files.SaveAsync(stream, fileName, ct); }
            catch (InvalidOperationException ex) { return new(false, ex.Message); }

            var column = typeof(Bio).GetProperty(urlColumn)!;
            var bio = (await _bios.ListAsync(ct)).FirstOrDefault();
            var previous = bio is null ? "" : (string)column.GetValue(bio)!;
            if (bio is null)
            {
                bio = new Bio();
                column.SetValue(bio, url);
                await _bios.AddAsync(bio, ct);
            }
            else if (await _bios.UpdateFieldAsync(bio.Id, urlColumn, url, ct) == 0)
            {
                // The profile row changed underneath the upload: nothing points at the new file, so don't keep it
                // and don't touch the old one.
                try { await _files.DeleteAsync(url, ct); } catch (InvalidOperationException) { /* orphan is harmless */ }
                return new(false, "Your profile changed while the file was uploading. Please try again.");
            }

            if (!string.IsNullOrEmpty(previous) && previous != url && await CanDeleteFileAsync(previous, null, ct))
            {
                // The new file is already live; failing to clean up the old one must not report the upload as failed.
                try { await _files.DeleteAsync(previous, ct); }
                catch (InvalidOperationException ex)
                {
                    return new(true, $"{successMessage} The previous file could not be removed from storage: {ex.Message}",
                        CleanupFailed: true);
                }
            }

            return new(true, successMessage);
        }
        finally
        {
            if (owned) await stream.DisposeAsync();
        }
    }

    /// <summary>A stored file may back several records (de-duplicated gallery uploads, a project cover, or a Bio
    /// URL pasted from the gallery), so it is deleted only when it is one of this app's own uploads and none of
    /// them still points at it. URLs are compared by the storage's file key, not as strings: a hand-typed Bio
    /// link without the Cloudinary version segment is still the gallery's file.</summary>
    private async Task<bool> CanDeleteFileAsync(string url, int? ignoringMediaId, CancellationToken ct)
    {
        var key = _files.FileKey(url);
        if (key is null) return false;
        bool Same(string other) => _files.FileKey(other) == key;

        // The file name narrows the candidates in SQL (case-insensitively, since file keys are); the key
        // comparison then decides.
        var name = Path.GetFileNameWithoutExtension(url.Split('?', '#')[0]).ToLowerInvariant();
        return !(await _media.ListAsync(m => m.Url.ToLower().Contains(name) && m.Id != ignoringMediaId, ct)).Any(m => Same(m.Url))
               && !(await _projects.ListAsync(p => p.ImageUrl.ToLower().Contains(name), ct)).Any(p => Same(p.ImageUrl))
               && !(await _bios.ListAsync(b => b.ProfileImageUrl.ToLower().Contains(name) || b.ResumeUrl.ToLower().Contains(name), ct))
                   .Any(b => Same(b.ProfileImageUrl) || Same(b.ResumeUrl));
    }

    /// <summary>IFormFile streams are buffered and seekable; anything else is spooled to a self-deleting
    /// temp file so validation can sniff the header and then rewind without buffering it all in memory.</summary>
    private static async Task<(Stream Stream, bool Owned)> EnsureSeekableAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek) return (content, false);
        var spool = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite,
            FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try { await content.CopyToAsync(spool, ct); }
        catch { await spool.DisposeAsync(); throw; }
        return (spool, true);
    }

    private static async Task<byte[]> ReadHeaderAsync(Stream stream, CancellationToken ct)
    {
        stream.Position = 0;
        var header = new byte[16];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        return header.AsSpan(0, read).ToArray();
    }

    // ---- Services ----
    public Task<Service?> GetServiceAsync(int id, CancellationToken ct = default) => _services.GetByIdAsync(id, ct);
    public async Task SaveServiceAsync(Service s, CancellationToken ct = default)
    { if (s.Id == 0) await AppendAsync(_services, s, ct); else await UpdateKeepingListStateAsync(_services, s, ct); }
    public async Task DeleteServiceAsync(int id, CancellationToken ct = default)
    { var s = await _services.GetByIdAsync(id, ct); if (s is not null) await _services.RemoveAsync(s, ct); }

    // ---- Projects ----
    public Task<Project?> GetProjectAsync(int id, CancellationToken ct = default) => _projects.GetByIdAsync(id, ct);
    public async Task SaveProjectAsync(Project p, CancellationToken ct = default, bool keepStoredCover = false)
    {
        p.Slug = string.IsNullOrWhiteSpace(p.Slug) ? SlugGenerator.Generate(p.Title) : p.Slug.ToLowerInvariant();
        p.Slug = await EnsureUniqueSlugAsync(p.Slug, p.Id, ct);
        p.ProjectUrl = UrlValidator.SanitizeOrEmpty(p.ProjectUrl);
        p.GitHubUrl = UrlValidator.SanitizeOrEmpty(p.GitHubUrl);
        p.ImageUrl = UrlValidator.SanitizeOrEmpty(p.ImageUrl);
        if (p.Id == 0) await AppendAsync(_projects, p, ct);
        else if (keepStoredCover) await _projects.UpdateExcludingAsync(p, [.. ListStateColumns, nameof(Project.ImageUrl)], ct);
        else await UpdateKeepingListStateAsync(_projects, p, ct);
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
    {
        e.StartDate = PeriodFormatter.ToMonth(e.StartDate);
        e.EndDate = PeriodFormatter.ToMonth(e.EndDate);
        // Legacy rows without dates keep their hand-written Period; dated rows always derive it.
        if (e.StartDate is not null) e.Period = PeriodFormatter.Format(e.StartDate, e.EndDate);
        if (e.Id == 0) await AppendAsync(_experiences, e, ct); else await UpdateKeepingListStateAsync(_experiences, e, ct);
    }
    public async Task DeleteExperienceAsync(int id, CancellationToken ct = default)
    { var e = await _experiences.GetByIdAsync(id, ct); if (e is not null) await _experiences.RemoveAsync(e, ct); }

    // ---- Skills ----
    public Task<Skill?> GetSkillAsync(int id, CancellationToken ct = default) => _skills.GetByIdAsync(id, ct);
    public async Task SaveSkillAsync(Skill s, CancellationToken ct = default)
    { if (s.Id == 0) await AppendAsync(_skills, s, ct); else await UpdateKeepingListStateAsync(_skills, s, ct); }
    public async Task DeleteSkillAsync(int id, CancellationToken ct = default)
    { var s = await _skills.GetByIdAsync(id, ct); if (s is not null) await _skills.RemoveAsync(s, ct); }

    // ---- Testimonials ----
    public Task<Testimonial?> GetTestimonialAsync(int id, CancellationToken ct = default) => _testimonials.GetByIdAsync(id, ct);
    public async Task SaveTestimonialAsync(Testimonial t, CancellationToken ct = default)
    { if (t.Id == 0) await AppendAsync(_testimonials, t, ct); else await UpdateKeepingListStateAsync(_testimonials, t, ct); }
    public async Task DeleteTestimonialAsync(int id, CancellationToken ct = default)
    { var t = await _testimonials.GetByIdAsync(id, ct); if (t is not null) await _testimonials.RemoveAsync(t, ct); }

    // ---- Education ----
    public Task<Education?> GetEducationAsync(int id, CancellationToken ct = default) => _educations.GetByIdAsync(id, ct);
    public async Task SaveEducationAsync(Education e, CancellationToken ct = default)
    { if (e.Id == 0) await AppendAsync(_educations, e, ct); else await UpdateKeepingListStateAsync(_educations, e, ct); }
    public async Task DeleteEducationAsync(int id, CancellationToken ct = default)
    { var e = await _educations.GetByIdAsync(id, ct); if (e is not null) await _educations.RemoveAsync(e, ct); }

    // ---- Certifications ----
    public Task<Certification?> GetCertificationAsync(int id, CancellationToken ct = default) => _certifications.GetByIdAsync(id, ct);
    public async Task SaveCertificationAsync(Certification c, CancellationToken ct = default)
    {
        c.CredentialUrl = UrlValidator.SanitizeOrEmpty(c.CredentialUrl);
        if (c.Id == 0) await AppendAsync(_certifications, c, ct); else await UpdateKeepingListStateAsync(_certifications, c, ct);
    }
    public async Task DeleteCertificationAsync(int id, CancellationToken ct = default)
    { var c = await _certifications.GetByIdAsync(id, ct); if (c is not null) await _certifications.RemoveAsync(c, ct); }

    // ---- Ordering & visibility ----
    public Task ReorderAsync(ContentKind kind, IReadOnlyList<int> orderedIds, CancellationToken ct = default) => kind switch
    {
        ContentKind.Projects => ReorderAsync(_projects, orderedIds, ct),
        ContentKind.Experience => ReorderAsync(_experiences, orderedIds, ct),
        ContentKind.Services => ReorderAsync(_services, orderedIds, ct),
        ContentKind.Skills => ReorderAsync(_skills, orderedIds, ct),
        ContentKind.Testimonials => ReorderAsync(_testimonials, orderedIds, ct),
        ContentKind.Education => ReorderAsync(_educations, orderedIds, ct),
        ContentKind.Certifications => ReorderAsync(_certifications, orderedIds, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public Task<bool> SetPublishedAsync(ContentKind kind, int id, bool isPublished, CancellationToken ct = default) => kind switch
    {
        ContentKind.Projects => SetPublishedAsync(_projects, id, isPublished, ct),
        ContentKind.Experience => SetPublishedAsync(_experiences, id, isPublished, ct),
        ContentKind.Services => SetPublishedAsync(_services, id, isPublished, ct),
        ContentKind.Skills => SetPublishedAsync(_skills, id, isPublished, ct),
        ContentKind.Testimonials => SetPublishedAsync(_testimonials, id, isPublished, ct),
        ContentKind.Education => SetPublishedAsync(_educations, id, isPublished, ct),
        ContentKind.Certifications => SetPublishedAsync(_certifications, id, isPublished, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private async Task ReorderAsync<T>(IRepository<T> repo, IReadOnlyList<int> orderedIds, CancellationToken ct)
        where T : class, IOrderedContent
    {
        var rows = await repo.ListAsync(ct);
        var position = orderedIds.Distinct().Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var reordered = rows
            .OrderBy(r => position.TryGetValue(r.Id, out var p) ? p : int.MaxValue)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .ToList();

        // One transaction; only the SortOrder column is written, so a concurrent edit or hide of any row survives.
        // Every row is written (skipping ones whose pre-transaction value looked right lets overlapping reorders
        // interleave into duplicates), in Id order so concurrent reorders lock rows in the same order.
        var targets = reordered.Select((r, i) => (r.Id, Position: i + 1)).OrderBy(x => x.Id).ToList();
        await _uow.ExecuteAsync(async token =>
        {
            foreach (var (id, position) in targets)
                await repo.UpdateFieldAsync(id, nameof(IOrderedContent.SortOrder), position, token);
        }, ct);
    }

    /// <summary>New rows without an explicit position go to the end of their list (forms don't post one;
    /// position is changed by reordering on the dashboard).</summary>
    private static async Task AppendAsync<T>(IRepository<T> repo, T row, CancellationToken ct)
        where T : class, IOrderedContent
    {
        if (row.SortOrder == 0) row.SortOrder = await repo.MaxAsync(_ => true, x => x.SortOrder, ct) + 1;
        await repo.AddAsync(row, ct);
    }

    private static readonly string[] ListStateColumns = { nameof(IOrderedContent.SortOrder), nameof(IOrderedContent.IsPublished) };

    /// <summary>Position and visibility of an existing row belong to the dashboard (drag, Live/Hidden). An edit
    /// form, possibly opened before a reorder or hide, saves everything else and leaves those two columns alone.</summary>
    private static Task UpdateKeepingListStateAsync<T>(IRepository<T> repo, T row, CancellationToken ct)
        where T : class, IOrderedContent
        => repo.UpdateExcludingAsync(row, ListStateColumns, ct);

    /// <summary>Single-column write: flipping visibility never rewrites (or reverts) the rest of the row.</summary>
    private static async Task<bool> SetPublishedAsync<T>(IRepository<T> repo, int id, bool isPublished, CancellationToken ct)
        where T : class, IOrderedContent
        => await repo.UpdateFieldAsync(id, nameof(IOrderedContent.IsPublished), isPublished, ct) > 0;

    // ---- Project media ----
    public async Task<IReadOnlyList<ProjectMedia>> GetProjectMediaAsync(int projectId, CancellationToken ct = default)
        => await _media.ListAsync(m => m.ProjectId == projectId, q => q.OrderBy(m => m.SortOrder).ThenBy(m => m.Id), ct);

    public async Task<MediaResult> UploadMediaAsync(int projectId, Stream content, string fileName, string caption, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(projectId, ct);
        if (project is null) return new(false, "Project not found.");

        // Never buffer the whole upload in memory: sniff 16 bytes, then hash and persist by streaming.
        var (stream, owned) = await EnsureSeekableAsync(content, ct);
        try
        {
            if (stream.Length == 0) return new(false, "No file selected.");

            var check = FileSignatureValidator.Validate(await ReadHeaderAsync(stream, ct), fileName);
            if (!check.Ok) return new(false, check.Message);

            // De-duplicate by content: if an identical file was already uploaded, reuse its stored
            // asset instead of uploading a copy.
            stream.Position = 0;
            var contentHash = await ContentHasher.Sha256HexAsync(stream, ct);
            var existing = await _media.FirstOrDefaultAsync(
                m => m.ContentHash != "" && m.ContentHash == contentHash, ct);
            // Already in this project's gallery (a repeat upload, or the same file chosen twice): nothing to add, but it
            // still becomes the cover if the project has none, as a fresh upload would.
            var inGallery = await _media.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.ContentHash != "" && m.ContentHash == contentHash, ct);
            if (inGallery is not null)
            {
                if (inGallery.MediaType == "image")
                    await _projects.UpdateFieldWhereAsync(p => p.Id == projectId && (p.ImageUrl == null || p.ImageUrl.Trim() == ""),
                        nameof(Project.ImageUrl), inGallery.Url, ct);
                return new(true, "Already in this gallery.", Skipped: true);
            }

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

                // Only the cover column, and only while it is still empty. The emptiness test is part of the UPDATE
                // itself, so a hide, reorder, edit or cover set during the upload is never overwritten.
                if (check.MediaType == "image")
                    await _projects.UpdateFieldWhereAsync(
                        p => p.Id == projectId && (p.ImageUrl == null || p.ImageUrl.Trim() == ""),
                        nameof(Project.ImageUrl), url, token);
            }, ct);

            return new(true, message);
        }
        finally
        {
            if (owned) await stream.DisposeAsync();
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

        // Is this item the cover? Compared by storage file key, like the in-use check: a cover link typed with a
        // different query string or letter case is still this file.
        var project = await _projects.GetByIdAsync(media.ProjectId, ct);
        var key = _files.FileKey(media.Url);
        var wasCover = media.MediaType == "image" && project is not null && !string.IsNullOrEmpty(project.ImageUrl)
                       && (project.ImageUrl == media.Url || (key is not null && _files.FileKey(project.ImageUrl) == key));
        var coverAtRead = wasCover ? project!.ImageUrl : media.Url;

        // Row and cover together, row first, file last: an orphaned file is harmless; a row or cover pointing at a
        // deleted file is not. The replacement is read inside the transaction (so it isn't an image removed meanwhile),
        // and the UPDATE matches this item's URL as well as the cover just read, so a cover set to this item by another
        // tab after the read is caught too, while a cover changed to anything else is left alone. The in-use check runs
        // after both, so a de-duplicated upload that reused this file keeps it.
        await _uow.ExecuteAsync(async token =>
        {
            await _media.RemoveAsync(media, token);
            if (media.MediaType != "image") return;
            var images = (await GetProjectMediaAsync(media.ProjectId, token)).Where(m => m.MediaType == "image" && m.Id != media.Id).ToList();
            var replacement = (images.FirstOrDefault(m => m.SortOrder > media.SortOrder) ?? images.LastOrDefault())?.Url ?? "";
            await _projects.UpdateFieldWhereAsync(
                p => p.Id == media.ProjectId && (p.ImageUrl == media.Url || p.ImageUrl == coverAtRead),
                nameof(Project.ImageUrl), replacement, token);
        }, ct);

        if (await CanDeleteFileAsync(media.Url, null, ct))
            await _files.DeleteAsync(media.Url, ct);
    }

    public async Task<MediaResult> SetCoverAsync(int projectId, int mediaId, CancellationToken ct = default)
    {
        var media = await _media.GetByIdAsync(mediaId, ct);
        if (media is null || media.ProjectId != projectId) return new(false, "That item is no longer in the gallery.");
        if (media.MediaType != "image") return new(false, "Only an image can be the cover.");
        var previous = (await _projects.GetByIdAsync(projectId, ct))?.ImageUrl;
        if (previous is null || await _projects.UpdateFieldAsync(projectId, nameof(Project.ImageUrl), media.Url, ct) == 0)
            return new(false, "Project not found.");

        // Removed in another tab between the read and the write? Then its file may be gone: put the previous cover
        // back, unless the cover has changed again since. A query, not GetById, which may answer from the change tracker.
        if (!await _media.AnyAsync(m => m.Id == mediaId, ct))
        {
            await _projects.UpdateFieldWhereAsync(p => p.Id == projectId && p.ImageUrl == media.Url, nameof(Project.ImageUrl), previous, ct);
            return new(false, "That item is no longer in the gallery.");
        }
        return new(true, "Cover image updated.");
    }

    public async Task<bool> MoveMediaAsync(int projectId, int mediaId, int offset, CancellationToken ct = default)
    {
        var found = false;
        // Read and renumber in one transaction, writing every row in Id order, like ReorderAsync: skipping rows that
        // looked right lets two overlapping moves interleave into duplicate positions.
        await _uow.ExecuteAsync(async token =>
        {
            var items = (await GetProjectMediaAsync(projectId, token)).ToList();
            var from = items.FindIndex(m => m.Id == mediaId);
            if (from < 0) return;
            found = true;
            var to = (int)Math.Clamp((long)from + offset, 0, items.Count - 1);
            if (to == from) return;

            var moved = items[from];
            items.RemoveAt(from);
            items.Insert(to, moved);
            foreach (var (id, position) in items.Select((m, i) => (m.Id, i + 1)).OrderBy(x => x.Id))
                await _media.UpdateFieldAsync(id, nameof(ProjectMedia.SortOrder), position, token);
        }, ct);
        return found;
    }

    /// <summary>The caption column's size (AppDbContext uses it too).</summary>
    public const int MaxCaptionLength = 500;

    public async Task<bool> UpdateMediaCaptionAsync(int projectId, int mediaId, string caption, CancellationToken ct = default)
    {
        caption = Telemetry.TrafficSources.Truncate((caption ?? "").Trim(), MaxCaptionLength);   // never splits a surrogate pair
        return await _media.UpdateFieldWhereAsync(m => m.Id == mediaId && m.ProjectId == projectId,
            nameof(ProjectMedia.Caption), caption, ct) > 0;
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
