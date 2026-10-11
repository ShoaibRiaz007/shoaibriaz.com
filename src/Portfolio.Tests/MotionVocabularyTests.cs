using System.Text.RegularExpressions;
using Xunit;

namespace Portfolio.Tests;

// The motion system is driven by data attributes in the views (motion.js lists the vocabulary). A misspelt or
// removed name fails silently in the browser: the element falls back to the default or does nothing. So the
// views are checked here against what motion.css styles and motion.js handles.
public class MotionVocabularyTests
{
    private static readonly string Web = Path.Combine(RepoRoot(), "src", "Portfolio.Web");
    // Comments removed, so a name mentioned only in prose doesn't count as defined.
    private static readonly string Css = Regex.Replace(
        File.ReadAllText(Path.Combine(Web, "wwwroot", "css", "motion.css")), @"/\*.*?\*/", "", RegexOptions.Singleline);
    private static readonly string Js = File.ReadAllText(Path.Combine(Web, "wwwroot", "js", "motion.js"));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Portfolio.slnx not found above the test output folder.");
    }

    private static HashSet<string> Styled(string attribute) => Styled(Css, attribute);

    // Names a stylesheet styles for an attribute, e.g. [data-enter="card"] { ... }: the selector of a rule that
    // has at least one declaration. Names inside :not(...) are excluded there, not styled, so they don't count.
    // For data-pointer-each the selector must reach the children ("> *"), which is where the effect shows.
    internal static HashSet<string> Styled(string css, string attribute)
    {
        var names = new HashSet<string>();
        foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            if (!rule.Groups[2].Value.Contains(':')) continue;
            var selector = Regex.Replace(rule.Groups[1].Value, @":not\((?:[^()]|\([^()]*\))*\)", "");
            var pattern = $@"\[{Regex.Escape(attribute)}=[""']([a-z-]+)[""']\]" + (attribute == "data-pointer-each" ? @"\s*>\s*\*" : "");
            foreach (Match m in Regex.Matches(selector, pattern)) names.Add(m.Groups[1].Value);
        }
        return names;
    }

    // Pointer effects are the keys of motion.js's effects table.
    private static HashSet<string> PointerEffects()
    {
        var table = Regex.Match(Js, @"const effects = \{(.*?)\n    \};", RegexOptions.Singleline).Groups[1].Value;
        return Regex.Matches(table, @"^\s{8}([a-z]+): \{", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet();
    }

    // Each value a view gives an attribute, in either quote style: the literal, or for a Razor expression
    // (@(...), parentheses balanced) every lowercase string literal inside it, e.g. the two names in
    // "@(n % 2 == 0 ? "tilt-left" : "tilt-right")". The project card's entrance can also arrive as
    // ["Enter"] = "..." in the view data a page passes it.
    // A value with no name the test can read (@(someVariable), @Model.Enter) comes back as itself, so it fails
    // the checks: motion names in views are written as literals, which keeps them checkable.
    internal static IEnumerable<(string View, string Value)> Used(string attribute)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Web, "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var text = Regex.Replace(File.ReadAllText(file), @"@\*.*?\*@", "", RegexOptions.Singleline);   // Razor comments
            var view = Path.GetFileName(file);
            foreach (var value in AttributeValues(text, attribute))
            {
                if (value.StartsWith("@("))
                {
                    var literals = Regex.Matches(value, @"""([a-z][a-z-]*)""").Select(m => m.Groups[1].Value).ToList();
                    if (literals.Count == 0) yield return (view, value);
                    foreach (var lit in literals) yield return (view, lit);
                }
                else if (value.Length > 0)
                    yield return (view, value);
            }
            if (attribute == "data-enter")
                foreach (Match m in Regex.Matches(text, @"\[""Enter""\]\s*=\s*""([a-z-]+)""")) yield return (view, m.Groups[1].Value);
        }
    }

    internal static IEnumerable<string> AttributeValues(string text, string attribute)
    {
        foreach (Match m in Regex.Matches(text, $@"\s{Regex.Escape(attribute)}=([""'])"))
        {
            var quote = m.Groups[1].Value[0];
            var i = m.Index + m.Length;
            if (i + 1 < text.Length && text[i] == '@' && text[i + 1] == '(')
            {
                var depth = 0;
                var start = i;
                for (i++; i < text.Length; i++)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')' && --depth == 0) break;
                }
                yield return text[start..Math.Min(i + 1, text.Length)];
            }
            else
            {
                var end = text.IndexOf(quote, i);
                if (end >= 0) yield return text[i..end];
            }
        }
    }

    [Theory]
    [InlineData("data-enter")]
    [InlineData("data-enter-item")]
    [InlineData("data-scroll")]
    public void EveryNameAViewUses_IsStyledInMotionCss(string attribute)
    {
        var styled = Styled(attribute);
        Assert.NotEmpty(styled);
        var unknown = Used(attribute).Where(u => !styled.Contains(u.Value)).Select(u => $"{u.View}: {attribute}=\"{u.Value}\"").ToList();
        Assert.True(unknown.Count == 0, "Not styled in motion.css: " + string.Join(", ", unknown));
    }

    // An effect needs both halves: motion.js to follow the pointer and motion.css to show it, for the attribute
    // (route) the view uses.
    [Theory]
    [InlineData("data-pointer")]
    [InlineData("data-pointer-each")]
    public void EveryPointerEffectAViewUses_IsHandledAndStyled(string attribute)
    {
        var effects = PointerEffects();
        var styled = Styled(attribute);
        var unknown = Used(attribute).Where(u => !effects.Contains(u.Value) || !styled.Contains(u.Value))
            .Select(u => $"{u.View}: {attribute}=\"{u.Value}\"").ToList();
        Assert.True(unknown.Count == 0, "Missing from motion.js or motion.css: " + string.Join(", ", unknown));
    }

    // The vocabulary in motion.js's header is what authors copy; each name in it must work.
    [Theory]
    [InlineData("data-enter")]
    [InlineData("data-enter-item")]
    [InlineData("data-scroll")]
    [InlineData("data-pointer")]
    public void EveryDocumentedName_Works(string attribute)
    {
        var line = Regex.Match(Js, $@"//\s+{Regex.Escape(attribute)}=""([a-z|-]+)""").Groups[1].Value;
        Assert.NotEmpty(line);
        var missing = line.Split('|').Except(Styled(attribute)).ToList();
        if (attribute == "data-pointer")
        {
            missing.AddRange(line.Split('|').Except(PointerEffects()));
            missing.AddRange(line.Split('|').Except(Styled("data-pointer-each")));
        }
        Assert.True(missing.Count == 0, $"Documented but not working: {attribute} " + string.Join(", ", missing.Distinct()));
    }

    [Fact]
    public void BothPointerRoutes_AreWired()
    {
        // data-pointer binds the element, data-pointer-each binds each child, through the same effects table.
        Assert.Contains("bind(el, el.dataset.pointer)", Js);
        Assert.Contains("bind(el, list.dataset.pointerEach)", Js);
    }

    [Fact]
    public void TheScanner_ReadsEveryForm()
    {
        // Guards the scanner itself: if it stopped finding attributes, the checks above would pass vacuously.
        const string markup = """
            <a data-enter="card"></a><b data-enter='flip'></b>
            <c data-enter="@((n % 2) == 0 ? "tilt-left" : "tilt-right")"></c>
            """;
        Assert.Equal(new[] { "card", "flip", "@((n % 2) == 0 ? \"tilt-left\" : \"tilt-right\")" }, AttributeValues(markup, "data-enter"));
        Assert.Contains(Used("data-enter"), u => u.View == "Index.cshtml" && u.Value == "deal");
        Assert.Contains(Used("data-pointer-each"), u => u.Value == "tilt");

        const string css = """
            /* [data-enter="commented"] { opacity: 0; } */
            .x [data-enter]:not(.in, [data-enter="excluded"]) { opacity: 0; }
            [data-enter="empty"] { }
            [data-enter='single'] { --enter: a; }
            @media (x) { [data-enter="nested"] { --enter: b; } }
            [data-pointer-each="container"] { color: red; }
            [data-pointer-each="children"] > * { color: red; }
            """;
        var stripped = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        Assert.Equal(new[] { "nested", "single" }, Styled(stripped, "data-enter").Order());
        Assert.Equal(new[] { "children" }, Styled(stripped, "data-pointer-each"));
    }
}
