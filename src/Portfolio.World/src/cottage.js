// Cozy Cottage Interior: walk-around indoor hub with warm hearth, retro CRT terminal,
// interactive vinyl record player with live Web Audio synthesis, bookshelf, and visitor guestbook.
// Invariant: transitioning between OUTSIDE and INSIDE preserves camera limits and player busy state.
import * as THREE from 'three';
import { box, mat } from './builders.js';
import { addSurface, groundAt } from './terrain.js';
import { setPose, SIT_DROP } from './character.js';

const cyl = (rt, rb, h, seg, color, opts) => {
    const m = new THREE.Mesh(new THREE.CylinderGeometry(rt, rb, h, seg), mat(color, opts));
    m.castShadow = m.receiveShadow = true;
    return m;
};

export const CottageState = {
    OUTSIDE: 'outside',
    ENTERING: 'entering',
    INSIDE: 'inside',
    SITTING: 'sitting',
    EXITING: 'exiting',
};

// Isolated offshore coordinates for the interior room
export const ROOM = {
    x: 220,
    y: 35,
    z: 220,
    w: 8.6,
    d: 6.8,
    h: 3.4,
};

// Web Audio synthesizer for the cozy vinyl record player
function createRadioAudio(save) {
    let actx = null, masterGain = null, crackleNode = null, currentTrackId = null, musicTimer = null;
    let synthNodes = [];
    let scheduledTimeouts = [];

    function ensureCtx() {
        if (save && save.settings && !save.settings.sound) return null;
        if (!actx) {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return null;
            actx = new AudioCtx();
            masterGain = actx.createGain();
            masterGain.gain.value = 0.45;
            masterGain.connect(actx.destination);
        }
        if (actx.state === 'suspended') actx.resume();
        return actx;
    }

    function stopAll() {
        if (musicTimer) { clearInterval(musicTimer); musicTimer = null; }
        for (const t of scheduledTimeouts) clearTimeout(t);
        scheduledTimeouts = [];
        for (const n of synthNodes) {
            try { n.stop?.(); } catch (err) { /* Node already stopped */ }
            try { n.disconnect?.(); } catch (err) { /* Node already disconnected */ }
        }
        synthNodes = [];
        if (crackleNode) {
            try { crackleNode.stop?.(); } catch (err) { /* Crackle node already stopped */ }
            try { crackleNode.disconnect?.(); } catch (err) { /* Crackle node already disconnected */ }
            crackleNode = null;
        }
        currentTrackId = null;
    }

    function startCrackle() {
        const c = ensureCtx();
        if (!c) return;
        const buf = c.createBuffer(1, c.sampleRate * 2, c.sampleRate);
        const data = buf.getChannelData(0);
        let last = 0;
        for (let i = 0; i < data.length; i++) {
            last = last * 0.9 + (Math.random() * 2 - 1) * 0.1;
            const pop = Math.random() < 0.0018 ? (Math.random() * 2 - 1) * 0.8 : 0;
            data[i] = last * 0.4 + pop;
        }
        const src = c.createBufferSource();
        src.buffer = buf;
        src.loop = true;
        const filter = c.createBiquadFilter();
        filter.type = 'bandpass';
        filter.frequency.value = 1600;
        const g = c.createGain();
        g.gain.value = 0.07;
        src.connect(filter).connect(g).connect(masterGain);
        src.start();
        crackleNode = src;
    }

    function playTone(freq, type = 'sine', duration = 0.8, gainVal = 0.2, detune = 0) {
        const c = ensureCtx();
        if (!c) return;
        const osc = c.createOscillator();
        const g = c.createGain();
        osc.type = type;
        osc.frequency.value = freq;
        osc.detune.value = detune;
        const now = c.currentTime;
        g.gain.setValueAtTime(0, now);
        g.gain.linearRampToValueAtTime(gainVal, now + 0.04);
        g.gain.exponentialRampToValueAtTime(0.001, now + duration);
        osc.connect(g).connect(masterGain);
        synthNodes.push(osc);
        osc.onended = () => {
            const idx = synthNodes.indexOf(osc);
            if (idx >= 0) synthNodes.splice(idx, 1);
            try { g.disconnect(); osc.disconnect(); } catch (_) { /* Nodes already disconnected */ }
        };
        osc.start(now);
        osc.stop(now + duration);
    }

    function playLoFiBeats() {
        stopAll();
        startCrackle();
        currentTrackId = 'lofi';
        const chords = [
            [261.63, 311.13, 392.00, 466.16], // Cm7
            [220.00, 261.63, 329.63, 392.00], // Am7
            [174.61, 220.00, 261.63, 329.63], // Fmaj7
            [196.00, 246.94, 293.66, 349.23], // G7
        ];
        let idx = 0;
        const step = () => {
            scheduledTimeouts = [];
            const chord = chords[idx % chords.length];
            chord.forEach((f, i) => {
                const t1 = setTimeout(() => playTone(f * 0.5, 'triangle', 2.2, 0.12, (Math.random() - 0.5) * 8), i * 60);
                const t2 = setTimeout(() => playTone(f, 'sine', 1.6, 0.08), i * 60);
                scheduledTimeouts.push(t1, t2);
            });
            // Soft kick / bass thud
            playTone(65, 'sine', 0.35, 0.3);
            idx++;
        };
        step();
        musicTimer = setInterval(step, 2400);
    }

    function playAcousticSunset() {
        stopAll();
        currentTrackId = 'acoustic';
        const notes = [
            196.00, 246.94, 293.66, 392.00, 440.00, 392.00, 293.66, 246.94,
            164.81, 196.00, 246.94, 329.63, 392.00, 329.63, 246.94, 196.00,
            146.83, 220.00, 293.66, 369.99, 440.00, 369.99, 293.66, 220.00,
            174.61, 220.00, 261.63, 349.23, 392.00, 349.23, 261.63, 220.00,
        ];
        let i = 0;
        const step = () => {
            const f = notes[i % notes.length];
            playTone(f, 'triangle', 0.9, 0.18, (i % 2 ? 4 : -4));
            if (i % 4 === 0) playTone(f * 0.5, 'sine', 1.2, 0.22);
            i++;
        };
        step();
        musicTimer = setInterval(step, 450);
    }

    function playMidnightRain() {
        stopAll();
        startCrackle();
        currentTrackId = 'rain';
        const melody = [329.63, 392.00, 440.00, 493.88, 440.00, 392.00, 329.63, 293.66];
        let i = 0;
        const step = () => {
            const f = melody[i % melody.length];
            playTone(f, 'sine', 1.8, 0.15, (Math.random() - 0.5) * 6);
            if (i % 2 === 0) playTone(f * 0.5, 'triangle', 2.0, 0.18);
            i++;
        };
        step();
        musicTimer = setInterval(step, 1600);
    }

    function playChiptune() {
        stopAll();
        currentTrackId = 'chiptune';
        const arp = [
            523.25, 659.25, 783.99, 1046.50,
            440.00, 523.25, 659.25, 880.00,
            349.23, 440.00, 523.25, 698.46,
            392.00, 493.88, 587.33, 783.99,
        ];
        let i = 0;
        const step = () => {
            const f = arp[i % arp.length];
            playTone(f, 'square', 0.14, 0.08);
            if (i % 4 === 0) playTone(f * 0.25, 'triangle', 0.25, 0.22);
            i++;
        };
        step();
        musicTimer = setInterval(step, 140);
    }

    return {
        get currentTrack() { return currentTrackId; },
        play(id) {
            if (id === 'lofi') playLoFiBeats();
            else if (id === 'acoustic') playAcousticSunset();
            else if (id === 'rain') playMidnightRain();
            else if (id === 'chiptune') playChiptune();
            else stopAll();
        },
        stop() { stopAll(); },
    };
}

