using System.Globalization;

namespace Portfolio.Application.Telemetry;

/// <summary>ISO 3166-1 alpha-2 helpers: region from a language tag, display name and flag emoji.</summary>
public static class Countries
{
    /// <summary>The region subtag of a BCP 47 language tag ("en-GB" → "GB", "zh-Hant-TW" → "TW"), or null when it
    /// has none or only a numeric one ("es-419"). Also accepts a raw Accept-Language header (uses the first entry).</summary>
    public static string? FromLanguageTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var first = tag.Split(',')[0].Split(';')[0].Trim();
        foreach (var part in first.Split('-', '_').Skip(1))
            if (part.Length == 2 && char.IsAsciiLetter(part[0]) && char.IsAsciiLetter(part[1]))
                return part.ToUpperInvariant();
        return null;
    }

    /// <summary>English country name, or the code itself when the runtime doesn't know it.</summary>
    public static string Name(string code)
    {
        try { return new RegionInfo(code).EnglishName; }
        catch (ArgumentException) { return code; }
    }

    /// <summary>The flag emoji (two regional-indicator symbols); empty for anything that isn't two ASCII letters.</summary>
    public static string Flag(string? code)
    {
        if (code is not { Length: 2 } || !char.IsAsciiLetter(code[0]) || !char.IsAsciiLetter(code[1])) return string.Empty;
        var upper = code.ToUpperInvariant();
        return char.ConvertFromUtf32(0x1F1E6 + upper[0] - 'A') + char.ConvertFromUtf32(0x1F1E6 + upper[1] - 'A');
    }
}
