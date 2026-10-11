// Shared building blocks for the procedural low-poly models: cached flat-shaded materials, boxes, glow
// sprites, canvas text, static-mesh merging, and the car.
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

const PALETTE = { accent: 0x4fb8a8 };   // vintage mint

const materialCache = new Map();
export function mat(color, opts = {}) {
    const key = `${color}|${JSON.stringify(opts)}`;
    if (!materialCache.has(key)) {
        materialCache.set(key, new THREE.MeshStandardMaterial({ color, flatShading: true, roughness: 0.85, ...opts }));
    }
    return materialCache.get(key);
}

function shadow(mesh, cast = true, receive = true) {
    mesh.castShadow = cast;
    mesh.receiveShadow = receive;
    return mesh;
}

export function box(w, h, d, color, opts) {
    return shadow(new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat(color, opts)));
}

/** Canvas texture with wrapped text, used for signs and screens. Text is drawn, never parsed as markup. */
export function textTexture(lines, { width = 512, height = 256, bg = '#0d1a2c', fg = '#e7f0f4', accent = '#2ad6a8', title = true } = {}) {
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const g = canvas.getContext('2d');
    g.fillStyle = bg;
    g.fillRect(0, 0, width, height);
    g.fillStyle = accent;
    g.fillRect(0, 0, width, 8);
    const pad = 28;
    let y = pad + 18;
    lines.filter(Boolean).forEach((line, i) => {
        const big = title && i === 0;
        const size = big ? Math.round(height / 6.2) : Math.round(height / 11);
        g.font = `${big ? 800 : 600} ${size}px system-ui, -apple-system, "Segoe UI", sans-serif`;
        g.fillStyle = big ? fg : (i === 1 ? accent : '#9fb0c1');
        for (const wrapped of wrap(g, String(line), width - pad * 2)) {
            y += size * 1.05;
            if (y > height - pad / 2) return;
            g.fillText(wrapped, pad, y);
        }
        y += size * 0.35;
    });
    const tex = new THREE.CanvasTexture(canvas);
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.anisotropy = 4;
    return tex;
}

function wrap(g, text, maxWidth) {
    const words = text.split(/\s+/);
    const out = [];
    let line = '';
    for (const w of words) {
        const test = line ? `${line} ${w}` : w;
        if (g.measureText(test).width > maxWidth && line) { out.push(line); line = w; }
        else line = test;
    }
    if (line) out.push(line);
    return out;
}

let glowTexture = null;
/** Additive radial sprite that fakes bloom around lamps, fire and windows (far cheaper than post-processing). */
export function glow(color = 0xffd9a0, size = 3) {
    if (!glowTexture) {
        const c = document.createElement('canvas');
        c.width = c.height = 64;
        const g = c.getContext('2d');
        const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
        grad.addColorStop(0, 'rgba(255,255,255,1)');
        grad.addColorStop(0.25, 'rgba(255,255,255,.5)');
        grad.addColorStop(1, 'rgba(255,255,255,0)');
        g.fillStyle = grad;
        g.fillRect(0, 0, 64, 64);
        glowTexture = new THREE.CanvasTexture(c);
    }
    const s = new THREE.Sprite(new THREE.SpriteMaterial({
        map: glowTexture, color, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, fog: false,
    }));
    s.scale.setScalar(size);
    return s;
}

/** Horizontal stripes as a texture (hammock cloth, awnings). */
export function stripeTexture(colors, repeat = 1) {
    const c = document.createElement('canvas');
    c.width = 4;
    c.height = colors.length * 8;
    const g = c.getContext('2d');
    colors.forEach((col, i) => { g.fillStyle = col; g.fillRect(0, i * 8, 4, 8); });
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.repeat.set(1, repeat);
    t.magFilter = THREE.NearestFilter;
    return t;
}

/**
 * Merges every static mesh under `group` that shares a material into one mesh, cutting a building's dozens
 * of parts down to a few draw calls. Children flagged `userData.dynamic` (and their subtrees) are left alone.
 */
export function bakeStatic(group) {
    group.updateMatrixWorld(true);
    const inv = new THREE.Matrix4().copy(group.matrixWorld).invert();
    const buckets = new Map();
    const remove = [];
    group.traverse(o => {
        if (o === group) return;
        let p = o, dynamic = false;
        while (p && p !== group) { if (p.userData.dynamic) { dynamic = true; break; } p = p.parent; }
        if (dynamic || !o.isMesh || o.isInstancedMesh || Array.isArray(o.material)) return;
        let g = o.geometry.clone().applyMatrix4(new THREE.Matrix4().multiplyMatrices(inv, o.matrixWorld));
        if (g.index) g = g.toNonIndexed();
        for (const name of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(name)) g.deleteAttribute(name);
        if (!g.attributes.uv) g.setAttribute('uv', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 2), 2));
        const key = o.material.uuid;
        if (!buckets.has(key)) buckets.set(key, { material: o.material, geos: [], cast: false });
        const b = buckets.get(key);
        b.geos.push(g);
        b.cast ||= o.castShadow;
        remove.push(o);
    });
    for (const o of remove) o.parent.remove(o);
    for (const { material, geos, cast } of buckets.values()) {
        const merged = new THREE.Mesh(mergeGeometries(geos), material);
        merged.castShadow = cast;
        merged.receiveShadow = true;
        group.add(merged);
    }
    return group;
}

