using System.Collections.Concurrent;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Application.Abstractions;
using Portfolio.Application.Models;

namespace Portfolio.Telemetry;

/// <summary>
/// Rate limiter OnRejected hook: logs a "rate limited" attempt when the "login" policy turns a sign-in away.
/// At most one row per partition (client IP) per minute, so a flood of rejected requests can't become a flood of inserts.
/// </summary>
public static class LoginRejectionRecorder
{
    public const string LoginPolicy = "login";

    private static readonly TimeSpan Quiet = TimeSpan.FromMinutes(1);
    private const int MaxTracked = 10_000;
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastRecorded = new();

    public static async ValueTask OnRejectedAsync(OnRejectedContext context, string partitionKey, CancellationToken ct)
    {
        var http = context.HttpContext;
        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        if (policy != LoginPolicy || !HttpMethods.IsPost(http.Request.Method)) return;
        if (!ShouldRecord(partitionKey, DateTimeOffset.UtcNow)) return;

        try
        {
            // Only the Username value is used, and only to find the account it refers to: the typed text (often a
            // password in the wrong box) and the password are never stored or logged. No request token from here on:
            // the minute's slot is already claimed, so an aborted request still leaves its row.
            string? account = null;
            if (http.Request.HasFormContentType)
            {
                var typed = (await http.Request.ReadFormAsync(ct))["Username"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(typed))
                    account = await http.RequestServices.GetRequiredService<IAuthService>().FindAccountNameAsync(typed, CancellationToken.None);
            }

            var telemetry = http.RequestServices.GetRequiredService<ITelemetryFacade>();
            await telemetry.RecordLoginAttemptAsync(new LoginAttemptInput(
                account, false, LoginFailureReasons.RateLimited,
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent, http.Request.Headers.AcceptLanguage), CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(LoginRejectionRecorder).FullName!)
                .LogError(ex, "Could not record a rate-limited login attempt.");
        }
    }

    /// <summary>True when this partition hasn't produced a row within the quiet period (and claims it).</summary>
    public static bool ShouldRecord(string partitionKey, DateTimeOffset now)
    {
        // Unbounded keys are possible in principle (one per client); dropping the table only costs a few extra rows.
        if (LastRecorded.Count > MaxTracked) LastRecorded.Clear();
        // AddOrUpdate may run a factory more than once when threads race; each run decides afresh, so only the
        // attempt whose value was actually stored reports the claim.
        var claimed = false;
        LastRecorded.AddOrUpdate(partitionKey,
            _ => { claimed = true; return now; },
            (_, last) => { claimed = now - last >= Quiet; return claimed ? now : last; });
        return claimed;
    }
}
