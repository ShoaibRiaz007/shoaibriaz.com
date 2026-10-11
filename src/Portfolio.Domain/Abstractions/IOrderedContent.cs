namespace Portfolio.Domain.Abstractions;

/// <summary>Content rows the admin can reorder (drag-and-drop) and hide without deleting.
/// Shared so a single reorder/publish code path serves every content list.</summary>
public interface IOrderedContent
{
    int Id { get; }
    int SortOrder { get; set; }

    /// <summary>Hidden rows stay editable in the admin but never reach the public site or API,
    /// so content can be archived without losing it.</summary>
    bool IsPublished { get; set; }
}
