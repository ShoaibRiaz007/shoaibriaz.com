using System.Text.RegularExpressions;
using Xunit;

namespace Portfolio.Tests;

/// <summary>
/// Regression and invariant test suite for Island world systems in Portfolio.World,
/// ensuring complete removal of vehicle and rally circuit mechanics while verifying
/// natural hammock palm kinematics and seamless sky cloud drifting.
/// </summary>
public class IslandCircuitAndVehicleKinematicsTests
{
    private static readonly string Root = RepoRoot();
    private static readonly string ActivitiesJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "activities.js"));
    private static readonly string AudioJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "audio.js"));
    private static readonly string BuildersJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "builders.js"));
    private static readonly string CharacterJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "character.js"));
    private static readonly string MainJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "main.js"));
    private static readonly string PlayerJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "player.js"));
    private static readonly string PropsJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "props.js"));
    private static readonly string SaveJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "save.js"));
    private static readonly string SkyJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "sky.js"));
    private static readonly string StyleCss = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "style.css"));
    private static readonly string TerrainJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "terrain.js"));
    private static readonly string UiJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "ui.js"));
    private static readonly string VegetationJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "vegetation.js"));
    private static readonly string WorldHtml = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "index.html"));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Portfolio.slnx not found above the test output folder.");
    }

    [Fact]
    public void CarAndTrack_AreCompletelyRemoved_FromWorldAndActivities()
    {
        // 1. Race HUD elements removed from DOM and styles
        Assert.DoesNotContain("id=\"raceHud\"", WorldHtml);
        Assert.DoesNotContain("id=\"raceHudCp\"", WorldHtml);
        Assert.DoesNotContain("id=\"raceHudBest\"", WorldHtml);
        Assert.DoesNotContain(".race-hud", StyleCss);

        // 2. Activities list trimmed: neither 'drive' nor 'race' present, exactly 22 activities
        var actMatches = Regex.Matches(ActivitiesJs, @"id:\s*'([a-z0-9_-]+)'");
        var activityIds = actMatches.Select(m => m.Groups[1].Value).ToList();
        Assert.DoesNotContain("drive", activityIds);
        Assert.DoesNotContain("race", activityIds);

        var activitiesBlock = Regex.Match(ActivitiesJs, @"export const ACTIVITIES = \[(.*?)\];", RegexOptions.Singleline).Groups[1].Value;
        var actItems = Regex.Matches(activitiesBlock, @"\{\s*id:\s*'([^']+)'");
        Assert.Equal(22, actItems.Count);

        // 3. Race.js removed from disk and main.js
        var raceJsPath = Path.Combine(Root, "src", "Portfolio.World", "src", "race.js");
        Assert.False(File.Exists(raceJsPath), "race.js should be removed from disk");
        Assert.DoesNotContain("createRace", MainJs);
        Assert.DoesNotContain("race.js", MainJs);
        Assert.DoesNotContain("carObj:", MainJs);

        // 4. Terrain does not define RALLY_TRACK or trackDistance or car pad
        Assert.DoesNotContain("RALLY_TRACK", TerrainJs);
        Assert.DoesNotContain("trackDistance", TerrainJs);
        Assert.DoesNotContain("car:", TerrainJs);
        Assert.DoesNotContain("trackDistance", VegetationJs);
        Assert.DoesNotContain("trackDistance", UiJs);

        // 5. Props does not build car or export anchors.car
        Assert.DoesNotContain("buildCar", PropsJs);
        Assert.DoesNotContain("anchors.car", PropsJs);

        // 6. Player does not contain DRIVE state or car kinematics
        Assert.DoesNotContain("DRIVE: 'drive'", PlayerJs);
        Assert.DoesNotContain("player.car", PlayerJs);
        Assert.DoesNotContain("enterCar", PlayerJs);
        Assert.DoesNotContain("exitCar", PlayerJs);

        // 7. Save and UI do not track bestLap
        Assert.DoesNotContain("bestLap", SaveJs);
        Assert.DoesNotContain("Best circuit lap", UiJs);

        // 8. Builders does not export dead car mesh function
        Assert.DoesNotContain("export function car", BuildersJs);

        // 9. Audio does not contain engine synthesizer or driving audio updates
        Assert.DoesNotContain("ensureEngine", AudioJs);
        Assert.DoesNotContain("drive.speed", AudioJs);
        Assert.DoesNotContain("drive = null", AudioJs);

        // 10. Character does not contain drive pose or DRIVE state
        Assert.DoesNotContain("drive: P({", CharacterJs);
        Assert.DoesNotContain("DRIVE: 'drive'", CharacterJs);

        // 11. Activities completion template uses dynamic ACTIVITIES.length
        Assert.Contains("All ${ACTIVITIES.length} activities complete!", ActivitiesJs);

        // 12. Player followPoint uses getWorldPosition and eliminates hot-path Vector3 allocations
        Assert.Contains("avatar.getWorldPosition(out);", PlayerJs);
        Assert.DoesNotContain("new THREE.Vector3(0, 1.45, 0)", PlayerJs);

        // 13. UI modal registry includes completionModal
        Assert.Contains("'completionModal'", UiJs);
    }

    [Fact]
    public void HammockPalms_TrunkSegmentsOverlap_WithoutDisconnectionsOrGaps()
    {
        // Must use overlapping segments so joints never produce disconnected gaps
        Assert.Contains("const segH = 0.95;", PropsJs);
        Assert.Contains("const step = 0.88;", PropsJs);

        // Rotation around Z must tilt outward along joint progression (-s * lean) rather than opposite (+s * lean)
        Assert.Contains("const rz = -s * lean;", PropsJs);
        Assert.Contains("const cx = jx + s * (step * 0.5) * Math.sin(lean);", PropsJs);
        Assert.Contains("const cy = jy + (step * 0.5) * Math.cos(lean);", PropsJs);
        Assert.Contains("jx += s * step * Math.sin(lean);", PropsJs);
        Assert.Contains("jy += step * Math.cos(lean);", PropsJs);

        // Crown tuft and fronds must be anchored at the accumulated top joint (jx, jy)
        Assert.Contains("g.add(put(cyl(0.18, 0.14, 0.35, 7, 0x5a3e26), jx, jy + 0.1", PropsJs);
        Assert.Contains("put(frond, jx, jy + 0.12", PropsJs);
    }

    [Fact]
    public void Sky_CloudsDriftWithDirectionalWind_AndSeamlessWrap()
    {
        // Must define directional drift vector and sufficient coverage span
        Assert.Contains("const WIND_DIR = new THREE.Vector2(0.88, 0.47).normalize();", SkyJs);
        Assert.Contains("const DRIFT_SPAN = 520;", SkyJs);

        // Movement must advance along wind vector with real delta-time
        Assert.Contains("u.x += WIND_DIR.x * u.speed * dt;", SkyJs);
        Assert.Contains("u.z += WIND_DIR.y * u.speed * dt;", SkyJs);

        // Must wrap seamlessly across boundary
        Assert.Contains("if (relX > HALF_SPAN)", SkyJs);
        Assert.Contains("else if (relX < -HALF_SPAN)", SkyJs);
        Assert.Contains("if (relZ > HALF_SPAN)", SkyJs);
        Assert.Contains("else if (relZ < -HALF_SPAN)", SkyJs);
    }
}
