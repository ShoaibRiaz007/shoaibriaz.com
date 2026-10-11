using Portfolio.Application.Models;

namespace Portfolio.Caching;

/// <summary>
/// The 404 page's suggestions and Bio, shared across requests. Bot probes hit the 404 page constantly, so it is
/// loaded at most once per <see cref="Duration"/>; concurrent misses wait for that one load instead of each
/// querying the database, and every admin content change invalidates it alongside the output cache.
/// </summary>
public sealed class NotFoundCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    private sealed record Entry(NotFoundData Data, DateTime ExpiresUtc);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile Entry? _entry;
    private int _version;

    public async Task<NotFoundData> GetAsync(Func<CancellationToken, Task<NotFoundData>> load, CancellationToken ct)
    {
        if (_entry is { } hit && DateTime.UtcNow < hit.ExpiresUtc) return hit.Data;

        await _gate.WaitAsync(ct);
        try
        {
            if (_entry is { } fresh && DateTime.UtcNow < fresh.ExpiresUtc) return fresh.Data;
            // A load that started before an invalidation may be stale: return it to this caller, don't cache it.
            var version = Volatile.Read(ref _version);
            var data = await load(ct);
            if (version == Volatile.Read(ref _version)) _entry = new Entry(data, DateTime.UtcNow + Duration);
            return data;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _version);
        _entry = null;
    }
}
