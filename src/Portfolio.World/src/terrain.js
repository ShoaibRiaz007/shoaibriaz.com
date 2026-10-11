// The island as functions: coastline, height, surface type and the places on it. Everything else (meshes,
// grass, the map, footstep sounds, shell spots) samples these, so the shape is defined exactly once.
// Units are metres; x is east, z is south (toward the beach), the water surface is y = 0.
import * as THREE from 'three';
import { clamp, fbm, lerp, noise, smoothstep } from './noise.js';

export const ISLAND = { rx: 92, rz: 72 };
const HILL = { x: -30, z: -40, sigma: 21, peak: 11, plateau: 5 };
const CAPE = { x: 82, z: -38, r: 13 };          // rocky headland for the lighthouse

/** Raw inland-ness: 1 at the centre, 0 on the coast, negative out at sea. */
function mask(x, z) {
    const e = Math.sqrt((x / ISLAND.rx) ** 2 + (z / ISLAND.rz) ** 2);
    const wobble = (fbm(x * 0.018 + 4, z * 0.018 - 2, 3) - 0.5) * 0.22;
    const cape = Math.exp(-((x - CAPE.x) ** 2 + (z - CAPE.z) ** 2) / (2 * CAPE.r ** 2)) * 0.3;
    return 1 - e + wobble + cape;
}

/** Point on the coastline in direction `deg` (0 = east, 90 = south), found by bisection along the ray. */
export function coastPoint(deg) {
    const a = THREE.MathUtils.degToRad(deg), dx = Math.cos(a), dz = Math.sin(a);
    let lo = 0, hi = 160;
    for (let i = 0; i < 30; i++) {
        const mid = (lo + hi) / 2;
        if (mask(dx * mid, dz * mid) > 0) lo = mid; else hi = mid;
    }
    return { x: dx * lo, z: dz * lo, nx: dx, nz: dz };
}

// Beach is wide on the south shore and almost absent on the cliffy north.
const beachWidth = z => lerp(0.035, 0.17, smoothstep(-40, 40, z));

function baseHeight(x, z) {
    const d = mask(x, z);
    if (d < 0) return Math.max(-9, d * 34) - 0.15;
    const bw = beachWidth(z);
    if (d < bw) return (d / bw) * 0.9;
    const inland = smoothstep(bw, bw + 0.16, d);
    let h = 0.9 + inland * (1.1 + 2.8 * fbm(x * 0.021, z * 0.021));
    // North cliffs: a sharp lip right behind the narrow beach.
    h += smoothstep(bw, bw + 0.05, d) * 2.6 * smoothstep(-5, -45, z);
    // The lookout hill, with a flat summit for the tree and swing.
    const r = Math.max(HILL.plateau, Math.hypot(x - HILL.x, z - HILL.z));
    h += HILL.peak * Math.exp(-(r * r) / (2 * HILL.sigma ** 2));
    // The headland is a raised rock shelf.
    const cr = Math.hypot(x - CAPE.x, z - CAPE.z);
    h += smoothstep(CAPE.r + 6, CAPE.r - 4, cr) * 3.2;
    return h;
}

// ---- Places, derived from the coastline so they always land on sand or grass ----
function inland(p, metres) { return { x: p.x - p.nx * metres, z: p.z - p.nz * metres }; }

const pierRoot = coastPoint(118);
const campCoast = coastPoint(92);
const hammockCoast = coastPoint(66);

export const PLACES = {
    cottage: { x: -8, z: 6, facing: 0.15 },                    // facing: radians about y, 0 = door toward +z
    car: { x: 8, z: 16, heading: 0.9 },
    windmill: { x: 38, z: -14 },
    hill: { x: HILL.x, z: HILL.z },
    lighthouse: { x: CAPE.x + 2, z: CAPE.z - 1 },
    campfire: inland(campCoast, 9),
    hammock: inland(hammockCoast, 6),
    pier: { ...inland(pierRoot, 5), dirX: pierRoot.nx, dirZ: pierRoot.nz, length: 32 },   // starts on the sand
    signpost: { x: -2, z: 18 },
    gallery: { x: 46, z: 14 },                                // open-air project gallery on the eastern meadow
    spawn: { x: -6.6, z: 16.2 },                             // just off the porch, looking down the path to the sea
};

