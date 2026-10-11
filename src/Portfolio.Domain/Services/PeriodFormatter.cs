using System.Globalization;

namespace Portfolio.Domain.Services;

/// <summary>Formats a month-precision date range the way LinkedIn and CVs do: "Jun 2025 - Present".</summary>
public static class PeriodFormatter
{
    public static string Format(DateOnly? start, DateOnly? end)
    {
        if (start is null) return string.Empty;
        var from = Month(start.Value);
        return end is null ? $"{from} - Present" : $"{from} - {Month(end.Value)}";
    }

    /// <summary>Normalises to the first of the month so ordering and "same month" checks are stable
    /// regardless of which day a browser month picker submits.</summary>
    public static DateOnly? ToMonth(DateOnly? date)
        => date is null ? null : new DateOnly(date.Value.Year, date.Value.Month, 1);

    private static string Month(DateOnly d) => d.ToString("MMM yyyy", CultureInfo.InvariantCulture);
}
