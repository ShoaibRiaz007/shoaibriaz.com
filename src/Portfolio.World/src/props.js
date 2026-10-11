// Hand-shaped, procedurally built props: the cottage, pier and rowboat, campfire, hammock, lighthouse, windmill,
// the old tree with its rope swing, benches, lanterns, signpost, garden and beach bits. Static parts are merged
// per material (bakeStatic) so the whole set costs a few dozen draw calls. Each builder also registers what
// the rest of the world needs: colliders, walkable decks, clear zones for vegetation, and activity anchors.
import * as THREE from 'three';
import { bakeStatic, box, car as buildCar, glow, mat, stripeTexture, textTexture } from './builders.js';
import { addSurface, coastPoint, heightAt, PATHS, PLACES } from './terrain.js';
import { rng } from './noise.js';

const WOOD = 0xa8794e, WOOD_DARK = 0x6e4a30, STONE = 0x9d968b, CREAM = 0xf3e6cc, ROOF = 0xc65a3e, WHITE = 0xf6f2ea;

const cyl = (rt, rb, h, seg, color, opts) => {
    const m = new THREE.Mesh(new THREE.CylinderGeometry(rt, rb, h, seg), mat(color, opts));
    m.castShadow = m.receiveShadow = true;
    return m;
};
const put = (obj, x, y, z, rx = 0, ry = 0, rz = 0) => { obj.position.set(x, y, z); obj.rotation.set(rx, ry, rz); return obj; };
/** Rotates a local (x, z) offset by a yaw, matching Object3D.rotation.y. */
const rotXZ = (x, z, yaw) => ({ x: x * Math.cos(yaw) + z * Math.sin(yaw), z: -x * Math.sin(yaw) + z * Math.cos(yaw) });

