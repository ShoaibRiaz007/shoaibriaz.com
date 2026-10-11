using Portfolio.Application.Models;

namespace Portfolio.Models;

/// <summary>One admin dashboard list (projects, experience, …), rendered by the shared _ContentList partial.</summary>
public class ContentListViewModel
{
    public ContentKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string EditAction { get; init; } = string.Empty;
    public string DeleteAction { get; init; } = string.Empty;
    public string AddLabel { get; init; } = "+ Add";
    /// <summary>Column headings after the title column.</summary>
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ContentRow> Rows { get; init; } = Array.Empty<ContentRow>();

    public string Anchor => Kind.ToString().ToLowerInvariant();
}

/// <param name="Cells">Values for <see cref="ContentListViewModel.Columns"/>, in order.</param>
/// <param name="SearchedCells">How many of the leading <paramref name="Cells"/> the list search matches; the rest
/// (numbers such as view counts) are shown only. Null searches them all.</param>
public record ContentRow(int Id, string Title, string Subtitle, bool IsPublished, IReadOnlyList<string> Cells, int? SearchedCells = null)
{
    /// <summary>What the list search matches (lower case): title, subtitle and the searched cells.</summary>
    public string SearchText => string.Join(' ', new[] { Title, Subtitle }.Concat(Cells.Take(SearchedCells ?? Cells.Count))).ToLowerInvariant();
}
