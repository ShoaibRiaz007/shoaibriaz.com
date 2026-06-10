using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Application;
using Portfolio.Hosting;
using Portfolio.Infrastructure;
using Portfolio.Infrastructure.Persistence;
using Portfolio.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Listen on PORT (default 8080) — honours the PORT env var for container/cloud hosting.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---- Composition root: wire the layers ----
var connectionString = ConnectionStringBuilder.Build(builder.Configuration);
var uploadsPath = Path.Combine(builder.Environment.WebRootPath, "uploads");
var fileStorage = new FileStorageOptions(uploadsPath, "/uploads");
var adminSeed = new AdminSeedOptions(
    builder.Configuration["Admin:Username"] ?? "admin",
    builder.Configuration["Admin:Password"]);
var cloudinary = new CloudinaryOptions(
    builder.Configuration["Cloudinary:CloudName"],
    builder.Configuration["Cloudinary:ApiKey"],
    builder.Configuration["Cloudinary:ApiSecret"]);

builder.Services.AddInfrastructure(connectionString, fileStorage, adminSeed, cloudinary);
builder.Services.AddApplication();
builder.Services.AddControllersWithViews(options =>
{
    // Entities are bound directly in the admin forms; with <Nullable> on, non-nullable string
    // properties would otherwise be treated as implicitly required, blocking blank optional fields.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    // Keep blank optional fields as "" (not null) so they satisfy the NOT NULL string columns.
    options.ModelMetadataDetailsProviders.Add(new Portfolio.ModelBinding.KeepEmptyStringsMetadataProvider());
});
builder.Services.AddHostedService<MigrateAndSeedHostedService>();

// Behind a reverse proxy (cloud/Neon host): trust forwarded scheme so HTTPS detection works.
// KnownNetworks/KnownProxies are cleared because the platform proxy IP isn't static, so a direct
// client could spoof X-Forwarded-For. ForwardLimit = 1 means only the entry appended by the
// nearest hop is honoured, and brute-force protection does not rely on the client IP alone:
// AuthService enforces a per-username lockout that spoofed headers cannot reset.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---- Authentication: secure cookie ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        // Re-validate the security stamp each request so a password change kills every other
        // outstanding session (including a stolen cookie).
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var name = context.Principal?.Identity?.Name;
                var stamp = context.Principal?.FindFirst("ss")?.Value;
                var auth = context.HttpContext.RequestServices
                    .GetRequiredService<Portfolio.Application.Abstractions.IAuthService>();
                if (name is null || stamp is null || !await auth.ValidateSecurityStampAsync(name, stamp))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });
builder.Services.AddAuthorization();

// ---- Brute-force protection on the login endpoint ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ---- Output caching for the public site ----
// Public pages are read-heavy and change only on admin edits; admin POSTs evict the "content" tag.
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("public-content", b => b.Expire(TimeSpan.FromMinutes(10)).Tag("content"));
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ---- Security headers ----
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "img-src 'self' data: https:; " +
        "media-src 'self' https://res.cloudinary.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "script-src 'self'; " +
        "frame-src https://www.youtube.com https://player.vimeo.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
