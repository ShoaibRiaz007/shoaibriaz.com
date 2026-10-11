using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;
using Portfolio.Models;
using Portfolio.Telemetry;

namespace Portfolio.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly ITelemetryFacade _telemetry;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthService auth, ITelemetryFacade telemetry, ILogger<AccountController> logger)
    {
        _auth = auth; _telemetry = telemetry; _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(LoginRejectionRecorder.LoginPolicy)]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken ct = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var check = await _auth.CheckAsync(model.Username, model.Password, ct);
        var result = check.User;
        await RecordAttemptAsync(model.Username, result?.Username, result is not null,
            check.LockedOut ? LoginFailureReasons.Locked : LoginFailureReasons.InvalidCredentials);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(result.Username, result.SecurityStamp));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Admin");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Adds the attempt to the admin login log: the account it was for, never the typed name or the
    /// password. Uses no cancellation token so an aborted request still leaves its record, and a logging failure
    /// (including the account lookup) never blocks signing in.</summary>
    private async Task RecordAttemptAsync(string typed, string? signedInAs, bool succeeded, string failureReason)
    {
        try
        {
            var account = signedInAs ?? await _auth.FindAccountNameAsync(typed);
            await _telemetry.RecordLoginAttemptAsync(new LoginAttemptInput(
                account, succeeded, failureReason,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent, Request.Headers.AcceptLanguage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not record a login attempt.");
        }
    }

    /// <summary>The "ss" claim is re-validated against the stored stamp on every request,
    /// so rotating the stamp (password change) invalidates outstanding cookies.</summary>
    internal static ClaimsPrincipal BuildPrincipal(string username, string securityStamp)
    {
        var identity = new ClaimsIdentity(new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "Admin"),
            new("ss", securityStamp),
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
