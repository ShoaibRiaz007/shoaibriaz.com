// Trees, grass, flowers, bushes and rocks. Each kind is one InstancedMesh (a handful of draw calls for the
// whole island), and a shared wind shader sways canopies and grass; grass also parts around the player.
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { fbm, noise, rng } from './noise.js';
import { groundColor, heightAt, isBeach, isMeadow, pathDistance, slopeAt } from './terrain.js';

export const WIND = { uTime: { value: 0 }, uPlayer: { value: new THREE.Vector3(0, -100, 0) }, uGust: { value: 1 } };

/**
 * Adds wind sway (and optionally player push) to a material. `height` is the model-space height where sway is
 * full; below that it falls off quadratically so trunks and stems stay planted.
 */
export function windify(material, { height = 1, sway = 0.3, push = false } = {}) {
    material.onBeforeCompile = shader => {
        Object.assign(shader.uniforms, WIND);
        shader.uniforms.uHeight = { value: height };
        shader.uniforms.uSway = { value: sway };
        shader.vertexShader = shader.vertexShader
            .replace('#include <common>', `#include <common>
                uniform float uTime, uHeight, uSway, uGust; uniform vec3 uPlayer;`)
            .replace('#include <project_vertex>', `
                vec4 wpos = vec4(transformed, 1.0);
                #ifdef USE_INSTANCING
                    wpos = instanceMatrix * wpos;
                    vec2 base = (modelMatrix * instanceMatrix[3]).xz;
                #else
                    vec2 base = modelMatrix[3].xz;
                #endif
                wpos = modelMatrix * wpos;
                float tip = clamp(position.y / uHeight, 0.0, 1.0); tip *= tip;
                float ph = base.x * 0.21 + base.y * 0.17;
                float gust = (sin(uTime * 0.35 + base.x * 0.025 + base.y * 0.01) * 0.5 + 0.5) * uGust;
                wpos.x += (sin(uTime * 1.6 + ph) * 0.45 + gust * 0.7) * uSway * tip;
                wpos.z += cos(uTime * 1.2 + ph * 1.3) * 0.35 * uSway * tip;
                ${push ? `
                vec2 dp = wpos.xz - uPlayer.xz; float dl = length(dp);
                float k = (1.0 - smoothstep(0.2, 1.4, dl)) * tip * step(abs(wpos.y - uPlayer.y), 1.6);
                wpos.xz += (dp / max(dl, 1e-3)) * k * 0.55; wpos.y -= k * 0.3;` : ''}
                vec4 mvPosition = viewMatrix * wpos;
                gl_Position = projectionMatrix * mvPosition;`);
    };
    material.customProgramCacheKey = () => `wind-${height}-${sway}-${push}`;
    return material;
}

// ---- Model geometry (merged, vertex-coloured, so every tree of a kind shares one draw call) ----
function colored(geo, color) {
    const g = geo.index ? geo.toNonIndexed() : geo;
    const c = new THREE.Color(color), n = g.attributes.position.count;
    const arr = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) arr.set([c.r, c.g, c.b], i * 3);
    g.setAttribute('color', new THREE.BufferAttribute(arr, 3));
    g.deleteAttribute('uv');
    return g;
}
const at = (geo, x, y, z, rx = 0, ry = 0, rz = 0, s = [1, 1, 1]) => {
    const m = new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, rz)), new THREE.Vector3(...s));
    return geo.applyMatrix4(m);
};
/** Squash icosahedron vertices with a little noise so canopies aren't perfect spheres. */
function lumpy(radius, detail, seed) {
    const g = new THREE.IcosahedronGeometry(radius, detail);
    const p = g.attributes.position, v = new THREE.Vector3();
    for (let i = 0; i < p.count; i++) {
        v.fromBufferAttribute(p, i);
        v.multiplyScalar(0.88 + noise(v.x * 1.7 + seed, v.z * 1.7 + v.y) * 0.26);
        p.setXYZ(i, v.x, v.y, v.z);
    }
    return g;
}

