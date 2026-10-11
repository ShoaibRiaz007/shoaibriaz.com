using Portfolio.Application.Abstractions;
using Portfolio.Application.Telemetry;
using Portfolio.Domain.Entities;

namespace Portfolio.Application.Facades;

public class SitePageFacade : ISitePageFacade
{
    public const int MaxTitleLength = 200;
    /// <summary>The body column's size (AppDbContext uses it too).</summary>
    public const int MaxBodyLength = 20_000;

    private readonly IRepository<SitePage> _pages;
    private readonly TimeProvider _clock;

    public SitePageFacade(IRepository<SitePage> pages, TimeProvider clock)
    {
        _pages = pages; _clock = clock;
    }

    public Task<SitePage?> GetAsync(string slug, CancellationToken ct = default)
        => _pages.FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task SaveAsync(string slug, string title, string body, CancellationToken ct = default)
    {
        title = TrafficSources.Truncate((title ?? "").Trim(), MaxTitleLength);
        body = TrafficSources.Truncate((body ?? "").Replace("\r\n", "\n").Trim(), MaxBodyLength);
        var now = _clock.GetUtcNow().UtcDateTime;
        var page = await _pages.FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (page is null)
        {
            await _pages.AddAsync(new SitePage { Slug = slug, Title = title, Body = body, UpdatedAt = now }, ct);
            return;
        }
        page.Title = title;
        page.Body = body;
        page.UpdatedAt = now;
        await _pages.UpdateAsync(page, ct);
    }

    public async Task ResetAsync(string slug, CancellationToken ct = default)
    {
        var page = await _pages.FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (page is not null) await _pages.RemoveAsync(page, ct);
    }
}
