// The site owner's avatar, modelled on their photo: short dark hair swept up at the front, medium warm skin,
// clean-shaven, a charcoal check blazer over a light-blue shirt, dark trousers. Built at real scale (1.8 m) with
// smooth stylised forms and a proper joint chain (pelvis → spine → chest → neck → head, shoulder → elbow → hand,
// hip → knee → ankle), so locomotion and poses read naturally.
//
// Locomotion is procedural and stride-driven: the gait phase advances with distance travelled, not time, so the
// feet keep pace with the ground at any speed (no gliding), and walk blends continuously into run.
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { mat } from './builders.js';

export const AVATAR_SCALE = 1;
export const HIP = 0.92;                 // hip joint height when standing
export const SIT_DROP = HIP - 0.075;     // seat surface → avatar origin, for sitting poses

export const LOOK = {
    skin: 0xc99a7c, skinShade: 0xb5866a, lips: 0x9c5d50,
    hair: 0x1a1310,
    blazer: 0x3b3e44, blazerLight: 0x4b4f57,
    shirt: 0x9fc0e8, trousers: 0x2c3037, shoes: 0x2a1d16, sole: 0x5a4636,
    eyes: 0x2a1a12,
};

let checkTexture = null;
/** Subtle charcoal check, like the blazer in the photo. */
function blazerMap() {
    if (checkTexture) return checkTexture;
    const c = document.createElement('canvas');
    c.width = c.height = 64;
    const g = c.getContext('2d');
    g.fillStyle = '#6a6e77';
    g.fillRect(0, 0, 64, 64);
    g.fillStyle = 'rgba(255,255,255,.10)';
    for (let i = 0; i < 64; i += 16) { g.fillRect(i, 0, 2, 64); g.fillRect(0, i, 64, 2); }
    g.fillStyle = 'rgba(0,0,0,.12)';
    for (let i = 8; i < 64; i += 16) { g.fillRect(i, 0, 1, 64); g.fillRect(0, i, 64, 1); }
    checkTexture = new THREE.CanvasTexture(c);
    checkTexture.colorSpace = THREE.SRGBColorSpace;
    checkTexture.wrapS = checkTexture.wrapT = THREE.RepeatWrapping;
    checkTexture.repeat.set(3, 3);
    return checkTexture;
}

const smooth = (color, o = {}) => mat(color, { flatShading: false, roughness: 0.72, ...o });
let blazerMat = null;
const cloth = () => (blazerMat ??= new THREE.MeshStandardMaterial({ color: 0xffffff, map: blazerMap(), roughness: 0.85 }));

function mesh(geo, material, x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0, s = null) {
    const m = new THREE.Mesh(geo, typeof material === 'number' ? smooth(material) : material);
    m.position.set(x, y, z);
    m.rotation.set(rx, ry, rz);
    if (s) m.scale.set(...s);
    m.castShadow = true;
    return m;
}
const capsule = (r, len) => new THREE.CapsuleGeometry(r, len, 6, 12);
/** A tapered limb segment: a cone frustum with rounded ends (reads as muscle, not a tube). */
function limb(rTop, rBottom, len) {
    const parts = [
        new THREE.CylinderGeometry(rTop, rBottom, len, 14, 1, true),
        new THREE.SphereGeometry(rTop, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2).translate(0, len / 2, 0),
        new THREE.SphereGeometry(rBottom, 14, 8, 0, Math.PI * 2, Math.PI / 2, Math.PI / 2).translate(0, -len / 2, 0),
    ].map(g => g.toNonIndexed());
    return mergeGeometries(parts);
}
const sphere = (r, w = 20, h = 14) => new THREE.SphereGeometry(r, w, h);

/** A rounded box (corners pulled toward an ellipsoid). */
function softBox(w, h, d, segs = 3, k = 0.72) {
    const g = new THREE.BoxGeometry(w, h, d, segs, segs, segs);
    const p = g.attributes.position, v = new THREE.Vector3(), n = new THREE.Vector3();
    for (let i = 0; i < p.count; i++) {
        v.fromBufferAttribute(p, i);
        n.copy(v).multiply(new THREE.Vector3(2 / w, 2 / h, 2 / d));
        const len = n.length();
        if (len > 1) v.multiplyScalar(k + (1 - k) / len);
        p.setXYZ(i, v.x, v.y, v.z);
    }
    g.computeVertexNormals();
    return g;
}

