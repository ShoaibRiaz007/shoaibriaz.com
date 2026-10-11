using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Api;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Domain.Entities;
using Portfolio.Hosting;
using Portfolio.Infrastructure.Persistence;
using Portfolio.Infrastructure.Security;
using Portfolio.Telemetry;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

/// <summary>Text that can't be stored: a lone surrogate isn't valid UTF-8, so Postgres would refuse the row.</summary>
public class TelemetryTextTests
{
    private static bool HasLoneSurrogate(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { i++; continue; }
            if (char.IsSurrogate(s[i])) return true;
        }
        return false;
    }

    [Fact]
    public void Clean_DoesNotSplitAnEmojiAtTheLimit()
    {
        var cleaned = TelemetryFacade.Clean(new string('a', 99) + "😀", 100)!;
        Assert.Equal(new string('a', 99), cleaned);
        Assert.Equal("ab😀", TelemetryFacade.Clean("ab😀", 4));
    }

    [Fact]
    public void Clean_DropsUnpairedSurrogates()
        => Assert.Equal("x😀y", TelemetryFacade.Clean("x\uD800😀\uDC00y", 100));

    [Fact]
    public async Task Record_APathCutAtTheLimit_StaysValidText()
    {
        var store = new FakeTelemetryStore();
        var facade = new TelemetryFacade(store, new FakeVisitorHasher(), new FakeClock(DateTimeOffset.UtcNow));
        await facade.RecordPageViewAsync(
            new BeaconInput("/" + new string('p', 298) + "😀", null, null, null, null, new string('u', 99) + "😀", null, null),
            new VisitContext("198.51.100.7", "Mozilla/5.0 Firefox/131.0", null, "shoaibriaz.com"));

        var v = Assert.Single(store.PageViews);
        Assert.False(HasLoneSurrogate(v.Path));
        Assert.False(HasLoneSurrogate(v.UtmSource!));
        Assert.False(HasLoneSurrogate(v.Source));
    }
}

public class VisitorSaltTests
{
    [Fact]
    public void AClockCorrectedBackwards_GetsAFreshSalt_SoNoSaltServesTwoDates()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));   // running ahead
        var hasher = new DailySaltVisitorHasher(clock);
        var ahead = hasher.Hash("203.0.113.42", "UA");

        clock.Now = clock.Now.AddDays(-2);   // NTP steps it back

        Assert.NotEqual(ahead, hasher.Hash("203.0.113.42", "UA"));
    }
}

/// <summary>POST /api/t: the guards in the order the endpoint applies them.</summary>
public class TelemetryEndpointTests
{
    private const string Host = "shoaibriaz.com";
    private readonly FakeTelemetryStore _store = new();

    private async Task<int?> Post(Action<HttpRequest>? arrange = null, ClaimsPrincipal? user = null, string json = """{"path":"/projects"}""")
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Host = new HostString(Host);
        http.Request.ContentType = "application/json";
        http.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0) Firefox/131.0";
        var body = Encoding.UTF8.GetBytes(json);
        http.Request.Body = new MemoryStream(body);
        http.Request.ContentLength = body.Length;
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.7");
        if (user is not null) http.User = user;
        arrange?.Invoke(http.Request);

        var controller = new TelemetryController(new TelemetryFacade(_store, new FakeVisitorHasher(), new FakeClock(DateTimeOffset.UtcNow)))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        var result = await controller.Collect(CancellationToken.None);
        return (result as IStatusCodeActionResult)?.StatusCode;
    }

    [Fact]
    public async Task ASameOriginReport_IsRecorded()
    {
        Assert.Equal(204, await Post(r => r.Headers.Origin = "https://" + Host));
        Assert.Equal("/projects", Assert.Single(_store.PageViews).Path);
    }

    [Theory]
    [InlineData("https://evil.example", null)]
    [InlineData(null, "cross-site")]
    public async Task ACrossSiteReport_IsRefused(string? origin, string? fetchSite)
    {
        Assert.Equal(403, await Post(r =>
        {
            if (origin is not null) r.Headers.Origin = origin;
            if (fetchSite is not null) r.Headers["Sec-Fetch-Site"] = fetchSite;
        }));
        Assert.Empty(_store.PageViews);
    }

    [Theory]
    [InlineData("DNT")]
    [InlineData("Sec-GPC")]
    public async Task AnOptedOutVisitor_IsNotRecorded(string header)
    {
        Assert.Equal(204, await Post(r => r.Headers[header] = "1"));
        Assert.Empty(_store.PageViews);
    }

    [Fact]
    public async Task AnOptedOutVisitor_IsAnsweredBeforeTheBodyIsRead()
        => Assert.Equal(204, await Post(r => r.Headers["Sec-GPC"] = "1", json: "not json"));

    [Fact]
    public async Task TheSignedInOwner_IsNotRecorded()
    {
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "cookie"));
        Assert.Equal(204, await Post(user: admin));
        Assert.Empty(_store.PageViews);
    }
}