/** The player's car: a bevelled body extruded from a side profile (sporty hatch), tinted glass cabin, wheel
 * arches, spoiler and emissive lights. Wheels and the seat anchor are exposed for animation and the driver. */
export function car() {
    const g = new THREE.Group();
    const chassis = new THREE.Group();      // bobs on the suspension; wheels stay planted
    g.add(chassis);

    // Side profile in (z, y): nose at +z. Extruded across the car's width with a soft bevel.
    const profile = new THREE.Shape();
    [[-2.3, 0.45], [-2.35, 1.05], [-2.0, 1.2], [-0.9, 1.25], [1.4, 1.15], [2.25, 0.85], [2.35, 0.45]]
        .forEach(([z, y], i) => (i ? profile.lineTo(z, y) : profile.moveTo(z, y)));
    profile.closePath();
    const bodyGeo = new THREE.ExtrudeGeometry(profile, { depth: 1.8, bevelEnabled: true, bevelSize: 0.18, bevelThickness: 0.18, bevelSegments: 2 });
    bodyGeo.rotateY(Math.PI / 2);
    bodyGeo.translate(-0.9, 0, 0);
    const paint = mat(PALETTE.accent, { roughness: 0.35, metalness: 0.35 });
    const body = new THREE.Mesh(bodyGeo, paint);
    body.castShadow = body.receiveShadow = true;
    chassis.add(body);

    // Cabin: tinted glass with a body-coloured roof strip.
    const cabinShape = new THREE.Shape();
    [[-1.55, 1.2], [-1.25, 1.9], [0.25, 1.95], [1.05, 1.2]].forEach(([z, y], i) => (i ? cabinShape.lineTo(z, y) : cabinShape.moveTo(z, y)));
    cabinShape.closePath();
    const cabinGeo = new THREE.ExtrudeGeometry(cabinShape, { depth: 1.5, bevelEnabled: true, bevelSize: 0.08, bevelThickness: 0.08, bevelSegments: 1 });
    cabinGeo.rotateY(Math.PI / 2);
    cabinGeo.translate(-0.75, 0, 0);
    const glass = new THREE.Mesh(cabinGeo, mat(0x0d1d2c, { roughness: 0.1, metalness: 0.6, transparent: true, opacity: 0.55 }));
    chassis.add(glass);
    const roof = box(1.5, 0.08, 1.5, PALETTE.accent, { roughness: 0.35, metalness: 0.35 });
    roof.position.set(0, 1.99, -0.5);
    chassis.add(roof);

    // Details: racing stripe, spoiler, lights, grille, mirrors.
    const stripe = box(0.35, 0.02, 4.3, 0xf5f7fa, { roughness: 0.4 });
    stripe.position.set(0, 1.32, 0.05);
    chassis.add(stripe);
    const wing = box(2.0, 0.08, 0.45, 0x1b2a3c);
    wing.position.set(0, 1.55, -2.25);
    chassis.add(wing);
    for (const x of [-0.75, 0.75]) {
        const strut = box(0.08, 0.32, 0.12, 0x1b2a3c);
        strut.position.set(x, 1.38, -2.2);
        chassis.add(strut);
        const head = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.16, 0.06), mat(0xfff4d6, { emissive: 0xfff4d6, emissiveIntensity: 1.6 }));
        head.position.set(x, 0.85, 2.52);
        const tail = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.14, 0.06), mat(0xff3b3b, { emissive: 0xff3b3b, emissiveIntensity: 1.2 }));
        tail.position.set(x, 0.95, -2.5);
        const mirror = box(0.25, 0.14, 0.18, PALETTE.accent);
        mirror.position.set(x * 1.45, 1.35, 0.95);
        chassis.add(head, tail, mirror);
    }
    const grille = box(1.1, 0.22, 0.05, 0x0d141c);
    grille.position.set(0, 0.6, 2.53);
    chassis.add(grille);

    // Wheels with rims, sitting in dark arches.
    const wheels = [];
    const tyre = new THREE.CylinderGeometry(0.48, 0.48, 0.36, 14);
    const rim = new THREE.CylinderGeometry(0.28, 0.28, 0.38, 8);
    for (const [x, z] of [[-1.0, 1.45], [1.0, 1.45], [-1.0, -1.45], [1.0, -1.45]]) {
        const arch = box(0.3, 0.5, 1.25, 0x0d141c);
        arch.position.set(x * 1.02, 0.62, z);
        chassis.add(arch);
        const w = new THREE.Group();
        const t = new THREE.Mesh(tyre, mat(0x101418, { roughness: 0.9 }));
        const r = new THREE.Mesh(rim, mat(0xc9d3dc, { roughness: 0.3, metalness: 0.8 }));
        t.castShadow = true;
        w.add(t, r);
        w.rotation.z = Math.PI / 2;
        w.position.set(x * 1.05, 0.48, z);
        g.add(w);
        wheels.push(w);
    }
    // Where a seated driver goes (left-hand drive seat, under the glass).
    const seat = new THREE.Group();
    seat.position.set(0.35, 0.55, -0.45);
    chassis.add(seat);

    g.userData = { wheels, chassis, seat };
    return g;
}