/** Merge a joint's own meshes by material (children joints are left alone): ~40 parts → ~25 draw calls. */
function mergeDirect(group) {
    const buckets = new Map();
    for (const m of [...group.children]) {
        if (!m.isMesh) continue;
        m.updateMatrix();
        let g = m.geometry.clone().applyMatrix4(m.matrix);
        if (g.index) g = g.toNonIndexed();
        for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(k)) g.deleteAttribute(k);
        if (!g.attributes.uv) g.setAttribute('uv', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 2), 2));
        if (!buckets.has(m.material)) buckets.set(m.material, []);
        buckets.get(m.material).push(g);
        group.remove(m);
    }
    for (const [material, geos] of buckets) {
        const merged = new THREE.Mesh(mergeGeometries(geos), material);
        merged.castShadow = true;
        group.add(merged);
    }
}

function buildHead() {
    const head = new THREE.Group();
    // Skull and jaw: an oval face with a defined, clean-shaven chin.
    head.add(mesh(sphere(0.135, 28, 20), LOOK.skin, 0, 0.12, 0, 0, 0, 0, [0.9, 1.1, 1]));
    head.add(mesh(sphere(0.105, 24, 16), LOOK.skin, 0, 0.05, 0.022, 0, 0, 0, [0.86, 0.72, 0.9]));
    for (const s of [-1, 1]) {
        head.add(mesh(sphere(0.03, 12, 10), LOOK.skinShade, s * 0.12, 0.12, -0.005, 0, 0, 0, [0.45, 1, 0.75]));     // ears
        head.add(mesh(sphere(0.016, 14, 10), 0xf6f1ea, s * 0.045, 0.135, 0.112, 0, 0, 0, [1.35, 0.85, 0.6]));        // eye whites
        head.add(mesh(sphere(0.0105, 12, 10), LOOK.eyes, s * 0.045, 0.135, 0.12));                                  // irises
        head.add(mesh(sphere(0.003, 6, 6), 0xffffff, s * 0.045 + 0.004, 0.139, 0.13));                             // catchlights
        head.add(mesh(softBox(0.048, 0.011, 0.014, 2, 0.6), LOOK.hair, s * 0.046, 0.165, 0.114, 0, 0, s * 0.06));    // brows, relaxed
    }
    head.add(mesh(new THREE.ConeGeometry(0.019, 0.055, 10), LOOK.skinShade, 0, 0.1, 0.13, 1.2, 0, 0));                 // nose
    const smile = new THREE.TorusGeometry(0.03, 0.0055, 6, 16, Math.PI * 0.7);
    head.add(mesh(smile, LOOK.lips, 0, 0.068, 0.111, 0.25, 0, Math.PI + Math.PI * 0.15));                            // closed, slight smile
    // Hair: short at the back and sides, real volume on top, swept up and back from the forehead.
    head.add(mesh(new THREE.SphereGeometry(0.141, 28, 16, 0, Math.PI * 2, 0, Math.PI * 0.44), LOOK.hair, 0, 0.13, -0.012, -0.32, 0, 0, [0.93, 1.1, 1.03]));
    head.add(mesh(new THREE.SphereGeometry(0.139, 24, 14, Math.PI, Math.PI, 0, Math.PI * 0.72), LOOK.hair, 0, 0.12, -0.006, 0, 0, 0, [0.93, 1.05, 1.02]));
    head.add(mesh(sphere(0.1, 20, 12), LOOK.hair, 0, 0.262, 0.0, -0.2, 0, 0, [1.08, 0.55, 1.15]));                 // top volume
    head.add(mesh(sphere(0.07, 18, 12), LOOK.hair, 0.008, 0.29, 0.07, -0.75, 0, 0.06, [1.4, 0.95, 1.1]));           // front quiff, swept up
    head.add(mesh(sphere(0.05, 14, 10), LOOK.hair, -0.045, 0.295, 0.085, -0.9, 0, 0.3, [1.3, 0.8, 1]));             // side sweep
    for (const s of [-1, 1]) head.add(mesh(softBox(0.012, 0.045, 0.024, 2, 0.6), LOOK.hair, s * 0.117, 0.105, 0.03)); // sideburns
    mergeDirect(head);
    return head;
}

