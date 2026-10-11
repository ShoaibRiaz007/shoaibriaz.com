using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Application.Abstractions;

namespace Portfolio.Api;

/// <summary>
/// Read-only JSON over the published content, for the 3D world and any other client.
/// Shares the "public-content" output-cache policy, so admin edits evict it with the HTML pages.
/// Responses are DTOs (<see cref="ApiModels"/>), never entities, so internal columns stay private.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class PublicApiController : ControllerBase
{
    private readonly IPublicContentFacade _content;

    public PublicApiController(IPublicContentFacade content) => _content = content;

    /// <summary>Everything published, in one request (the 3D world loads only this).</summary>
    [HttpGet("site")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<ActionResult<SiteDto>> Site(CancellationToken ct)
        => ApiModels.ToDto(await _content.GetSiteAsync(ct));

    [HttpGet("projects")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> Projects(CancellationToken ct)
        => (await _content.GetProjectsAsync(ct)).Projects.Select(ApiModels.ToDto).ToList();

    [HttpGet("projects/{slug}")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<ActionResult<ProjectDetailDto>> Project(string slug, CancellationToken ct)
    {
        var detail = await _content.GetProjectDetailAsync(slug, ct);
        return detail is null ? NotFound() : ApiModels.ToDto(detail);
    }
}
