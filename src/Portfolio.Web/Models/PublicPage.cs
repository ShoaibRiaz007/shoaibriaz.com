using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Globalization;
using Portfolio.Application.Common;
using Portfolio.Domain.Entities;

namespace Portfolio.Models;

/// <summary>What the public layout needs from every page model: the owner's identity for the header, footer
/// and structured data, plus per-page SEO metadata.</summary>
public interface IPublicPage
{
    Bio Bio { get; }
    PageMeta Meta { get; }

    /// <summary>Career facts for the structured data, on pages that load them (home, CV).</summary>
    PersonFacts? Person => null;
}

/// <summary>What search engines get to know about the owner beyond the profile: the current role and employer,
/// skills and schools. Taken from the same published content the page shows.</summary>
public record PersonFacts(string? Role, string? Employer, IReadOnlyList<string> Skills, IReadOnlyList<string> Schools)
{
    public static PersonFacts From(IEnumerable<Experience> experiences, IEnumerable<Skill> skills, IEnumerable<Education> educations)
    {
        // The same "current" rule as the home page's hero and the API (a dated role with no end), so search engines
        // are told what the page shows.
        var current = experiences.FirstOrDefault(e => e.IsCurrent);
        return new PersonFacts(
            NullIfBlank(current?.Role), NullIfBlank(current?.Company),
            skills.Select(s => s.Name.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            educations.Select(e => e.Institution.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <param name="Path">Site-relative path of the page ("/projects"), used for the canonical URL.</param>
/// <param name="Image">Absolute or site-relative image for link previews; falls back to the profile photo.</param>
/// <param name="NoIndex">Emits robots noindex (the 404 page).</param>
public record PageMeta(string Title, string Description, string Path, string? Image = null, string Type = "website", bool NoIndex = false);

public static class PublicSite
{
    /// <summary>Canonical origin. Taken from the admin-edited Website field rather than the request's Host
    /// header, so a spoofed Host can't put a foreign origin into canonical/OG tags of cached pages.</summary>
    public static string Origin(Bio bio, HttpRequest request)
    {
        var website = UrlValidator.SanitizeOrEmpty(bio.Website);
        if (TryHttpUrl(website, out var uri)) return uri.GetLeftPart(UriPartial.Authority);
        return $"{request.Scheme}://{request.Host}";
    }

    /// <summary>Absolute URL for a site path ("/projects") or an absolute http(s) URL, which is kept as is.
    /// A protocol-relative "//host/x" is a URL on another host, so it takes the origin's scheme.</summary>
    public static string Absolute(string origin, string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return origin;
        if (pathOrUrl.StartsWith("//")) return origin[..origin.IndexOf(':')] + ":" + pathOrUrl;
        if (pathOrUrl.StartsWith('/')) return origin + pathOrUrl;
        return TryHttpUrl(pathOrUrl, out _) ? pathOrUrl : origin + "/" + pathOrUrl;
    }

    /// <summary>The scheme check matters: on Linux, Uri.TryCreate("/projects", UriKind.Absolute) succeeds as
    /// file:///projects, so "is absolute" alone would pass site paths through unchanged.</summary>
    private static bool TryHttpUrl(string value, [NotNullWhen(true)] out Uri? uri)
        => Uri.TryCreate(value, UriKind.Absolute, out uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static readonly string[] StaticRoots = { "/world", "/css", "/js", "/lib", "/uploads" };

    /// <summary>A request for a file (wwwroot folders, or any last segment with an extension such as
    /// "/favicon.png" or "/wp-login.php"). A miss there keeps a plain 404 instead of the designed page.</summary>
    public static bool IsStaticAssetPath(PathString path)
        => StaticRoots.Any(root => path.StartsWithSegments(root, StringComparison.OrdinalIgnoreCase))
           || System.IO.Path.HasExtension(path.Value);

    /// <summary>Name tokens worth showing in the brand mark. Abbreviated tokens ("M." in "M. Shoaib Riaz") are
    /// skipped so the header reads "Shoaib" / "SR" rather than "M." / "MS"; if that leaves nothing, the raw
    /// tokens are used with their dots trimmed.</summary>
    private static string[] NameParts(Bio bio)
    {
        var tokens = (bio.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var words = tokens.Where(t => !t.EndsWith('.') && char.IsLetterOrDigit(t[0])).ToArray();
        return words.Length > 0 ? words : tokens.Select(t => t.Trim('.')).Where(t => t.Length > 0).ToArray();
    }

    public static string FirstName(Bio bio) => NameParts(bio).FirstOrDefault() ?? "Portfolio";

    public static string Initials(Bio bio)
        => string.Concat(NameParts(bio).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    /// <summary>Longest description search results show in full.</summary>
    public const int DescriptionMax = 160;

    /// <summary>The page description for search results: the tagline, plus where the owner is based when it fits
    /// (people search for "C# developer Lahore").</summary>
    public static string Describe(Bio bio)
    {
        var tagline = (bio.Tagline ?? "").Trim();
        var place = Place(bio.Location);
        if (place is null) return Truncate(tagline, DescriptionMax);
        var sentence = tagline.Length == 0 || ".!?".Contains(tagline[^1]) ? tagline : tagline + ".";
        var full = $"{sentence} Based in {place}.".TrimStart();
        return full.Length <= DescriptionMax ? full : Truncate(tagline, DescriptionMax);
    }

    /// <summary>A free-text location read as (locality, region, country). The last part counts as the country only
    /// when it is a country's English name (or USA / UK / UAE), so "Lahore, Punjab" is a city and a region, and
    /// "Los Angeles, CA" a city and a state, not a city in Canada.</summary>
    public static (string? Locality, string? Region, string? Country) ParseLocation(string? location)
    {
        var parts = Csv(location);
        if (parts.Length == 0) return (null, null, null);
        var country = IsCountry(parts[^1]) ? parts[^1] : null;
        var rest = country is null ? parts : parts[..^1];
        return (rest.Length > 0 ? rest[0] : null, rest.Length > 1 ? rest[^1] : null, country);
    }

    /// <summary>"Lahore, Punjab, Pakistan" → "Lahore, Pakistan": the city and country (or region) people search with.</summary>
    public static string? Place(string? location)
    {
        var (locality, region, country) = ParseLocation(location);
        var wider = country ?? region;
        return locality is null ? wider : wider is null ? locality : $"{locality}, {wider}";
    }

    private static readonly HashSet<string> CountryNames = BuildCountryNames();

    private static HashSet<string> BuildCountryNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "USA", "UK", "UAE" };
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try { names.Add(new RegionInfo(culture.Name).EnglishName); }
            catch (ArgumentException) { /* culture without a region */ }
        }
        return names;
    }

    private static bool IsCountry(string name) => CountryNames.Contains(name.Trim());

    /// <summary>The headline's parts, split on any of the separators admins use ("Team Lead · C# / .NET").</summary>
    public static string[] HeadlineParts(string? headline)
        => (headline ?? "").Split(['·', '•', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The job title for the headline alone: its first part ("Team Lead · C# / .NET" → "Team Lead").</summary>
    public static string? HeadlineTitle(string? headline) => HeadlineParts(headline).FirstOrDefault();

    /// <summary>A project metric as a stat: the leading figure and the words after it ("70–80% faster level
    /// production" → "70–80%", "faster level production"). Null when the metric isn't led by a number
    /// ("Claude-powered diagrams"), so labels stay on the cards. A figure may carry a symbol in front ("~70%",
    /// "$2M") and a range written with spaces ("70 – 80%") stays together.</summary>
    public static (string Figure, string Label)? ProofFigure(string? metric)
    {
        var m = ProofPattern.Match(metric ?? "");
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value.Trim()) : null;
    }

    private static readonly Regex ProofPattern = new(@"^\s*([~≈<>+$€£]?[0-9][^\s]*(?:\s+[–—-]\s*[0-9][^\s]*)?)(.*)$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>A video embed in the providers' privacy modes: YouTube's no-cookie domain, and Vimeo's Do Not Track.
    /// Stored embeds keep their canonical URL; this is applied when the page renders them.</summary>
    public static string PrivateEmbed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        if (uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/embed/"))
            return "https://www.youtube-nocookie.com" + uri.PathAndQuery;
        if (uri.Host.Equals("player.vimeo.com", StringComparison.OrdinalIgnoreCase) && !uri.Query.Contains("dnt=1"))
            return url + (uri.Query.Length == 0 ? "?" : "&") + "dnt=1";
        return url;
    }

    /// <summary>schema.org JSON-LD: the owner as a Person, the site as a WebSite, and on the home page a ProfilePage
    /// about that Person. The career details (job title, employer, skills, schools) appear only on pages that load
    /// them, so no page states a different job title for the same Person. The default encoder escapes &lt;, &gt; and
    /// &amp;, so admin text can't close the script element.</summary>
    public static string StructuredData(Bio bio, string origin, string canonical, string? image, PersonFacts? facts, bool profilePage)
    {
        var personId = origin + "/#person";
        var (locality, region, country) = ParseLocation(bio.Location);
        var person = Compact(new()
        {
            ["@type"] = "Person",
            ["@id"] = personId,
            ["name"] = bio.FullName,
            ["url"] = origin,
            ["jobTitle"] = facts is null ? null : facts.Role ?? HeadlineTitle(bio.Headline),
            ["description"] = string.IsNullOrWhiteSpace(bio.Tagline) ? null : bio.Tagline.Trim(),
            ["image"] = image,
            ["worksFor"] = facts?.Employer is { } employer ? new Dictionary<string, object?> { ["@type"] = "Organization", ["name"] = employer } : null,
            ["alumniOf"] = facts is { Schools.Count: > 0 }
                ? facts.Schools.Select(s => new Dictionary<string, object?> { ["@type"] = "EducationalOrganization", ["name"] = s }).ToList()
                : null,
            ["knowsAbout"] = facts is { Skills.Count: > 0 } ? facts.Skills : null,
            ["address"] = locality is null && country is null ? null : Compact(new()
            {
                ["@type"] = "PostalAddress",
                ["addressLocality"] = locality,
                ["addressRegion"] = region,
                ["addressCountry"] = country,
            }),
            ["sameAs"] = new[] { bio.LinkedIn, bio.GitHub }.Where(u => !string.IsNullOrWhiteSpace(u)).ToArray() is { Length: > 0 } links ? links : null,
        });
        var graph = new List<object>
        {
            person,
            new Dictionary<string, object?>
            {
                ["@type"] = "WebSite", ["@id"] = origin + "/#website", ["url"] = origin, ["name"] = bio.FullName,
                ["publisher"] = new Dictionary<string, string> { ["@id"] = personId },
            },
        };
        if (profilePage)
            graph.Add(new Dictionary<string, object?>
            {
                ["@type"] = "ProfilePage", ["url"] = canonical,
                ["mainEntity"] = new Dictionary<string, string> { ["@id"] = personId },
            });
        return JsonSerializer.Serialize(new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@graph"] = graph });
    }

    private static Dictionary<string, object?> Compact(Dictionary<string, object?> values)
        => values.Where(kv => kv.Value is not null && kv.Value is not "").ToDictionary(kv => kv.Key, kv => kv.Value);

    public static string Truncate(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

    /// <summary>Newline-delimited admin text as display lines.</summary>
    public static string[] Lines(string? text)
        => (text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Filter areas for the projects page, in display order. Admin categories are free text
    /// ("AI / Tooling", "Strategy RPG", …), which makes far too many chips, so they're bucketed here.</summary>
    public static readonly string[] Areas = { "AI & LLM", ".NET & Desktop", "XR", "Unity & Games" };

    /// <summary>URL-safe key for an area, used in /projects?area=… and as the card art's theme.</summary>
    public static string AreaSlug(string area) => Array.IndexOf(Areas, area) switch
    {
        0 => "ai",
        1 => "dotnet",
        2 => "xr",
        _ => "unity",
    };

    /// <summary>The area for a ?area= value, or null for "all" / anything unrecognised.</summary>
    public static string? AreaFromSlug(string? slug)
        => string.IsNullOrWhiteSpace(slug) ? null : Areas.FirstOrDefault(a => AreaSlug(a) == slug.Trim().ToLowerInvariant());

    /// <summary>Stable 0..variants-1 pick for a seed (slug/title), so a project's generated cover art doesn't
    /// change between requests or restarts (string.GetHashCode is randomised per process).</summary>
    public static int Variant(string? seed, int variants = 4)
    {
        var h = 0;
        foreach (var ch in seed ?? "") h = unchecked(h * 31 + ch);
        return (int)((uint)h % (uint)variants);
    }

    /// <summary>Previous and next project around <paramref name="slug"/> in list order, wrapping at the ends.
    /// Both are null when the project isn't in the list or is the only one.</summary>
    public static (Domain.Entities.Project? Previous, Domain.Entities.Project? Next) Neighbours(
        IReadOnlyList<Domain.Entities.Project> projects, string slug)
    {
        var i = projects.ToList().FindIndex(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (i < 0 || projects.Count < 2) return (null, null);
        return (projects[(i - 1 + projects.Count) % projects.Count], projects[(i + 1) % projects.Count]);
    }

    public static string AreaOf(Domain.Entities.Project p)
    {
        var c = $" {p.Category} ".ToUpperInvariant();
        if (c.Contains(" AI ") || c.Contains("LLM")) return Areas[0];
        if (c.Contains(".NET") || c.Contains("BACKEND") || c.Contains("DESKTOP") || c.Contains("API")) return Areas[1];
        if (c.Contains("AR/VR") || c.Contains(" XR") || c.Contains(" VR") || c.Contains(" AR ")) return Areas[2];
        return Areas[3];
    }

    public static string[] Csv(string? text)
        => (text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