/** Builds the full figure. Returns the group with joints in userData for animation. */
export function createCharacter() {
    const root = new THREE.Group();
    const pelvis = new THREE.Group();
    pelvis.position.y = HIP;
    root.add(pelvis);
    pelvis.add(mesh(softBox(0.32, 0.17, 0.2), LOOK.trousers, 0, -0.03, 0));

    const spine = new THREE.Group();
    spine.position.y = 0.06;
    pelvis.add(spine);
    const chest = new THREE.Group();
    chest.position.y = 0.12;
    spine.add(chest);
    // Blazer body, hem, rounded shoulders.
    chest.add(mesh(softBox(0.35, 0.4, 0.215, 3, 0.66), cloth(), 0, 0.15, 0));
    chest.add(mesh(softBox(0.33, 0.17, 0.225, 3, 0.7), cloth(), 0, -0.07, 0.002));
    for (const s of [-1, 1]) chest.add(mesh(sphere(0.06), cloth(), s * 0.168, 0.305, 0, 0, 0, 0, [1.1, 0.85, 1]));
    // Open front: shirt V, lapels, collar, buttons, pocket square, lapel pin.
    const v = new THREE.Shape([new THREE.Vector2(-0.075, 0.36), new THREE.Vector2(0.075, 0.36), new THREE.Vector2(0, 0.1)]);
    chest.add(mesh(new THREE.ShapeGeometry(v), LOOK.shirt, 0, 0, 0.114));
    for (const s of [-1, 1]) {
        chest.add(mesh(softBox(0.06, 0.25, 0.016, 2, 0.6), cloth(), s * 0.062, 0.235, 0.116, 0, 0, s * 0.36));
        chest.add(mesh(softBox(0.05, 0.035, 0.02, 2, 0.6), LOOK.shirt, s * 0.035, 0.37, 0.08, -0.4, 0, s * -0.5));
    }
    for (const y of [0.09, 0.0]) chest.add(mesh(sphere(0.008, 8, 6), 0x1c1d20, 0, y, 0.118));
    chest.add(mesh(softBox(0.04, 0.018, 0.01, 2, 0.6), 0xf4f1ea, -0.115, 0.29, 0.116));
    chest.add(mesh(sphere(0.007, 8, 6), 0xd9b25a, 0.1, 0.3, 0.12, 0, 0, 0, [1, 1, 0.5]));
    mergeDirect(chest);

    const neck = new THREE.Group();
    neck.position.y = 0.35;
    chest.add(neck);
    neck.add(mesh(new THREE.CylinderGeometry(0.048, 0.054, 0.1, 14), LOOK.skin, 0, 0.03, 0.005));
    const head = buildHead();
    head.position.y = 0.03;
    head.scale.setScalar(1.1);
    neck.add(head);

    const arms = [];
    for (const s of [-1, 1]) {
        const shoulder = new THREE.Group();
        shoulder.position.set(s * 0.19, 0.3, 0);
        shoulder.add(mesh(limb(0.055, 0.043, 0.22), cloth(), 0, -0.14, 0));
        const elbow = new THREE.Group();
        elbow.position.y = -0.29;
        elbow.add(mesh(limb(0.044, 0.036, 0.19), cloth(), 0, -0.12, 0));
        elbow.add(mesh(new THREE.CylinderGeometry(0.037, 0.035, 0.025, 12), LOOK.shirt, 0, -0.245, 0));
        const hand = new THREE.Group();
        hand.position.y = -0.27;
        hand.add(mesh(softBox(0.06, 0.095, 0.034, 2, 0.6), LOOK.skin, 0, -0.045, 0.004));
        hand.add(mesh(capsule(0.012, 0.035), LOOK.skin, -s * 0.028, -0.03, 0.016, 0.3, 0, s * 0.4));
        mergeDirect(hand);
        mergeDirect(elbow);
        mergeDirect(shoulder);
        elbow.add(hand);
        shoulder.add(elbow);
        chest.add(shoulder);
        arms.push({ shoulder, elbow, hand, side: s });
    }

    const legs = [];
    for (const s of [-1, 1]) {
        const hip = new THREE.Group();
        hip.position.set(s * 0.093, 0, 0);
        hip.add(mesh(limb(0.085, 0.058, 0.32), LOOK.trousers, 0, -0.21, 0));
        const knee = new THREE.Group();
        knee.position.y = -0.44;
        knee.add(mesh(limb(0.058, 0.044, 0.31), LOOK.trousers, 0, -0.19, 0));
        const ankle = new THREE.Group();
        ankle.position.y = -0.41;
        ankle.add(mesh(softBox(0.1, 0.07, 0.26, 3, 0.6), LOOK.shoes, 0, -0.03, 0.05));
        ankle.add(mesh(softBox(0.104, 0.018, 0.265, 2, 0.6), LOOK.sole, 0, -0.064, 0.05));
        mergeDirect(ankle);
        mergeDirect(knee);
        mergeDirect(hip);
        knee.add(ankle);
        hip.add(knee);
        pelvis.add(hip);
        legs.push({ hip, knee, ankle, side: s });
    }
    mergeDirect(pelvis);

    // Fishing rod in the right hand, shown only while fishing. rodTip is where the line starts.
    const rod = new THREE.Group();
    rod.add(mesh(new THREE.CylinderGeometry(0.008, 0.016, 2.2, 6), 0x3b2a1e, 0, 1.0, 0));
    rod.add(mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.06, 10), 0xb8893a, 0, 0.12, 0.03));
    const rodTip = new THREE.Object3D();
    rodTip.position.y = 2.1;
    rod.add(rodTip);
    rod.position.y = -0.05;
    rod.rotation.x = Math.PI / 2 - 0.35;
    rod.visible = false;
    arms[1].hand.add(rod);

    root.userData = {
        pelvis, spine, chest, neck, head, arms, legs, rod, rodTip,
        state: 'idle', waveUntil: 0, phase: 0, still: 0, lastHeading: 0, lastSpeed: 0, turn: 0, accel: 0,
        // Current joint values; every frame these ease toward the target pose.
        cur: null,
    };
    return root;
}

