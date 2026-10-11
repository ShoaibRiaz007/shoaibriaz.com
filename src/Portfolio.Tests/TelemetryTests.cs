using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Application.Telemetry;
using Portfolio.Controllers;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Security;
using Portfolio.Models;
using Portfolio.Telemetry;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

public class TrafficSourceTests
{
    [Theory]
    [InlineData("linkedin.com", null, "LinkedIn")]
    [InlineData("lnkd.in", null, "LinkedIn")]
    [InlineData("google.co.uk", null, "Google")]
    [InlineData("google.com.pk", null, "Google")]
    [InlineData("mail.google.com", null, "Email")]
    [InlineData("bing.com", null, "Bing")]
    [InlineData("gist.github.com", null, "GitHub")]
    [InlineData("t.co", null, "X/Twitter")]
    [InlineData("l.facebook.com", null, "Facebook")]
    [InlineData("old.reddit.com", null, "Reddit")]
    [InlineData("youtu.be", null, "YouTube")]
    [InlineData("com.linkedin.android", null, "LinkedIn")]
    [InlineData("example.org", null, "Other")]
    [InlineData("notgoogle", null, "Other")]
    [InlineData(null, null, "Direct")]
    [InlineData("google.com", "linkedin", "LinkedIn")]
    [InlineData(null, "newsletter", "Email")]
    [InlineData("google.com", "conference-badge", "conference-badge")]
    public void Classify_UsesReferrerHost_AndUtmSourceWins(string? host, string? utm, string expected)
        => Assert.Equal(expected, TrafficSources.Classify(host, utm));

    [Fact]
    public void Classify_TruncatesAnUnknownUtmSource()
        => Assert.Equal(TrafficSources.MaxSourceLength, TrafficSources.Classify(null, new string('x', 200)).Length);

    [Fact]
    public void CleanReferrer_DropsQueryExceptUtm_AndFragment_AndWww()
    {
        var (host, url) = TrafficSources.CleanReferrer(
            "https://www.LinkedIn.com/feed/update/123?trk=abc&utm_source=li&session=secret#comments", "shoaibriaz.com");
        Assert.Equal("linkedin.com", host);
        Assert.Equal("https://www.linkedin.com/feed/update/123?utm_source=li", url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://shoaibriaz.com/projects")]
    public void CleanReferrer_IgnoresEmptyUnsafeAndSameSite(string referrer)
        => Assert.Equal((null, null), TrafficSources.CleanReferrer(referrer, "shoaibriaz.com"));

    [Fact]
    public void CleanReferrer_TruncatesLongUrls()
    {
        var (_, url) = TrafficSources.CleanReferrer("https://example.org/" + new string('a', 2000), null);
        Assert.Equal(TelemetryLimits.Referrer, url!.Length);
    }
}

public class UserAgentTests
{
    private const string IPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";
    private const string IPad = "Mozilla/5.0 (iPad; CPU OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";
    private const string AndroidPhone = "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36";
    private const string AndroidTablet = "Mozilla/5.0 (Linux; Android 13; SM-X700) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
    private const string WindowsEdge = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36 Edg/129.0.0.0";
    private const string MacFirefox = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.5; rv:131.0) Gecko/20100101 Firefox/131.0";
    private const string Cubot = "Mozilla/5.0 (Linux; Android 12; CUBOT KINGKONG 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    [InlineData("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; bingbot/2.0)")]
    [InlineData("curl/8.5.0")]
    [InlineData("python-requests/2.32.3")]
    [InlineData("Go-http-client/1.1")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/129.0.0.0 Safari/537.36")]
    [InlineData("Mozilla/5.0 (Linux; Android 11; moto g power (2022)) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36 Chrome-Lighthouse")]
    [InlineData("facebookexternalhit/1.1")]
    [InlineData("")]
    [InlineData(null)]
    public void IsBot_FlagsCrawlersToolsAndMissingAgents(string? ua) => Assert.True(UserAgents.IsBot(ua));

    [Theory]
    [InlineData(IPhone)]
    [InlineData(WindowsEdge)]
    [InlineData(Cubot)]
    public void IsBot_LetsRealBrowsersThrough(string ua) => Assert.False(UserAgents.IsBot(ua));

    [Theory]
    [InlineData(IPhone, "Mobile", "Safari", "iOS")]
    [InlineData(IPad, "Tablet", "Safari", "iOS")]
    [InlineData(AndroidPhone, "Mobile", "Chrome", "Android")]
    [InlineData(AndroidTablet, "Tablet", "Chrome", "Android")]
    [InlineData(WindowsEdge, "Desktop", "Edge", "Windows")]
    [InlineData(MacFirefox, "Desktop", "Firefox", "macOS")]
    public void Parse_GivesDeviceBrowserAndOs(string ua, string device, string browser, string os)
        => Assert.Equal(new UserAgentInfo(device, browser, os), UserAgents.Parse(ua));
}

public class CountryTests
{
    [Theory]
    [InlineData("Asia/Karachi", "PK")]
    [InlineData("America/New_York", "US")]
    [InlineData("Europe/London", "GB")]
    [InlineData("Asia/Kolkata", "IN")]
    [InlineData("Asia/Calcutta", "IN")]
    [InlineData("Europe/Kiev", "UA")]
    [InlineData("America/Argentina/Buenos_Aires", "AR")]
    [InlineData("asia/dubai", "AE")]
    [InlineData("UTC", null)]
    [InlineData("Etc/GMT+5", null)]
    [InlineData("Mars/Olympus_Mons", null)]
    [InlineData(null, null)]
    public void CountryOf_MapsIanaZones(string? zone, string? expected)
        => Assert.Equal(expected, TimeZoneCountries.CountryOf(zone));