/// <summary>The rate limiter's rejection hook: only the login policy writes to the login log.</summary>
public class LoginRejectionTests
{
    private const string Admin = "owner";
    private readonly FakeTelemetryStore _store = new();

    private sealed class NoLease : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [];
        public override bool TryGetMetadata(string metadataName, out object? metadata) { metadata = null; return false; }
    }

    private async Task Reject(string policy, string username)
    {
        var admins = new FakeRepository<AdminUser>();
        admins.Items.Add(new AdminUser { Id = 1, Username = Admin, PasswordHash = "x", SecurityStamp = "s" });
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryFacade>(new TelemetryFacade(_store, new FakeVisitorHasher(), new FakeClock(DateTimeOffset.UtcNow)))
            .AddSingleton<IAuthService>(new AuthService(admins, new PasswordHasher()))
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Username={Uri.EscapeDataString(username)}&Password=secret-value"));
        http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new EnableRateLimitingAttribute(policy)), "test"));

        await LoginRejectionRecorder.OnRejectedAsync(new OnRejectedContext { HttpContext = http, Lease = new NoLease() },
            $"partition-{Guid.NewGuid():N}", CancellationToken.None);
    }

    [Fact]
    public async Task ARejectedBeacon_IsNotALoginAttempt()
    {
        await Reject(TelemetryController.RateLimitPolicy, Admin);
        Assert.Empty(_store.LoginAttempts);
    }

    [Fact]
    public async Task ARejectedSignIn_IsLoggedAsRateLimited_WithoutThePassword()
    {
        await Reject(LoginRejectionRecorder.LoginPolicy, Admin);

        var a = Assert.Single(_store.LoginAttempts);
        Assert.Equal(Admin, a.Username);
        Assert.Equal(LoginFailureReasons.RateLimited, a.FailureReason);
        Assert.DoesNotContain(typeof(LoginAttempt).GetProperties(), p => p.GetValue(a)?.ToString()?.Contains("secret-value") == true);
    }

    [Fact]
    public async Task AUsernameThatIsNotAnAccount_IsNotStored()
    {
        await Reject(LoginRejectionRecorder.LoginPolicy, "my-real-password");
        Assert.Equal(string.Empty, Assert.Single(_store.LoginAttempts).Username);
    }

    [Fact]
    public async Task TheAccountIsRecorded_NotTheTypedVariant()
    {
        await Reject(LoginRejectionRecorder.LoginPolicy, "  OWNER ");
        Assert.Equal(Admin, Assert.Single(_store.LoginAttempts).Username);
    }
}

public class TelemetryRetentionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TelemetryRetentionTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private static PageView View(int daysAgo, string path) => new()
    {
        OccurredAt = Now.UtcDateTime.AddDays(-daysAgo), Path = path, Source = "Direct", VisitorHash = "v-" + path, Device = "Desktop",
    };

    [Fact]
    public async Task TheDailySweep_RemovesOnlyRowsPastTheRetentionPeriod()
    {
        await using (var db = NewContext())
        {
            db.PageViews.AddRange(View(181, "/old"), View(179, "/kept"));
            db.LoginAttempts.AddRange(
                new LoginAttempt { OccurredAt = Now.UtcDateTime.AddDays(-200), Username = "old" },
                new LoginAttempt { OccurredAt = Now.UtcDateTime.AddDays(-1), Username = "kept" });
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection().AddScoped<ITelemetryStore>(_ => new EfTelemetryStore(NewContext())).BuildServiceProvider();
        var sweep = new TelemetryRetentionService(services, new ConfigurationBuilder().Build(), new FakeClock(Now),
            NullLogger<TelemetryRetentionService>.Instance);

        Assert.Equal(2, await sweep.SweepAsync(CancellationToken.None));

        await using var check = NewContext();
        Assert.Equal("/kept", Assert.Single(check.PageViews).Path);
        Assert.Equal("kept", Assert.Single(check.LoginAttempts).Username);
    }
}
