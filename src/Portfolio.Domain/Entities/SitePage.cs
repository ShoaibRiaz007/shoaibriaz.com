namespace Portfolio.Domain.Entities;

/// <summary>An editable text page of the site (the privacy policy). A page with no row shows its built-in text.</summary>
public class SitePage
{
    public int Id { get; set; }

    /// <summary>Which page: see <c>SitePages</c> for the known slugs.</summary>
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Light markup: "## " headings, "- " bullets, blank lines between paragraphs, **bold**, [text](url),
    /// and {placeholders} filled in when the page is shown.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>UTC.</summary>
    public DateTime UpdatedAt { get; set; }
}
