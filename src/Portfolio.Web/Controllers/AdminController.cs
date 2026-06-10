using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Entities;
using Portfolio.Models;

namespace Portfolio.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IAdminContentFacade _admin;
    private readonly IOutputCacheStore _cache;

    public AdminController(IAdminContentFacade admin, IOutputCacheStore cache)
    {
        _admin = admin; _cache = cache;
    }

    /// <summary>Public pages are output-cached; every content mutation evicts them.</summary>
    private ValueTask EvictPublicCacheAsync(CancellationToken ct) => _cache.EvictByTagAsync("content", ct);

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var d = await _admin.GetDashboardAsync(ct);
        return View(new HomeViewModel
        {
            Bio = d.Bio,
            Services = d.Services.ToList(),
            Experiences = d.Experiences.ToList(),
            Projects = d.Projects.ToList(),
            Skills = d.Skills.ToList(),
            Testimonials = d.Testimonials.ToList(),
        });
    }

    // ================= BIO =================
    [HttpGet]
    public async Task<IActionResult> EditBio(CancellationToken ct) => View(await _admin.GetBioAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBio(Bio model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        await _admin.SaveBioAsync(model, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Bio updated.";
        return RedirectToAction(nameof(Index));
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
        if (!ModelState.IsValid) return View(model);
        await _admin.SaveServiceAsync(model, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Service saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id, CancellationToken ct)
    {
        await _admin.DeleteServiceAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Service deleted.";
        return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> EditProject(Project model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            // Re-fetch the gallery so a validation error doesn't blank the media list on the form.
            ViewBag.Media = model.Id == 0
                ? new List<ProjectMedia>()
                : (await _admin.GetProjectMediaAsync(model.Id, ct)).ToList();
            return View(model);
        }
        await _admin.SaveProjectAsync(model, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Project saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProject(int id, CancellationToken ct)
    {
        await _admin.DeleteProjectAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Project deleted.";
        return RedirectToAction(nameof(Index));
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
        if (!ModelState.IsValid) return View(model);
        await _admin.SaveExperienceAsync(model, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Experience saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExperience(int id, CancellationToken ct)
    {
        await _admin.DeleteExperienceAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Experience deleted.";
        return RedirectToAction(nameof(Index));
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
        if (!ModelState.IsValid) return View(model);
        await _admin.SaveSkillAsync(model, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Skill saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSkill(int id, CancellationToken ct)
    {
        await _admin.DeleteSkillAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Skill deleted.";
        return RedirectToAction(nameof(Index));
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
        if (!ModelState.IsValid) return View(model);
        await _admin.SaveTestimonialAsync(model, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Testimonial saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTestimonial(int id, CancellationToken ct)
    {
        await _admin.DeleteTestimonialAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Testimonial deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ================= PROJECT MEDIA =================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(157_286_400)]
    [RequestFormLimits(MultipartBodyLengthLimit = 157_286_400)]
    public async Task<IActionResult> UploadMedia(int projectId, IFormFile? file, string? caption, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Message"] = "No file selected.";
            return RedirectToAction(nameof(EditProject), new { id = projectId });
        }
        await using var stream = file.OpenReadStream();
        var result = await _admin.UploadMediaAsync(projectId, stream, file.FileName, caption ?? "", ct);
        if (result.Success) await EvictPublicCacheAsync(ct);
        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(EditProject), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEmbed(int projectId, string? embedUrl, string? caption, CancellationToken ct)
    {
        var result = await _admin.AddEmbedAsync(projectId, embedUrl ?? "", caption ?? "", ct);
        if (result.Success) await EvictPublicCacheAsync(ct);
        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(EditProject), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMedia(int id, int projectId, CancellationToken ct)
    {
        await _admin.DeleteMediaAsync(id, ct);
        await EvictPublicCacheAsync(ct);
        TempData["Message"] = "Media removed.";
        return RedirectToAction(nameof(EditProject), new { id = projectId });
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
        TempData["Message"] = "Password changed. All other sessions have been signed out.";
        return RedirectToAction(nameof(Index));
    }
}
