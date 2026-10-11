using System.Text.RegularExpressions;
using Portfolio.Domain.Entities;

namespace Portfolio.Domain.Services;

/// <summary>Roles at one company, in display order (a promotion path, newest first).</summary>
public sealed record CompanyGroup(string Company, IReadOnlyList<Experience> Roles)
{
    /// <summary>Earliest known start across the roles; null if no role has dates.</summary>
    public DateOnly? StartDate => Roles.Where(r => r.StartDate is not null).Min(r => r.StartDate);

    /// <summary>The latest end date. Null when any role is still current ("Present", including an undated legacy
    /// role whose hand-written Period says so), and also when no role has dates at all (an all-legacy group):
    /// null alone does not mean current; <see cref="Period"/> carries the text either way.</summary>
    public DateOnly? EndDate => Roles.Any(IsOngoing) ? null : Roles.Max(r => r.EndDate);

    /// <summary>Derived from the dates when any role has them. An all-legacy group spans the oldest role's start
    /// text to the newest role's end text ("2018 - 2021" from "2018 - 2019" and "2019 - 2021"); if a Period
    /// can't be split that way, the newest role's Period is shown.</summary>
    public string Period
    {
        get
        {
            if (StartDate is not null) return PeriodFormatter.Format(StartDate, EndDate);
            if (Roles.Count == 1) return Roles[0].Period;
            var newest = SplitPeriod(Roles[0].Period);
            var oldest = SplitPeriod(Roles[^1].Period);
            return newest is null || oldest is null ? Roles[0].Period : $"{oldest.Value.From} - {newest.Value.To}";
        }
    }

    private static bool IsOngoing(Experience r)
        => r.IsCurrent || (r.StartDate is null && r.Period.TrimEnd().EndsWith("Present", StringComparison.OrdinalIgnoreCase));

    /// <summary>" - ", " – " (en dash) or " — " (em dash) between the two ends of a hand-written range.</summary>
    private static readonly Regex RangeSeparator = new(@"\s+[-–—]\s+", RegexOptions.CultureInvariant);

    private static (string From, string To)? SplitPeriod(string period)
    {
        var parts = RangeSeparator.Split(period.Trim());
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 ? (parts[0], parts[1]) : null;
    }
}

public static class ExperienceGrouping
{
    /// <summary>
    /// Groups <em>consecutive</em> roles with the same company (case-insensitive), the way LinkedIn shows
    /// promotions. Only adjacent rows merge, so leaving and later rejoining a company stays two entries.
    /// Input order is preserved, so callers control it via SortOrder.
    /// </summary>
    public static IReadOnlyList<CompanyGroup> GroupConsecutive(IEnumerable<Experience> roles)
    {
        var groups = new List<CompanyGroup>();
        List<Experience>? current = null;
        foreach (var role in roles)
        {
            if (current is not null && string.Equals(current[0].Company.Trim(), role.Company.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                current.Add(role);
                continue;
            }
            current = new List<Experience> { role };
            groups.Add(new CompanyGroup(role.Company.Trim(), current));
        }
        return groups;
    }
}
