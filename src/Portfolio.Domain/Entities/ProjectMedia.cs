namespace Portfolio.Domain.Entities;

/// <summary>A media item (image, uploaded video, or embed) belonging to a Project's gallery.</summary>
public class ProjectMedia
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>"image" | "video" | "embed"</summary>
    public string MediaType { get; set; } = "image";
    public string Url { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    /// <summary>SHA-256 (hex) of the uploaded file's bytes, used to de-duplicate identical uploads.
    /// Empty for embeds and legacy rows.</summary>
    public string ContentHash { get; set; } = string.Empty;
}
