using System.Text.RegularExpressions;
using Xunit;

namespace Portfolio.Tests;

/// <summary>
/// Regression and invariant test suite for Island 100% Mastery Completion,
/// Offshore Buoy Easter Egg discovery, and atmospheric polish (rowboat creak,
/// campfire firefly clusters).
/// </summary>
public class IslandCompletionAndDiscoveryTests
{
    private static readonly string Root = RepoRoot();
    private static readonly string IndexHtml = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "index.html"));
    private static readonly string StyleCss = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "style.css"));
    private static readonly string UiJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "ui.js"));
    private static readonly string ActivitiesJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "activities.js"));
    private static readonly string PropsJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "props.js"));
    private static readonly string LifeJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "life.js"));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Portfolio.slnx not found above the test output folder.");
    }

    [Fact]
    public void CompletionModal_HtmlDialogAndElements_AreDefined()
    {
        // Dialog container and accessibility attributes
        Assert.Contains("id=\"completionModal\"", IndexHtml);
        Assert.Contains("class=\"panel completion-modal\"", IndexHtml);
        Assert.Contains("aria-labelledby=\"completionTitle\"", IndexHtml);

        // Header and kicker
        Assert.Contains("ISLAND EXPLORATION 100% COMPLETE", IndexHtml);
        Assert.Contains("You've Reached the End of the Island", IndexHtml);

        // Personal quote and engineer call-to-action
        Assert.Contains("If you took the time to explore this world, roast marshmallows by the campfire", IndexHtml);
        Assert.Contains("— Shoaib Riaz", IndexHtml);
        Assert.Contains("Looking for a senior / lead engineer who builds with this level of craft and passion?", IndexHtml);

        // Action links
        Assert.Contains("href=\"/#contact\"", IndexHtml);
        Assert.Contains("href=\"/projects\"", IndexHtml);
        Assert.Contains("href=\"/cv\"", IndexHtml);

        // CSS styling
        Assert.Contains(".completion-modal", StyleCss);
        Assert.Contains(".completion-trophy", StyleCss);
        Assert.Contains(".completion-quote", StyleCss);
    }

    [Fact]
    public void JournalMastery_100PercentCelebrationAndBanner_AreWired()
    {
        // UI showCompletion handler
        Assert.Contains("ui.showCompletion = () => {", UiJs);
        Assert.Contains("openModal($('completionModal'));", UiJs);

        // Journal master banner
        Assert.Contains("const isMaster = done === ACTIVITIES.length;", UiJs);
        Assert.Contains("Journal · ${done}/${ACTIVITIES.length} ★ Complete!", UiJs);
        Assert.Contains("journalMasterBanner", UiJs);
        Assert.Contains("100% Island Explored!", UiJs);

        // Activities completion trigger in markDone
        Assert.Contains("const completedCount = ACTIVITIES.filter(act => journal.done[act.id]).length;", ActivitiesJs);
        Assert.Contains("if (completedCount === ACTIVITIES.length)", ActivitiesJs);
        Assert.Contains("journal.completedAll = true;", ActivitiesJs);
        Assert.Contains("ui.showCompletion();", ActivitiesJs);
        Assert.Contains("audio.play('chime', [60, 64, 67, 72, 76, 79, 84]);", ActivitiesJs);
    }

    [Fact]
    public void OffshoreBuoy_DiscoveryPropAndPlaque_AreAnchored()
    {
        // 3D Navigation Buoy prop in props.js
        Assert.Contains("buoy.name = 'offshore-buoy';", PropsJs);
        Assert.Contains("anchors.buoy = buoy;", PropsJs);
        Assert.Contains("colliders.push({ x: bx, z: bz, r: 1.3 });", PropsJs);
        Assert.Contains("const bx = 18.0, bz = 65.0;", PropsJs);

        // Read buoy plaque action in activities.js
        Assert.Contains("function readBuoyPlaque()", ActivitiesJs);
        Assert.Contains("kicker: 'OFFSHORE DISCOVERY · EASTER EGG'", ActivitiesJs);
        Assert.Contains("title: 'The Outer Buoy'", ActivitiesJs);
        Assert.Contains("You rowed all the way out into the open sea! A true explorer.", ActivitiesJs);
        Assert.Contains("href: '/#contact'", ActivitiesJs);

        // Candidate offered when within radius of buoy
        Assert.Contains("if (anchors.buoy)", ActivitiesJs);
        Assert.Contains("add('buoy', anchors.buoy.position, 5.0, 'Read the offshore plaque', readBuoyPlaque);", ActivitiesJs);
    }

    [Fact]
    public void Rowboat_HullCreakAudio_TriggersOnSwellAndSurge()
    {
        // Wooden hull creak audio trigger in rowboatActivity.update
        Assert.Contains("audio.play('creak');", ActivitiesJs);
        Assert.Contains("Math.abs(strokeRock) > 0.02 || Math.abs(wavePitch) > 0.038", ActivitiesJs);
    }

    [Fact]
    public void Life_Fireflies_AreClusteredAroundCampfireAndHammock()
    {
        // Cluster fireflies around campfire center and hammock palms at night
        Assert.Contains("i < 14 && anchors.campfire?.center", LifeJs);
        Assert.Contains("x = anchors.campfire.center.x + Math.cos(angle) * dist;", LifeJs);
        Assert.Contains("i < 24 && anchors.hammock", LifeJs);
        Assert.Contains("x = anchors.hammock.x + Math.cos(angle) * dist;", LifeJs);
    }
}
