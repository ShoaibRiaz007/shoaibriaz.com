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
using Portfolio.Telemetry;

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
builder.Services.AddSingleton<Portfolio.Caching.NotFoundCache>();
builder.Services.AddControllersWithViews(options =>
{
    // Entities are bound directly in the admin forms; with <Nullable> on, non-nullable string
    // properties would otherwise be treated as implicitly required, blocking blank optional fields.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    // Keep blank optional fields as "" (not null) so they satisfy the NOT NULL string columns.
    options.ModelMetadataDetailsProviders.Add(new Portfolio.ModelBinding.KeepEmptyStringsMetadataProvider());
    // <input type="month"> posts "yyyy-MM", which the default DateOnly binder rejects.
    options.ModelBinderProviders.Insert(0, new Portfolio.ModelBinding.MonthDateOnlyModelBinderProvider());
});
builder.Services.AddHostedService<MigrateAndSeedHostedService>();

// ---- Cookie-free visitor analytics + admin login log (POST /api/t, /Admin/Activity) ----
builder.Services.AddTelemetryInfrastructure();
builder.Services.AddTelemetryApplication();
builder.Services.AddHostedService<TelemetryRetentionService>();

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
// Partitioned on the client IP that UseForwardedHeaders takes from X-Forwarded-For. With ForwardLimit = 1 only the
// rightmost entry counts, which is the one the platform proxy appends for the connection it accepted, so a client
// can't choose it by sending its own header through the proxy. The raw socket address is the proxy's own and is
// shared by every visitor: keyed on that, one bucket would serve the whole site and anyone could keep the owner from
// signing in. If the app were ever reachable without the proxy, a client could rotate the header for fresh buckets;
// AuthService's per-username lockout is the backstop no header can reset.
static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(LoginRejectionRecorder.LoginPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIp(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    // Page-view beacons: keyed on the client IP the same way.
    options.AddPolicy(Portfolio.Api.TelemetryController.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIp(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.OnRejected = (context, ct) => LoginRejectionRecorder.OnRejectedAsync(context, ClientIp(context.HttpContext), ct);
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
    // Other sites learn nothing about where their visitors came from; the site's own pages see the referrer, so the
    // visit beacon can tell a click inside the site from a new arrival. One exception: embedded YouTube/Vimeo players
    // are sent the site's origin (Views/Home/_VideoFrame.cshtml), which they need before they will play.
    headers["Referrer-Policy"] = "same-origin";
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "img-src 'self' data: https:; " +
        "media-src 'self' https://res.cloudinary.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "script-src 'self'; " +
        "frame-src https://www.youtube-nocookie.com https://player.vimeo.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

// Serves /world/index.html for /world (the 3D tour, built from src/Portfolio.World); the redirect from
// /world to /world/ keeps its relative asset URLs working.
app.UseDefaultFiles();
app.UseStaticFiles();

// Unmatched public URLs get the designed 404 page (only real misses; see PublicNotFoundPages).
app.UsePublicNotFoundPages();

app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Liveness for the host and the keep-alive ping. Deliberately touches no database, so pings keep the
// web instance warm without spending the database's compute allowance.
app.MapGet("/health", () => Results.Text("ok"));

app.Run();
