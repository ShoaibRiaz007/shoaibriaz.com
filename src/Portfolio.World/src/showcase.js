// The owner's work, placed in the world: a Career Trail of milestones climbing the lookout path (oldest at the
// bottom, today at the summit) and an open-air Gallery of the top five projects on the eastern meadow. All text
// comes from /api/site and is drawn onto canvas textures or set as DOM text, never parsed as HTML.
import * as THREE from 'three';
import { bakeStatic, box, glow, mat, textTexture } from './builders.js';
import { heightAt, PATHS, PLACES } from './terrain.js';

const ACCENTS = [0xe8743b, 0x3f8f5a, 0x3d8fb8, 0xb86fc9, 0xd9a43a, 0x5a7fd6];
const WOOD = 0x8a5a36, STONE = 0x9d968b;

/** The five projects to show: featured ones first (in their admin order), topped up with the next ones. */
export function topProjects(projects = []) {
    const featured = projects.filter(p => p.isFeatured);
    return [...featured, ...projects.filter(p => !p.isFeatured)].slice(0, 5);
}

const put = (o, x, y, z, rx = 0, ry = 0, rz = 0) => { o.position.set(x, y, z); o.rotation.set(rx, ry, rz); return o; };
const cyl = (rt, rb, h, seg, c, o) => { const m = new THREE.Mesh(new THREE.CylinderGeometry(rt, rb, h, seg), mat(c, o)); m.castShadow = true; return m; };

/** A point and direction a given distance along a polyline. */
function along(line, dist) {
    let left = dist;
    for (let i = 1; i < line.length; i++) {
        const [ax, az] = line[i - 1], [bx, bz] = line[i];
        const len = Math.hypot(bx - ax, bz - az);
        if (left <= len || i === line.length - 1) {
            const k = Math.min(1, left / len);
            return { x: ax + (bx - ax) * k, z: az + (bz - az) * k, dx: (bx - ax) / len, dz: (bz - az) / len };
        }
        left -= len;
    }
    return null;
}
const lineLength = line => line.slice(1).reduce((s, [x, z], i) => s + Math.hypot(x - line[i][0], z - line[i][1]), 0);

/** A floating emblem that hints at what a project is. */
function emblem(project, color) {
    const g = new THREE.Group();
    const text = `${project.category} ${project.slug} ${project.title}`.toLowerCase();
    const glowMat = mat(color, { emissive: color, emissiveIntensity: 0.55, roughness: 0.3 });
    if (/fruit/.test(text)) {
        // A watermelon slice for the Fruit Ninja level pipeline.
        const rind = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.42, 0.16, 20, 1, false, 0, Math.PI), mat(0x3f8f3a));
        const flesh = new THREE.Mesh(new THREE.CylinderGeometry(0.36, 0.36, 0.17, 20, 1, false, 0, Math.PI), mat(0xe8504a, { emissive: 0xe8504a, emissiveIntensity: 0.2 }));
        g.add(put(rind, 0, 0, 0, Math.PI / 2, 0, 0), put(flesh, 0, 0, 0.002, Math.PI / 2, 0, 0));
        for (let i = 0; i < 5; i++) g.add(put(new THREE.Mesh(new THREE.SphereGeometry(0.025, 6, 4), mat(0x1c1410)), -0.22 + i * 0.11, 0.12 + (i % 2) * 0.06, 0.09));
    } else if (/\bai\b|llm|claude|diagram/.test(text)) {
        g.add(new THREE.Mesh(new THREE.IcosahedronGeometry(0.34, 0), glowMat));
        for (let i = 0; i < 3; i++) {
            const orb = new THREE.Mesh(new THREE.SphereGeometry(0.05, 8, 6), mat(0xffffff, { emissive: 0xffffff, emissiveIntensity: 0.8 }));
            orb.userData.orbit = i;
            g.add(orb);
        }
    } else if (/\.net|desktop|launcher|ipc/.test(text)) {
        g.add(put(box(0.7, 0.45, 0.06, 0x2a2f36), 0, 0.08, 0));
        g.add(put(new THREE.Mesh(new THREE.PlaneGeometry(0.62, 0.37), mat(color, { emissive: color, emissiveIntensity: 0.6 })), 0, 0.08, 0.032));
        g.add(put(box(0.08, 0.18, 0.06, 0x2a2f36), 0, -0.22, 0), put(box(0.32, 0.03, 0.18, 0x2a2f36), 0, -0.31, 0));
    } else if (/xr|vr|ar\b|metaverse/.test(text)) {
        g.add(new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.3, 0.3), mat(0xf2f2f2)));
        g.add(put(new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.2, 0.02), mat(0x15181d, { roughness: 0.2 })), 0, 0, 0.16));
        g.add(put(new THREE.Mesh(new THREE.TorusGeometry(0.3, 0.03, 6, 20), mat(color)), 0, 0, -0.1, Math.PI / 2, 0, 0));
    } else {
        // Games: a little trophy.
        const gold = mat(0xe3b341, { metalness: 0.7, roughness: 0.3, emissive: 0x6a4a10, emissiveIntensity: 0.3 });
        g.add(put(new THREE.Mesh(new THREE.CylinderGeometry(0.26, 0.12, 0.34, 16), gold), 0, 0.12, 0));
        g.add(put(new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 0.16, 8), gold), 0, -0.13, 0));
        g.add(put(new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.18, 0.06, 16), gold), 0, -0.23, 0));
        for (const s of [-1, 1]) g.add(put(new THREE.Mesh(new THREE.TorusGeometry(0.09, 0.02, 6, 12), gold), s * 0.27, 0.15, 0, 0, 0, 0));
    }
    g.traverse(o => { if (o.isMesh) o.castShadow = true; });
    return g;
}

