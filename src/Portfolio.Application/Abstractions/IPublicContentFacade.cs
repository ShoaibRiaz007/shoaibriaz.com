using Portfolio.Application.Models;

namespace Portfolio.Application.Abstractions;

/// <summary>Read-only facade serving the public-facing site. Implementations are injected (polymorphic).</summary>
public interface IPublicContentFacade
{
    Task<HomeData> GetHomeAsync(int prominentCount = 6, CancellationToken ct = default);
    Task<ProjectsData> GetProjectsAsync(CancellationToken ct = default);
    Task<ProjectDetail?> GetProjectDetailAsync(string slug, CancellationToken ct = default);
}