const BARK = 0x7a5236, BARK_LIGHT = 0xa3784f;
function broadleaf() {
    const parts = [colored(at(new THREE.CylinderGeometry(0.22, 0.38, 3.2, 6), 0, 1.6, 0), BARK)];
    parts.push(colored(at(new THREE.CylinderGeometry(0.08, 0.14, 1.4, 5), 0.5, 2.9, 0, 0, 0, -0.7), BARK));
    [[0, 4.4, 0, 2.1, 0x4f9a45], [1.2, 3.8, 0.4, 1.5, 0x5fae4e], [-1.1, 3.9, -0.3, 1.6, 0x468c3d], [0.2, 5.4, -0.2, 1.4, 0x6dbb55]]
        .forEach(([x, y, z, r, c], i) => parts.push(colored(at(lumpy(r, 1, i * 3.1), x, y, z), c)));
    return mergeGeometries(parts);
}
function pine() {
    const parts = [colored(at(new THREE.CylinderGeometry(0.16, 0.3, 2.4, 6), 0, 1.2, 0), BARK)];
    [[1.9, 2.6, 2.0, 0x2f6b45], [1.55, 2.3, 3.5, 0x357a4c], [1.15, 2.0, 4.9, 0x3d8754], [0.7, 1.6, 6.1, 0x46935c]]
        .forEach(([r, h, y, c], i) => parts.push(colored(at(new THREE.ConeGeometry(r, h, 7), 0, y, 0, 0, i * 1.3, 0), c)));
    return mergeGeometries(parts);
}
function palm() {
    const parts = [];
    let x = 0, y = 0;
    for (let i = 0; i < 6; i++) {
        const lean = 0.06 + i * 0.045;
        parts.push(colored(at(new THREE.CylinderGeometry(0.17 - i * 0.012, 0.2 - i * 0.012, 1.05, 6), x, y + 0.5, 0, 0, 0, -lean), i % 2 ? BARK_LIGHT : 0x8f6a45));
        x += Math.sin(lean) * 1.0;
        y += Math.cos(lean) * 1.0;
    }
    for (let i = 0; i < 8; i++) {
        const a = (i / 8) * Math.PI * 2;
        const inner = at(new THREE.BoxGeometry(0.5, 0.05, 1.5), 0, 0, 0.75, -0.25, 0, 0);
        const outer = at(new THREE.BoxGeometry(0.42, 0.05, 1.4), 0, -0.25, 2.1, 0.55, 0, 0);
        const frond = mergeGeometries([colored(inner, i % 2 ? 0x4c9a3c : 0x5aae46), colored(outer, i % 2 ? 0x3f8a33 : 0x4c9a3c)]);
        parts.push(at(frond, x, y + 0.05, 0, 0, a, 0));
    }
    for (let i = 0; i < 3; i++) parts.push(colored(at(new THREE.IcosahedronGeometry(0.18, 0), x + Math.cos(i * 2.1) * 0.25, y - 0.2, Math.sin(i * 2.1) * 0.25), 0x6b4a2a));
    return mergeGeometries(parts);
}
function blossom() {
    const parts = [colored(at(new THREE.CylinderGeometry(0.18, 0.32, 2.6, 6), 0, 1.3, 0, 0, 0, 0.08), 0x5e4033)];
    parts.push(colored(at(new THREE.CylinderGeometry(0.07, 0.12, 1.3, 5), -0.5, 2.6, 0, 0, 0, 0.8), 0x5e4033));
    [[0, 3.7, 0, 1.8, 0xf6b6c8], [1.0, 3.3, 0.5, 1.2, 0xf9c9d6], [-1.1, 3.4, -0.2, 1.3, 0xef9fb8], [0.1, 4.5, -0.3, 1.1, 0xfbd6e0]]
        .forEach(([x, y, z, r, c], i) => parts.push(colored(at(lumpy(r, 1, i * 5.3 + 2), x, y, z), c)));
    return mergeGeometries(parts);
}
function bush() {
    return mergeGeometries([[0, 0.5, 0, 0.8, 0x4a8f3e], [0.6, 0.4, 0.2, 0.6, 0x5aa34a], [-0.5, 0.45, -0.1, 0.65, 0x3f7f37]]
        .map(([x, y, z, r, c], i) => colored(at(lumpy(r, 0, i), x, y, z), c)));
}
function grassBlade() {
    const w = 0.06;
    const pts = [[-w, 0], [w, 0], [-w * 0.7, 0.4], [w * 0.7, 0.4], [-w * 0.35, 0.75], [w * 0.35, 0.75], [0, 1]];
    const pos = [], col = [], nor = [];
    // A brightness ramp, not a colour: each blade is tinted by its instance colour (the ground beneath it).
    const base = new THREE.Color(0.85, 0.9, 0.85), tip = new THREE.Color(2.1, 2.0, 1.55), c = new THREE.Color();
    const tri = (a, b, d) => [a, b, d].forEach(i => {
        pos.push(pts[i][0], pts[i][1], 0);
        c.copy(base).lerp(tip, pts[i][1]);
        col.push(c.r, c.g, c.b);
        nor.push(0, 1, 0);          // ground-facing normals: blades light like the terrain, no flicker
    });
    tri(0, 1, 2); tri(1, 3, 2); tri(2, 3, 4); tri(3, 5, 4); tri(4, 5, 6);
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
    return g;
}
export function flowerGeometry(big = false) {
    const s = big ? 1.6 : 1;
    const stem = colored(at(new THREE.CylinderGeometry(0.015 * s, 0.02 * s, 0.4 * s, 4), 0, 0.2 * s, 0), 0x3f7f37);
    const leaf = colored(at(new THREE.BoxGeometry(0.12 * s, 0.01, 0.05 * s), 0.06 * s, 0.12 * s, 0, 0, 0, 0.4), 0x4c9a3c);
    const petals = colored(at(new THREE.CylinderGeometry(0.11 * s, 0.03 * s, 0.07 * s, 6), 0, 0.42 * s, 0), 0xffffff);
    const heart = colored(at(new THREE.IcosahedronGeometry(0.04 * s, 0), 0, 0.46 * s, 0), 0xffd84a);
    return mergeGeometries([stem, leaf, petals, heart]);
}

