using System.Text.RegularExpressions;

namespace Portfolio.Models;

/// <summary>
/// The light markup editable pages and technical articles are written in, parsed into blocks the view renders
/// (every piece of text is HTML-encoded there, so the markup can't inject anything):
/// "## Heading", "### SubHeading", "> Blockquote", fenced "```lang" code blocks, "- bullet" lines,
/// paragraphs separated by a blank line, **bold**, `code`, [text](link) and {placeholders}.
/// Links may only be http(s), mailto: or a site path; anything else is shown as plain text.
/// </summary>
public static partial class RichText
{
    public abstract record Block;
    public sealed record Heading(string Text) : Block;
    public sealed record SubHeading(string Text) : Block;
    public sealed record CodeBlock(string Language, string Code) : Block;
    public sealed record Blockquote(IReadOnlyList<Inline> Content) : Block;
    public sealed record Paragraph(IReadOnlyList<Inline> Content) : Block;
    public sealed record BulletList(IReadOnlyList<IReadOnlyList<Inline>> Items) : Block;

    public abstract record Inline;
    public sealed record Text(string Value) : Inline;
    public sealed record Bold(string Value) : Inline;
    public sealed record InlineCode(string Value) : Inline;
    public sealed record Link(string Label, string Href) : Inline;

    public static IReadOnlyList<Block> Parse(string? body, IReadOnlyDictionary<string, string>? values = null)
    {
        var text = Fill(DropEmpty(body ?? "", values), values).Replace("\r\n", "\n");
        var blocks = new List<Block>();
        var paragraph = new List<string>();
        var bullets = new List<IReadOnlyList<Inline>>();
        var codeLines = new List<string>();
        var inCodeBlock = false;
        var codeLang = "";

        void FlushParagraph()
        {
            if (paragraph.Count > 0) blocks.Add(new Paragraph(Inlines(string.Join(" ", paragraph))));
            paragraph.Clear();
        }
        void FlushBullets()
        {
            if (bullets.Count > 0) blocks.Add(new BulletList(bullets.ToList()));
            bullets.Clear();
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();

            if (inCodeBlock)
            {
                if (line.StartsWith("```"))
                {
                    blocks.Add(new CodeBlock(codeLang, string.Join("\n", codeLines)));
                    codeLines.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    codeLines.Add(raw);
                }
                continue;
            }

            if (line.Length == 0) { FlushParagraph(); FlushBullets(); continue; }

            if (line.StartsWith("```"))
            {
                FlushParagraph(); FlushBullets();
                inCodeBlock = true;
                codeLang = line[3..].Trim();
                codeLines.Clear();
            }
            else if (line.StartsWith("### "))
            {
                FlushParagraph(); FlushBullets();
                blocks.Add(new SubHeading(line[4..].Trim()));
            }
            else if (IsHeading(line))
            {
                FlushParagraph(); FlushBullets();
                blocks.Add(new Heading(line.TrimStart('#').Trim()));
            }
            else if (line.StartsWith("> "))
            {
                FlushParagraph(); FlushBullets();
                blocks.Add(new Blockquote(Inlines(line[2..].Trim())));
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                FlushParagraph();
                bullets.Add(Inlines(line[2..].Trim()));
            }
            else
            {
                FlushBullets();
                paragraph.Add(line);
            }
        }
        if (inCodeBlock && codeLines.Count > 0)
        {
            blocks.Add(new CodeBlock(codeLang, string.Join("\n", codeLines)));
        }
        FlushParagraph();
        FlushBullets();
        return blocks;
    }

    /// <summary>Leaves out text lines that use a placeholder with no value (no email on the profile), rather than show
    /// an empty link. A heading is left out only when every line of its section went this way.</summary>
    private static string DropEmpty(string text, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || !values.Values.Any(string.IsNullOrWhiteSpace)) return text;
        bool UsesEmpty(string line) => !IsHeading(line)
            && Placeholder().Matches(line).Any(m => values.TryGetValue(m.Groups[1].Value, out var v) && string.IsNullOrWhiteSpace(v));

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (UsesEmpty(lines[i])) continue;
            if (IsHeading(lines[i]))
            {
                var section = lines.Skip(i + 1).TakeWhile(l => !IsHeading(l)).Where(l => l.Trim().Length > 0).ToList();
                if (section.Count > 0 && section.All(UsesEmpty)) continue;
            }
            kept.Add(lines[i]);
        }
        return string.Join('\n', kept);
    }

    private static bool IsHeading(string line)
    {
        var t = line.Trim();
        return t.StartsWith("## ") || t.StartsWith("# ");
    }

    /// <summary>Replaces {name} with its value; unknown placeholders are left as typed so a typo is visible.</summary>
    public static string Fill(string text, IReadOnlyDictionary<string, string>? values)
        => values is null ? text : Placeholder().Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    public static IReadOnlyList<Inline> Inlines(string text)
    {
        var result = new List<Inline>();
        var last = 0;
        foreach (Match m in InlineToken().Matches(text))
        {
            if (m.Index > last) result.Add(new Text(text[last..m.Index]));
            if (m.Groups["bold"].Success) result.Add(new Bold(m.Groups["bold"].Value));
            else if (m.Groups["code"].Success) result.Add(new InlineCode(m.Groups["code"].Value));
            else if (SafeHref(m.Groups["href"].Value) is { } href) result.Add(new Link(m.Groups["label"].Value, href));
            else result.Add(new Text(m.Groups["label"].Value));
            last = m.Index + m.Length;
        }
        if (last < text.Length) result.Add(new Text(text[last..]));
        return result;
    }

    /// <summary>http(s), mailto: or a site path ("/cv"); null for anything else (javascript:, data:, "//host").</summary>
    public static string? SafeHref(string href)
    {
        href = href.Trim();
        if (href.StartsWith('/') && !href.StartsWith("//") && !href.StartsWith("/\\")) return href;
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) && href.Length > 7) return href;
        return Uri.TryCreate(href, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? href
            : null;
    }

    /// <summary>Stable HTML anchor id for a section heading, shared by heading tags and table-of-contents links.</summary>
    public static string HeadingId(string text)
        => NonSlugChars().Replace((text ?? "").Trim().ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\*\*(?<bold>[^*]+)\*\*|`(?<code>[^`]+)`|\[(?<label>[^\]]+)\]\((?<href>[^)\s]+)\)")]
    private static partial Regex InlineToken();
}