    [Fact]
    public void Map_CoversAllOfZoneTab() => Assert.True(TimeZoneCountries.Count >= 400);

    [Theory]
    [InlineData("en-GB", "GB")]
    [InlineData("zh-Hant-TW", "TW")]
    [InlineData("fr-CA,fr;q=0.9,en;q=0.8", "CA")]
    [InlineData("es-419", null)]
    [InlineData("en", null)]
    [InlineData(null, null)]
    public void FromLanguageTag_ReadsTheRegion(string? tag, string? expected)
        => Assert.Equal(expected, Countries.FromLanguageTag(tag));

    [Fact]
    public void Flag_IsTwoRegionalIndicators()
    {
        Assert.Equal("\U0001F1F5\U0001F1F0", Countries.Flag("PK"));
        Assert.Equal("", Countries.Flag("P1"));
        Assert.Equal("", Countries.Flag(null));
    }
}

public class VisitorHasherTests
{
    private const string Ip = "203.0.113.42";
    private const string Ua = "Mozilla/5.0 Test";

    [Fact]
    public void Hash_IsStableWithinADay_AndExposesNoInput()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));
        var hasher = new DailySaltVisitorHasher(clock);

        var morning = hasher.Hash(Ip, Ua);
        clock.Now = clock.Now.AddHours(14);
        var evening = hasher.Hash(Ip, Ua);

        Assert.Equal(morning, evening);
        Assert.Equal(DailySaltVisitorHasher.HashLength, morning.Length);
        Assert.Matches("^[0-9a-f]+$", morning);
        Assert.DoesNotContain(Ip, morning);
        Assert.NotEqual(morning, hasher.Hash("203.0.113.43", Ua));
        Assert.NotEqual(morning, hasher.Hash(Ip, Ua + " other"));
    }

    [Fact]
    public void Hash_RotatesAtUtcMidnight()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 11, 23, 59, 0, TimeSpan.Zero));
        var hasher = new DailySaltVisitorHasher(clock);
        var before = hasher.Hash(Ip, Ua);

        clock.Now = clock.Now.AddMinutes(2);

        Assert.NotEqual(before, hasher.Hash(Ip, Ua));
    }

    [Fact]
    public void Hash_SaltIsNotDerivableAcrossInstances()
    {
        // Two processes on the same day (e.g. before/after a restart) can't reproduce each other's ids, which shows
        // the salt is random rather than derived from the date.
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        Assert.NotEqual(new DailySaltVisitorHasher(clock).Hash(Ip, Ua), new DailySaltVisitorHasher(clock).Hash(Ip, Ua));
    }
}