// Paths are polylines between places; they are painted into the ground colour, kept clear of grass, and
// lit by lanterns.
const P = PLACES;
export const PATHS = [
    [[P.cottage.x + 1, P.cottage.z + 7], [P.signpost.x, P.signpost.z + 2], [-18, 34], [P.pier.x - P.pier.dirX * 6, P.pier.z - P.pier.dirZ * 6], [P.pier.x, P.pier.z]],
    [[P.signpost.x, P.signpost.z + 2], [8, 30], [P.campfire.x - 2, P.campfire.z - 7]],
    [[P.cottage.x + 6, P.cottage.z + 4], [20, 0], [30, -10], [P.windmill.x - 3, P.windmill.z + 3]],
    [[P.windmill.x + 4, P.windmill.z - 2], [56, -22], [70, -30], [P.lighthouse.x - 6, P.lighthouse.z + 2]],
    [[P.cottage.x - 5, P.cottage.z - 4], [-20, -10], [-22, -24], [-28, -32], [P.hill.x, P.hill.z + 4]],
    [[P.campfire.x + 4, P.campfire.z - 2], [P.hammock.x - 6, P.hammock.z - 1]],
    [[20, 0], [31, 8], [P.gallery.x - 11.5, P.gallery.z]],
];

const segments = PATHS.flatMap(line => line.slice(1).map((b, i) => [line[i], b]));
/** Distance to the nearest path centreline. */
export function pathDistance(x, z) {
    let best = Infinity;
    for (const [[ax, az], [bx, bz]] of segments) {
        const vx = bx - ax, vz = bz - az;
        const t = clamp(((x - ax) * vx + (z - az) * vz) / (vx * vx + vz * vz), 0, 1);
        best = Math.min(best, Math.hypot(x - (ax + vx * t), z - (az + vz * t)));
    }
    return best;
}

// Flat pads under buildings so nothing floats or sinks.
const FLAT = [
    { ...P.cottage, r: 10, blend: 8 },
    { ...P.windmill, r: 6, blend: 6 },
    { ...P.campfire, r: 5, blend: 4 },
    { ...P.car, r: 4, blend: 4 },
    { ...P.lighthouse, r: 6, blend: 5 },
    { ...P.gallery, r: 11, blend: 6 },
].map(f => ({ ...f, h: baseHeight(f.x, f.z) }));

/** Final terrain height at (x, z). */
export function heightAt(x, z) {
    let h = baseHeight(x, z);
    for (const f of FLAT) {
        const d = Math.hypot(x - f.x, z - f.z);
        if (d < f.r + f.blend) h = lerp(f.h, h, smoothstep(f.r, f.r + f.blend, d));
    }
    // Paths are worn slightly into the ground.
    const pd = pathDistance(x, z);
    if (pd < 2.2 && h > 0.6) h -= (1 - pd / 2.2) * 0.12;
    return h;
}

export function slopeAt(x, z) {
    const e = 0.8;
    const dx = heightAt(x + e, z) - heightAt(x - e, z), dz = heightAt(x, z + e) - heightAt(x, z - e);
    return Math.hypot(dx, dz) / (2 * e);
}

/** Inland-ness used for "is this beach?" checks and ambient sound. */
export const inlandness = mask;

export function isBeach(x, z) {
    const d = mask(x, z);
    return d >= 0 && d < beachWidth(z) + 0.02;
}

// ---- Walkable surfaces that are not terrain (pier deck, porch, lighthouse gallery) ----
const surfaces = [];
/**
 * Registers an oriented rectangle you can stand on. Its local +z runs along `d`; height is `y` at the -z end
 * and `y1` (default y) at the +z end, so ramps work. `kind` drives footstep sounds.
 */
