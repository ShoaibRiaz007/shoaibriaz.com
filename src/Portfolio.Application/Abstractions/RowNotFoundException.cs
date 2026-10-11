namespace Portfolio.Application.Abstractions;

/// <summary>An update targeted a row that no longer exists (typically deleted in another tab after the edit
/// form was opened). Thrown by <see cref="IRepository{T}"/> so callers can answer "gone" instead of a 500.</summary>
public sealed class RowNotFoundException : Exception
{
    public RowNotFoundException(string entityName, Exception? inner = null)
        : base($"The {entityName} being updated no longer exists.", inner) { }
}