public class BeaconTests
{
    private const string Browser = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTelemetryStore _store = new();
    private TelemetryFacade Sut() => new(_store, new FakeVisitorHasher(), new FakeClock(Now));
    private static VisitContext Ctx(string? ua = Browser, string? acceptLanguage = null)
        => new("198.51.100.7", ua, acceptLanguage, "shoaibriaz.com");

    [Fact]
    public async Task Record_StoresACleanedEnrichedView_WithoutTheIp()
    {
        var recorded = await Sut().RecordPageViewAsync(new BeaconInput(
            "/projects?ref=x#top", "https://www.linkedin.com/feed/?trk=1", "Asia/Karachi", "en-US", 1440,
            "linkedin", "social", "launch"), Ctx());

        Assert.True(recorded);
        var v = Assert.Single(_store.PageViews);
        Assert.Equal(Now.UtcDateTime, v.OccurredAt);
        Assert.Equal("/projects", v.Path);
        Assert.Equal("linkedin.com", v.ReferrerHost);
        Assert.Equal("https://www.linkedin.com/feed/", v.Referrer);
        Assert.Equal("LinkedIn", v.Source);
        Assert.Equal(("linkedin", "social", "launch"), (v.UtmSource, v.UtmMedium, v.UtmCampaign));
        Assert.Equal("PK", v.Country);
        Assert.Equal("Asia/Karachi", v.TimeZone);
        Assert.Equal(("Desktop", "Chrome", "Windows"), (v.Device, v.Browser, v.Os));
        Assert.Equal(1440, v.ScreenWidth);
        Assert.False(v.IsIsland);
        Assert.Equal("visitor-1", v.VisitorHash);
        // No column holds the client IP.
        Assert.DoesNotContain(typeof(PageView).GetProperties(), p => p.GetValue(v)?.ToString()?.Contains("198.51.100.7") == true);
    }

    [Fact]
    public async Task Record_AClickFromTheSitesOwnPage_IsInternal()
    {
        await Sut().RecordPageViewAsync(new BeaconInput("/cv", "https://shoaibriaz.com/projects", null, null, null, "linkedin", null, null), Ctx());

        var v = Assert.Single(_store.PageViews);
        Assert.Equal(TrafficSources.Internal, v.Source);
        Assert.Null(v.ReferrerHost);
    }

    [Fact]
    public async Task Record_TruncatesAndDropsInvalidFields()
    {
        await Sut().RecordPageViewAsync(new BeaconInput(
            "/" + new string('p', 1000), null, "Not a zone; DROP TABLE", "<script>", 999_999,
            new string('s', 500), "m\u0000ail", new string('c', 500)), Ctx(acceptLanguage: "de-DE,de;q=0.9"));

        var v = Assert.Single(_store.PageViews);
        Assert.Equal(TelemetryLimits.Path, v.Path.Length);
        Assert.Null(v.TimeZone);
        Assert.Null(v.Language);
        Assert.Null(v.ScreenWidth);
        Assert.Equal(TelemetryLimits.Utm, v.UtmSource!.Length);
        Assert.Equal("mail", v.UtmMedium);
        Assert.Equal(TelemetryLimits.Utm, v.UtmCampaign!.Length);
        Assert.Equal(TrafficSources.MaxSourceLength, v.Source.Length);
        Assert.Equal("DE", v.Country);   // no zone or language: falls back to Accept-Language
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("projects")]
    [InlineData("//evil.example/x")]
    [InlineData("/has space")]
    [InlineData("/Admin")]
    [InlineData("/admin/list/projects")]
    [InlineData("/Account/Login")]
    [InlineData("/api/site")]
    public async Task Record_IgnoresUnusableOrPrivatePaths(string? path)
    {
        Assert.False(await Sut().RecordPageViewAsync(new BeaconInput(path), Ctx()));
        Assert.Empty(_store.PageViews);
    }

