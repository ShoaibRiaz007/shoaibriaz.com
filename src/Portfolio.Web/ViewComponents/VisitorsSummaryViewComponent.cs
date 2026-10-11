using Microsoft.AspNetCore.Mvc;
using Portfolio.Application.Abstractions;

namespace Portfolio.ViewComponents;

/// <summary>Dashboard stat card linking to the Activity page: page views and daily-unique visitors for 7 days.</summary>
public class VisitorsSummaryViewComponent : ViewComponent
{
    private readonly ITelemetryFacade _telemetry;

    public VisitorsSummaryViewComponent(ITelemetryFacade telemetry) => _telemetry = telemetry;

    public async Task<IViewComponentResult> InvokeAsync()
        => View(await _telemetry.GetVisitorSummaryAsync(7, HttpContext.RequestAborted));
}
