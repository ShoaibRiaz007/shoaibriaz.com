using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Models;

namespace Portfolio.Controllers;

/// <summary>Visitor analytics and the login-attempt log, inside the admin shell. The action is named Activity (not
/// Index) so the sidebar can tell it apart from the dashboard by action name.</summary>
[Authorize(Roles = "Admin")]
public class ActivityController : Controller
{
    private readonly ITelemetryFacade _telemetry;
    private readonly IAdminContentFacade _admin;

    public ActivityController(ITelemetryFacade telemetry, IAdminContentFacade admin)
    {
        _telemetry = telemetry; _admin = admin;
    }

    [HttpGet("Admin/Activity")]
    public async Task<IActionResult> Activity(int days = 7, int visits = 1, string? vq = null, int logins = 1, string? lq = null,
        CancellationToken ct = default)
    {
        days = TelemetryFacade.NormalizeRange(days);
        var report = await _telemetry.GetActivityAsync(days, ct, new TableQuery(visits, vq, logins, lq));
        var views = await _telemetry.GetProjectViewsAsync(days, ct);
        var dashboard = await AdminController.LoadDashboardAsync(HttpContext, _admin, ct);
        return View("~/Views/Admin/Activity.cshtml", new ActivityViewModel(report, ActivityViewModel.ProjectRows(dashboard.Projects, views)));
    }
}
