using System.Collections.Concurrent;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Entities;

namespace Portfolio.Application.Facades;

public class AuthService : IAuthService
{
    // Per-username lockout: independent of client IP, so it cannot be bypassed by spoofing
    // X-Forwarded-For the way an IP-keyed rate limit can.
    // NOTE: this is process-local. Under a horizontally-scaled (multi-instance) deployment an
    // attacker can spread attempts for the same username across instances to bypass the lockout;
    // fixing that requires a shared store (e.g. the database or a distributed cache), which single
    // instance hosting today does not warrant.
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, (int Count, DateTimeOffset WindowStart)> Failures = new();

    // An unauthenticated caller can spray distinct, never-repeated usernames to grow this dictionary
    // without bound. Sweep expired entries out periodically rather than only pruning on next access.
    private const int SweepEveryNFailures = 200;
    private static int _failureCount;

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
        => (await CheckAsync(username, password, ct)).User;

    public async Task<SignInCheck> CheckAsync(string username, string password, CancellationToken ct = default)
    {
        var key = Normalize(username);
        // The attempt is counted before the password is checked, in one atomic step with the limit test: otherwise
        // parallel requests would all read "under the limit" while each spends ~100 ms in PBKDF2, and a burst could
        // test any number of passwords. A correct password clears the count.
        if (CountAttempt(key) > MaxFailures) return new SignInCheck(null, LockedOut: true);

        var user = await _admins.FirstOrDefaultAsync(u => u.Username == username, ct);
        var ok = user is not null
            ? _hasher.Verify(password, user.PasswordHash)
            : VerifyDummyAndFail(password);
        if (!ok) return new SignInCheck(null, LockedOut: false);

        Failures.TryRemove(key, out _);
        return new SignInCheck(new AuthResult(user!.Username, user.SecurityStamp), LockedOut: false);
    }

    public async Task<string?> FindAccountNameAsync(string username, CancellationToken ct = default)
    {
        var key = Normalize(username);
        if (key.Length == 0) return null;
        return (await _admins.FirstOrDefaultAsync(u => u.Username.Trim().ToLower() == key, ct))?.Username;
    }

    private static string Normalize(string username) => username.Trim().ToLowerInvariant();

    public async Task<bool> ValidateSecurityStampAsync(string username, string securityStamp, CancellationToken ct = default)
    {
        var user = await _admins.FirstOrDefaultAsync(u => u.Username == username, ct);
        return user is not null && user.SecurityStamp == securityStamp;
    }

    /// <summary>Runs a real PBKDF2 verification against a throwaway hash so an unknown username costs
    /// the same wall-clock time as a known one (no timing-based username enumeration), then always fails.</summary>
    private bool VerifyDummyAndFail(string password)
    {
        _hasher.Verify(password, _dummyHash!);
        return false;
    }

    /// <summary>Counts one attempt in the current window and returns the count including it; the first
    /// <see cref="MaxFailures"/> attempts of a window get a password check, later ones are refused.</summary>
    private static int CountAttempt(string key)
    {
        var entry = Failures.AddOrUpdate(key,
            _ => (1, DateTimeOffset.UtcNow),
            (_, f) => DateTimeOffset.UtcNow - f.WindowStart >= LockoutWindow
                ? (1, DateTimeOffset.UtcNow)
                : (f.Count + 1, f.WindowStart));

        if (Interlocked.Increment(ref _failureCount) % SweepEveryNFailures == 0)
            SweepExpired();
        return entry.Count;
    }

    private static void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, value) in Failures)
            if (now - value.WindowStart >= LockoutWindow)
                Failures.TryRemove(key, out _);
    }
}