    [Fact]
    public async Task Record_IgnoresBots()
    {
        Assert.False(await Sut().RecordPageViewAsync(new BeaconInput("/"), Ctx("Mozilla/5.0 (compatible; Googlebot/2.1)")));
        Assert.Empty(_store.PageViews);
    }

    [Fact]
    public async Task Record_MarksIslandVisits()
    {
        await Sut().RecordPageViewAsync(new BeaconInput("/world/"), Ctx());
        Assert.True(Assert.Single(_store.PageViews).IsIsland);
    }

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("application/json; charset=utf-8", true)]
    [InlineData("text/plain;charset=UTF-8", false)]
    [InlineData("application/x-www-form-urlencoded", false)]
    [InlineData(null, false)]
    public void Rules_RequireJson(string? contentType, bool ok) => Assert.Equal(ok, BeaconRules.IsJsonContentType(contentType));

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("https://shoaibriaz.com", "same-origin", true)]
    [InlineData(null, "same-origin", true)]
    [InlineData("https://shoaibriaz.com", null, true)]
    [InlineData("https://evil.example", null, false)]
    [InlineData("https://shoaibriaz.com.evil.example", "same-origin", false)]
    [InlineData("null", null, false)]
    [InlineData(null, "cross-site", false)]
    [InlineData("https://shoaibriaz.com", "same-site", false)]
    public void Rules_RequireSameOriginWhenTheBrowserSaysWhere(string? origin, string? fetchSite, bool ok)
        => Assert.Equal(ok, BeaconRules.IsSameOrigin(origin, fetchSite, "shoaibriaz.com"));

    [Fact]
    public async Task Rules_ReadAtMostTwoKilobytes()
    {
        Assert.NotNull(await BeaconRules.ReadBoundedAsync(new MemoryStream(new byte[TelemetryLimits.MaxBeaconBytes]), default));
        Assert.Null(await BeaconRules.ReadBoundedAsync(new MemoryStream(new byte[TelemetryLimits.MaxBeaconBytes + 1]), default));
    }

    [Fact]
    public void Rules_ParseThePayload_AndRejectMalformedJson()
    {
        var p = BeaconRules.Parse(Encoding.UTF8.GetBytes(
            """{"path":"/cv","tz":"Europe/Berlin","lang":"de","sw":390,"utm":{"source":"github","campaign":"readme"}}"""));
        Assert.Equal(new BeaconInput("/cv", null, "Europe/Berlin", "de", 390, "github", null, "readme"), p!.ToInput());

        Assert.Null(BeaconRules.Parse(Encoding.UTF8.GetBytes("{not json")));
        Assert.Null(BeaconRules.Parse(Encoding.UTF8.GetBytes("null")));
        Assert.Null(BeaconRules.Parse(Encoding.UTF8.GetBytes("""{"sw":"wide"}""")));
    }
}

public class LoginAttemptTests
{
    private const string Password = "correct horse battery staple";
    private const string WrongPassword = "Tr0ub4dor&3-wrong";

    private readonly FakeTelemetryStore _store = new();
    private readonly string _username = $"admin-{Guid.NewGuid():N}";   // AuthService lockout state is process-wide
    private readonly ListLogger _log = new();