// ---------------- Poses ----------------
// Angles in radians. Shoulder x < 0 raises the arm forward; hip x < 0 swings the thigh forward; knee/elbow
// bend positive/negative respectively. py is a pelvis height offset in metres.
const P = (o) => ({
    py: 0, px: 0, pyaw: 0, proll: 0, spine: 0, chestYaw: 0, neckX: 0, neckY: 0,
    ls: [0, -0.07], le: -0.12, rs: [0, 0.07], re: -0.12, lh: 0, rh: 0, lk: 0, rk: 0, lf: 0, rf: 0, ...o,
});
const POSES = {
    sit: P({ spine: -0.06, ls: [-0.45, -0.12], le: -0.55, rs: [-0.45, 0.12], re: -0.55, lh: -1.5, rh: -1.5, lk: 1.5, rk: 1.5, lf: 0.05, rf: 0.05 }),
    drive: P({ spine: -0.12, ls: [-0.95, -0.12], le: -0.55, rs: [-0.95, 0.12], re: -0.55, lh: -1.45, rh: -1.45, lk: 1.25, rk: 1.25, lf: -0.2, rf: -0.2 }),
    swing: P({ spine: -0.05, ls: [-2.8, -0.08], le: 0.05, rs: [-2.8, 0.08], re: 0.05, lh: -1.4, rh: -1.4, lk: 1.15, rk: 1.15 }),
    lie: P({ ls: [-2.75, -0.55], le: -2.1, rs: [-2.75, 0.55], re: -2.1, lh: -0.5, rh: 0, lk: 1.0, rk: 0.05, neckX: -0.15 }),
    crouch: P({ py: -0.42, spine: 0.45, ls: [-0.55, -0.15], le: -0.35, rs: [-0.95, 0.12], re: -0.45, lh: -1.35, rh: -1.1, lk: 2.1, rk: 1.9, lf: -0.6, rf: -0.6, neckX: 0.25 }),
    fish: P({ spine: 0.02, ls: [-0.7, 0.3], le: -0.85, rs: [-0.65, 0.12], re: -0.75, lh: -0.06, rh: 0.08, lk: 0.08, rk: 0.06, pyaw: 0.15 }),
    cast: P({ spine: -0.18, ls: [-0.9, 0.3], le: -0.6, rs: [-2.7, 0.15], re: -0.4, lh: -0.15, rh: 0.15, lk: 0.1, rk: 0.15, pyaw: -0.2 }),
    reel: P({ spine: -0.14, ls: [-0.95, 0.35], le: -1.2, rs: [-1.15, 0.15], re: -1.0, lh: -0.1, rh: 0.12, lk: 0.15, rk: 0.15 }),
    throw: P({ spine: 0.12, ls: [-0.5, -0.25], le: -0.4, rs: [-1.5, 0.35], re: -0.2, lh: -0.25, rh: 0.15, lk: 0.2, rk: 0.1, pyaw: 0.3, chestYaw: 0.25 }),
    release: P({ spine: -0.12, ls: [-2.6, -0.2], le: -0.25, rs: [-2.6, 0.2], re: -0.25, neckX: -0.35 }),
    look: P({ spine: 0.15, ls: [-1.35, 0.4], le: -1.65, rs: [-1.25, -0.3], re: -1.75, lh: -0.08, rh: 0.08, neckX: 0.05 }),
    read: P({ spine: 0.06, ls: [-0.9, -0.05], le: -1.2, rs: [-0.9, 0.05], re: -1.2, neckX: 0.35 }),
};

