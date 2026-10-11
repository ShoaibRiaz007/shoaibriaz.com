using System.Text.RegularExpressions;

namespace Portfolio.Domain.Services;

/// <summary>Domain service: turns a human title into a URL-safe slug.</summary>
public static class SlugGenerator
{
    public static string Generate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var s = value.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"[^a-z0-9\s-]", "");
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"-+", "-").Trim('-');
        return s;
    }
}
