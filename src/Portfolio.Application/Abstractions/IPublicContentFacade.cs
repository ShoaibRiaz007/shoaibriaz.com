using Portfolio.Application.Models;

namespace Portfolio.Application.Abstractions;

/// <summary>Read-only facade serving the public-facing site. Implementations are injected (polymorphic).
/// Only published content is ever returned.</summary>
public interface IPublicContentFacade
{
    Task<HomeData> GetHomeAsync(int prominentCount = 6, CancellationToken ct = default);
    Task<ProjectsData> GetProjectsAsync(CancellationToken ct = default);
    Task<ProjectDetail?> GetProjectDetailAsync(string slug, CancellationToken ct = default);
    Task<SiteData> GetSiteAsync(CancellationToken ct = default);

    /// <summary>Just the profile, for pages that show nothing else from the content.</summary>
    Task<Portfolio.Domain.Entities.Bio> GetBioAsync(CancellationToken ct = default);
    /// <summary>The owner's identity plus a few project suggestions for the 404 page (a bounded query).</summary>
    Task<NotFoundData> GetNotFoundAsync(int suggestionCount = 3, CancellationToken ct = default);
}
