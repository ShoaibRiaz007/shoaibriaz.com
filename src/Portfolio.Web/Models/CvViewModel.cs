using Portfolio.Application.Models;
using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;

namespace Portfolio.Models;

/// <summary>The printable /cv page: everything published, with experience grouped by company.</summary>
public class CvViewModel : IPublicPage
{
    public required SiteData Site { get; init; }
    public required IReadOnlyList<CompanyGroup> ExperienceGroups { get; init; }
    public Bio Bio => Site.Bio;
    public required PageMeta Meta { get; init; }
    public PersonFacts? Person { get; init; }
}

/// <summary>/privacy: the saved policy text (or the built-in one), parsed for display.</summary>
public class PrivacyViewModel : IPublicPage
{
    public required Bio Bio { get; init; }
    public required PageMeta Meta { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<RichText.Block> Blocks { get; init; }
    /// <summary>When the text was last edited in the admin; null for the built-in text.</summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>404 page; still needs the owner's identity for the shared layout.</summary>
public class NotFoundViewModel : IPublicPage
{
    public required Bio Bio { get; init; }
    /// <summary>The URL the visitor asked for (before the status-code re-execute).</summary>
    public string RequestedPath { get; init; } = "";
    public IReadOnlyList<Project> Suggestions { get; init; } = Array.Empty<Project>();
    public required PageMeta Meta { get; init; }
}
