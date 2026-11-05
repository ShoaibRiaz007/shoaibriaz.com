using System.Text.RegularExpressions;
using Xunit;

namespace Portfolio.Tests;

/// <summary>
/// Unit and regression test suite for Rowboat Exploration and Campfire Marshmallow Roasting
/// activities in Portfolio.World. Verifies physical rowboat structure with articulate oars,
/// rowing kinematics with coastline safety barriers, disembarkation safeguards, and
/// marshmallow cooking progression with audio sizzle and journal achievements.
/// </summary>
public class IslandActivitiesAndRowboatTests
{
    private static readonly string Root = RepoRoot();
    private static readonly string PropsJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "props.js"));
    private static readonly string ActivitiesJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "activities.js"));
    private static readonly string CharacterJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "character.js"));
    private static readonly string AudioJs = File.ReadAllText(Path.Combine(Root, "src", "Portfolio.World", "src", "audio.js"));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Portfolio.slnx not found above the test output folder.");
    }

    [Fact]
    public void Rowboat_StructureAndArticulateOars_AreAnchoredAndRendered()
    {
        // 3D Rowboat structure in props.js
        Assert.Contains("boat.name = 'island-rowboat';", PropsJs);
        Assert.Contains("boat.userData.seat = rowerSeat;", PropsJs);
        Assert.Contains("boat.userData.leftOar = oarPivot;", PropsJs);
        Assert.Contains("boat.userData.rightOar = oarPivot;", PropsJs);
        Assert.Contains("anchors.rowboat = boat;", PropsJs);

        // Oar components: shaft, blade, pin, inboard handle & grip
        Assert.Contains("const shaft = new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.02, 2.2, 6)", PropsJs);
        Assert.Contains("const blade = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.025, 0.55)", PropsJs);
        Assert.Contains("const handleShaft = new THREE.Mesh(new THREE.CylinderGeometry(0.022, 0.024, 0.48, 8)", PropsJs);
        Assert.Contains("const grip = new THREE.Mesh(new THREE.CylinderGeometry(0.020, 0.020, 0.14, 8), mat(0xdec096", PropsJs);

        // Procedural clinker hull, transom stern, wooden gunwales, stem post & brass bow ring
        Assert.Contains("const hullGeo = new THREE.BufferGeometry();", PropsJs);
        Assert.Contains("const transomGeo = new THREE.ExtrudeGeometry(transomShape", PropsJs);
        Assert.Contains("const railGeo = new THREE.TubeGeometry(railCurve", PropsJs);
        Assert.Contains("const stemBand = box(0.044, 0.58, 0.02, 0xd4a359", PropsJs);
        Assert.Contains("const bowRing = new THREE.Mesh(new THREE.TorusGeometry(0.045, 0.012, 6, 12)", PropsJs);

        // Animated swell bobbing pauses when boat is actively rowed
        Assert.Contains("if (boat.userData.isRowing) return;", PropsJs);
    }

    [Fact]
    public void RowboatActivity_KinematicsAndDisembarkationInvariants_AreWired()
    {
        // Journal checklist entry
        Assert.Contains("{ id: 'rowboat', title: 'Row out onto the water', hint: 'A wooden rowboat is tied to the pier.' }", ActivitiesJs);

        // Activity definition
        Assert.Contains("const rowboatActivity = {", ActivitiesJs);
        Assert.Contains("id: 'rowboat',", ActivitiesJs);
        Assert.Contains("label: 'Take the rowboat',", ActivitiesJs);

        // Rowing stroke acceleration, oar sfx, and water splash
        Assert.Contains("this.strokePhase += dt * 3.8;", ActivitiesJs);
        Assert.Contains("audio.play('oar');", ActivitiesJs);
        Assert.Contains("life.splash(", ActivitiesJs);

        // Camera chase tracking behind boat heading & bow wake ripple
        Assert.Contains("lookFrom(boat.position, this.heading + Math.PI", ActivitiesJs);
        Assert.Contains("rig.behind(this.heading", ActivitiesJs);
        Assert.Contains("this.lastWake = t;", ActivitiesJs);

        // Coastline collision and water depth constraint
        Assert.Contains("if (groundH < -0.22 && distCenter < 145)", ActivitiesJs);
        Assert.Contains("this.speed *= -0.3;", ActivitiesJs);

        // Buoyancy with dynamic water surface and wave pitch/roll
        Assert.Contains("const waterY = water.heightAt(boat.position.x, boat.position.z, t);", ActivitiesJs);
        Assert.Contains("const wavePitch = (fwdY - waterY) / 1.5;", ActivitiesJs);

        // Disembarkation safety: only step ashore when near pier mooring or shallow coastline
        Assert.Contains("const distMoor = boat.position.distanceTo(boat.userData.moor);", ActivitiesJs);
        Assert.Contains("if (distMoor < 4.2 || nearCoast)", ActivitiesJs);
        Assert.Contains("this.leaveLabel = 'Step ashore';", ActivitiesJs);
        Assert.Contains("this.leaveLabel = 'Row closer to shore to step out';", ActivitiesJs);

        // Candidate prompt beside pier
        Assert.Contains("add('rowboat', anchors.rowboat.position, 3.4, 'Take the rowboat', () => begin(rowboatActivity));", ActivitiesJs);
    }

    [Fact]
    public void Campfire_MarshmallowRoasting_ProgressionAndChime_AreWired()
    {
        // Roasting stick attached to avatar's right hand in character.js
        Assert.Contains("const roastStick = new THREE.Group();", CharacterJs);
        Assert.Contains("const marshmallow = new THREE.Mesh(mallowGeo, mallowMat);", CharacterJs);
        Assert.Contains("arms[1].hand.add(roastStick);", CharacterJs);
        Assert.Contains("root.userData = {", CharacterJs);
        Assert.Contains("roastStick, marshmallow,", CharacterJs);

        // Character roast pose
        Assert.Contains("ROAST: 'roast'", CharacterJs);
        Assert.Contains("roast: P({", CharacterJs);

        // Campfire roasting activity
        Assert.Contains("{ id: 'marshmallow', title: 'Roast a golden marshmallow'", ActivitiesJs);
        Assert.Contains("function campfireActivity(anchor, { camera })", ActivitiesJs);
        Assert.Contains("ui.setControls(['Hold <kbd>Space</kbd> to roast marshmallow over embers'", ActivitiesJs);

        // Sizzle sound effect
        Assert.Contains("audio.play('sizzle');", ActivitiesJs);

        // Toasting progression & golden brown achievement
        Assert.Contains("markDone('marshmallow');", ActivitiesJs);
        Assert.Contains("audio.play('chime', [79, 83, 86, 91]);", ActivitiesJs);
        Assert.Contains("Toasted to sweet, golden-brown perfection!", ActivitiesJs);
        Assert.Contains("avatar.userData.marshmallow.rotation.y += dt * 1.8;", ActivitiesJs);

        // Campfire hearth, glowing embers, criss-cross firewood, and terrain-adapted log seats
        Assert.Contains("const ashBed = new THREE.Mesh(new THREE.CylinderGeometry(0.72", PropsJs);
        Assert.Contains("const emberBed = new THREE.Mesh(new THREE.CylinderGeometry(0.55", PropsJs);
        Assert.Contains("const lx = Math.cos(a) * 1.65, lz = Math.sin(a) * 1.65;", PropsJs);
        Assert.Contains("const localY = (logGroundH - campGroundH) + 0.20;", PropsJs);

        // Roasting stick downward angle toward embers
        Assert.Contains("roastStick.rotation.set(1.98", CharacterJs);
    }

    [Fact]
    public void Character_RowPoseAndArmStrokeArticulation_AreImplemented()
    {
        // Character row pose and state
        Assert.Contains("ROW: 'row'", CharacterJs);
        Assert.Contains("row: P({ spine: 0.12, ls: [-0.85, -0.15], le: -0.65", CharacterJs);

        // Post-joints dynamic rowing articulation based on stroke phase
        Assert.Contains("if (state === CharacterPoseState.ROW && parts?.leftArm && parts?.rightArm)", CharacterJs);
        Assert.Contains("const stroke = u.rowStroke ?? 0;", CharacterJs);
        Assert.Contains("parts.leftArm.shoulder.rotation.x = -0.85 - pull * 0.35;", CharacterJs);
        Assert.Contains("parts.rightArm.shoulder.rotation.x = -0.85 - pull * 0.35;", CharacterJs);
    }

    [Fact]
    public void Audio_OarAndSizzleSFX_AreRegistered()
    {
        // Oar stroke sound
        Assert.Contains("oar() {", AudioJs);
        Assert.Contains("burst({ freq: 850, dur: 0.28, vol: 0.14, sweepTo: 320 });", AudioJs);

        // Sizzle frying/crackling sound
        Assert.Contains("sizzle() {", AudioJs);
        Assert.Contains("burst({ freq: 4200, type: 'bandpass', dur: 0.14, vol: 0.08, Q: 3 });", AudioJs);
    }
}
