using System.Text.RegularExpressions;
using Xunit;

namespace Portfolio.Tests;

// The motion system is driven by data attributes in the views (motion.js lists the vocabulary). A misspelt or
// removed name fails silently in the browser, the element just doesn't move, so the views are checked here
// against what motion.css and motion.js actually define.
public class MotionVocabularyTests
{
    private static readonly string Web = Path.Combine(RepoRoot(), "src", "Portfolio.Web");
    private static readonly string Css = File.ReadAllText(Path.Combine(Web, "wwwroot", "css", "motion.css"));
    private static readonly string Js = File.ReadAllText(Path.Combine(Web, "wwwroot", "js", "motion.js"));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Portfolio.slnx not found above the test output folder.");
    }

    // Names motion.css styles for an attribute, e.g. [data-enter="card"].
    private static HashSet<string> Defined(string attribute) =>
        Regex.Matches(Css, $@"\[{Regex.Escape(attribute)}=""([a-z-]+)""\]").Select(m => m.Groups[1].Value).ToHashSet();

    // Pointer effects are the keys of motion.js's effects table.
    private static HashSet<string> PointerEffects()
    {
        var table = Regex.Match(Js, @"const effects = \{(.*?)\n    \};", RegexOptions.Singleline).Groups[1].Value;
        return Regex.Matches(table, @"^\s{8}([a-z]+): \{", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet();
    }

    // Every value a view gives an attribute: a literal, or each lowercase string literal inside a Razor
    // expression ("@(n % 2 == 0 ? "tilt-left" : "tilt-right")"). The project card's entrance also comes through
    // ViewData["Enter"].
    private static IEnumerable<(string View, string Value)> Used(string attribute)
    {
        var attr = new Regex($@"\s{Regex.Escape(attribute)}=""(@\([^)]*\)|[^""]*)""");
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Web, "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var view = Path.GetFileName(file);
            foreach (Match m in attr.Matches(text))
            {
                var value = m.Groups[1].Value;
                if (value.StartsWith("@("))
                    foreach (Match lit in Regex.Matches(value, @"""([a-z][a-z-]*)""")) yield return (view, lit.Groups[1].Value);
                else if (value.Length > 0)
                    yield return (view, value);
            }
            if (attribute == "data-enter")
                foreach (Match m in Regex.Matches(text, @"ViewData\[""Enter""\]\s*=\s*""([a-z-]+)""")) yield return (view, m.Groups[1].Value);
        }
    }

    [Theory]
    [InlineData("data-enter")]
    [InlineData("data-enter-item")]
    [InlineData("data-scroll")]
    public void EveryNameAViewUses_IsDefinedInMotionCss(string attribute)
    {
        var defined = Defined(attribute);
        Assert.NotEmpty(defined);
        var unknown = Used(attribute).Where(u => !defined.Contains(u.Value)).Select(u => $"{u.View}: {attribute}=\"{u.Value}\"").ToList();
        Assert.True(unknown.Count == 0, "Not defined in motion.css: " + string.Join(", ", unknown));
    }

    [Theory]
    [InlineData("data-pointer")]
    [InlineData("data-pointer-each")]
    public void EveryPointerEffectAViewUses_ExistsInMotionJs(string attribute)
    {
        var effects = PointerEffects();
        Assert.Contains("tilt", effects);
        var unknown = Used(attribute).Where(u => !effects.Contains(u.Value)).Select(u => $"{u.View}: {attribute}=\"{u.Value}\"").ToList();
        Assert.True(unknown.Count == 0, "No such effect in motion.js: " + string.Join(", ", unknown));
    }

    [Fact]
    public void TheHomePage_UsesTheSystem()
    {
        // Guards the scanner itself: if it stopped finding attributes, the checks above would pass vacuously.
        Assert.Contains(Used("data-enter"), u => u.View == "Index.cshtml" && u.Value == "deal");
        Assert.Contains(Used("data-pointer-each"), u => u.Value == "tilt");
    }
}