    private AccountController Sut()
    {
        var hasher = new PasswordHasher();
        var admins = new FakeRepository<AdminUser>();
        admins.Items.Add(new AdminUser { Id = 1, Username = _username, PasswordHash = hasher.Hash(Password), SecurityStamp = "s" });
        var telemetry = new TelemetryFacade(_store, new FakeVisitorHasher(), new FakeClock(DateTimeOffset.UtcNow));

        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService, NoopAuthenticationService>().BuildServiceProvider(),
        };
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.10");
        http.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0) Firefox/131.0";
        http.Request.Headers.AcceptLanguage = "en-PK,en;q=0.9";
        return new AccountController(new AuthService(admins, hasher), telemetry, _log)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            TempData = new TempDataDictionary(http, new NullTempDataProvider()),
            Url = new UrlHelper(new ActionContext(http, new RouteData(), new ActionDescriptor())),
        };
    }

    [Fact]
    public async Task SuccessfulLogin_IsRecorded()
    {
        Assert.IsType<RedirectToActionResult>(await Sut().Login(new LoginViewModel { Username = _username, Password = Password }));

        var a = Assert.Single(_store.LoginAttempts);
        Assert.True(a.Succeeded);
        Assert.Null(a.FailureReason);
        Assert.Equal(_username, a.Username);
        Assert.Equal("192.0.2.10", a.IpAddress);
        Assert.Equal("PK", a.Country);
        Assert.Contains("Firefox", a.UserAgent);
        AssertNoPassword(a, Password);
    }

    [Fact]
    public async Task FailedLogin_IsRecordedWithAReason_AndNoPassword()
    {
        Assert.IsType<ViewResult>(await Sut().Login(new LoginViewModel { Username = _username, Password = WrongPassword }));

        var a = Assert.Single(_store.LoginAttempts);
        Assert.False(a.Succeeded);
        Assert.Equal(LoginFailureReasons.InvalidCredentials, a.FailureReason);
        AssertNoPassword(a, WrongPassword);
    }

    [Fact]
    public async Task LoginDuringLockout_IsRecordedAsLocked()
    {
        // A fresh controller per request, as in production (a failed attempt leaves a ModelState error behind).
        for (var i = 0; i < 5; i++) await Sut().Login(new LoginViewModel { Username = _username, Password = WrongPassword });

        await Sut().Login(new LoginViewModel { Username = _username, Password = Password });

        Assert.Empty(_log.Errors);
        Assert.Equal(6, _store.LoginAttempts.Count);
        Assert.All(_store.LoginAttempts.Take(5), a => Assert.Equal(LoginFailureReasons.InvalidCredentials, a.FailureReason));
        var last = _store.LoginAttempts[^1];
        Assert.False(last.Succeeded);
        Assert.Equal(LoginFailureReasons.Locked, last.FailureReason);
    }

    [Fact]
    public async Task LoginDuringLockout_IsLabelledLocked_WhateverTheCaseOrSpacing()
    {
        for (var i = 0; i < 5; i++) await Sut().Login(new LoginViewModel { Username = _username, Password = WrongPassword });

        await Sut().Login(new LoginViewModel { Username = "  " + _username.ToUpperInvariant() + " ", Password = WrongPassword });

        var last = _store.LoginAttempts[^1];
        Assert.Equal(LoginFailureReasons.Locked, last.FailureReason);
        Assert.Equal(_username, last.Username);   // the account it was for, not the typed variant
    }

    [Fact]
    public async Task APasswordTypedAsTheUsername_IsNotStored_EvenOnceLocked()
    {
        for (var i = 0; i < 6; i++) await Sut().Login(new LoginViewModel { Username = WrongPassword, Password = "x" });

        Assert.Equal(LoginFailureReasons.Locked, _store.LoginAttempts[^1].FailureReason);
        Assert.All(_store.LoginAttempts, a => Assert.Equal(string.Empty, a.Username));
    }

    [Fact]
    public async Task AUsernameThatIsNotAnAccount_IsNotStored()
    {
        // The classic slip: the password typed into the Username box.
        await Sut().Login(new LoginViewModel { Username = Password, Password = Password });

        var a = Assert.Single(_store.LoginAttempts);
        Assert.Equal(string.Empty, a.Username);
        AssertNoPassword(a, Password);
    }

    [Fact]
    public void RateLimitedRows_AreThrottledPerPartition()
    {
        var key = $"203.0.113.{Random.Shared.Next(1, 250)}-{Guid.NewGuid():N}";
        var t = DateTimeOffset.UtcNow;
        Assert.True(LoginRejectionRecorder.ShouldRecord(key, t));
        Assert.False(LoginRejectionRecorder.ShouldRecord(key, t.AddSeconds(30)));
        Assert.True(LoginRejectionRecorder.ShouldRecord(key, t.AddSeconds(61)));
    }

    private static void AssertNoPassword(LoginAttempt a, string password)
        => Assert.DoesNotContain(typeof(LoginAttempt).GetProperties(), p => p.GetValue(a)?.ToString()?.Contains(password) == true);

    /// <summary>Captures errors so a swallowed recording failure fails the test instead of passing silently.</summary>
    private sealed class ListLogger : ILogger<AccountController>
    {
        public readonly List<string> Errors = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error) Errors.Add($"{formatter(state, exception)} {exception}");
        }
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class NoopAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}