function instanced(geo, material, count, castShadow = true) {
    const mesh = new THREE.InstancedMesh(geo, material, count);
    mesh.castShadow = castShadow;
    mesh.receiveShadow = true;
    mesh.count = 0;
    return mesh;
}

/**
 * Populates the island. `clear` is a list of {x, z, r} circles kept free (buildings, activity spots);
 * trunks and big rocks are appended to `colliders`.
 */
export function buildVegetation(scene, { quality, clear, colliders, canopies = [] }) {
    const rand = rng(42);
    const blocked = (x, z, pad = 0) => clear.some(c => (x - c.x) ** 2 + (z - c.z) ** 2 < (c.r + pad) ** 2);
    const dummy = new THREE.Object3D();
    const place = (mesh, x, z, scale, yaw = rand() * Math.PI * 2, sink = 0.1, tilt = 0.06) => {
        dummy.position.set(x, heightAt(x, z) - sink, z);
        dummy.rotation.set((rand() - 0.5) * tilt, yaw, (rand() - 0.5) * tilt);
        dummy.scale.setScalar(scale);
        dummy.updateMatrix();
        mesh.setMatrixAt(mesh.count++, dummy.matrix);
    };

    const treeMat = h => windify(new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 0.9 }), { height: h, sway: 0.18 });
    const kinds = {
        broadleaf: instanced(broadleaf(), treeMat(6), 160),
        pine: instanced(pine(), treeMat(7), 160),
        palm: instanced(palm(), treeMat(6.5), 40),
        blossom: instanced(blossom(), treeMat(5), 14),
        bush: instanced(bush(), treeMat(1.2), 220),
    };
    const density = quality === 'low' ? 0.6 : 1;

    // Trees: forest mask decides where groves are; pines on high and northern ground, broadleaf elsewhere.
    for (let i = 0; i < 9000; i++) {
        const x = (rand() * 2 - 1) * 95, z = (rand() * 2 - 1) * 75;
        const h = heightAt(x, z);
        if (h < 1.3 || isBeach(x, z) || slopeAt(x, z) > 0.8 || pathDistance(x, z) < 3.2 || blocked(x, z, 2.5)) continue;
        const forest = fbm(x * 0.012 + 9, z * 0.012, 2);
        const want = forest > 0.55 ? 0.09 : 0.012;
        if (rand() > want * density) continue;
        const nearTrunk = colliders.some(c => (x - c.x) ** 2 + (z - c.z) ** 2 < 16);
        if (nearTrunk) continue;
        const mesh = (h > 7 || z < -25) && rand() < 0.75 ? kinds.pine : kinds.broadleaf;
        if (mesh.count >= mesh.instanceMatrix.count) continue;
        const scale = 0.8 + rand() * 0.55;
        place(mesh, x, z, scale);
        colliders.push({ x, z, r: 0.6, h: 3 });
        // The canopy blocks the camera too (a sphere around the crown).
        const pine = mesh === kinds.pine;
        canopies.push({ type: 'sphere', x, y: h + (pine ? 3.8 : 4.4) * scale, z, r: (pine ? 2.3 : 3.0) * scale });
    }
    // Palms line the southern beach, leaning seaward.
    for (let i = 0; i < 4000 && kinds.palm.count < 40 * density; i++) {
        const x = (rand() * 2 - 1) * 95, z = rand() * 75;
        const h = heightAt(x, z);
        if (h < 0.5 || h > 2.2 || blocked(x, z, 3) || pathDistance(x, z) < 2.5) continue;
        if (colliders.some(c => (x - c.x) ** 2 + (z - c.z) ** 2 < 36)) continue;
        place(kinds.palm, x, z, 0.85 + rand() * 0.4, Math.atan2(x, z) - Math.PI / 2 + (rand() - 0.5) * 0.8, 0.15, 0.02);
        colliders.push({ x, z, r: 0.45 });
    }
    // Bushes fill edges of groves and paths.
    for (let i = 0; i < 9000 && kinds.bush.count < 220 * density; i++) {
        const x = (rand() * 2 - 1) * 95, z = (rand() * 2 - 1) * 75;
        if (!isMeadow(x, z) || blocked(x, z, 1.5) || pathDistance(x, z) < 2.6) continue;
        if (fbm(x * 0.012 + 9, z * 0.012, 2) < 0.45 && rand() > 0.15) continue;
        place(kinds.bush, x, z, 0.6 + rand() * 0.7);
    }

    // Rocks: beach scatter, northern cliffs and the headland.
    const rockGeo = new THREE.DodecahedronGeometry(1, 0);
    const rocks = instanced(rockGeo, new THREE.MeshStandardMaterial({ flatShading: true, roughness: 0.95 }), 140);
    for (let i = 0; i < 6000 && rocks.count < 140; i++) {
        const x = (rand() * 2 - 1) * 100, z = (rand() * 2 - 1) * 80;
        const h = heightAt(x, z);
        if (h < -1.2 || blocked(x, z, 1.5) || pathDistance(x, z) < 2.5) continue;
        const coastal = h < 1.2 && (z < -10 || rand() < 0.25);
        const cliff = slopeAt(x, z) > 0.7;
        if (!(coastal || cliff) || rand() > 0.25) continue;
        const s = 0.4 + rand() * rand() * 2.2;
        dummy.position.set(x, h + s * 0.15, z);
        dummy.rotation.set(rand() * 3, rand() * 3, rand() * 3);
        dummy.scale.set(s * (0.8 + rand() * 0.6), s * (0.5 + rand() * 0.4), s * (0.8 + rand() * 0.6));
        dummy.updateMatrix();
        rocks.setMatrixAt(rocks.count, dummy.matrix);
        rocks.setColorAt(rocks.count++, new THREE.Color().setHSL(0.08, 0.06, 0.48 + rand() * 0.16));
        if (s > 1 && h > -0.3) { colliders.push({ x, z, r: s * 0.8 }); canopies.push({ type: 'sphere', x, y: h, z, r: s * 0.75 }); }
    }

    // Grass: clumps in meadow patches, coloured from the ground beneath so it blends in.
    const grassMax = { high: 48000, medium: 26000, low: 9000 }[quality];
    const grass = instanced(grassBlade(), windify(new THREE.MeshStandardMaterial({ vertexColors: true, side: THREE.DoubleSide, roughness: 1 }), { height: 1, sway: 0.16, push: true }), grassMax, false);
    const flowers = instanced(flowerGeometry(), windify(new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 0.8 }), { height: 0.5, sway: 0.08, push: true }), 1600, false);
    const petals = [0xffffff, 0xffd84a, 0xff9ec4, 0xb89cff, 0xff7f6a, 0x8fd0ff];
    const c = new THREE.Color();
    for (let i = 0; i < 400000 && grass.count < grassMax; i++) {
        const x = (rand() * 2 - 1) * 95, z = (rand() * 2 - 1) * 75;
        if (noise(x * 0.06, z * 0.06) < 0.42 || !isMeadow(x, z) || blocked(x, z)) continue;
        groundColor(x, z, undefined, c);
        for (let k = 0; k < 14 && grass.count < grassMax; k++) {
            const gx = x + (rand() - 0.5) * 2.2, gz = z + (rand() - 0.5) * 2.2;
            dummy.position.set(gx, heightAt(gx, gz) - 0.02, gz);
            dummy.rotation.set((rand() - 0.5) * 0.4, rand() * Math.PI, (rand() - 0.5) * 0.4);
            const s = 0.35 + rand() * 0.45;
            dummy.scale.set(s * (0.8 + rand() * 0.5), s, s);
            dummy.updateMatrix();
            grass.setMatrixAt(grass.count, dummy.matrix);
            grass.setColorAt(grass.count++, c.clone().multiplyScalar(0.9 + rand() * 0.25));
        }
    }
    for (let i = 0; i < 60000 && flowers.count < 1600; i++) {
        const x = (rand() * 2 - 1) * 95, z = (rand() * 2 - 1) * 75;
        if (noise(x * 0.045 + 30, z * 0.045) < 0.6 || !isMeadow(x, z) || blocked(x, z)) continue;
        const col = petals[Math.floor(rand() * petals.length)];
        for (let k = 0; k < 8 && flowers.count < 1600; k++) {
            const fx = x + (rand() - 0.5) * 3, fz = z + (rand() - 0.5) * 3;
            place(flowers, fx, fz, 0.7 + rand() * 0.6, rand() * 6, 0.02, 0.3);
            flowers.setColorAt(flowers.count - 1, new THREE.Color(col));
        }
    }

    for (const m of [...Object.values(kinds), rocks, grass, flowers]) {
        m.instanceMatrix.needsUpdate = true;
        if (m.instanceColor) m.instanceColor.needsUpdate = true;
        m.computeBoundingSphere();
        scene.add(m);
    }
    const placed = grass.count;
    return { grass, flowers, setGrassDensity: f => { grass.count = Math.floor(placed * f); } };
}
