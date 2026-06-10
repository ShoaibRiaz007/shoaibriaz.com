using System.Collections.Concurrent;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Entities;

namespace Portfolio.Application.Facades;

public class AuthService : IAuthService
{
    // Per-username lockout: independent of client IP, so it cannot be bypassed by spoofing
    // X-Forwarded-For the way an IP-keyed rate limit can.
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, (int Count, DateTimeOffset WindowStart)> Failures = new();

    private readonly IRepository<AdminUser> _admins;
    private readonly IPasswordHasher _hasher;

    // A real hash of a random throwaway password, verified when the username doesn't exist so
    // unknown and known usernames cost the same (no timing-based username enumeration).
    private static string? _dummyHash;

    public AuthService(IRepository<AdminUser> admins, IPasswordHasher hasher)
    {
        _admins = admins; _hasher = hasher;
        _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString("N"));
    }

    public async Task<AuthResult?> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        var key = username.Trim().ToLowerInvariant();
        if (IsLockedOut(key)) return null;

        var user = await _admins.FirstOrDefaultAsync(u => u.Username == username, ct);
        var ok = user is not null
            ? _hasher.Verify(password, user.PasswordHash)
            : _hasher.Verify(password, _dummyHash!) && false; // equalise timing; always fails

        if (!ok)
        {
            RecordFailure(key);
            return null;
        }

        Failures.TryRemove(key, out _);
        return new AuthResult(user!.Username, user.SecurityStamp);
    }

    public async Task<bool> ValidateSecurityStampAsync(string username, string securityStamp, CancellationToken ct = default)
    {
        var user = await _admins.FirstOrDefaultAsync(u => u.Username == username, ct);
        return user is not null && user.SecurityStamp == securityStamp;
    }

    private static bool IsLockedOut(string key)
        => Failures.TryGetValue(key, out var f)
           && f.Count >= MaxFailures
           && DateTimeOffset.UtcNow - f.WindowStart < LockoutWindow;

    private static void RecordFailure(string key)
        => Failures.AddOrUpdate(key,
            _ => (1, DateTimeOffset.UtcNow),
            (_, f) => DateTimeOffset.UtcNow - f.WindowStart >= LockoutWindow
                ? (1, DateTimeOffset.UtcNow)
                : (f.Count + 1, f.WindowStart));
}