public class ActivityReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 15, 0, 0, TimeSpan.Zero);
    private readonly FakeTelemetryStore _store = new();
    private TelemetryFacade Sut() => new(_store, new FakeVisitorHasher(), new FakeClock(Now));

    private void View(int daysAgo, string visitor, string source = "Direct", string path = "/", string? country = null, bool island = false, string? campaign = null)
        => _store.PageViews.Add(new PageView
        {
            Id = _store.PageViews.Count + 1, OccurredAt = Now.UtcDateTime.AddDays(-daysAgo), VisitorHash = visitor, Source = source,
            Path = path, Country = country, IsIsland = island, Device = "Desktop", UtmCampaign = campaign,
        });

    [Fact]
    public async Task GetActivity_AggregatesTheRange_AndFillsEmptyDays()
    {
        View(0, "a", "LinkedIn", "/", "PK");
        View(0, "a", "LinkedIn", "/projects", "PK", campaign: "launch");
        View(0, "b", "Google", "/world/", "US", island: true);
        View(2, "c", "LinkedIn", "/", "PK");
        View(6, "d", "Direct", "/cv", "GB");
        View(7, "old", "Reddit", "/");                  // outside a 7-day range
        _store.LoginAttempts.Add(new LoginAttempt { Id = 1, OccurredAt = Now.UtcDateTime.AddMinutes(-10), Username = "x", Succeeded = false });
        _store.LoginAttempts.Add(new LoginAttempt { Id = 2, OccurredAt = Now.UtcDateTime.AddHours(-30), Username = "y", Succeeded = false });
        _store.LoginAttempts.Add(new LoginAttempt { Id = 3, OccurredAt = Now.UtcDateTime.AddMinutes(-5), Username = "z", Succeeded = true });

        var r = await Sut().GetActivityAsync(7);

        Assert.Equal(7, r.Days);
        Assert.Equal(5, r.PageViews);
        Assert.Equal(4, r.UniqueVisitors);
        Assert.Equal(1, r.IslandVisits);
        Assert.Equal(1, r.FailedLogins24h);
        Assert.Equal(1, r.FailedLoginsLastHour);
        Assert.Equal(7, r.Daily.Count);
        Assert.Equal(new DateOnly(2026, 10, 5), r.Daily[0].Day);
        Assert.Equal(new DailyVisits(new DateOnly(2026, 10, 11), 3, 2), r.Daily[^1]);
        Assert.Equal(0, r.Daily[5].Views);
        Assert.Equal(new LabelCount("LinkedIn", 3), r.Sources[0]);
        Assert.DoesNotContain(r.Sources, s => s.Label == "Reddit");
        Assert.Equal("PK", r.Countries[0].Code);
        Assert.Equal(3, r.Countries[0].Count);
        Assert.Equal(new LabelCount("/", 2), r.Pages[0]);
        Assert.Equal(new LabelCount("launch", 1), Assert.Single(r.Campaigns));
        Assert.Empty(r.ReferrerHosts);
        Assert.Equal(5, r.RecentVisits.Count);
        Assert.Equal(3, r.RecentVisits[0].Id);     // newest first, ties broken by id
        Assert.Equal(3, r.LoginAttempts.Count);    // the 30-hour-old failure is outside "24h" but inside the range
    }

    [Fact]
    public async Task ClicksInsideTheSite_AreNotArrivals()
    {
        View(0, "a", "LinkedIn", "/");
        View(0, "a", TrafficSources.Internal, "/projects");
        View(0, "a", TrafficSources.Internal, "/cv");
        View(0, "b", "Direct", "/");

        var r = await Sut().GetActivityAsync(7);

        Assert.Equal(4, r.PageViews);
        Assert.Equal(2, r.Arrivals);
        Assert.DoesNotContain(r.Sources, s => s.Label == TrafficSources.Internal);
        Assert.Equal("50%", new Portfolio.Models.ActivityViewModel(r).Sources.Share(new Portfolio.Models.BarRow("LinkedIn", 1)));
    }

    [Theory]
    [InlineData(1, 8, "13%")]   // 12.5 rounds up, like 37.5 does
    [InlineData(3, 8, "38%")]
    [InlineData(5, 200, "3%")]
    public void Shares_RoundHalvesUp(int count, int total, string expected)
        => Assert.Equal(expected, new Portfolio.Models.BarList("x", [], total).Share(new Portfolio.Models.BarRow("a", count)));

    [Fact]
    public async Task VisitorSummary_CountsViewsAndDistinctVisitorsInTheRange()
    {
        View(0, "a"); View(0, "a"); View(0, "b"); View(3, "c"); View(10, "d");

        Assert.Equal(new VisitorSummary(7, 4, 3), await Sut().GetVisitorSummaryAsync(7));
        Assert.Equal(new VisitorSummary(30, 5, 4), await Sut().GetVisitorSummaryAsync(30));
    }

    [Theory]
    [InlineData(7, 7)]
    [InlineData(30, 30)]
    [InlineData(90, 90)]
    [InlineData(13, 7)]
    [InlineData(-1, 7)]
    public async Task GetActivity_OnlyOffersTheKnownRanges(int asked, int expected)
        => Assert.Equal(expected, (await Sut().GetActivityAsync(asked)).Daily.Count);

    [Fact]
    public void ViewModel_FormatsTimesAndBars()
    {
        var now = Now.UtcDateTime;
        Assert.Equal("just now", ActivityViewModel.Ago(now.AddSeconds(-20), now));
        Assert.Equal("5 min ago", ActivityViewModel.Ago(now.AddMinutes(-5), now));
        Assert.Equal("3 h ago", ActivityViewModel.Ago(now.AddHours(-3), now));
        Assert.Equal("yesterday 20:30", ActivityViewModel.Ago(new DateTime(2026, 10, 10, 20, 30, 0, DateTimeKind.Utc), now));
        Assert.Equal("2 Oct 08:00", ActivityViewModel.Ago(new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), now));

        var list = new BarList("x", [new BarRow("a", 10), new BarRow("b", 5)], 40);
        Assert.Equal(100, list.WidthPercent(list.Rows[0]));
        Assert.Equal(50, list.WidthPercent(list.Rows[1]));
        Assert.Equal("25%", list.Share(list.Rows[0]));
    }
}