export function addSurface({ x, z, w, d, rot = 0, y, y1 = y, kind = 'wood', enabled = () => true }) {
    surfaces.push({ x, z, w, d, rot, y, y1, kind, enabled, c: Math.cos(rot), s: Math.sin(rot) });
}
function surfaceAt(x, z) {
    let best = null;
    for (const s of surfaces) {
        if (!s.enabled()) continue;
        const dx = x - s.x, dz = z - s.z;
        const lx = dx * s.c - dz * s.s, lz = dx * s.s + dz * s.c;
        if (Math.abs(lx) > s.w / 2 || Math.abs(lz) > s.d / 2) continue;
        const y = s.y + (s.y1 - s.y) * (lz / s.d + 0.5);
        if (!best || y > best.y) best = { kind: s.kind, y };
    }
    return best;
}

/** Height you stand on: terrain, or a deck above it. */
export function groundAt(x, z) {
    const s = surfaceAt(x, z);
    return s ? Math.max(s.y, heightAt(x, z)) : heightAt(x, z);
}

/** What's underfoot: 'wood' | 'water' | 'sand' | 'path' | 'rock' | 'grass'. */
export function surfaceKind(x, z) {
    const s = surfaceAt(x, z);
    if (s) return s.kind;
    const h = heightAt(x, z);
    if (h < 0.05) return 'water';
    if (isBeach(x, z) || h < 0.7) return 'sand';
    if (pathDistance(x, z) < 1.6) return 'path';
    if (slopeAt(x, z) > 0.75 || Math.hypot(x - CAPE.x, z - CAPE.z) < CAPE.r) return 'rock';
    return 'grass';
}

/** Somewhere grass and flowers may grow. */
export function isMeadow(x, z) {
    const h = heightAt(x, z);
    if (h < 1 || isBeach(x, z)) return false;
    if (pathDistance(x, z) < 2.2) return false;
    if (slopeAt(x, z) > 0.6) return false;
    return Math.hypot(x - CAPE.x, z - CAPE.z) > CAPE.r + 2;
}

// ---- Mesh ----
const C = {
    deep: new THREE.Color(0x24566a), shallow: new THREE.Color(0x8cc7b5),
    wetSand: new THREE.Color(0xcbb184), sand: new THREE.Color(0xecd8aa),
    grassA: new THREE.Color(0x5f9f45), grassB: new THREE.Color(0x467f38), grassC: new THREE.Color(0x9cc45c),
    forest: new THREE.Color(0x3b6e33), path: new THREE.Color(0xc2a271), pathEdge: new THREE.Color(0x9c9a5a),
    rock: new THREE.Color(0x8e8a84), rockDark: new THREE.Color(0x6f6a64),
};

export function groundColor(x, z, h = heightAt(x, z), out = new THREE.Color()) {
    if (h < 0.05) return out.copy(C.shallow).lerp(C.deep, smoothstep(0, -6, h));
    if (isBeach(x, z) || h < 0.7) return out.copy(C.wetSand).lerp(C.sand, smoothstep(0.05, 0.45, h));
    const n = fbm(x * 0.05, z * 0.05, 3), n2 = noise(x * 0.18 + 40, z * 0.18);
    out.copy(C.grassB).lerp(C.grassA, n).lerp(C.grassC, Math.max(0, n2 - 0.62) * 1.8);
    out.lerp(C.forest, smoothstep(0.55, 0.8, fbm(x * 0.012 + 9, z * 0.012, 2)) * 0.7);
    // Sand bleeds a little into the grass behind the beach.
    out.lerp(C.sand, smoothstep(1.2, 0.7, h) * 0.6);
    const slope = slopeAt(x, z), cape = Math.hypot(x - CAPE.x, z - CAPE.z);
    const rocky = Math.max(smoothstep(0.55, 0.95, slope), smoothstep(CAPE.r + 3, CAPE.r - 3, cape));
    if (rocky > 0) out.lerp(noise(x * 0.3, z * 0.3) > 0.5 ? C.rock : C.rockDark, rocky);
    const pd = pathDistance(x, z);
    if (pd < 2.4) out.lerp(pd < 1.4 ? C.path : C.pathEdge, smoothstep(2.4, 1.0, pd));
    return out;
}