export function createCottage(scene, { player, rig, audio, ui, save, colliders, anchors }) {
    const avatar = player.avatar;
    const radio = createRadioAudio(save);

    // ---------------- 3D Interior Meshes ----------------
    const interiorGroup = new THREE.Group();
    interiorGroup.position.set(ROOM.x, ROOM.y, ROOM.z);
    scene.add(interiorGroup);

    // Floor surface registration for walking footsteps
    addSurface({
        x: ROOM.x,
        z: ROOM.z,
        w: ROOM.w,
        d: ROOM.d,
        y: ROOM.y,
        kind: 'wood',
    });

    // Invisible boundary box colliders around interior room
    const wallThick = 0.5;
    colliders.push(
        { type: 'box', x: ROOM.x, z: ROOM.z - ROOM.d / 2 - wallThick / 2, w: ROOM.w + 1, d: wallThick, rot: 0, h: ROOM.h },
        { type: 'box', x: ROOM.x, z: ROOM.z + ROOM.d / 2 + wallThick / 2, w: ROOM.w + 1, d: wallThick, rot: 0, h: ROOM.h },
        { type: 'box', x: ROOM.x - ROOM.w / 2 - wallThick / 2, z: ROOM.z, w: wallThick, d: ROOM.d + 1, rot: 0, h: ROOM.h },
        { type: 'box', x: ROOM.x + ROOM.w / 2 + wallThick / 2, z: ROOM.z, w: wallThick, d: ROOM.d + 1, rot: 0, h: ROOM.h },
        // Fireplace collider
        { type: 'box', x: ROOM.x, z: ROOM.z - ROOM.d / 2 + 0.6, w: 2.6, d: 1.2, rot: 0, h: ROOM.h },
        // Desk collider
        { type: 'box', x: ROOM.x + 2.7, z: ROOM.z - 1.2, w: 1.8, d: 1.4, rot: 0, h: 1.5 },
        // Bookshelf collider
        { type: 'box', x: ROOM.x - 3.4, z: ROOM.z - 0.4, w: 1.2, d: 2.6, rot: 0, h: 2.8 },
        // Sideboard / Turntable collider
        { type: 'box', x: ROOM.x - 2.8, z: ROOM.z + 1.8, w: 1.6, d: 1.0, rot: 0, h: 1.3 }
    );

    // Camera blockers for interior walls and ceiling so the camera doesn't clip outside:
    if (rig?.blockers) {
        rig.blockers.push(
            { type: 'box', x: ROOM.x, y0: ROOM.y - 0.2, z: ROOM.z - ROOM.d / 2, w: ROOM.w + 1, d: wallThick, rot: 0, h: ROOM.h + 0.5 },
            { type: 'box', x: ROOM.x, y0: ROOM.y - 0.2, z: ROOM.z + ROOM.d / 2, w: ROOM.w + 1, d: wallThick, rot: 0, h: ROOM.h + 0.5 },
            { type: 'box', x: ROOM.x - ROOM.w / 2, y0: ROOM.y - 0.2, z: ROOM.z, w: wallThick, d: ROOM.d + 1, rot: 0, h: ROOM.h + 0.5 },
            { type: 'box', x: ROOM.x + ROOM.w / 2, y0: ROOM.y - 0.2, z: ROOM.z, w: wallThick, d: ROOM.d + 1, rot: 0, h: ROOM.h + 0.5 },
            { type: 'box', x: ROOM.x, y0: ROOM.y + ROOM.h - 0.25, z: ROOM.z, w: ROOM.w + 1, d: ROOM.d + 1, rot: 0, h: 0.6 }
        );
    }

    // Materials
    const WOOD_FLOOR = 0x94653e;
    const WALL_CREAM = 0xf5edd6;
    const OAK_DARK = 0x5a3620;
    const BRICK_RED = 0x8a4533;
    const STONE_GREY = 0x736d65;
    const RUG_RED = 0xa33f34;
    const LEATHER_RED = 0x7a2928;
    const BEIGE_PC = 0xd2c8b4;

    // Floor & Ceiling
    const floor = box(ROOM.w, 0.1, ROOM.d, WOOD_FLOOR);
    floor.position.set(0, -0.05, 0);
    floor.receiveShadow = true;
    interiorGroup.add(floor);

    const ceiling = box(ROOM.w + 0.4, 0.15, ROOM.d + 0.4, OAK_DARK);
    ceiling.position.set(0, ROOM.h, 0);
    interiorGroup.add(ceiling);

    // Ceiling beams
    for (let bx = -3.2; bx <= 3.2; bx += 1.6) {
        const beam = box(0.22, 0.25, ROOM.d, OAK_DARK);
        beam.position.set(bx, ROOM.h - 0.12, 0);
        interiorGroup.add(beam);
    }

    // Walls
    const wallN = box(ROOM.w, ROOM.h, 0.15, WALL_CREAM);
    wallN.position.set(0, ROOM.h / 2, -ROOM.d / 2);
    interiorGroup.add(wallN);

    const wallS = box(ROOM.w, ROOM.h, 0.15, WALL_CREAM);
    wallS.position.set(0, ROOM.h / 2, ROOM.d / 2);
    interiorGroup.add(wallS);

    const wallW = box(0.15, ROOM.h, ROOM.d, WALL_CREAM);
    wallW.position.set(-ROOM.w / 2, ROOM.h / 2, 0);
    interiorGroup.add(wallW);

    const wallE = box(0.15, ROOM.h, ROOM.d, WALL_CREAM);
    wallE.position.set(ROOM.w / 2, ROOM.h / 2, 0);
    interiorGroup.add(wallE);

    // Baseboards
    const baseboard = box(ROOM.w, 0.22, 0.08, OAK_DARK);
    baseboard.position.set(0, 0.11, -ROOM.d / 2 + 0.04);
    interiorGroup.add(baseboard);

    // Persian / Geometric Rug
    const rug = box(4.4, 0.02, 3.2, RUG_RED);
    rug.position.set(0, 0.01, 0.2);
    interiorGroup.add(rug);

    const rugBorder = box(4.6, 0.018, 3.4, 0xd4a86a);
    rugBorder.position.set(0, 0.009, 0.2);
    interiorGroup.add(rugBorder);

    // ---- Fireplace & Hearth ----
    const hearthGroup = new THREE.Group();
    hearthGroup.position.set(0, 0, -ROOM.d / 2 + 0.6);

    // Stone Chimney Breast with Open Arch (no solid block blocking the cavity!)
    const chimneyUpper = box(2.2, ROOM.h - 1.45, 0.65, STONE_GREY);
    chimneyUpper.position.set(0, 1.45 + (ROOM.h - 1.45) / 2, 0);
    const jambL = box(0.48, 1.3, 0.65, STONE_GREY);
    jambL.position.set(-0.86, 0.65, 0.02);
    const jambR = box(0.48, 1.3, 0.65, STONE_GREY);
    jambR.position.set(0.86, 0.65, 0.02);
    const lintel = box(2.2, 0.22, 0.68, STONE_GREY);
    lintel.position.set(0, 1.3 + 0.11, 0.03);
    hearthGroup.add(chimneyUpper, jambL, jambR, lintel);

    // Hollow Firebox Recess (Dark soot-brick cavity)
    const fireboxBack = box(1.24, 1.25, 0.06, 0x181412);
    fireboxBack.position.set(0, 0.625, -0.28);
    const fireboxL = box(0.06, 1.25, 0.62, 0x181412);
    fireboxL.position.set(-0.62, 0.625, 0.01);
    const fireboxR = box(0.06, 1.25, 0.62, 0x181412);
    fireboxR.position.set(0.62, 0.625, 0.01);
    const fireboxCeil = box(1.24, 0.06, 0.62, 0x181412);
    fireboxCeil.position.set(0, 1.25, 0.01);
    hearthGroup.add(fireboxBack, fireboxL, fireboxR, fireboxCeil);

    // Brick Hearthstone Floor and Dark Oak Mantel
    const hearthBase = box(2.5, 0.16, 1.2, BRICK_RED);
    hearthBase.position.set(0, 0.08, 0.32);
    const mantel = box(2.6, 0.16, 0.52, OAK_DARK);
    mantel.position.set(0, 1.5, 0.22);
    hearthGroup.add(hearthBase, mantel);

    // Mantel decor: mantle clock and brass candle holder
    const mantelClock = box(0.3, 0.22, 0.12, 0x3d2012);
    mantelClock.position.set(0, 1.58 + 0.11, 0.2);
    const candleBase = cyl(0.05, 0.06, 0.14, 8, 0xc49a45);
    candleBase.position.set(0.65, 1.58 + 0.07, 0.2);
    const candleWax = cyl(0.02, 0.02, 0.18, 8, 0xfcf5e8);
    candleWax.position.set(0.65, 1.58 + 0.23, 0.2);
    hearthGroup.add(mantelClock, candleBase, candleWax);

    // Wrought-iron andirons / log grate
    const ironL = box(0.06, 0.16, 0.38, 0x222222);
    ironL.position.set(-0.35, 0.16, 0.02);
    const ironR = box(0.06, 0.16, 0.38, 0x222222);
    ironR.position.set(0.35, 0.16, 0.02);
    const ironBar = box(0.76, 0.05, 0.05, 0x222222);
    ironBar.position.set(0, 0.22, 0.18);
    hearthGroup.add(ironL, ironR, ironBar);

    // Glowing Embers bed
    const embers = box(0.72, 0.06, 0.38, 0x280e04, { emissive: 0xd93800, emissiveIntensity: 2.2, roughness: 0.9 });
    embers.position.set(0, 0.14, 0.02);
    hearthGroup.add(embers);

    // Burning Firewood Logs (elevated in the grate)
    const log1 = cyl(0.065, 0.065, 0.68, 6, 0x422514);
    log1.rotation.z = Math.PI / 2;
    log1.position.set(0, 0.18, 0.0);
    const log2 = cyl(0.055, 0.055, 0.58, 6, 0x4a2a16);
    log2.rotation.set(0.2, 0.3, Math.PI / 2);
    log2.position.set(-0.08, 0.24, 0.06);
    const log3 = cyl(0.05, 0.05, 0.52, 6, 0x381d10);
    log3.rotation.set(-0.15, -0.3, Math.PI / 2);
    log3.position.set(0.1, 0.28, 0.02);
    hearthGroup.add(log1, log2, log3);

    // Multi-tier Animated Fireplace Flames
    const flameOuter = new THREE.Mesh(
        new THREE.ConeGeometry(0.3, 0.62, 6),
        new THREE.MeshBasicMaterial({ color: 0xff3800, transparent: true, opacity: 0.85, depthWrite: false })
    );
    flameOuter.position.set(0, 0.22 + 0.31, 0.04);

    const flameMid = new THREE.Mesh(
        new THREE.ConeGeometry(0.2, 0.48, 6),
        new THREE.MeshBasicMaterial({ color: 0xff9900, transparent: true, opacity: 0.9, depthWrite: false })
    );
    flameMid.position.set(-0.03, 0.22 + 0.24, 0.06);

    const flameCore = new THREE.Mesh(
        new THREE.ConeGeometry(0.11, 0.34, 6),
        new THREE.MeshBasicMaterial({ color: 0xfff468, transparent: true, opacity: 0.96, depthWrite: false })
    );
    flameCore.position.set(0.02, 0.22 + 0.17, 0.04);

    const flameSide = new THREE.Mesh(
        new THREE.ConeGeometry(0.09, 0.26, 5),
        new THREE.MeshBasicMaterial({ color: 0xff6600, transparent: true, opacity: 0.85, depthWrite: false })
    );
    flameSide.position.set(0.16, 0.2 + 0.13, 0.07);

    hearthGroup.add(flameOuter, flameMid, flameCore, flameSide);

    // Warm Fireplace Glow Light
    const hearthLight = new THREE.PointLight(0xff8e38, 2.9, 7.5);
    hearthLight.position.set(0, 0.65, 0.35);
    hearthGroup.add(hearthLight);

    interiorGroup.add(hearthGroup);

    // ---- Cozy Armchair (Sitting Station) ----
    const chairGroup = new THREE.Group();
    chairGroup.position.set(-1.2, 0, -1.2);
    chairGroup.rotation.y = 0.55;

    const chairSeat = box(0.9, 0.35, 0.9, LEATHER_RED);
    chairSeat.position.set(0, 0.35, 0);
    const chairBack = box(0.9, 0.9, 0.25, LEATHER_RED);
    chairBack.position.set(0, 0.75, -0.38);
    const armL = box(0.2, 0.55, 0.9, LEATHER_RED);
    armL.position.set(-0.45, 0.55, 0);
    const armR = box(0.2, 0.55, 0.9, LEATHER_RED);
    armR.position.set(0.45, 0.55, 0);
    const leg1 = cyl(0.04, 0.03, 0.25, 6, OAK_DARK);
    leg1.position.set(-0.35, 0.12, -0.35);
    const leg2 = leg1.clone(); leg2.position.set(0.35, 0.12, -0.35);
    const leg3 = leg1.clone(); leg3.position.set(-0.35, 0.12, 0.35);
    const leg4 = leg1.clone(); leg4.position.set(0.35, 0.12, 0.35);
    chairGroup.add(chairSeat, chairBack, armL, armR, leg1, leg2, leg3, leg4);
    interiorGroup.add(chairGroup);

    // ---- Bookshelf ----
    const shelfGroup = new THREE.Group();
    shelfGroup.position.set(-ROOM.w / 2 + 0.5, 0, -0.4);
    shelfGroup.rotation.y = Math.PI / 2;

    const shelfFrame = box(2.4, 2.7, 0.45, OAK_DARK);
    shelfFrame.position.set(0, 1.35, 0);
    shelfGroup.add(shelfFrame);

    // Books on shelves
    const bookColors = [0x9e2a2b, 0x335c67, 0xe09f3e, 0x540b0e, 0x2e6f40, 0x3a0ca3];
    for (let row = 0; row < 3; row++) {
        for (let b = 0; b < 9; b++) {
            const bw = 0.08 + (b % 3) * 0.02;
            const bh = 0.4 + (b % 4) * 0.05;
            const bk = box(bw, bh, 0.32, bookColors[(row * 3 + b) % bookColors.length]);
            bk.position.set(-0.9 + b * 0.2, 0.5 + row * 0.75, 0.05);
            shelfGroup.add(bk);
        }
    }
    interiorGroup.add(shelfGroup);

    // ---- Study Desk with Retro CRT Computer & Guestbook ----
    const deskGroup = new THREE.Group();
    deskGroup.position.set(ROOM.w / 2 - 1.1, 0, -1.2);
    deskGroup.rotation.y = -Math.PI / 2;

    const deskTop = box(2.2, 0.1, 1.1, OAK_DARK);
    deskTop.position.set(0, 0.85, 0);
    const dLegL = box(0.12, 0.85, 1.0, OAK_DARK);
    dLegL.position.set(-0.95, 0.42, 0);
    const dLegR = box(0.12, 0.85, 1.0, OAK_DARK);
    dLegR.position.set(0.95, 0.42, 0);
    deskGroup.add(deskTop, dLegL, dLegR);

    // Retro CRT Monitor & PC Tower
    const pcCase = box(0.35, 0.5, 0.6, BEIGE_PC);
    pcCase.position.set(0.75, 1.15, -0.15);

    const crtMonitor = box(0.65, 0.55, 0.55, BEIGE_PC);
    crtMonitor.position.set(-0.25, 1.25, -0.15);

    const crtScreenMat = mat(0x1a3320, { emissive: 0x48e068, emissiveIntensity: 0.85, roughness: 0.3 });
    const crtScreen = new THREE.Mesh(new THREE.PlaneGeometry(0.52, 0.42), crtScreenMat);
    crtScreen.position.set(-0.25, 1.25, 0.13);
    crtScreen.rotation.x = -0.06;

    const crtLight = new THREE.PointLight(0x48e068, 1.2, 3.2);
    crtLight.position.set(-0.25, 1.25, 0.4);

    const keyboard = box(0.55, 0.04, 0.22, BEIGE_PC);
    keyboard.position.set(-0.25, 0.92, 0.3);

    deskGroup.add(pcCase, crtMonitor, crtScreen, crtLight, keyboard);

    // Guestbook on Desk
    const guestbookMesh = box(0.42, 0.05, 0.32, 0x5a2d1d);
    guestbookMesh.position.set(0.5, 0.92, 0.25);
    guestbookMesh.rotation.y = 0.25;
    const quill = cyl(0.015, 0.005, 0.35, 5, 0xf6f2ea);
    quill.position.set(0.7, 0.98, 0.2);
    quill.rotation.set(0.4, 0, 0.5);
    deskGroup.add(guestbookMesh, quill);

    interiorGroup.add(deskGroup);

    // ---- Sideboard & Vintage Vinyl Turntable (Radio Station) ----
    const sideboardGroup = new THREE.Group();
    sideboardGroup.position.set(-ROOM.w / 2 + 1.2, 0, 1.8);
    sideboardGroup.rotation.y = Math.PI / 2;

    const credenza = box(1.8, 0.8, 0.8, OAK_DARK);
    credenza.position.set(0, 0.4, 0);
    sideboardGroup.add(credenza);

    // Turntable Cabinet
    const ttBase = box(0.75, 0.14, 0.65, 0x422616);
    ttBase.position.set(-0.35, 0.87, 0);

    // Spinning Vinyl Record
    const vinylGeo = new THREE.CylinderGeometry(0.24, 0.24, 0.02, 24);
    const vinylMat = mat(0x181818, { roughness: 0.3, metalness: 0.4 });
    const vinylDisc = new THREE.Mesh(vinylGeo, vinylMat);
    vinylDisc.position.set(-0.4, 0.95, 0);

    const labelGeo = new THREE.CylinderGeometry(0.09, 0.09, 0.022, 16);
    const labelMat = mat(0xd44a2c);
    const vinylLabel = new THREE.Mesh(labelGeo, labelMat);
    vinylLabel.position.set(0, 0.005, 0);
    vinylDisc.add(vinylLabel);

    // Tonearm
    const tonearm = cyl(0.015, 0.015, 0.28, 6, 0xd4af37);
    tonearm.rotation.z = Math.PI / 2;
    tonearm.position.set(-0.16, 0.96, -0.15);

    // Vintage Horn / Speaker
    const speaker = box(0.45, 0.5, 0.4, 0x3d2012);
    speaker.position.set(0.45, 1.05, 0);
    const speakerCloth = box(0.38, 0.42, 0.02, 0xdfcbaf);
    speakerCloth.position.set(0.45, 1.05, 0.21);

    sideboardGroup.add(ttBase, vinylDisc, tonearm, speaker, speakerCloth);
    interiorGroup.add(sideboardGroup);

    // ---- Interior Exit Door (South Wall) ----
    const exitDoorMesh = box(1.2, 2.2, 0.1, 0x5a2d1d);
    exitDoorMesh.position.set(0, 1.1, ROOM.d / 2 - 0.05);
    const doorKnob = box(0.08, 0.08, 0.1, 0xd4af37);
    doorKnob.position.set(0.45, 1.05, ROOM.d / 2 - 0.12);
    interiorGroup.add(exitDoorMesh, doorKnob);

    // Warm Ambient Interior Room Light
    const roomWarmLight = new THREE.PointLight(0xffdfa8, 1.4, 10);
    roomWarmLight.position.set(0, 2.8, 0);
    interiorGroup.add(roomWarmLight);

    // World interaction target positions
    const POS_DOOR_EXTERIOR = anchors.door ?? { x: -8, y: 0.9, z: 9.4, facing: 0.15 };
    const POS_DOOR_INTERIOR = { x: ROOM.x, y: ROOM.y, z: ROOM.z + ROOM.d / 2 - 1.2 };
    const POS_HEARTH_CHAIR = { x: ROOM.x - 1.2, y: ROOM.y, z: ROOM.z - 1.2 };
    const POS_COMPUTER_DESK = { x: ROOM.x + 2.0, y: ROOM.y, z: ROOM.z - 1.2 };
    const POS_TURNTABLE = { x: ROOM.x - 2.0, y: ROOM.y, z: ROOM.z + 1.8 };

    // ---------------- State Machine Context ----------------
    const fsmContext = {
        state: CottageState.OUTSIDE,
        musicPlaying: false,
        activeStation: null,
    };

    function animateFireplace(dt, t) {
        flameOuter.scale.y = 0.95 + Math.sin(t * 13) * 0.16 + Math.cos(t * 21) * 0.08;
        flameMid.scale.y = 0.92 + Math.sin(t * 15 + 1) * 0.18 + Math.cos(t * 27) * 0.09;
        flameCore.scale.y = 0.9 + Math.sin(t * 17 + 2) * 0.15;
        flameOuter.rotation.y += dt * 3.2;
        flameMid.rotation.y -= dt * 2.8;
        flameSide.scale.y = 0.85 + Math.sin(t * 19) * 0.22;
        hearthLight.intensity = 2.7 + Math.sin(t * 11) * 0.65 + Math.sin(t * 29) * 0.2;
    }

    // ---------------- State Handlers ----------------
    const states = {
        [CottageState.OUTSIDE]: {
            enter(ctx) {
                ctx.activeStation = null;
                player.setBusy(false);
            },
            update() {},
            exit() {},
        },

        [CottageState.ENTERING]: {
            enter(ctx) {
                audio.play('door');
                player.setBusy(true);
                const ticket = (ctx.fadeTicket = (ctx.fadeTicket ?? 0) + 1);
                ui.fade(() => {
                    if (machine.current !== CottageState.ENTERING || ctx.fadeTicket !== ticket) return;
                    player.place(POS_DOOR_INTERIOR.x, POS_DOOR_INTERIOR.z, 0);
                    rig.override = null;
                    rig.limits = { min: 1.6, max: 4.5 };
                    rig.setView({ yaw: 0, pitch: 0.35, distance: 3.2 }, true);
                    machine.transition(CottageState.INSIDE);
                    ui.toast({ icon: '🏡', title: 'Cozy Cottage', body: 'Warmth, records, and a retro terminal await.' });
                    save.journal.seen.cottage = true;
                    save.journal.done.cottage = true;
                    ui.journalChanged();
                });
            },
            update() {},
            exit() {},
        },

        [CottageState.INSIDE]: {
            enter(ctx) {
                ctx.activeStation = null;
                player.setBusy(false);
                rig.limits = { min: 1.6, max: 4.5 };
            },
            update(ctx, dt, t) {
                // Fireplace flame & light animation
                animateFireplace(dt, t);

                // CRT monitor subtle flicker
                crtScreenMat.emissiveIntensity = 0.85 + Math.sin(t * 30) * 0.04;

                // Turntable vinyl spinning
                if (radio.currentTrack) {
                    vinylDisc.rotation.y += dt * 3.8;
                }
            },
            exit() {},
        },

        [CottageState.SITTING]: {
            enter() {
                player.setBusy(true, 'sit');
                const chairFacing = 0.55;
                const chairBack = new THREE.Vector3(Math.sin(chairFacing), 0, Math.cos(chairFacing)).multiplyScalar(-0.06);
                avatar.position.set(POS_HEARTH_CHAIR.x + chairBack.x, ROOM.y + 0.52 - SIT_DROP, POS_HEARTH_CHAIR.z + chairBack.z);
                avatar.rotation.set(0, chairFacing, 0);
                player.heading = chairFacing;
                setPose(avatar, 'sit');
                rig.override = {
                    focus: new THREE.Vector3(POS_HEARTH_CHAIR.x + 0.1, ROOM.y + 0.85, POS_HEARTH_CHAIR.z),
                    limits: { min: 1.4, max: 2.8 },
                };
                rig.setView({ yaw: chairFacing + Math.PI - 0.45, pitch: 0.22, distance: 2.2 });
                ui.setControls(['Warming hands by the fire', '<kbd>E</kbd> Stand up']);
            },
            update(ctx, dt, t) {
                animateFireplace(dt, t);
            },
            exit() {
                rig.override = null;
                player.setBusy(false);
                const f = new THREE.Vector3(Math.sin(0.55), 0, Math.cos(0.55)).multiplyScalar(0.75);
                player.place(POS_HEARTH_CHAIR.x + f.x, POS_HEARTH_CHAIR.z + f.z, 0.55);
                ui.setControls(null);
            },
        },

        [CottageState.EXITING]: {
            enter(ctx) {
                audio.play('door');
                player.setBusy(true);
                radio.stop();
                const ticket = (ctx.fadeTicket = (ctx.fadeTicket ?? 0) + 1);
                ui.fade(() => {
                    if (machine.current !== CottageState.EXITING || ctx.fadeTicket !== ticket) return;
                    const p = anchors.porch ?? { x: -8, y: 0.9, z: 9.0, facing: 0.15 };
                    player.place(p.x, p.z, p.facing ?? 0.15);
                    rig.limits = { min: 2.6, max: 28 };
                    rig.setView({ yaw: (p.facing ?? 0.15) + Math.PI, pitch: 0.35, distance: 8 }, true);
                    machine.transition(CottageState.OUTSIDE);
                });
            },
            update() {},
            exit() {},
        },
    };

    // Functional State Machine engine
    function createStateMachine(initialState, stateMap, context) {
        let current = initialState;
        stateMap[current]?.enter?.(context);

        return {
            get current() { return current; },
            transition(next, ...args) {
                if (current === next) return;
                stateMap[current]?.exit?.(context);
                current = next;
                stateMap[current]?.enter?.(context, ...args);
            },
            update(dt, t) {
                stateMap[current]?.update?.(context, dt, t);
            },
        };
    }

    const machine = createStateMachine(CottageState.OUTSIDE, states, fsmContext);

    // ---------------- Interactive Modals ----------------
    function openRadioModal() {
        audio.play('click');
        ui.info({
            kicker: 'Vintage Turntable & Island Radio',
            title: 'Vinyl Music Player',
            build(el, h) {
                const tracks = [
                    { id: 'lofi', icon: '☕', name: "Shoaib's Lo-Fi Beats", desc: 'Mellow chords, vinyl crackle & coding flow.' },
                    { id: 'acoustic', icon: '🎸', name: 'Island Acoustic Sunset', desc: 'Warm fingerstyle acoustic guitar melodies.' },
                    { id: 'rain', icon: '🌧️', name: 'Midnight Rain & Keys', desc: 'Gentle piano notes over distant sea rain.' },
                    { id: 'chiptune', icon: '👾', name: '8-Bit Chiptune Island', desc: 'Nostalgic NES-style square-wave arpeggios.' },
                ];

                const list = h('div', { class: 'turntable-tracks' });
                tracks.forEach(tr => {
                    const active = radio.currentTrack === tr.id;
                    const btn = h('button', {
                        class: `chip track-btn ${active ? 'track-active' : ''}`,
                        type: 'button',
                    }, `${tr.icon} ${tr.name}`);
                    btn.onclick = () => {
                        radio.play(tr.id);
                        audio.play('pop');
                        ui.toast({ icon: tr.icon, title: 'Now Playing', body: tr.name });
                        document.querySelectorAll('.track-btn').forEach(b => b.classList.remove('track-active'));
                        btn.classList.add('track-active');
                    };
                    list.append(btn);
                });

                const stopBtn = h('button', { class: 'chip', type: 'button' }, '⏹️ Turn off music');
                stopBtn.onclick = () => {
                    radio.stop();
                    document.querySelectorAll('.track-btn').forEach(b => b.classList.remove('track-active'));
                    ui.toast({ icon: '⏹️', title: 'Radio', body: 'Music paused.' });
                };

                el.append(
                    h('p', {}, 'Select a vinyl record to play through the cottage horn speaker:'),
                    list,
                    h('div', { class: 'info-actions', style: 'margin-top: 16px; display: flex; gap: 8px;' },
                        stopBtn,
                        h('button', { class: 'btn-primary', type: 'button', 'data-close': '' }, 'Done')
                    )
                );
            },
        });
    }

    function openTerminalModal() {
        audio.play('click');
        ui.info({
            kicker: 'SHOAIB-OS v4.2 [640K RAM]',
            title: 'Retro CRT Computer Terminal',
            build(el, h) {
                const termBox = h('div', { class: 'crt-terminal' });
                const screen = h('div', { class: 'crt-screen' });
                const output = h('div', { class: 'crt-output' });
                const inputRow = h('div', { class: 'crt-input-row' });
                const promptSign = h('span', { class: 'crt-prompt' }, 'C:\\>');
                const input = h('input', {
                    class: 'crt-input',
                    type: 'text',
                    placeholder: 'Type help, about, skills, snake, clear...',
                    autofocus: 'true',
                });

                output.innerHTML = [
                    '========================================',
                    '  SHOAIB RIAZ ARCHITECTURE TERMINAL v4.2',
                    '  Connected to Cottage Mainframe [ONLINE]',
                    '========================================',
                    'Type <b style="color:#52e196">help</b> for commands or <b style="color:#f6c344">snake</b> to play!',
                    '',
                ].join('<br>');

                const print = (text, color = '#52e196') => {
                    const line = document.createElement('div');
                    line.style.color = color;
                    line.innerHTML = text;
                    output.append(line);
                    screen.scrollTop = screen.scrollHeight;
                };

                const escapeHtml = (str) => String(str).replace(/[&<>'"]/g, tag => ({
                    '&': '&amp;',
                    '<': '&lt;',
                    '>': '&gt;',
                    "'": '&#39;',
                    '"': '&quot;',
                }[tag] || tag));

                // Snake Game State Machine inside the terminal
                let snakeInterval = null;
                let activeSnakeKeyHandler = null;

                const stopSnake = () => {
                    if (snakeInterval) {
                        clearInterval(snakeInterval);
                        snakeInterval = null;
                    }
                    if (activeSnakeKeyHandler) {
                        window.removeEventListener('keydown', activeSnakeKeyHandler);
                        activeSnakeKeyHandler = null;
                    }
                };

                // Dialog close listener handles Esc key or header 'X' dismiss
                const infoDialog = document.getElementById('info');
                infoDialog?.addEventListener('close', stopSnake, { once: true });

                const startSnake = () => {
                    stopSnake();
                    print('--- RETRO SNAKE ARCADE ---', '#f6c344');
                    print('Use Arrow Keys or WASD. Eat ★ to grow! Press ESC to exit game.', '#fff');

                    const W = 20, H = 10;
                    let snake = [{ x: 5, y: 5 }, { x: 4, y: 5 }, { x: 3, y: 5 }];
                    let dir = { x: 1, y: 0 };
                    let food = { x: 12, y: 5 };
                    let score = 0;

                    const boardDiv = document.createElement('pre');
                    boardDiv.className = 'crt-snake-board';
                    output.append(boardDiv);

                    const keyHandler = (e) => {
                        const k = e.key.toLowerCase();
                        if (k === 'escape') {
                            stopSnake();
                            print('Exited Snake game.', '#aaa');
                            e.preventDefault();
                            return;
                        }
                        if (k === 'arrowup' || k === 'w') { if (dir.y === 0) dir = { x: 0, y: -1 }; e.preventDefault(); }
                        if (k === 'arrowdown' || k === 's') { if (dir.y === 0) dir = { x: 0, y: 1 }; e.preventDefault(); }
                        if (k === 'arrowleft' || k === 'a') { if (dir.x === 0) dir = { x: -1, y: 0 }; e.preventDefault(); }
                        if (k === 'arrowright' || k === 'd') { if (dir.x === 0) dir = { x: 1, y: 0 }; e.preventDefault(); }
                    };
                    activeSnakeKeyHandler = keyHandler;
                    window.addEventListener('keydown', keyHandler);

                    snakeInterval = setInterval(() => {
                        const head = { x: snake[0].x + dir.x, y: snake[0].y + dir.y };
                        // Wrap walls
                        if (head.x < 0) head.x = W - 1;
                        if (head.x >= W) head.x = 0;
                        if (head.y < 0) head.y = H - 1;
                        if (head.y >= H) head.y = 0;

                        // Self collision
                        if (snake.some(s => s.x === head.x && s.y === head.y)) {
                            stopSnake();
                            print(`GAME OVER! Final Score: ${score}`, '#e8743b');
                            audio.play('pop');
                            return;
                        }

                        snake.unshift(head);
                        if (head.x === food.x && head.y === food.y) {
                            score += 10;
                            audio.play('chime');
                            food = { x: Math.floor(Math.random() * W), y: Math.floor(Math.random() * H) };
                        } else {
                            snake.pop();
                        }

                        // Render board
                        let str = `┌${'─'.repeat(W)}┐ Score: ${score}\n`;
                        for (let y = 0; y < H; y++) {
                            str += '│';
                            for (let x = 0; x < W; x++) {
                                if (snake[0].x === x && snake[0].y === y) str += 'O';
                                else if (snake.some(s => s.x === x && s.y === y)) str += 'o';
                                else if (food.x === x && food.y === y) str += '★';
                                else str += ' ';
                            }
                            str += '│\n';
                        }
                        str += `└${'─'.repeat(W)}┘`;
                        boardDiv.textContent = str;
                        screen.scrollTop = screen.scrollHeight;
                    }, 160);
                };

                const executeCmd = (cmd) => {
                    const c = cmd.trim().toLowerCase();
                    print(`C:\\> ${escapeHtml(cmd)}`, '#fff');
                    stopSnake();

                    if (c === 'help') {
                        print('Available Commands:');
                        print('  <b>about</b>    - Engineering background & bio');
                        print('  <b>skills</b>   - Technical expertise');
                        print('  <b>projects</b> - Featured system designs');
                        print('  <b>snake</b>    - Play Retro Snake ASCII arcade');
                        print('  <b>stats</b>    - Island exploration telemetry');
                        print('  <b>matrix</b>   - Digital rain mode');
                        print('  <b>clear</b>    - Clear screen');
                        print('  <b>exit</b>     - Leave terminal');
                    } else if (c === 'about') {
                        print('Shoaib Riaz — Senior Software Engineer & Solutions Architect.');
                        print('12+ years designing distributed systems, cloud microservices, and interactive Web3D.');
                    } else if (c === 'skills') {
                        print('• Backend: .NET 10, C#, ASP.NET Core, EF Core, Microservices');
                        print('• Architecture: Clean Architecture, DDD, Event-Driven, CQRS');
                        print('• Web & 3D: Three.js, TypeScript, WebGL, Shader programming');
                        print('• Cloud: Azure, Docker, Kubernetes, CI/CD, Redis, SQL Server');
                    } else if (c === 'projects') {
                        print('• <b>Portfolio.World</b> - Procedural 3D Island Engine');
                        print('• <b>Clean Architecture Harness</b> - Enterprise Domain Templates');
                        print('• Check the Island Gallery plaza for interactive 3D project plaques!');
                    } else if (c === 'stats') {
                        print(`• Island Visits: ${save.journal.visits}`);
                        print(`• Flowers Planted: ${save.journal.planted}`);
                        print(`• Seashells Collected: ${save.journal.shells.length} / 12`);
                        print(`• Treasures Discovered: ${save.journal.treasures.length} / 4`);
                    } else if (c === 'snake') {
                        startSnake();
                    } else if (c === 'matrix') {
                        print('Wake up, Neo... The Matrix has you.', '#52e196');
                    } else if (c === 'clear') {
                        output.innerHTML = '';
                    } else if (c === 'exit') {
                        document.querySelector('#info [data-close]')?.click();
                    } else {
                        print(`Bad command or filename: "${escapeHtml(cmd)}". Type "help" for options.`, '#e8743b');
                    }
                };

                input.onkeydown = (e) => {
                    if (e.key === 'Enter') {
                        executeCmd(input.value);
                        input.value = '';
                    }
                };

                inputRow.append(promptSign, input);
                screen.append(output, inputRow);
                termBox.append(screen);

                // Quick command pills
                const quickRow = h('div', { class: 'crt-quick-pills' });
                ['help', 'about', 'skills', 'snake', 'stats', 'clear'].forEach(q => {
                    const pill = h('button', { class: 'chip crt-pill', type: 'button' }, q);
                    pill.onclick = () => { executeCmd(q); input.focus(); };
                    quickRow.append(pill);
                });

                el.append(
                    termBox,
                    quickRow,
                    h('div', { class: 'info-actions', style: 'margin-top: 12px;' },
                        h('button', {
                            class: 'btn-primary',
                            type: 'button',
                            'data-close': '',
                            onclick: () => { stopSnake(); }
                        }, 'Close Terminal')
                    )
                );
                setTimeout(() => input.focus(), 150);
            },
        });
    }

    function openGuestbookModal() {
        audio.play('click');
        ui.info({
            kicker: 'Cottage Study Desk',
            title: 'Island Visitor Guestbook',
            build(el, h) {
                const reactions = [
                    { id: 'heart', icon: '❤️', label: 'Loved the experience' },
                    { id: 'coffee', icon: '☕', label: 'Super cozy & peaceful' },
                    { id: 'rocket', icon: '🚀', label: 'Incredible tech & performance' },
                    { id: 'sparkles', icon: '✨', label: 'Inspiring engineering' },
                ];

                const currentReaction = save.cottage?.reaction ?? null;
                const reactionBox = h('div', { class: 'guestbook-reactions' });

                reactions.forEach(r => {
                    const isSelected = currentReaction === r.id;
                    const btn = h('button', {
                        class: `chip ${isSelected ? 'reaction-active' : ''}`,
                        type: 'button',
                    }, `${r.icon} ${r.label}`);
                    btn.onclick = () => {
                        if (!save.cottage) save.cottage = {};
                        save.cottage.reaction = r.id;
                        audio.play('chime');
                        ui.toast({ icon: r.icon, title: 'Guestbook Signed!', body: `Thank you for visiting: "${r.label}"` });
                        document.querySelectorAll('.guestbook-reactions .chip').forEach(b => b.classList.remove('reaction-active'));
                        btn.classList.add('reaction-active');
                    };
                    reactionBox.append(btn);
                });

                el.append(
                    h('p', {}, 'Thank you for taking the time to explore this interactive 3D world. Leave a note or signature in the cottage ledger:'),
                    h('p', { style: 'font-weight: 800; margin-bottom: 6px;' }, 'Sign your reaction:'),
                    reactionBox,
                    h('div', { class: 'info-actions', style: 'margin-top: 16px;' },
                        h('button', { class: 'btn-primary', type: 'button', 'data-close': '' }, 'Save & Close')
                    )
                );
            },
        });
    }

    return {
        update(dt, t) {
            machine.update(dt, t);
        },
        get state() { return machine.current; },
        // Candidate queries for activities.js interaction loop
        candidate(pos) {
            // Outside: approaching cottage door on the porch
            if (machine.current === CottageState.OUTSIDE) {
                const door = POS_DOOR_EXTERIOR;
                const d = Math.hypot(pos.x - door.x, pos.z - door.z);
                if (d < 2.5) {
                    return {
                        id: 'cottage',
                        pos: door,
                        d,
                        label: 'Enter cozy cottage',
                        run: () => machine.transition(CottageState.ENTERING),
                    };
                }
            }

            // Inside: free walking between interactive stations
            if (machine.current === CottageState.INSIDE) {
                const stations = [
                    {
                        id: 'cottage_chair',
                        pos: POS_HEARTH_CHAIR,
                        d: Math.hypot(pos.x - POS_HEARTH_CHAIR.x, pos.z - POS_HEARTH_CHAIR.z),
                        r: 1.6,
                        label: 'Sit by the fireplace',
                        run: () => machine.transition(CottageState.SITTING),
                    },
                    {
                        id: 'cottage_radio',
                        pos: POS_TURNTABLE,
                        d: Math.hypot(pos.x - POS_TURNTABLE.x, pos.z - POS_TURNTABLE.z),
                        r: 1.7,
                        label: 'Play records / Tune radio',
                        run: openRadioModal,
                    },
                    {
                        id: 'cottage_terminal',
                        pos: POS_COMPUTER_DESK,
                        d: Math.hypot(pos.x - POS_COMPUTER_DESK.x, pos.z - POS_COMPUTER_DESK.z),
                        r: 1.5,
                        label: 'Use retro computer terminal',
                        run: openTerminalModal,
                    },
                    {
                        id: 'cottage_guestbook',
                        pos: { x: ROOM.x + 1.2, z: ROOM.z - 1.2 },
                        d: Math.hypot(pos.x - (ROOM.x + 1.2), pos.z - (ROOM.z - 1.2)),
                        r: 1.5,
                        label: 'Sign visitor guestbook',
                        run: openGuestbookModal,
                    },
                    {
                        id: 'cottage_exit',
                        pos: POS_DOOR_INTERIOR,
                        d: Math.hypot(pos.x - POS_DOOR_INTERIOR.x, pos.z - POS_DOOR_INTERIOR.z),
                        r: 1.8,
                        label: 'Step outside to porch',
                        run: () => machine.transition(CottageState.EXITING),
                    },
                ].filter(s => s.d < s.r);

                if (stations.length > 0) {
                    stations.sort((a, b) => a.d - b.d);
                    return stations[0];
                }
            }

            // Seated in armchair
            if (machine.current === CottageState.SITTING) {
                return {
                    id: 'cottage_stand',
                    pos: pos,
                    d: 0,
                    label: 'Stand up',
                    run: () => machine.transition(CottageState.INSIDE),
                };
            }

            return null;
        },
        get isSitting() { return machine.current === CottageState.SITTING; },
        get isInside() {
            return machine.current === CottageState.INSIDE ||
                   machine.current === CottageState.SITTING ||
                   machine.current === CottageState.ENTERING;
        },
        get isBusy() {
            return machine.current === CottageState.SITTING ||
                   machine.current === CottageState.ENTERING ||
                   machine.current === CottageState.EXITING;
        },
        exitToPorch() {
            fsmContext.fadeTicket = (fsmContext.fadeTicket ?? 0) + 1;
            radio.stop();
            rig.limits = { min: 2.6, max: 28 };
            if (machine.current === CottageState.INSIDE || machine.current === CottageState.SITTING || machine.current === CottageState.ENTERING || machine.current === CottageState.EXITING) {
                machine.transition(CottageState.OUTSIDE);
            }
        },
    };
}
