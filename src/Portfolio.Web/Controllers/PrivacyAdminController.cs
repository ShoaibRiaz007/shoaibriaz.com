using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Facades;
using Portfolio.Hosting;
using Portfolio.Models;

namespace Portfolio.Controllers;

/// <summary>Editing the public privacy policy (/privacy), inside the admin shell.</summary>
[Authorize(Roles = "Admin")]
public class PrivacyAdminController : Controller
{
    private readonly ISitePageFacade _pages;
    private readonly IPublicContentFacade _content;
    private readonly IOutputCacheStore _cache;
    private readonly IConfiguration _config;

    public PrivacyAdminController(ISitePageFacade pages, IPublicContentFacade content, IOutputCacheStore cache, IConfiguration config)
    {
        _pages = pages; _content = content; _cache = cache; _config = config;
    }

    [HttpGet("Admin/PrivacyPolicy")]
    public async Task<IActionResult> PrivacyPolicy(CancellationToken ct)
    {
        var saved = await _pages.GetAsync(SitePages.Privacy, ct);
        var (title, body) = DefaultPages.Privacy(saved);
        return View("~/Views/Admin/EditPrivacy.cshtml", new EditPrivacyViewModel
        {
            Title = title,
            Body = body,
            UpdatedAt = saved?.UpdatedAt,
            Preview = await PreviewAsync(body, ct),
        });
    }

    [HttpPost("Admin/PrivacyPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrivacyPolicy(EditPrivacyViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Body))
            ModelState.AddModelError(nameof(model.Body), "Write the policy, or use \"Reset to the built-in text\".");
        // Measured as stored: browsers send each line break as two characters, saving keeps one.
        else if (model.Body.Replace("\r\n", "\n").Trim().Length > SitePageFacade.MaxBodyLength)
            ModelState.AddModelError(nameof(model.Body), $"Keep the policy under {SitePageFacade.MaxBodyLength:N0} characters.");
        if (!ModelState.IsValid)
        {
            model.UpdatedAt = (await _pages.GetAsync(SitePages.Privacy, ct))?.UpdatedAt;
            model.Preview = await PreviewAsync(model.Body ?? "", ct);
            return View("~/Views/Admin/EditPrivacy.cshtml", model);
        }
        await _pages.SaveAsync(SitePages.Privacy, model.Title ?? "", model.Body!, ct);
        await _cache.EvictByTagAsync("content", ct);
        TempData["Message"] = "Privacy policy saved. It's live at /privacy.";
        return RedirectToAction(nameof(PrivacyPolicy));
    }

    [HttpPost("Admin/PrivacyPolicy/Reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await _pages.ResetAsync(SitePages.Privacy, ct);
        await _cache.EvictByTagAsync("content", ct);
        TempData["Message"] = "Privacy policy reset to the built-in text.";
        return RedirectToAction(nameof(PrivacyPolicy));
    }

    private async Task<IReadOnlyList<RichText.Block>> PreviewAsync(string body, CancellationToken ct)
        => RichText.Parse(body, DefaultPages.PrivacyValues(await _content.GetBioAsync(ct), TelemetryRetentionService.RetentionDays(_config)));
}

/// <summary>The privacy policy editor.</summary>
public class EditPrivacyViewModel
{
    [System.ComponentModel.DataAnnotations.StringLength(Portfolio.Application.Facades.SitePageFacade.MaxTitleLength)]
    public string? Title { get; set; }

    /// <summary>Length is checked in the controller after line endings are normalised.</summary>
    public string? Body { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<RichText.Block> Preview { get; set; } = [];
}
