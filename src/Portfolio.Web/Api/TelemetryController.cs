using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;
using Portfolio.Application.Telemetry;
using Portfolio.Telemetry;

namespace Portfolio.Api;

/// <summary>
/// POST /api/t: the cookie-free page-view beacon. Anonymous and exempt from antiforgery (it stores nothing a forged
/// request could abuse), so it instead insists on a small JSON body from the same origin, and is rate-limited per IP.
/// Always answers 204 for anything it chooses to ignore (bots, signed-in admins, Do Not Track / Global Privacy
/// Control), so callers learn nothing. The opt-out is enforced here as well as in the scripts, so no client can miss it.
/// </summary>
[Route("api")]
public class TelemetryController : ControllerBase
{
    public const string RateLimitPolicy = "beacon";

    private readonly ITelemetryFacade _telemetry;

    public TelemetryController(ITelemetryFacade telemetry) => _telemetry = telemetry;

    [HttpPost("t")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Collect(CancellationToken ct)
    {
        if (!BeaconRules.IsJsonContentType(Request.ContentType)) return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (!BeaconRules.IsSameOrigin(Request.Headers.Origin, Request.Headers["Sec-Fetch-Site"], Request.Host.Value ?? ""))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (Request.ContentLength > TelemetryLimits.MaxBeaconBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        // The visitor opted out, or it's the owner browsing their own site: nothing to read or record.
        if (Request.Headers["DNT"] == "1" || Request.Headers["Sec-GPC"] == "1" || User.IsInRole("Admin")) return NoContent();

        var body = await BeaconRules.ReadBoundedAsync(Request.Body, ct);
        if (body is null) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        var payload = BeaconRules.Parse(body);
        if (payload is null) return BadRequest();

        // The forwarded client IP (set by UseForwardedHeaders) is hashed with a daily salt and never stored.
        await _telemetry.RecordPageViewAsync(payload.ToInput(), new VisitContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent,
            Request.Headers.AcceptLanguage,
            Request.Host.Host), ct);
        return NoContent();
    }
}