export function buildShowcase(scene, { site, colliders, clear }) {
    const animated = [];
    const glows = [];
    const career = [], gallery = [];
    const companies = [...(site?.experience ?? [])].reverse();        // oldest first, up the hill
    const projects = topProjects(site?.projects);

    // ---------------- Career Trail ----------------
    const trail = PATHS[4];
    const total = lineLength(trail);
    if (companies.length) {
        // Trailhead sign.
        const head = along(trail, 2.5);
        const sg = new THREE.Group();
        sg.add(put(box(0.16, 2.2, 0.16, WOOD), 0, 1.1, 0));
        const tex = textTexture(['Career Trail', 'From my first studio to today', 'Follow the path up the hill ↑'], { width: 512, height: 256, bg: '#f6ecd6', fg: '#3a2a1c', accent: '#e8743b' });
        const board = new THREE.Mesh(new THREE.PlaneGeometry(1.8, 0.9), new THREE.MeshStandardMaterial({ map: tex, roughness: 0.9 }));
        sg.add(put(board, 0, 1.75, 0.09), put(box(1.9, 1.0, 0.1, WOOD), 0, 1.75, 0.02));
        const hx = head.x - head.dz * 2.6, hz = head.z + head.dx * 2.6;
        sg.position.set(hx, heightAt(hx, hz), hz);
        sg.rotation.y = Math.atan2(head.dz, -head.dx);
        scene.add(sg);
        colliders.push({ x: hx, z: hz, r: 0.3 });
        clear.push({ x: hx, z: hz, r: 1.8 });
    }
    companies.forEach((co, i) => {
        const dist = 7 + (i / Math.max(1, companies.length - 1)) * (total - 11);
        const p = along(trail, dist);
        const side = i % 2 ? 1 : -1;
        const x = p.x - p.dz * 3.1 * side, z = p.z + p.dx * 3.1 * side;
        const accent = ACCENTS[i % ACCENTS.length];
        const g = new THREE.Group();
        // Board facing the path.
        g.add(put(box(0.14, 2.3, 0.14, WOOD), -0.75, 1.15, 0), put(box(0.14, 2.3, 0.14, WOOD), 0.75, 1.15, 0));
        const latest = co.roles?.[0]?.role ?? '';
        const lines = [co.company, co.period, latest, co.roles.length > 1 ? `${co.roles.length} roles here` : ''];
        const tex = textTexture(lines, { width: 512, height: 300, bg: '#f6ecd6', fg: '#2a2219', accent: `#${accent.toString(16).padStart(6, '0')}` });
        const board = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 1.0), new THREE.MeshStandardMaterial({ map: tex, roughness: 0.9 }));
        g.add(put(board, 0, 1.65, 0.085), put(box(1.82, 1.1, 0.1, 0x6e4a30), 0, 1.65, 0.02));
        // Chapter number disc on top.
        const numTex = textTexture([String(i + 1)], { width: 128, height: 128, bg: `#${accent.toString(16).padStart(6, '0')}`, fg: '#ffffff', accent: `#${accent.toString(16).padStart(6, '0')}` });
        g.add(put(new THREE.Mesh(new THREE.CircleGeometry(0.22, 20), new THREE.MeshBasicMaterial({ map: numTex })), 0, 2.42, 0.07));
        g.add(put(cyl(0.24, 0.24, 0.06, 20, accent), 0, 2.42, 0.02, Math.PI / 2, 0, 0));
        // A cairn: one stone per role, so promotions stack up.
        co.roles.forEach((_, k) => {
            const s = 0.36 - k * 0.06;
            const st = new THREE.Mesh(new THREE.DodecahedronGeometry(s, 0), mat(k % 2 ? 0x8d877d : 0xa49d92));
            st.scale.y = 0.55;
            st.castShadow = true;
            g.add(put(st, 1.45, s * 0.5 + k * 0.32, 0.25, 0, k * 1.3, 0));
        });
        bakeStatic(g);
        // Pennant fluttering on a pole.
        const pole = cyl(0.03, 0.03, 3.0, 6, 0x3a3f44);
        g.add(put(pole, -1.25, 1.5, -0.1));
        const flag = new THREE.Mesh(new THREE.PlaneGeometry(0.7, 0.4, 6, 1), mat(accent, { side: THREE.DoubleSide, flatShading: false }));
        flag.geometry.translate(0.35, 0, 0);
        flag.position.set(-1.25, 2.75, -0.1);
        g.add(flag);
        const base = flag.geometry.attributes.position.array.slice();
        animated.push((dt, t, env) => {
            const a = flag.geometry.attributes.position;
            for (let v = 0; v < a.count; v++) {
                const px = base[v * 3];
                a.array[v * 3 + 2] = Math.sin(px * 6 - t * 5 + i) * 0.06 * px * (env.gust ?? 1);
            }
            a.needsUpdate = true;
        });
        const lamp = glow(0xffc46b, 2.2);
        lamp.position.set(0, 2.42, 0.4);
        lamp.material.opacity = 0;
        g.add(lamp);
        glows.push(lamp);

        const face = Math.atan2(p.x - x, p.z - z);   // board looks back at the path
        g.position.set(x, heightAt(x, z) - 0.05, z);
        g.rotation.y = face;
        scene.add(g);
        const r = (lx, lz) => ({ x: x + lx * Math.cos(face) + lz * Math.sin(face), z: z - lx * Math.sin(face) + lz * Math.cos(face) });
        colliders.push({ type: 'box', x, z, w: 1.9, d: 0.35, rot: face, h: 2.6 });
        const cairn = r(1.45, 0.25);
        colliders.push({ x: cairn.x, z: cairn.z, r: 0.4 });
        clear.push({ x, z, r: 2.6 });
        const front = r(0, 1.6);
        career.push({ index: i, company: co, x: front.x, z: front.z, facing: face + Math.PI, total: companies.length });
    });

    // ---------------- Project Gallery ----------------
    const G = PLACES.gallery;
    if (projects.length) {
        const plaza = new THREE.Group();
        const ground = new THREE.Mesh(new THREE.CircleGeometry(9.5, 40), mat(0xd9ccb0, { flatShading: false }));
        ground.rotation.x = -Math.PI / 2;
        ground.position.y = 0.04;
        ground.receiveShadow = true;
        plaza.add(ground);
        for (let i = 0; i < 40; i++) {      // a ring of paving stones
            const a = (i / 40) * Math.PI * 2;
            const st = new THREE.Mesh(new THREE.BoxGeometry(1.3, 0.08, 0.5), mat(i % 2 ? 0xbfb196 : 0xcfc2a6));
            plaza.add(put(st, Math.cos(a) * 9.6, 0.05, Math.sin(a) * 9.6, 0, -a + Math.PI / 2, 0));
        }
        // Entrance sign facing the path in from the west.
        const tex = textTexture(['Selected work', 'Five projects I’m proud of', 'Walk up to an exhibit to read more'], { width: 512, height: 256, bg: '#f6ecd6', fg: '#3a2a1c', accent: '#3d8fb8' });
        const sign = new THREE.Group();
        sign.add(put(box(0.16, 2.3, 0.16, WOOD), -1, 1.15, 0), put(box(0.16, 2.3, 0.16, WOOD), 1, 1.15, 0));
        sign.add(put(new THREE.Mesh(new THREE.PlaneGeometry(2.2, 1.1), new THREE.MeshStandardMaterial({ map: tex, roughness: 0.9 })), 0, 1.8, 0.09));
        sign.add(put(box(2.3, 1.2, 0.1, 0x6e4a30), 0, 1.8, 0.02));
        put(sign, -10.5, 0, 0, 0, -Math.PI / 2, 0);
        plaza.add(sign);
        bakeStatic(plaza);

        const arc = projects.length > 1 ? Math.PI * 0.95 : 0;
        projects.forEach((pr, i) => {
            const accent = ACCENTS[(i + 1) % ACCENTS.length];
            const a = -arc / 2 + (projects.length > 1 ? (i / (projects.length - 1)) * arc : 0);   // an arc on the east side, facing the entrance
            const ex = Math.cos(a) * 6.5, ez = Math.sin(a) * 6.5;
            const face = Math.atan2(-ex, -ez);
            const g = new THREE.Group();
            // Pedestal, easel and framed board.
            g.add(put(box(1.0, 0.95, 1.0, STONE), 0, 0.475, 0), put(box(1.15, 0.12, 1.15, 0xb8b0a2), 0, 1.0, 0));
            for (const s of [-1, 1]) g.add(put(box(0.09, 2.6, 0.09, WOOD), s * 0.95, 1.3, -1.0, -0.08, 0, s * -0.05));
            g.add(put(box(0.09, 2.4, 0.09, WOOD), 0, 1.2, -1.45, 0.35, 0, 0));
            const meta = [pr.title, pr.category, pr.metric || pr.timeline || '', pr.summary];
            const tex2 = textTexture(meta, { width: 640, height: 400, bg: '#fbf6ea', fg: '#22201c', accent: `#${accent.toString(16).padStart(6, '0')}` });
            const board = new THREE.Mesh(new THREE.PlaneGeometry(2.0, 1.25), new THREE.MeshStandardMaterial({ map: tex2, roughness: 0.85 }));
            g.add(put(board, 0, 2.0, -0.93, -0.08, 0, 0), put(box(2.14, 1.39, 0.08, 0x3a2a1c), 0, 2.0, -0.99, -0.08, 0, 0));
            bakeStatic(g);
            // Floating, slowly turning emblem above the pedestal, with a soft glow at night.
            const em = emblem(pr, accent);
            em.position.y = 1.65;
            g.add(em);
            const halo = glow(accent, 2.4);
            halo.position.y = 1.65;
            halo.material.opacity = 0;
            g.add(halo);
            glows.push(halo);
            animated.push((dt, t) => {
                em.rotation.y = t * 0.6 + i;
                em.position.y = 1.65 + Math.sin(t * 1.4 + i) * 0.08;
                em.children.forEach(c => {
                    if (c.userData.orbit === undefined) return;
                    const k = t * 1.5 + c.userData.orbit * 2.1;
                    c.position.set(Math.cos(k) * 0.55, Math.sin(k * 1.3) * 0.2, Math.sin(k) * 0.55);
                });
            });
            const wx = G.x + ex, wz = G.z + ez;
            g.position.set(wx, heightAt(wx, wz) - 0.02, wz);
            g.rotation.y = face;
            scene.add(g);
            colliders.push({ type: 'box', x: wx, z: wz, w: 1.15, d: 1.15, rot: face, h: 1.1 });
            const back = { x: wx - Math.sin(face) * 1.1, z: wz - Math.cos(face) * 1.1 };
            colliders.push({ type: 'box', x: back.x, z: back.z, w: 2.2, d: 0.4, rot: face, h: 2.7 });
            gallery.push({ index: i, project: pr, x: wx + Math.sin(face) * 1.5, z: wz + Math.cos(face) * 1.5, facing: face + Math.PI, total: projects.length });
        });
        plaza.position.set(G.x, heightAt(G.x, G.z), G.z);
        scene.add(plaza);
        colliders.push({ x: G.x - 10.5, z: G.z, r: 0.3 });
        clear.push({ x: G.x, z: G.z, r: 11 });
        // String lights around the plaza: little bulbs that glow after dusk.
        for (let i = 0; i < 24; i++) {
            const a = (i / 24) * Math.PI * 2;
            const bx = G.x + Math.cos(a) * 10.2, bz = G.z + Math.sin(a) * 10.2;
            if (Math.abs(Math.atan2(Math.sin(a - Math.PI), Math.cos(a - Math.PI))) < 0.35) continue;     // gap at the entrance
            const bulb = glow(0xffd28a, 1.1);
            bulb.position.set(bx, heightAt(bx, bz) + 2.6 + Math.sin(i * 1.7) * 0.15, bz);
            bulb.material.opacity = 0;
            scene.add(bulb);
            glows.push(bulb);
            if (i % 3 === 0) {
                const post = cyl(0.05, 0.06, 2.8, 6, 0x3a3f44);
                post.position.set(bx, heightAt(bx, bz) + 1.4, bz);
                scene.add(post);
            }
        }
    }

    function update(dt, t, env) {
        for (const a of animated) a(dt, t, env);
        for (const g of glows) g.material.opacity = Math.max(0, env.night - 0.1) * 0.95;
    }
    return { career, gallery, update, trailStart: along(trail, 1), plaza: G };
}