export function buildProps(scene, { colliders, clear }) {
    const night = [];       // { material, day, night } emissive ramps
    const glows = [];       // { sprite, size } faded in after dusk
    const animated = [];    // (dt, t, env) => void
    const anchors = { benches: [] };
    const canopies = [];    // camera blockers that aren't walk colliders
    // Keep the arrival spot and the signpost uncluttered (no lantern posts or bushes in the first view).
    clear.push({ x: PLACES.spawn.x, z: PLACES.spawn.z, r: 4 }, { x: PLACES.signpost.x, z: PLACES.signpost.z, r: 3 });

    const windowMat = mat(0xffd27a, { emissive: 0xffb54a, emissiveIntensity: 0.05, roughness: 0.4 });
    const lanternMat = mat(0xfff0c0, { emissive: 0xffc46b, emissiveIntensity: 0.05, roughness: 0.4 });
    night.push({ material: windowMat, day: 0.05, night: 1.5 }, { material: lanternMat, day: 0.1, night: 2.2 });
    const addGlow = (parent, x, y, z, color, size) => {
        const s = glow(color, size);
        s.position.set(x, y, z);
        s.material.opacity = 0;
        parent.add(s);
        glows.push({ sprite: s });
        return s;
    };

    /** Places a group on the terrain, registers a box collider in its local frame, returns world helpers. */
    function site(group, x, z, yaw = 0, sink = 0.05) {
        group.position.set(x, heightAt(x, z) - sink, z);
        group.rotation.y = yaw;
        scene.add(group);
        return {
            world: (lx, ly, lz) => { const r = rotXZ(lx, lz, yaw); return new THREE.Vector3(x + r.x, group.position.y + ly, z + r.z); },
            box: (lx, lz, w, d, h) => { const r = rotXZ(lx, lz, yaw); colliders.push({ type: 'box', x: x + r.x, z: z + r.z, w, d, rot: yaw, h }); },
            circle: (lx, lz, rad, h) => { const r = rotXZ(lx, lz, yaw); colliders.push({ x: x + r.x, z: z + r.z, r: rad, h }); },
        };
    }
    const facingFrom = (yaw, extra = 0) => yaw + extra;

    // ---------------- Cottage ----------------
    {
        const { x, z, facing } = PLACES.cottage;
        const g = new THREE.Group();
        const W = 8, D = 6, H = 3.2, base = 0.7, top = base + H;
        g.add(put(box(W + 0.6, base, D + 0.6, STONE), 0, base / 2, 0));
        g.add(put(box(W, H, D, CREAM), 0, base + H / 2, 0));
        for (const sx of [-1, 1]) for (const sz of [-1, 1]) g.add(put(box(0.28, H, 0.28, WOOD_DARK), sx * W / 2, base + H / 2, sz * D / 2));
        for (const sz of [-1, 1]) g.add(put(box(W + 0.1, 0.22, 0.1, WOOD_DARK), 0, top - 0.1, sz * (D / 2 + 0.02)));
        // Gabled roof: two slabs and the gable triangles.
        const half = W / 2 + 0.7, rise = 2.6, slope = Math.hypot(half, rise), ang = Math.atan2(rise, half);
        for (const s of [-1, 1]) g.add(put(box(slope + 0.2, 0.28, D + 1.4, ROOF), s * half / 2, top + rise / 2 + 0.1, 0, 0, 0, -s * ang));
        g.add(put(box(0.3, 0.3, D + 1.5, 0x8e3b2a), 0, top + rise + 0.18, 0));
        const gable = new THREE.Shape([new THREE.Vector2(-W / 2, 0), new THREE.Vector2(W / 2, 0), new THREE.Vector2(0, rise * (W / 2) / half)]);
        for (const s of [-1, 1]) {
            const m = new THREE.Mesh(new THREE.ExtrudeGeometry(gable, { depth: 0.1, bevelEnabled: false }), mat(CREAM));
            put(m, 0, top, s * D / 2 - 0.05);
            m.castShadow = true;
            g.add(m);
            g.add(put(box(0.6, 0.6, 0.12, WOOD_DARK), 0, top + 1.0, s * (D / 2 + 0.04)));        // attic vent
        }
        // Chimney.
        g.add(put(box(0.9, 3.2, 0.9, STONE), W / 2 - 1.8, top + 1.6, -1.2));
        g.add(put(box(1.1, 0.2, 1.1, 0x7d776e), W / 2 - 1.8, top + 3.25, -1.2));
        // Door, windows, shutters, flower boxes.
        g.add(put(box(1.2, 2.15, 0.14, 0x7a4a2c), 0, base + 1.07, D / 2 + 0.05));
        g.add(put(box(0.1, 0.1, 0.1, 0xd8b45a), 0.42, base + 1.05, D / 2 + 0.15));
        const windowAt = (wx, wy, wz, ry) => {
            const w = new THREE.Group();
            w.add(put(box(1.35, 1.25, 0.1, WHITE), 0, 0, 0));
            w.add(put(new THREE.Mesh(new THREE.BoxGeometry(1.08, 0.98, 0.12), windowMat), 0, 0, 0.02));
            w.add(put(box(0.07, 0.98, 0.14, WHITE), 0, 0, 0.03), put(box(1.08, 0.07, 0.14, WHITE), 0, 0, 0.03));
            for (const s of [-1, 1]) w.add(put(box(0.42, 1.25, 0.08, 0x4f8fb8), s * 0.92, 0, 0.02));
            w.add(put(box(1.4, 0.28, 0.36, 0x7a4a2c), 0, -0.76, 0.16));
            [0xff8fa3, 0xffd34a, 0xffffff, 0xb48cff].forEach((c, i) => w.add(put(new THREE.Mesh(new THREE.IcosahedronGeometry(0.13, 0), mat(c)), -0.48 + i * 0.32, -0.58, 0.18)));
            put(w, wx, wy, wz, 0, ry, 0);
            g.add(w);
        };
        windowAt(-2.5, base + 1.75, D / 2 + 0.03, 0);
        windowAt(2.5, base + 1.75, D / 2 + 0.03, 0);
        windowAt(W / 2 + 0.03, base + 1.75, 0, Math.PI / 2);
        windowAt(-W / 2 - 0.03, base + 1.75, 0, -Math.PI / 2);
        windowAt(-1.8, base + 1.75, -D / 2 - 0.03, Math.PI);
        // Porch with posts, a little roof and a step.
        const porchY = base + 0.12;
        g.add(put(box(5.2, 0.24, 2.4, WOOD), 0, porchY, D / 2 + 1.2));
        for (let i = 0; i < 8; i++) g.add(put(box(0.04, 0.02, 2.4, WOOD_DARK), -2.3 + i * 0.66, porchY + 0.12, D / 2 + 1.2));
        for (const s of [-1, 1]) g.add(put(box(0.18, 2.5, 0.18, WOOD_DARK), s * 2.4, porchY + 1.25, D / 2 + 2.25));
        g.add(put(box(5.6, 0.14, 2.9, ROOF), 0, porchY + 2.62, D / 2 + 1.3, 0.22, 0, 0));
        g.add(put(box(1.8, 0.36, 0.7, STONE), 0, 0.18, D / 2 + 2.75));
        // Door lantern.
        g.add(put(box(0.22, 0.32, 0.22, 0x2b2f33), 0.95, base + 2.05, D / 2 + 0.22));
        g.add(put(new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.22, 0.16), lanternMat), 0.95, base + 2.03, D / 2 + 0.22));
        addGlow(g, 0.95, base + 2.03, D / 2 + 0.4, 0xffc46b, 2.2);
        for (const [wx, wz] of [[-2.5, D / 2 + 0.5], [2.5, D / 2 + 0.5]]) addGlow(g, wx, base + 1.75, wz, 0xffb54a, 2.6);
        // Rain barrel and a stack of firewood make it lived-in.
        g.add(put(cyl(0.45, 0.4, 1.0, 10, 0x8a5a36), -W / 2 - 0.7, 0.5, D / 2 - 0.6));
        for (let i = 0; i < 6; i++) g.add(put(cyl(0.16, 0.16, 1.4, 6, i % 2 ? 0x9c6b42 : 0x87593a), W / 2 + 0.55, 0.2 + Math.floor(i / 3) * 0.3, -1.6 + (i % 3) * 0.34, Math.PI / 2, 0, 0));
        bakeStatic(g);
        const s = site(g, x, z, facing, 0.1);
        s.box(0, 0, W + 0.6, D + 0.6, top + rise + 0.4);
        s.circle(-W / 2 - 0.7, D / 2 - 0.6, 0.6);
        for (const sx of [-1, 1]) s.circle(sx * 2.4, D / 2 + 2.25, 0.2);
        const deck = s.world(0, porchY + 0.12, D / 2 + 1.2);
        addSurface({ x: deck.x, z: deck.z, w: 5.2, d: 2.4, rot: facing, y: deck.y });
        const step = s.world(0, 0.36, D / 2 + 2.75);
        addSurface({ x: step.x, z: step.z, w: 1.8, d: 0.7, rot: facing, y: step.y, kind: 'path' });
        clear.push({ x, z, r: 9 });
        anchors.chimney = s.world(W / 2 - 1.8, top + 3.4, -1.2);
        anchors.porch = { ...s.world(-1.6, porchY + 0.12, D / 2 + 1.0), facing };
        anchors.door = s.world(0, 0, D / 2 + 3.4);
    }

    // ---------------- Mailbox and signpost ----------------
    {
        const p = rotXZ(3.2, 6.6, PLACES.cottage.facing);
        const mx = PLACES.cottage.x + p.x, mz = PLACES.cottage.z + p.z;
        const g = new THREE.Group();
        g.add(put(box(0.14, 1.1, 0.14, WOOD_DARK), 0, 0.55, 0));
        g.add(put(box(0.46, 0.4, 0.7, 0x3d7fb5), 0, 1.25, 0));
        const top = cyl(0.23, 0.23, 0.7, 10, 0x3d7fb5);
        g.add(put(top, 0, 1.45, 0, Math.PI / 2, 0, 0));
        const flag = new THREE.Group();
        flag.add(put(box(0.04, 0.4, 0.04, 0xd94b3b), 0, 0.2, 0), put(box(0.04, 0.14, 0.2, 0xd94b3b), 0, 0.33, 0.1));
        flag.position.set(0.26, 1.2, -0.1);
        flag.userData.dynamic = true;
        g.add(flag);
        const s = site(g, mx, mz, PLACES.cottage.facing + Math.PI / 2);
        s.circle(0, 0, 0.35);
        anchors.mailbox = { x: mx, z: mz, y: g.position.y, flag, facing: PLACES.cottage.facing };
    }
    {
        const { x, z } = PLACES.signpost;
        const g = new THREE.Group();
        g.add(put(box(0.22, 3.2, 0.22, WOOD_DARK), 0, 1.6, 0));
        const dest = [['Pier', PLACES.pier], ['Campfire', PLACES.campfire], ['Windmill', PLACES.windmill], ['Lighthouse', PLACES.lighthouse], ['Lookout', PLACES.hill]];
        dest.forEach(([label, d], i) => {
            const yaw = Math.atan2(d.x - x, d.z - z) - Math.PI / 2;   // board's +x points at the destination
            const tex = textTexture([label], { width: 256, height: 64, bg: '#7a5236', fg: '#fbefd8', accent: '#7a5236', title: true });
            const board = new THREE.Group();
            board.add(put(box(1.7, 0.36, 0.08, 0x7a5236), 0.85, 0, 0));
            const tip = new THREE.Mesh(new THREE.CylinderGeometry(0.255, 0.255, 0.08, 3), mat(0x7a5236));
            board.add(put(tip, 1.78, 0, 0, Math.PI / 2, 0, -Math.PI / 2));
            for (const side of [1, -1]) {
                const face = new THREE.Mesh(new THREE.PlaneGeometry(1.6, 0.34), new THREE.MeshStandardMaterial({ map: tex, roughness: 0.9 }));
                put(face, 0.85, 0, side * 0.045, 0, side > 0 ? 0 : Math.PI, 0);
                if (side < 0) face.scale.x = -1;    // keep text reading left-to-right from behind
                board.add(face);
            }
            put(board, 0, 2.85 - i * 0.42, 0, 0, yaw, 0);
            g.add(board);
        });
        const s = site(g, x, z);
        s.circle(0, 0, 0.3);
    }

    // ---------------- Garden west of the cottage ----------------
    {
        const p = rotXZ(-10.5, 1, PLACES.cottage.facing);
        const gx = PLACES.cottage.x + p.x, gz = PLACES.cottage.z + p.z;
        const g = new THREE.Group();
        const rand = rng(11);
        for (let row = 0; row < 3; row++) {
            g.add(put(box(4.2, 0.4, 1.1, 0x8a5a36), 0, 0.2, -2 + row * 1.8));
            g.add(put(box(4.0, 0.08, 0.95, 0x5b3d27), 0, 0.42, -2 + row * 1.8));
            for (let i = 0; i < 7; i++) {
                const kind = row % 3;
                const px = -1.7 + i * 0.57, pz = -2 + row * 1.8;
                if (kind === 0) g.add(put(new THREE.Mesh(new THREE.IcosahedronGeometry(0.22, 0), mat(0x6fbf4a)), px, 0.58, pz));
                if (kind === 1) { g.add(put(new THREE.Mesh(new THREE.ConeGeometry(0.07, 0.3, 5), mat(0x3f8f3a)), px, 0.6, pz)); g.add(put(new THREE.Mesh(new THREE.IcosahedronGeometry(0.1, 0), mat(0xf28a2e)), px, 0.48, pz)); }
                if (kind === 2) g.add(put(new THREE.Mesh(new THREE.IcosahedronGeometry(0.16 + rand() * 0.06, 0), mat(0xd9412f)), px, 0.56, pz));
            }
        }
        // Picket fence.
        for (let i = 0; i <= 12; i++) for (const side of [-1, 1]) g.add(put(box(0.1, 0.8, 0.06, WHITE), -3 + i * 0.5, 0.4, side * 3.4));
        for (let i = 0; i <= 13; i++) for (const side of [-1, 1]) g.add(put(box(0.06, 0.8, 0.1, WHITE), side * 3.2, 0.4, -3.4 + i * 0.52));
        for (const side of [-1, 1]) { g.add(put(box(6.4, 0.08, 0.05, WHITE), 0, 0.6, side * 3.4)); g.add(put(box(0.05, 0.08, 6.8, WHITE), side * 3.2, 0.6, 0)); }
        // Scarecrow.
        g.add(put(box(0.12, 2.2, 0.12, WOOD_DARK), 2.3, 1.1, 1.8), put(box(1.6, 0.1, 0.1, WOOD_DARK), 2.3, 1.7, 1.8));
        g.add(put(box(0.6, 0.8, 0.3, 0x7d9bc4), 2.3, 1.55, 1.8));
        g.add(put(new THREE.Mesh(new THREE.IcosahedronGeometry(0.26, 1), mat(0xe9d39a)), 2.3, 2.2, 1.8));
        g.add(put(cyl(0.18, 0.4, 0.3, 8, 0xc9a35a), 2.3, 2.45, 1.8));
        bakeStatic(g);
        const s = site(g, gx, gz, PLACES.cottage.facing);
        s.box(0, 0, 6.6, 7);
        clear.push({ x: gx, z: gz, r: 5 });
        anchors.garden = { x: gx, z: gz };
    }

    // ---------------- Lanterns along the paths ----------------
    {
        const proto = new THREE.Group();
        proto.add(put(box(0.14, 2.3, 0.14, 0x2e3236), 0, 1.15, 0));
        proto.add(put(box(0.5, 0.08, 0.08, 0x2e3236), 0.2, 2.25, 0));
        proto.add(put(box(0.3, 0.42, 0.3, 0x2e3236), 0.42, 1.98, 0));
        proto.add(put(new THREE.Mesh(new THREE.BoxGeometry(0.24, 0.32, 0.24), lanternMat), 0.42, 1.98, 0));
        proto.add(put(new THREE.Mesh(new THREE.ConeGeometry(0.26, 0.18, 4), mat(0x2e3236)), 0.42, 2.27, 0, 0, Math.PI / 4, 0));
        proto.scale.setScalar(1.25);
        const posts = new THREE.Group();
        let side = 1;
        for (const line of PATHS) {
            // Walk each path and drop a lantern every 13 m, alternating sides.
            let next = 4, travelled = 0;
            for (let i = 1; i < line.length; i++) {
                const [ax, az] = line[i - 1], [bx, bz] = line[i];
                const len = Math.hypot(bx - ax, bz - az), dx = (bx - ax) / len, dz = (bz - az) / len;
                for (; next <= travelled + len; next += 13) {
                    const d = next - travelled;
                    const px = ax + dx * d - dz * 1.9 * side, pz = az + dz * d + dx * 1.9 * side;
                    if (heightAt(px, pz) < 0.4 || clear.some(c => (px - c.x) ** 2 + (pz - c.z) ** 2 < c.r ** 2)) continue;
                    const l = proto.clone();
                    l.position.set(px, heightAt(px, pz) - 0.05, pz);
                    l.rotation.y = Math.atan2(dx * side, dz * side);      // arm (local +x) leans back over the path
                    posts.add(l);
                    l.updateMatrixWorld();
                    const head = new THREE.Vector3(0.42, 1.98, 0).applyMatrix4(l.matrixWorld);
                    addGlow(scene, head.x, head.y, head.z, 0xffc46b, 3);
                    colliders.push({ x: px, z: pz, r: 0.25 });
                    side = -side;
                }
                travelled += len;
            }
        }
        bakeStatic(posts);
        scene.add(posts);
    }

    // ---------------- Pier, rowboat ----------------
    {
        const { x, z, dirX, dirZ, length } = PLACES.pier;
        const yaw = Math.atan2(dirX, dirZ);
        const g = new THREE.Group();
        const deckY = 1.35, rampLen = 6, platStart = length - 5;
        const deckAt = lz => (lz < rampLen ? 0.75 + (deckY - 0.75) * (lz / rampLen) : deckY);
        for (let lz = 0.3; lz < length; lz += 0.56) {
            const w = lz > platStart ? 6 : 3;
            const tilt = lz < rampLen ? -Math.atan2(deckY - 0.75, rampLen) : 0;
            const shade = (Math.floor(lz / 0.56) % 3) * 0x060403;
            g.add(put(box(w, 0.12, 0.48, WOOD - shade), 0, deckAt(lz) - 0.06, lz, tilt, 0, 0));
        }
        for (const sx of [-1, 1]) g.add(put(box(0.18, 0.3, platStart - rampLen, WOOD_DARK), sx * 1.4, deckY - 0.25, (platStart + rampLen) / 2));
        const post = (px, pz, top) => {
            const h = top - Math.min(heightAt(...worldOf(px, pz)) - 0.3, 0);
            g.add(put(cyl(0.14, 0.16, h, 7, WOOD_DARK), px, top - h / 2, pz));
        };
        const worldOf = (lx, lz) => { const r = rotXZ(lx, lz, yaw); return [x + r.x, z + r.z]; };
        for (let lz = rampLen; lz <= platStart; lz += 4) for (const sx of [-1, 1]) post(sx * 1.55, lz, deckY + 0.9);
        for (const [px, pz] of [[-3, platStart], [3, platStart], [-3, length], [3, length], [0, length]]) post(px, pz, deckY + 0.9);
        // Rope rails between the posts.
        for (const sx of [-1, 1]) g.add(put(cyl(0.035, 0.035, platStart - rampLen, 5, 0xcbb38a), sx * 1.55, deckY + 0.8, (platStart + rampLen) / 2, Math.PI / 2, 0, 0));
        for (const sx of [-1, 1]) g.add(put(cyl(0.035, 0.035, 5, 5, 0xcbb38a), sx * 3, deckY + 0.8, platStart + 2.5, Math.PI / 2, 0, 0));
        g.add(put(cyl(0.035, 0.035, 6, 5, 0xcbb38a), 0, deckY + 0.8, length, 0, 0, Math.PI / 2));
        // Lamp, bucket and a crate of tackle at the end.
        g.add(put(box(0.14, 2.6, 0.14, 0x2e3236), -2.8, deckY + 1.3, length - 0.3));
        g.add(put(new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.36, 0.28), lanternMat), -2.8, deckY + 2.7, length - 0.3));
        addGlow(g, -2.8, deckY + 2.7, length - 0.3, 0xffc46b, 3);
        g.add(put(cyl(0.22, 0.18, 0.4, 10, 0x6b8fa8), 2.2, deckY + 0.2, platStart + 1.2));
        g.add(put(box(0.8, 0.5, 0.6, 0x8a5a36), -2.2, deckY + 0.25, platStart + 1.4));
        bakeStatic(g);
        const s = site(g, x, z, yaw, 0);
        g.position.y = 0;
        // Walkable decks: ramp, walkway, end platform.
        const deckSurface = (lz, d, w, y0, y1 = y0) => { const c = s.world(0, 0, lz); addSurface({ x: c.x, z: c.z, w, d, rot: yaw, y: y0, y1 }); };
        deckSurface(rampLen / 2, rampLen + 0.4, 3.1, 0.75, deckY);
        deckSurface((rampLen + platStart) / 2, platStart - rampLen, 3.1, deckY);
        deckSurface(platStart + 2.5, 5.4, 6.1, deckY);
        // Rails keep feet on the planks.
        const rail = (lx, lz, w, d) => s.box(lx, lz, w, d);
        for (const sx of [-1, 1]) rail(sx * 1.7, (platStart + rampLen + 1) / 2, 0.2, platStart - rampLen - 1);
        for (const sx of [-1, 1]) { rail(sx * 3.15, platStart + 2.5, 0.2, 5.4); rail(sx * 2.35, platStart - 0.15, 1.5, 0.2); }
        rail(0, length + 0.15, 6.2, 0.2);
        anchors.fishing = { ...s.world(0.6, deckY, length - 0.8), facing: yaw };
        anchors.pierEnd = s.world(0, deckY, length);

        // Rowboat moored alongside, rocking on the swell.
        const boat = new THREE.Group();
        const hull = new THREE.Mesh(new THREE.SphereGeometry(1, 12, 6, 0, Math.PI * 2, Math.PI / 2, Math.PI / 2), mat(0x9a6a42, { side: THREE.DoubleSide }));
        hull.scale.set(0.95, 0.55, 2.1);
        hull.castShadow = true;
        boat.add(hull);
        boat.add(put(new THREE.Mesh(new THREE.TorusGeometry(1, 0.06, 4, 20), mat(WHITE)), 0, 0, 0, Math.PI / 2, 0, 0));
        boat.children[1].scale.set(0.95, 2.1, 1);
        boat.add(put(box(1.7, 0.08, 0.35, WOOD_DARK), 0, -0.12, 0.5), put(box(1.6, 0.08, 0.35, WOOD_DARK), 0, -0.15, -0.8));
        for (const sx of [-1, 1]) boat.add(put(box(0.07, 0.05, 2.4, 0xc9a26b), sx * 0.75, 0.05, 0, 0, sx * 0.15, sx * 0.3));
        const moor = s.world(3.4, 0, 18);
        boat.position.copy(moor);
        boat.rotation.y = yaw + 0.08;
        scene.add(boat);
        colliders.push({ x: moor.x, z: moor.z, r: 1.4 });
        animated.push((dt, t, env) => {
            boat.position.y = env.water.heightAt(moor.x, moor.z, t) + 0.32;
            boat.rotation.z = Math.sin(t * 1.1) * 0.06;
            boat.rotation.x = Math.sin(t * 0.8 + 1) * 0.04;
        });
        clear.push({ x, z, r: 4 });
    }

    // ---------------- Benches ----------------
    function bench(x, z, yaw, name) {
        const g = new THREE.Group();
        for (let i = 0; i < 3; i++) g.add(put(box(1.9, 0.07, 0.16, WOOD), 0, 0.48, -0.2 + i * 0.19));
        for (let i = 0; i < 2; i++) g.add(put(box(1.9, 0.14, 0.05, WOOD), 0, 0.72 + i * 0.2, -0.36, -0.12, 0, 0));
        for (const sx of [-0.8, 0.8]) {
            g.add(put(box(0.08, 0.48, 0.5, 0x3a3f44), sx, 0.24, 0));
            g.add(put(box(0.08, 0.5, 0.06, 0x3a3f44), sx, 0.75, -0.36, -0.12, 0, 0));
        }
        bakeStatic(g);
        const s = site(g, x, z, yaw);
        s.box(0, -0.05, 2, 0.75);
        const seat = s.world(0, 0.5, 0.02);
        anchors.benches.push({ x: seat.x, z: seat.z, y: seat.y, facing: yaw, name, front: s.world(0, 0, 1.2) });
        clear.push({ x, z, r: 2 });
    }

    // ---------------- Campfire with log seats ----------------
    {
        const { x, z } = PLACES.campfire;
        const g = new THREE.Group();
        for (let i = 0; i < 10; i++) {
            const a = (i / 10) * Math.PI * 2;
            const st = new THREE.Mesh(new THREE.DodecahedronGeometry(0.26, 0), mat(i % 2 ? 0x8d877d : 0x77716a));
            g.add(put(st, Math.cos(a) * 0.95, 0.12, Math.sin(a) * 0.95, a, a * 2, 0));
        }
        for (let i = 0; i < 4; i++) {
            const a = (i / 4) * Math.PI * 2 + 0.4;
            g.add(put(cyl(0.09, 0.11, 1.3, 6, 0x5a3a24), Math.cos(a) * 0.3, 0.42, Math.sin(a) * 0.3, Math.sin(a) * 0.6, 0, -Math.cos(a) * 0.6));
        }
        const seats = [];
        [-0.3, 1.5, 3.3].forEach((a, i) => {
            const lx = Math.cos(a) * 2.7, lz = Math.sin(a) * 2.7;
            g.add(put(cyl(0.3, 0.32, 2.0, 8, i % 2 ? 0x8a5a36 : 0x7a4f30), lx, 0.28, lz, 0, -a, Math.PI / 2));
            seats.push({ lx, lz, a });
        });
        bakeStatic(g);
        const s = site(g, x, z, 0, 0.05);
        s.circle(0, 0, 1.2);
        // Fire: flickering cones, embers, a warm light.
        const fire = new THREE.Group();
        fire.userData.dynamic = true;
        const flames = [[0xff7a1a, 0.55, 1.5], [0xffb13b, 0.38, 1.15], [0xffe58a, 0.2, 0.8]].map(([c, r, h]) => {
            const f = new THREE.Mesh(new THREE.ConeGeometry(r, h, 6), new THREE.MeshBasicMaterial({ color: c, transparent: true, opacity: 0.9, fog: false }));
            f.position.y = 0.3 + h / 2;
            fire.add(f);
            return f;
        });
        const light = new THREE.PointLight(0xff9a4a, 0, 18, 1.6);
        light.position.y = 1.4;
        fire.add(light);
        const fireGlow = glow(0xff9a4a, 5);
        fireGlow.position.y = 0.9;
        fire.add(fireGlow);
        const emberCount = 28, emberPos = new Float32Array(emberCount * 3), emberLife = new Float32Array(emberCount);
        const emberGeo = new THREE.BufferGeometry();
        emberGeo.setAttribute('position', new THREE.BufferAttribute(emberPos, 3));
        const embers = new THREE.Points(emberGeo, new THREE.PointsMaterial({ color: 0xffb44a, size: 0.09, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false }));
        embers.frustumCulled = false;
        fire.add(embers);
        for (let i = 0; i < emberCount; i++) emberLife[i] = Math.random() * 2;
        g.add(fire);
        animated.push((dt, t, env) => {
            flames.forEach((f, i) => {
                const k = 1 + Math.sin(t * (9 + i * 3) + i) * 0.12 + Math.sin(t * 23 + i * 2) * 0.06;
                f.scale.set(1 + Math.sin(t * 7 + i) * 0.08, k, 1 + Math.cos(t * 6 + i) * 0.08);
                f.rotation.y = t * (0.6 + i * 0.4);
            });
            light.intensity = (14 + Math.sin(t * 13) * 2.5 + Math.sin(t * 31) * 1.5) * (0.25 + env.night * 0.75);
            fireGlow.material.opacity = 0.35 + env.night * 0.6;
            for (let i = 0; i < emberCount; i++) {
                emberLife[i] -= dt;
                if (emberLife[i] <= 0) { emberLife[i] = 1.2 + Math.random() * 1.4; emberPos.set([(Math.random() - 0.5) * 0.5, 0.6, (Math.random() - 0.5) * 0.5], i * 3); }
                emberPos[i * 3 + 1] += dt * (1.1 + (i % 5) * 0.2);
                emberPos[i * 3] += Math.sin(t * 3 + i) * dt * 0.3;
            }
            emberGeo.attributes.position.needsUpdate = true;
        });
        anchors.campfire = {
            center: s.world(0, 0, 0),
            seats: seats.map(({ lx, lz, a }) => {
                const w = s.world(lx, 0.62, lz);
                return { x: w.x, z: w.z, y: w.y, facing: Math.atan2(-lx, -lz) };
            }),
        };
        for (const { lx, lz } of seats) s.circle(lx, lz, 0.5);
        clear.push({ x, z, r: 5 });
    }

    // ---------------- Hammock between two palms ----------------
    {
        const { x, z } = PLACES.hammock;
        const coast = coastPoint(66);
        const yaw = Math.atan2(coast.nx, coast.nz);   // local +z faces the sea, so the span (local x) runs along the shore
        const g = new THREE.Group();
        const span = 4.4;
        for (const s of [-1, 1]) {
            let px = 0, py = 0;
            for (let i = 0; i < 5; i++) {
                const lean = 0.08 + i * 0.05;
                g.add(put(cyl(0.16 - i * 0.012, 0.19 - i * 0.012, 1.1, 6, i % 2 ? 0xa3784f : 0x8f6a45), s * (span / 2 + px), py + 0.55, 0, 0, 0, s * lean));
                px += Math.sin(lean) * 1.05;
                py += Math.cos(lean) * 1.05;
            }
            for (let i = 0; i < 7; i++) {
                const a = (i / 7) * Math.PI * 2;
                const frond = new THREE.Group();
                frond.add(put(box(0.45, 0.05, 1.5, i % 2 ? 0x4c9a3c : 0x5aae46), 0, 0, 0.75, -0.2, 0, 0));
                frond.add(put(box(0.38, 0.05, 1.3, 0x3f8a33), 0, -0.22, 2.0, 0.55, 0, 0));
                put(frond, s * (span / 2 + px), py, 0, 0, a, 0);
                g.add(frond);
            }
        }
        bakeStatic(g);
        // The cloth: a striped sagging sheet that sways.
        const sway = new THREE.Group();
        sway.userData.dynamic = true;
        sway.position.y = 1.55;
        const cloth = new THREE.PlaneGeometry(span - 0.6, 1.1, 14, 3);
        const cp = cloth.attributes.position;
        for (let i = 0; i < cp.count; i++) {
            const u = cp.getX(i) / ((span - 0.6) / 2);
            cp.setZ(i, -0.55 * (1 - u * u));
            cp.setY(i, cp.getY(i) * (0.75 + 0.25 * u * u));
        }
        cloth.rotateX(-Math.PI / 2);
        cloth.computeVertexNormals();
        const clothMat = new THREE.MeshStandardMaterial({ map: stripeTexture(['#e9573f', '#f6e7c8', '#3d8fb8', '#f6e7c8'], 3), side: THREE.DoubleSide, roughness: 0.9 });
        clothMat.map.rotation = Math.PI / 2;
        const sheet = new THREE.Mesh(cloth, clothMat);
        sheet.castShadow = true;
        sway.add(sheet);
        for (const s of [-1, 1]) sway.add(put(cyl(0.02, 0.02, 0.5, 4, 0xe9dcc0), s * (span / 2 - 0.15), 0, 0, 0, 0, s * 1.2));
        g.add(sway);
        const site_ = site(g, x, z, yaw, 0.1);
        for (const s of [-1, 1]) site_.circle(s * span / 2, 0, 0.35);
        animated.push((dt, t, env) => { sway.rotation.x = Math.sin(t * 0.9) * (env.hammockSwing ?? 0.06); });
        const centre = site_.world(0, 1.1, 0);
        anchors.hammock = { x: centre.x, z: centre.z, y: centre.y, yaw, sway, front: site_.world(0, 0, -1.8), lookYaw: Math.atan2(-coast.nx, -coast.nz) };
        clear.push({ x, z, r: 4 });
    }

    // ---------------- Lighthouse on the headland ----------------
    {
        const { x, z } = PLACES.lighthouse;
        const yaw = Math.atan2(PLACES.windmill.x - x, PLACES.windmill.z - z);     // door faces the path
        const g = new THREE.Group();
        const segs = 6, segH = 2.5;
        for (let i = 0; i < segs; i++) {
            const r0 = 2.3 - i * 0.14, r1 = 2.3 - (i + 1) * 0.14;
            g.add(put(cyl(r1, r0, segH, 14, i % 2 ? 0xc9483b : WHITE), 0, i * segH + segH / 2, 0));
        }
        const topY = segs * segH;
        g.add(put(cyl(2.5, 2.3, 0.3, 16, 0x3a3f44), 0, topY + 0.15, 0));
        for (let i = 0; i < 16; i++) {
            const a = (i / 16) * Math.PI * 2;
            g.add(put(box(0.06, 0.9, 0.06, 0x3a3f44), Math.cos(a) * 2.35, topY + 0.75, Math.sin(a) * 2.35));
        }
        const rail = new THREE.Mesh(new THREE.TorusGeometry(2.35, 0.05, 4, 24), mat(0x3a3f44));
        g.add(put(rail, 0, topY + 1.2, 0, Math.PI / 2, 0, 0));
        const lampMat = mat(0xfff3c4, { emissive: 0xffd77a, emissiveIntensity: 0.3, roughness: 0.2, transparent: true, opacity: 0.9 });
        night.push({ material: lampMat, day: 0.3, night: 3 });
        g.add(put(new THREE.Mesh(new THREE.CylinderGeometry(1.15, 1.15, 1.9, 10), lampMat), 0, topY + 1.25, 0));
        for (let i = 0; i < 6; i++) { const a = (i / 6) * Math.PI * 2; g.add(put(box(0.08, 1.9, 0.08, 0x2a2e33), Math.cos(a) * 1.18, topY + 1.25, Math.sin(a) * 1.18)); }
        g.add(put(new THREE.Mesh(new THREE.ConeGeometry(1.5, 1.3, 10), mat(0xc9483b)), 0, topY + 2.85, 0));
        g.add(put(new THREE.Mesh(new THREE.SphereGeometry(0.2, 8, 6), mat(0x3a3f44)), 0, topY + 3.6, 0));
        g.add(put(box(1.1, 2.0, 0.3, 0x2f6b4f), 0, 1.0, 2.25));
        for (let i = 1; i < segs; i++) g.add(put(box(0.45, 0.6, 0.2, 0x2a3b4c), 0, i * segH + 1.2, 2.3 - i * 0.14 - 0.02, 0, 0, 0));
        // Keeper's hut.
        g.add(put(box(4, 2.6, 3.4, WHITE), -4.6, 1.3, 0.6));
        g.add(put(box(4.5, 0.25, 3.9, 0x2f6b4f), -4.6, 2.75, 0.6, 0, 0, 0.12));
        g.add(put(new THREE.Mesh(new THREE.BoxGeometry(0.9, 0.8, 0.12), windowMat), -4.6, 1.5, 2.32));
        addGlow(g, -4.6, 1.5, 2.6, 0xffb54a, 2.4);
        bakeStatic(g);
        // Rotating beam: two soft cones from the lamp, only after dusk.
        const beam = new THREE.Group();
        beam.userData.dynamic = true;
        beam.position.y = topY + 1.25;
        const beamMat = new THREE.MeshBasicMaterial({ color: 0xfff1b8, transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, fog: false });
        for (const s of [-1, 1]) {
            const cone = new THREE.Mesh(new THREE.ConeGeometry(6, 60, 16, 1, true), beamMat);
            cone.rotation.z = s * Math.PI / 2;
            cone.position.x = s * 30;
            beam.add(cone);
        }
        g.add(beam);
        const lampGlow = addGlow(g, 0, topY + 1.25, 0, 0xfff1b8, 9);
        const s = site(g, x, z, yaw, 0.1);
        s.circle(0, 0, 2.6, topY + 4);
        s.box(-4.6, 0.6, 4.2, 3.6, 3.2);
        animated.push((dt, t, env) => {
            beam.rotation.y = t * 0.55;
            beamMat.opacity = Math.max(0, env.night - 0.2) * 0.2;
            lampGlow.scale.setScalar(7 + Math.sin(t * 0.55 * 2) * 2);
        });
        const door = s.world(0, 0, 3.6);
        anchors.lighthouse = {
            door: { x: door.x, z: door.z, facing: yaw + Math.PI },
            gallery: s.world(0, topY + 0.3, -1.9),        // seaward side of the gallery
            galleryFacing: yaw + Math.PI,
            top: s.world(0, topY + 1.2, 0),
        };
        addSurface({ ...(() => { const c = s.world(0, 0, 0); return { x: c.x, z: c.z }; })(), w: 4.6, d: 4.6, rot: yaw, y: g.position.y + topY + 0.3, kind: 'wood', enabled: () => anchors.lighthouse.climbed === true });
        clear.push({ x, z, r: 8 });
        bench(...(() => { const w = s.world(5.5, 0, 4); return [w.x, w.z]; })(), yaw + Math.PI * 0.75, 'cliff');
    }

    // ---------------- Windmill ----------------
    {
        const { x, z } = PLACES.windmill;
        const yaw = Math.atan2(PLACES.cottage.x - x, PLACES.cottage.z - z) + 0.4;
        const g = new THREE.Group();
        g.add(put(cyl(1.7, 2.3, 8, 8, 0xeadfc7), 0, 4, 0));
        g.add(put(cyl(2.35, 2.35, 0.4, 8, STONE), 0, 0.2, 0));
        g.add(put(cyl(1.9, 1.75, 0.25, 8, WOOD_DARK), 0, 8.1, 0));
        g.add(put(new THREE.Mesh(new THREE.ConeGeometry(2.1, 2.6, 8), mat(0x8a4b36)), 0, 9.5, 0));
        g.add(put(box(1, 1.8, 0.2, 0x7a4a2c), 0, 0.9, 2.2, -0.1, 0, 0));
        g.add(put(new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.8, 0.15), windowMat), 0, 5, 1.9, -0.06, 0, 0));
        addGlow(g, 0, 5, 2.2, 0xffb54a, 2);
        g.add(put(cyl(0.18, 0.18, 1.2, 8, WOOD_DARK), 0, 8.6, 2.1, Math.PI / 2, 0, 0));
        bakeStatic(g);
        const sails = new THREE.Group();
        sails.userData.dynamic = true;
        sails.position.set(0, 8.6, 2.75);
        const cloth = mat(0xf4ecd8, { side: THREE.DoubleSide });
        for (let i = 0; i < 4; i++) {
            const arm = new THREE.Group();
            arm.add(put(box(0.18, 5.6, 0.14, WOOD_DARK), 0, 2.9, 0));
            arm.add(put(new THREE.Mesh(new THREE.PlaneGeometry(1.3, 4.4), cloth), 0.75, 3.2, 0.05));
            for (let k = 0; k < 5; k++) arm.add(put(box(1.4, 0.05, 0.06, WOOD_DARK), 0.75, 1.1 + k * 1.05, 0.08));
            arm.rotation.z = (i / 4) * Math.PI * 2;
            sails.add(arm);
        }
        sails.add(put(new THREE.Mesh(new THREE.SphereGeometry(0.32, 8, 6), mat(WOOD_DARK)), 0, 0, 0));
        sails.traverse(o => { if (o.isMesh) o.castShadow = true; });
        g.add(sails);
        const s = site(g, x, z, yaw, 0.1);
        s.circle(0, 0, 2.5, 11);
        animated.push((dt, t, env) => { sails.rotation.z -= dt * 0.35 * (env.gust ?? 1); });
        clear.push({ x, z, r: 6 });
        const b = s.world(-4.5, 0, 3.5);
        bench(b.x, b.z, yaw + 0.5, 'windmill');
    }

    // ---------------- The old tree on the lookout, with a rope swing ----------------
    {
        const { x, z } = PLACES.hill;
        const view = coastPoint(150);                          // look out over the western sea
        const yaw = Math.atan2(view.x - x, view.z - z);
        const g = new THREE.Group();
        const rand = rng(5);
        g.add(put(cyl(0.65, 1.05, 5.5, 8, 0x6a4630), 0, 2.75, 0));
        for (let i = 0; i < 6; i++) {
            const a = (i / 6) * Math.PI * 2 + 0.3, ang = 0.7 + rand() * 0.3;
            const len = 3 + rand() * 1.5;
            g.add(put(cyl(0.18, 0.32, len, 6, 0x6a4630), Math.cos(a) * len * 0.35, 5 + Math.cos(ang) * len * 0.4, Math.sin(a) * len * 0.35, Math.sin(a) * ang, 0, -Math.cos(a) * ang));
        }
        // The swing branch reaches out toward the view.
        g.add(put(cyl(0.2, 0.34, 5.2, 6, 0x6a4630), 0, 5.7, 2.4, Math.PI / 2 - 0.1, 0, 0));
        const greens = [0x4f9a45, 0x5fae4e, 0x468c3d, 0x6dbb55, 0x3f8238];
        for (let i = 0; i < 9; i++) {
            const a = (i / 9) * Math.PI * 2, r = i === 0 ? 0 : 2.6 + rand() * 1.2;
            const blob = new THREE.Mesh(new THREE.IcosahedronGeometry(2.4 + rand() * 1.1, 1), mat(greens[i % greens.length]));
            put(blob, Math.cos(a) * r, 8.2 + rand() * 1.6 - (i === 0 ? -0.8 : 0), Math.sin(a) * r);
            blob.castShadow = true;
            g.add(blob);
        }
        bakeStatic(g);
        const swing = new THREE.Group();
        swing.userData.dynamic = true;
        swing.position.set(0, 5.75, 4.3);
        for (const sx of [-0.45, 0.45]) swing.add(put(cyl(0.025, 0.025, 4.9, 4, 0xd8c39a), sx, -2.45, 0));
        swing.add(put(box(1.1, 0.08, 0.42, WOOD), 0, -4.9, 0));
        swing.traverse(o => { if (o.isMesh) o.castShadow = true; });
        g.add(swing);
        const s = site(g, x, z, yaw, 0.2);
        s.circle(0, 0, 1.3, 7);
        canopies.push({ type: 'sphere', ...s.world(0, 9, 0), r: 4.6 });
        animated.push((dt, t, env) => { if (!env.swinging) swing.rotation.x = Math.sin(t * 1.3) * 0.03; });
        anchors.swing = { group: swing, pivot: s.world(0, 5.75, 4.3), yaw, seatDrop: 4.9 };
        clear.push({ x, z, r: 7 });
        const b = s.world(3.6, 0, 4.4);
        bench(b.x, b.z, yaw + 0.25, 'lookout');
        // Telescope on a tripod, aimed at the lighthouse.
        const tg = new THREE.Group();
        for (let i = 0; i < 3; i++) { const a = (i / 3) * Math.PI * 2; tg.add(put(cyl(0.03, 0.03, 1.4, 4, 0x3a3f44), Math.cos(a) * 0.25, 0.65, Math.sin(a) * 0.25, Math.sin(a) * 0.35, 0, -Math.cos(a) * 0.35)); }
        tg.add(put(cyl(0.09, 0.13, 1.1, 10, 0xb8893a, { metalness: 0.6, roughness: 0.35 }), 0, 1.42, 0.1, Math.PI / 2 - 0.25, 0, 0));
        bakeStatic(tg);
        const tp = s.world(-3.2, 0, 3.8);
        const tyaw = Math.atan2(PLACES.lighthouse.x - tp.x, PLACES.lighthouse.z - tp.z);
        site(tg, tp.x, tp.z, tyaw).circle(0, 0, 0.4);
        anchors.telescope = { x: tp.x, z: tp.z, y: tg.position.y, facing: tyaw, eye: new THREE.Vector3(tp.x, tg.position.y + 1.6, tp.z) };
        anchors.hillTop = s.world(0, 0, 5);
    }

    // ---------------- Beach bits: sandcastle, starfish, driftwood ----------------
    {
        const g = new THREE.Group();
        const sand = 0xe2c48e;
        const hp = PLACES.hammock;
        const castle = new THREE.Group();
        castle.add(put(box(1.2, 0.35, 1.2, sand), 0, 0.17, 0));
        for (const [cx, cz] of [[-0.5, -0.5], [0.5, -0.5], [-0.5, 0.5], [0.5, 0.5]]) {
            castle.add(put(cyl(0.2, 0.24, 0.6, 8, sand), cx, 0.5, cz));
            castle.add(put(new THREE.Mesh(new THREE.ConeGeometry(0.24, 0.3, 8), mat(sand)), cx, 0.95, cz));
        }
        castle.add(put(cyl(0.3, 0.36, 0.8, 8, sand), 0, 0.75, 0), put(box(0.02, 0.5, 0.02, WOOD_DARK), 0, 1.4, 0), put(box(0.25, 0.15, 0.01, 0xd94b3b), 0.13, 1.55, 0));
        castle.position.set(hp.x + 5, heightAt(hp.x + 5, hp.z + 3), hp.z + 3);
        g.add(castle);
        const star = new THREE.Shape();
        for (let i = 0; i < 10; i++) { const r = i % 2 ? 0.09 : 0.24, a = (i / 10) * Math.PI * 2; i ? star.lineTo(Math.cos(a) * r, Math.sin(a) * r) : star.moveTo(r, 0); }
        const starGeo = new THREE.ExtrudeGeometry(star, { depth: 0.04, bevelEnabled: false });
        const rand = rng(3);
        for (let i = 0; i < 7; i++) {
            const c = coastPoint(30 + rand() * 120);
            const sx = c.x - c.nx * (2 + rand() * 4), sz = c.z - c.nz * (2 + rand() * 4);
            const m = new THREE.Mesh(starGeo, mat([0xf08a5d, 0xe8605a, 0xf2b45a][i % 3]));
            put(m, sx, heightAt(sx, sz) + 0.03, sz, -Math.PI / 2, 0, rand() * 6);
            g.add(m);
        }
        for (let i = 0; i < 5; i++) {
            const c = coastPoint(10 + i * 32 + rand() * 10);
            const dx = c.x - c.nx * 5, dz = c.z - c.nz * 5;
            g.add(put(cyl(0.16, 0.22, 2.4 + rand() * 1.5, 6, 0xb8a58a), dx, heightAt(dx, dz) + 0.12, dz, Math.PI / 2, rand() * 3, 0.1));
            colliders.push({ x: dx, z: dz, r: 0.5 });
        }
        bakeStatic(g);
        scene.add(g);
    }

    // ---------------- The car, parked by the cottage ----------------
    {
        const { x, z, heading } = PLACES.car;
        const carObj = buildCar();
        bakeStatic(carObj.userData.chassis);
        carObj.position.set(x, heightAt(x, z), z);
        carObj.rotation.y = heading;
        scene.add(carObj);
        anchors.car = carObj;
        clear.push({ x, z, r: 4 });
    }

    // Day/night ramp for emissive materials and glow sprites.
    function update(dt, t, env) {
        for (const n of night) n.material.emissiveIntensity = n.day + (n.night - n.day) * env.night;
        for (const g of glows) g.sprite.material.opacity = Math.max(0, env.night - 0.1) * 0.9;
        for (const a of animated) a(dt, t, env);
    }
    return { anchors, update, windowMat, lanternMat, canopies };
}
