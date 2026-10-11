using Microsoft.AspNetCore.Mvc;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;
using Portfolio.Controllers;
using Portfolio.Models;

namespace Portfolio.ViewComponents;

/// <summary>The admin sidebar with per-section counts. Reuses the snapshot the action already loaded when there
/// is one (AdminController.DashboardItemKey), otherwise runs count-only queries.</summary>
public class AdminNavViewComponent : ViewComponent
{
    private readonly IAdminContentFacade _admin;

    public AdminNavViewComponent(IAdminContentFacade admin) => _admin = admin;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var stats = HttpContext.Items[AdminController.DashboardItemKey] is AdminDashboard dashboard
            ? AdminOverview.Stats(dashboard)
            : AdminOverview.Stats(await _admin.GetContentCountsAsync(HttpContext.RequestAborted));
        var action = RouteData.Values["action"] as string;
        var slug = action == nameof(AdminController.List)
            ? (RouteData.Values["kind"] as string)?.ToLowerInvariant()
            : AdminOverview.Sections.FirstOrDefault(s => s.EditAction == action)?.Slug;
        return View(new AdminNavViewModel(stats, slug, action));
    }
}
