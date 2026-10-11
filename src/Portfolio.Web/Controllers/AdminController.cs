using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Caching;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;
using Portfolio.Domain.Abstractions;
using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;
using Portfolio.Models;

namespace Portfolio.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IAdminContentFacade _admin;
    private readonly IOutputCacheStore _cache;
    private readonly NotFoundCache _notFound;

    public AdminController(IAdminContentFacade admin, IOutputCacheStore cache, NotFoundCache notFound)
    {
        _admin = admin; _cache = cache; _notFound = notFound;
    }

    /// <summary>Public pages are output-cached and the 404 page's data is cached separately; every content
    /// mutation evicts both.</summary>
    private ValueTask EvictPublicCacheAsync(CancellationToken ct)
    {
        _notFound.Invalidate();
        return _cache.EvictByTagAsync("content", ct);
    }

    /// <summary>HttpContext.Items key under which an action shares the snapshot it already loaded with the
    /// sidebar (AdminNavViewComponent), so the page doesn't load every list twice.</summary>
    public const string DashboardItemKey = "Admin.Dashboard";
    /// <summary>TempData key that marks <c>TempData["Message"]</c> as a failure (shown as an error toast).</summary>
    public const string MessageTypeKey = "MessageType";

    private Task<AdminDashboard> LoadDashboardAsync(CancellationToken ct) => LoadDashboardAsync(HttpContext, _admin, ct);

    /// <summary>Loads the dashboard and leaves it for the sidebar, so the page doesn't count everything twice.</summary>
    public static async Task<AdminDashboard> LoadDashboardAsync(HttpContext http, IAdminContentFacade admin, CancellationToken ct)
    {
        var d = await admin.GetDashboardAsync(ct);
        http.Items[DashboardItemKey] = d;
        return d;
    }

    private RedirectToActionResult RedirectToList(ContentKind kind)
        => RedirectToAction(nameof(List), new { kind = kind.ToString().ToLowerInvariant() });

    /// <summary>admin.js toggles visibility with fetch and this header, the same way admin-lists.js reorders.</summary>
    private bool IsAjaxRequest => Request.Headers.XRequestedWith == "XMLHttpRequest";

    private void Flash(string message, bool success = true)
    {
        TempData["Message"] = message;
        if (!success) TempData[MessageTypeKey] = "error";
    }

    /// <summary>Saves an edit form and returns to its list. A row deleted (e.g. in another tab) after the form was
    /// opened comes back as an error toast on that list instead of a 500.</summary>
    private async Task<IActionResult> SaveAndReturnAsync(ContentKind kind, Func<Task> save, string message, CancellationToken ct)
    {
        try { await save(); }
        catch (RowNotFoundException)
        {
            Flash("This item no longer exists. It may have been deleted in another tab.", success: false);
            return RedirectToList(kind);
        }
        await EvictPublicCacheAsync(ct);
        Flash(message);
        return RedirectToList(kind);
    }

    /// <summary>Re-renders an edit form after a validation error. An existing row doesn't post its visibility
    /// (the list owns it), so the stored value is shown rather than the bound default.</summary>
    private async Task<IActionResult> RedisplayAsync<T>(T model, Func<int, CancellationToken, Task<T?>> load, CancellationToken ct)
        where T : class, IOrderedContent
    {
        if (model.Id != 0 && await load(model.Id, ct) is { } stored) model.IsPublished = stored.IsPublished;
        return View(model);
    }

    public async Task<IActionResult> Index(CancellationToken ct)
        => View(AdminOverview.Dashboard(await LoadDashboardAsync(ct)));

    /// <summary>One content list (reorder, show/hide, search). Attribute-routed so the kind reads as a path
    /// segment: /Admin/List/projects.</summary>
    [HttpGet("Admin/List/{kind}")]
    public async Task<IActionResult> List([BindRequired] ContentKind kind, CancellationToken ct,
        [FromServices] ITelemetryFacade? telemetry = null)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(kind)) return NotFound();
        var dashboard = await LoadDashboardAsync(ct);
        // The projects list shows how often each project page is opened, to help decide their order. The list
        // still works without the numbers if they can't be read.
        IReadOnlyDictionary<string, ProjectViews>? views = null;
        if (kind == ContentKind.Projects && telemetry is not null)
        {
            try { views = await telemetry.GetProjectViewsAsync(AdminOverview.ProjectViewsDays, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                (HttpContext.RequestServices as IServiceProvider)?.GetService<ILogger<AdminController>>()
                    ?.LogError(ex, "Project view counts could not be read.");
            }
        }
        return View(AdminOverview.List(dashboard, kind, views));
    }

    // ================= ORDERING & VISIBILITY (all lists) =================
    /// <summary>Called by admin-lists.js after a drag/move. Returns 204 so the page doesn't reload.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([BindRequired] ContentKind kind, [FromForm] int[] ids, CancellationToken ct)
    {
        // ContentKind's zero value is a real list, so a missing/misspelled kind must fail via ModelState
        // rather than silently binding to Projects.
        if (!ModelState.IsValid || !Enum.IsDefined(kind) || ids.Length == 0) return BadRequest();
        await _admin.ReorderAsync(kind, ids, ct);
        await EvictPublicCacheAsync(ct);
        return NoContent();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPublished([BindRequired] ContentKind kind, int id, [BindRequired] bool isPublished, CancellationToken ct)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(kind)) return BadRequest();
        if (!await _admin.SetPublishedAsync(kind, id, isPublished, ct)) return NotFound();
        await EvictPublicCacheAsync(ct);
        // The script updates the row in place and shows its own toast; a plain form post reloads the list.
        if (IsAjaxRequest) return NoContent();
        Flash(isPublished ? "Now visible on the site." : "Hidden from the site (still editable here).");
        return RedirectToList(kind);
    }

    // ================= BIO =================
    [HttpGet]
    public async Task<IActionResult> EditBio(CancellationToken ct) => View(await _admin.GetBioAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBio(Bio model, string? originalProfileImageUrl, string? originalResumeUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            // Re-rendering must carry the originally rendered URLs forward, not the edited ones, or the
            // resubmit would look "untouched" and drop the edit.
            ViewData[nameof(originalProfileImageUrl)] = originalProfileImageUrl;
            ViewData[nameof(originalResumeUrl)] = originalResumeUrl;
            return View(model);
        }

        await _admin.SaveBioAsync(model, UntouchedFileUrlColumns(model, originalProfileImageUrl, originalResumeUrl), ct);
        await EvictPublicCacheAsync(ct);
        Flash("Bio updated.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Uploads replace (and delete) the photo/CV files. A URL field the admin didn't change is left out
    /// of the save, so saving a form rendered before an upload (Back button, second tab) can't restore the URL of
    /// a file that has since been deleted. A field the admin did change is saved as typed.</summary>
    public static IReadOnlyCollection<string> UntouchedFileUrlColumns(Bio posted, string? originalProfileImageUrl, string? originalResumeUrl)
    {
        var untouched = new List<string>(2);
        if (posted.ProfileImageUrl == (originalProfileImageUrl ?? "")) untouched.Add(nameof(Bio.ProfileImageUrl));
        if (posted.ResumeUrl == (originalResumeUrl ?? "")) untouched.Add(nameof(Bio.ResumeUrl));
        return untouched;
    }

    /// <summary>Request ceiling, deliberately above the facade's per-file limits (5 MB photo, 10 MB CV) so an
    /// oversized file reaches the facade and gets its friendly "too large" message instead of a bare 413.</summary>
    private const long MaxBioRequestBytes = 25L * 1024 * 1024;

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBioRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBioRequestBytes)]
    public Task<IActionResult> UploadProfileImage(IFormFile? file, CancellationToken ct)
        => UploadBioAssetAsync(file, _admin.UploadProfileImageAsync, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBioRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBioRequestBytes)]
    public Task<IActionResult> UploadResume(IFormFile? file, CancellationToken ct)
        => UploadBioAssetAsync(file, _admin.UploadResumeAsync, ct);

    private async Task<IActionResult> UploadBioAssetAsync(
        IFormFile? file, Func<Stream, string, CancellationToken, Task<MediaResult>> upload, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            Flash("No file selected.", success: false);
            return RedirectToAction(nameof(EditBio), null, null, "files");
        }
        await using var stream = file.OpenReadStream();
        var result = await upload(stream, file.FileName, ct);
        if (result.Success) await EvictPublicCacheAsync(ct);
        Flash(result.Message, result.Success);
        return RedirectToAction(nameof(EditBio), null, null, "files");
    }

    // ================= SERVICES =================
    [HttpGet]
    public async Task<IActionResult> EditService(int? id, CancellationToken ct)
    {
        var model = id is null ? new Service() : await _admin.GetServiceAsync(id.Value, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditService(Service model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await RedisplayAsync(model, _admin.GetServiceAsync, ct);
        return await SaveAndReturnAsync(ContentKind.Services, () => _admin.SaveServiceAsync(model, ct), "Service saved.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id, CancellationToken ct)
    {
        await _admin.DeleteServiceAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Service deleted.");
        return RedirectToList(ContentKind.Services);
    }

    // ================= PROJECTS =================
    [HttpGet]
    public async Task<IActionResult> EditProject(int? id, CancellationToken ct)
    {
        var model = id is null ? new Project() : await _admin.GetProjectAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Media = id is null
            ? new List<ProjectMedia>()
            : (await _admin.GetProjectMediaAsync(id.Value, ct)).ToList();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProject(Project model, string? originalImageUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            // Re-fetch the gallery so a validation error doesn't blank the media list on the form.
            ViewBag.Media = model.Id == 0
                ? new List<ProjectMedia>()
                : (await _admin.GetProjectMediaAsync(model.Id, ct)).ToList();
            // Keep what the page first loaded with, not the link typed since, so the corrected resubmit still saves it.
            ViewData[nameof(originalImageUrl)] = originalImageUrl;
            return await RedisplayAsync(model, _admin.GetProjectAsync, ct);
        }
        if (model.Id == 0)
        {
            // A new project goes straight on to its media step: screenshots and videos need a saved project.
            await _admin.SaveProjectAsync(model, ct);
            await EvictPublicCacheAsync(ct);
            Flash("Project created. Next: add screenshots and videos, and pick a cover.");
            return MediaStep(model.Id);
        }
        // The cover link field still holds what the page loaded with: leave the stored cover alone, since the gallery
        // (set cover, remove) may have changed it since.
        var keepCover = (model.ImageUrl ?? "") == (originalImageUrl ?? "");
        return await SaveAndReturnAsync(ContentKind.Projects, () => _admin.SaveProjectAsync(model, ct, keepCover), "Project saved.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProject(int id, CancellationToken ct)
    {
        await _admin.DeleteProjectAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Project deleted.");
        return RedirectToList(ContentKind.Projects);
    }

    // ================= EXPERIENCE =================
    [HttpGet]
    public async Task<IActionResult> EditExperience(int? id, CancellationToken ct)
    {
        var model = id is null ? new Experience() : await _admin.GetExperienceAsync(id.Value, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditExperience(Experience model, CancellationToken ct)
    {
        ValidateExperienceDates(model, ModelState);
        if (!ModelState.IsValid) return await RedisplayAsync(model, _admin.GetExperienceAsync, ct);
        return await SaveAndReturnAsync(ContentKind.Experience, () => _admin.SaveExperienceAsync(model, ct), "Experience saved.", ct);
    }

    /// <summary>A start month is required, except on a legacy row that keeps its hand-written Period untouched;
    /// the end month (empty = "Present") can't precede it.</summary>
    public static void ValidateExperienceDates(Experience model, ModelStateDictionary modelState)
    {
        var keepsLegacyPeriod = model.Id != 0 && model.StartDate is null && model.EndDate is null
                                && !string.IsNullOrWhiteSpace(model.Period);
        if (model.StartDate is null && !keepsLegacyPeriod)
            modelState.AddModelError(nameof(model.StartDate), "Start month is required.");
        // Compared as saved (month precision): the binder also accepts full dates, and 20 Jun to 5 Jun is one month.
        if (model.StartDate is not null && model.EndDate is not null
            && PeriodFormatter.ToMonth(model.EndDate) < PeriodFormatter.ToMonth(model.StartDate))
            modelState.AddModelError(nameof(model.EndDate), "End month can't be before the start month.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExperience(int id, CancellationToken ct)
    {
        await _admin.DeleteExperienceAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Experience deleted.");
        return RedirectToList(ContentKind.Experience);
    }

    // ================= SKILLS =================
    [HttpGet]
    public async Task<IActionResult> EditSkill(int? id, CancellationToken ct)
    {
        var model = id is null ? new Skill() : await _admin.GetSkillAsync(id.Value, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSkill(Skill model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await RedisplayAsync(model, _admin.GetSkillAsync, ct);
        return await SaveAndReturnAsync(ContentKind.Skills, () => _admin.SaveSkillAsync(model, ct), "Skill saved.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSkill(int id, CancellationToken ct)
    {
        await _admin.DeleteSkillAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Skill deleted.");
        return RedirectToList(ContentKind.Skills);
    }

    // ================= TESTIMONIALS =================
    [HttpGet]
    public async Task<IActionResult> EditTestimonial(int? id, CancellationToken ct)
    {
        var model = id is null ? new Testimonial() : await _admin.GetTestimonialAsync(id.Value, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTestimonial(Testimonial model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await RedisplayAsync(model, _admin.GetTestimonialAsync, ct);
        return await SaveAndReturnAsync(ContentKind.Testimonials, () => _admin.SaveTestimonialAsync(model, ct), "Testimonial saved.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTestimonial(int id, CancellationToken ct)
    {
        await _admin.DeleteTestimonialAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Testimonial deleted.");
        return RedirectToList(ContentKind.Testimonials);
    }

    // ================= EDUCATION =================
    [HttpGet]
    public async Task<IActionResult> EditEducation(int? id, CancellationToken ct)
    {
        var model = id is null ? new Education() : await _admin.GetEducationAsync(id.Value, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEducation(Education model, CancellationToken ct)
    {
        if (model.StartYear is not null && model.EndYear is not null && model.EndYear < model.StartYear)
            ModelState.AddModelError(nameof(model.EndYear), "End year can't be before the start year.");
        if (!ModelState.IsValid) return await RedisplayAsync(model, _admin.GetEducationAsync, ct);
        return await SaveAndReturnAsync(ContentKind.Education, () => _admin.SaveEducationAsync(model, ct), "Education saved.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEducation(int id, CancellationToken ct)
    {
        await _admin.DeleteEducationAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Education deleted.");
        return RedirectToList(ContentKind.Education);
    }

    // ================= CERTIFICATIONS =================
    [HttpGet]
    public async Task<IActionResult> EditCertification(int? id, CancellationToken ct)
    {
        var model = id is null ? new Certification() : await _admin.GetCertificationAsync(id.Value, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCertification(Certification model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await RedisplayAsync(model, _admin.GetCertificationAsync, ct);
        return await SaveAndReturnAsync(ContentKind.Certifications, () => _admin.SaveCertificationAsync(model, ct), "Certification saved.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCertification(int id, CancellationToken ct)
    {
        await _admin.DeleteCertificationAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Certification deleted.");
        return RedirectToList(ContentKind.Certifications);
    }

    // ================= PROJECT MEDIA =================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(157_286_400)]
    [RequestFormLimits(MultipartBodyLengthLimit = 157_286_400)]
    public async Task<IActionResult> UploadMedia(int projectId, List<IFormFile>? files, CancellationToken ct)
    {
        var chosen = (files ?? new()).Where(f => f.Length > 0).ToList();
        if (chosen.Count == 0)
        {
            Flash("No file selected.", success: false);
            return MediaStep(projectId);
        }

        // One file at a time, each through the same validation and de-duplication; one bad file doesn't stop the rest
        // and is reported with the others. The public cache is refreshed for files already saved even if the request is
        // cancelled partway.
        var uploaded = 0;
        var skipped = 0;
        var problems = new List<string>();
        try
        {
            foreach (var file in chosen)
            {
                try
                {
                    await using var stream = file.OpenReadStream();
                    var result = await _admin.UploadMediaAsync(projectId, stream, file.FileName, "", ct);
                    if (result.Skipped) skipped++;
                    else if (result.Success) uploaded++;
                    else problems.Add($"{file.FileName}: {result.Message}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    (HttpContext.RequestServices as IServiceProvider)?.GetService<ILogger<AdminController>>()
                        ?.LogError(ex, "Upload of {File} to project {Project} failed.", file.FileName, projectId);
                    problems.Add($"{file.FileName}: it could not be saved. Please try again.");
                }
            }
        }
        finally
        {
            // A skipped file can still have become the cover, so it changes the public pages too.
            if (uploaded > 0 || skipped > 0) await EvictPublicCacheAsync(CancellationToken.None);
        }
        Flash(UploadSummary(chosen.Count, uploaded, problems, skipped), problems.Count == 0);
        return MediaStep(projectId);
    }

    public static string UploadSummary(int chosen, int uploaded, IReadOnlyList<string> problems, int skipped = 0)
    {
        var already = skipped == 0 ? "" : skipped == 1 ? " 1 was already in the gallery." : $" {skipped} were already in the gallery.";
        if (problems.Count == 0)
            return uploaded == 0 && skipped > 0
                ? (skipped == 1 ? "That file is already in the gallery." : $"Those {skipped} files are already in the gallery.")
                : (uploaded == 1 ? "1 file uploaded." : $"{uploaded} files uploaded.") + already;
        var lead = uploaded == 0 ? "Nothing was uploaded." : $"{uploaded} of {chosen} files uploaded.";
        return $"{lead}{already} {string.Join(" ", problems)}";
    }

    private RedirectToActionResult MediaStep(int projectId)
        => RedirectToAction(nameof(EditProject), null, new { id = projectId }, "media");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCover(int projectId, int mediaId, CancellationToken ct)
    {
        var result = await _admin.SetCoverAsync(projectId, mediaId, ct);
        if (result.Success) await EvictPublicCacheAsync(ct);
        Flash(result.Message, result.Success);
        return MediaStep(projectId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveMedia(int projectId, int mediaId, int offset, CancellationToken ct)
    {
        if (await _admin.MoveMediaAsync(projectId, mediaId, Math.Sign(offset), ct)) await EvictPublicCacheAsync(ct);
        else Flash("That item is no longer in the gallery.", success: false);
        return MediaStep(projectId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMediaCaption(int projectId, int mediaId, string? caption, CancellationToken ct)
    {
        if (await _admin.UpdateMediaCaptionAsync(projectId, mediaId, caption ?? "", ct))
        {
            await EvictPublicCacheAsync(ct);
            Flash("Caption saved.");
        }
        else Flash("That item is no longer in the gallery.", success: false);
        return MediaStep(projectId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEmbed(int projectId, string? embedUrl, string? caption, CancellationToken ct)
    {
        var result = await _admin.AddEmbedAsync(projectId, embedUrl ?? "", caption ?? "", ct);
        if (result.Success) await EvictPublicCacheAsync(ct);
        Flash(result.Message, result.Success);
        return RedirectToAction(nameof(EditProject), null, new { id = projectId }, "media");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMedia(int id, int projectId, CancellationToken ct)
    {
        await _admin.DeleteMediaAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        Flash("Media removed.");
        return RedirectToAction(nameof(EditProject), null, new { id = projectId }, "media");
    }

    // ================= CHANGE PASSWORD =================
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var username = User.Identity?.Name ?? "";
        var newStamp = await _admin.ChangePasswordAsync(username, model.CurrentPassword, model.NewPassword, ct);
        if (newStamp is null)
        {
            ModelState.AddModelError(string.Empty, "Current password is incorrect.");
            return View(model);
        }
        // Rotating the stamp invalidated every session, including this one — re-issue our cookie.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountController.BuildPrincipal(username, newStamp));
        Flash("Password changed. All other sessions have been signed out.");
        return RedirectToAction(nameof(Index));
    }
}