export function buildTerrain(scene, quality) {
    const seg = quality === 'low' ? 110 : 190;
    const W = 280, D = 230;
    const geo = new THREE.PlaneGeometry(W, D, seg, Math.round(seg * D / W));
    geo.rotateX(-Math.PI / 2);
    const p = geo.attributes.position;
    const colors = new Float32Array(p.count * 3);
    const c = new THREE.Color();
    for (let i = 0; i < p.count; i++) {
        const x = p.getX(i), z = p.getZ(i);
        const h = heightAt(x, z);
        p.setY(i, h);
        groundColor(x, z, h, c);
        colors.set([c.r, c.g, c.b], i * 3);
    }
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geo.computeVertexNormals();
    const mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.95, metalness: 0 }));
    mesh.receiveShadow = true;
    mesh.name = 'terrain';
    scene.add(mesh);
    return mesh;
}

/**
 * Depth below the waterline baked into a texture, so the water shader can tint shallows turquoise and draw
 * foam where waves meet the sand without reading the depth buffer.
 */
export function bakeDepthTexture(size = 256, extent = 300) {
    const data = new Uint8Array(size * size * 4);
    for (let j = 0; j < size; j++) for (let i = 0; i < size; i++) {
        const x = (i / (size - 1) - 0.5) * extent, z = (j / (size - 1) - 0.5) * extent;
        const depth = clamp(-heightAt(x, z), 0, 10) / 10;
        const k = (j * size + i) * 4;
        data[k] = Math.round(depth * 255);
        data[k + 1] = data[k + 2] = 0;
        data[k + 3] = 255;
    }
    const tex = new THREE.DataTexture(data, size, size);
    tex.magFilter = tex.minFilter = THREE.LinearFilter;
    tex.wrapS = tex.wrapT = THREE.ClampToEdgeWrapping;
    tex.needsUpdate = true;
    return { texture: tex, extent };
}

/** Approximate distance to open water, on a coarse grid (for surf volume). */
export function bakeShoreDistance(size = 96, extent = 260) {
    const cell = extent / size, grid = new Float32Array(size * size);
    for (let j = 0; j < size; j++) for (let i = 0; i < size; i++) {
        const x = (i + 0.5) * cell - extent / 2, z = (j + 0.5) * cell - extent / 2;
        grid[j * size + i] = heightAt(x, z) < 0 ? 0 : 1e9;
    }
    // Two-pass chamfer distance transform.
    const at = (i, j) => grid[j * size + i];
    for (let j = 0; j < size; j++) for (let i = 0; i < size; i++) {
        let v = at(i, j);
        if (i > 0) v = Math.min(v, at(i - 1, j) + cell);
        if (j > 0) v = Math.min(v, at(i, j - 1) + cell);
        if (i > 0 && j > 0) v = Math.min(v, at(i - 1, j - 1) + cell * 1.41);
        grid[j * size + i] = v;
    }
    for (let j = size - 1; j >= 0; j--) for (let i = size - 1; i >= 0; i--) {
        let v = at(i, j);
        if (i < size - 1) v = Math.min(v, at(i + 1, j) + cell);
        if (j < size - 1) v = Math.min(v, at(i, j + 1) + cell);
        if (i < size - 1 && j < size - 1) v = Math.min(v, at(i + 1, j + 1) + cell * 1.41);
        grid[j * size + i] = v;
    }
    return (x, z) => {
        const i = clamp(Math.floor((x + extent / 2) / cell), 0, size - 1), j = clamp(Math.floor((z + extent / 2) / cell), 0, size - 1);
        return grid[j * size + i];
    };
}