/** Switches pose; joints ease toward it. */
export function setPose(c, name) { c.userData.state = name; }

const clamp = THREE.MathUtils.clamp;

/** Standing locomotion: idle blended with a walk→run cycle by speed. */
function locomotion(u, dt, t, speed) {
    const g = clamp((speed - 2.6) / 2.6, 0, 1);              // 0 walk … 1 run
    const move = clamp(speed / 1.1, 0, 1);                   // 0 idle … 1 moving
    const φ = u.phase, s = Math.sin(φ), c = Math.cos(φ);
    const L = (a, b) => a + (b - a) * g;

    // Legs: thigh swing, knee folding through the swing phase, foot kept roughly level and pushing off.
    const thighA = L(0.42, 0.82), kneeA = L(0.85, 1.85), kneeBase = L(0.06, 0.32);
    const leg = sgn => {
        const ss = s * sgn, cc = c * sgn;
        const thigh = -ss * thighA + L(0, -0.12);
        const knee = kneeBase + Math.max(0, cc) * kneeA * (0.6 + 0.4 * Math.max(0, -ss + 0.3));
        const foot = -(thigh + knee) * 0.75 + Math.max(0, -ss) * L(0.25, 0.45) * Math.max(0, -cc);
        return [thigh, knee, foot];
    };
    const [lh, lk, lf] = leg(1), [rh, rk, rf] = leg(-1);
    // Pelvis: walking dips at each heel strike; running dips at mid-stance and floats between steps.
    const bob = L(-0.032 * Math.abs(s), -0.075 + 0.045 * Math.abs(s));
    const armA = L(0.32, 0.85);
    const elbow = L(-0.22, -1.45);

    // Idle: breathing, a slow weight shift, a look around once you've been still for a while.
    const breath = Math.sin(t * 1.7);
    const sway = Math.sin(t * 0.33);
    const look = clamp((u.still - 2.5) / 2, 0, 1);
    const idle = P({
        py: -0.006 + breath * 0.004, px: sway * 0.018, proll: sway * 0.025,
        spine: 0.01 + breath * 0.012, neckY: Math.sin(t * 0.23) * 0.45 * look, neckX: Math.sin(t * 0.17) * 0.08 * look,
        ls: [Math.sin(t * 0.9) * 0.02, -0.07 - breath * 0.01], rs: [Math.sin(t * 0.9 + 1) * 0.02, 0.07 + breath * 0.01],
        le: -0.14, re: -0.14, lh: sway * 0.03, rh: -sway * 0.03, lk: 0.03 + Math.max(0, -sway) * 0.06, rk: 0.03 + Math.max(0, sway) * 0.06,
    });
    const lean = L(0.04, 0.2) + clamp(u.accel * 0.025, -0.12, 0.15);
    const walk = P({
        py: bob, pyaw: s * L(0.1, 0.16), proll: c * L(0.035, 0.02) + clamp(-u.turn * speed * 0.018, -0.22, 0.22),
        spine: lean, chestYaw: -s * L(0.14, 0.22), neckY: clamp(u.turn * 0.12, -0.3, 0.3), neckX: -lean * 0.6,
        ls: [s * armA, -0.1], rs: [-s * armA, 0.1],
        le: elbow - Math.max(0, -s) * L(0.2, 0.25), re: elbow - Math.max(0, s) * L(0.2, 0.25),
        lh, rh, lk, rk, lf, rf,
    });
    const out = {};
    for (const k of Object.keys(idle)) {
        const a = idle[k], b = walk[k];
        out[k] = Array.isArray(a) ? [a[0] + (b[0] - a[0]) * move, a[1] + (b[1] - a[1]) * move] : a + (b - a) * move;
    }
    return out;
}

