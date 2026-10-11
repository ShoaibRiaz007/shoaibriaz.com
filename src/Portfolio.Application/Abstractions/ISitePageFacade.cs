using Portfolio.Domain.Entities;

namespace Portfolio.Application.Abstractions;

/// <summary>The site's editable text pages (the privacy policy). No row means the page shows its built-in text.</summary>
public interface ISitePageFacade
{
    /// <summary>The saved page, or null when it has never been edited (or was reset).</summary>
    Task<SitePage?> GetAsync(string slug, CancellationToken ct = default);

    /// <summary>Creates or replaces the page's text. Title and body are trimmed and cut to the column sizes.</summary>
    Task SaveAsync(string slug, string title, string body, CancellationToken ct = default);

    /// <summary>Removes the saved text, so the page shows its built-in text again.</summary>
    Task ResetAsync(string slug, CancellationToken ct = default);
}

/// <summary>Known page slugs.</summary>
public static class SitePages
{
    public const string Privacy = "privacy";
}
