using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Application.Abstractions;
using Portfolio.Models;

namespace Portfolio.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;

    public AccountController(IAuthService auth) => _auth = auth;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken ct = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.ValidateAsync(model.Username, model.Password, ct);
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