/**
 * Per-frame animation. `speed` is ground speed in m/s and `heading` the facing angle; onStep fires on each
 * footfall (for footstep sounds).
 */
export function animateCharacter(c, dt, t, { speed = 0, heading = c.rotation.y, onStep } = {}) {
    const u = c.userData;
    dt = Math.max(dt, 1e-4);
    const standing = u.state === 'idle';
    // Turn rate and acceleration feed the lean and banking.
    let dh = heading - u.lastHeading;
    dh = Math.atan2(Math.sin(dh), Math.cos(dh));
    u.turn += (dh / dt - u.turn) * Math.min(1, dt * 8);
    u.accel += ((speed - u.lastSpeed) / dt - u.accel) * Math.min(1, dt * 6);
    u.lastHeading = heading;
    u.lastSpeed = speed;
    u.still = speed < 0.2 && standing ? u.still + dt : 0;

    let target;
    if (standing) {
        if (speed > 0.05) {
            const g = clamp((speed - 2.6) / 2.6, 0, 1);
            const step = 0.78 + (1.85 - 0.78) * g;               // metres per step
            const prev = u.phase;
            u.phase += (speed * dt / step) * Math.PI;
            if (onStep && Math.floor(prev / Math.PI) !== Math.floor(u.phase / Math.PI)) onStep();
        } else {
            // Settle the legs together rather than freezing mid-stride.
            const near = Math.round(u.phase / Math.PI) * Math.PI;
            u.phase += (near - u.phase) * Math.min(1, dt * 6);
        }
        target = locomotion(u, dt, t, speed);
        if (t < u.waveUntil && speed < 0.3) {
            target.rs = [-0.35, 2.55];
            target.re = -0.4 + Math.sin(t * 9) * 0.35;
            target.neckX = -0.05;
        }
    } else {
        target = POSES[u.state] ?? P({});
    }

    // Ease every joint toward its target: fast enough to keep cycles crisp, soft enough to blend poses.
    const k = 1 - Math.exp(-dt * (standing ? 22 : 9));
    if (!u.cur) u.cur = structuredClone(target);
    const cur = u.cur;
    for (const key of Object.keys(target)) {
        const tv = target[key];
        if (Array.isArray(tv)) { cur[key][0] += (tv[0] - cur[key][0]) * k; cur[key][1] += (tv[1] - cur[key][1]) * k; }
        else cur[key] += (tv - cur[key]) * k;
    }

    const [left, right] = u.arms, [lleg, rleg] = u.legs;
    u.pelvis.position.set(cur.px, HIP + cur.py, 0);
    u.pelvis.rotation.set(0, cur.pyaw, cur.proll);
    u.spine.rotation.set(cur.spine, 0, 0);
    u.chest.rotation.set(0, cur.chestYaw - cur.pyaw * 0.5, -cur.proll * 0.6);
    u.neck.rotation.set(cur.neckX, cur.neckY - cur.chestYaw * 0.6, 0);
    left.shoulder.rotation.set(cur.ls[0], 0, cur.ls[1]);
    right.shoulder.rotation.set(cur.rs[0], 0, cur.rs[1]);
    left.elbow.rotation.set(cur.le, 0, 0);
    right.elbow.rotation.set(cur.re, 0, 0);
    lleg.hip.rotation.set(cur.lh, 0, -cur.proll);
    rleg.hip.rotation.set(cur.rh, 0, -cur.proll);
    lleg.knee.rotation.set(cur.lk, 0, 0);
    rleg.knee.rotation.set(cur.rk, 0, 0);
    lleg.ankle.rotation.set(cur.lf, 0, 0);
    rleg.ankle.rotation.set(cur.rf, 0, 0);
    // A crouch pats the ground / pets the cat.
    if (u.state === 'crouch') right.elbow.rotation.z = Math.sin(t * 7) * 0.25;
}
