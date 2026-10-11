using System.Security.Cryptography;
using System.Text;
using Portfolio.Application.Abstractions;

namespace Portfolio.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 of (client IP, User-Agent) keyed by a random salt that is replaced when the UTC date changes.
/// The salt exists only in this process's memory and the previous day's is discarded, so once a day ends nobody
/// (including the site owner with full database access) can recompute a hash from a guessed IP. A restart also
/// picks a new salt, which at worst counts a returning visitor twice that day.
/// </summary>
public sealed class DailySaltVisitorHasher : IVisitorHasher
{
    /// <summary>Hex characters kept (128 bits): ample to avoid collisions at portfolio-scale traffic.</summary>
    public const int HashLength = 32;

    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private DateOnly _saltDay;
    private byte[] _salt = [];

    public DailySaltVisitorHasher(TimeProvider clock) => _clock = clock;

    public string Hash(string? clientIp, string? userAgent)
    {
        var salt = SaltForToday();
        var input = Encoding.UTF8.GetBytes($"{clientIp}\n{userAgent}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(salt, input))[..HashLength];
    }

    private byte[] SaltForToday()
    {
        lock (_gate)
        {
            // The clock is read under the lock, so readings are in order and a thread that read it just before
            // midnight can't follow one that read it after and roll the day back. Any date change (including a
            // clock corrected backwards) gets a fresh salt, so no salt ever serves two dates.
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            if (today != _saltDay || _salt.Length == 0)
            {
                _salt = RandomNumberGenerator.GetBytes(32);
                _saltDay = today;
            }
            return _salt;
        }
    }
}
