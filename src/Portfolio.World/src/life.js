// Things that move on their own and make the island feel inhabited: gulls, butterflies by day, fireflies and
// shooting stars by night, jumping fish, chimney smoke, falling leaves, sky lanterns, and the cottage cat.
import * as THREE from 'three';
import { glow, mat } from './builders.js';
import { groundAt, heightAt, isMeadow, PLACES } from './terrain.js';
import { rng } from './noise.js';

const at = (mesh, x, y, z) => { mesh.position.set(x, y, z); return mesh; };

/** Sprite showing a glyph (♥, ✨, 💤), for little floating reactions. */
const glyphCache = new Map();
function glyphSprite(text, size = 0.6) {
    if (!glyphCache.has(text)) {
        const c = document.createElement('canvas');
        c.width = c.height = 64;
        const g = c.getContext('2d');
        g.font = '48px system-ui, "Segoe UI Emoji", "Apple Color Emoji", sans-serif';
        g.textAlign = 'center';
        g.textBaseline = 'middle';
        g.fillText(text, 32, 36);
        const t = new THREE.CanvasTexture(c);
        t.colorSpace = THREE.SRGBColorSpace;
        glyphCache.set(text, t);
    }
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glyphCache.get(text), transparent: true, depthWrite: false }));
    s.scale.setScalar(size);
    return s;
}

export function createLife(scene, { anchors, water, quality }) {
    const rand = rng(99);
    const updates = [];
    const floaters = [];

    /** A glyph that drifts up and fades. */
    function float(text, pos, { size = 0.6, rise = 0.8, life = 1.6 } = {}) {
        const s = glyphSprite(text, size);
        s.position.copy(pos);
        scene.add(s);
        floaters.push({ s, life, max: life, rise, drift: (Math.random() - 0.5) * 0.4 });
    }

    // ---- Splash rings (also used by stone skipping and fishing) ----
    const ringGeo = new THREE.RingGeometry(0.8, 1, 24);
    ringGeo.rotateX(-Math.PI / 2);
    const rings = [];
    function splash(x, z, size = 1, t = 0) {
        const m = new THREE.Mesh(ringGeo, new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.8, depthWrite: false }));
        m.position.set(x, water.heightAt(x, z, t) + 0.08, z);
        m.scale.setScalar(0.2 * size);
        scene.add(m);
        rings.push({ m, life: 1.4, size });
        // A few droplets.
        for (let i = 0; i < 6 * size; i++) drops.push({ p: new THREE.Vector3(x, m.position.y, z), v: new THREE.Vector3((Math.random() - 0.5) * 2, 2 + Math.random() * 2, (Math.random() - 0.5) * 2).multiplyScalar(size), life: 0.8 });
    }
    const drops = [];
    const dropGeo = new THREE.BufferGeometry();
    const dropPos = new Float32Array(120 * 3);
    dropGeo.setAttribute('position', new THREE.BufferAttribute(dropPos, 3));
    const dropPoints = new THREE.Points(dropGeo, new THREE.PointsMaterial({ color: 0xe8f6ff, size: 0.12, transparent: true, opacity: 0.9, depthWrite: false }));
    dropPoints.frustumCulled = false;
    scene.add(dropPoints);

    // ---- Gulls circling the coast ----
    const gullMat = mat(0xf4f6f8), gullTip = mat(0x3a3f44);
    const gulls = [];
    for (let i = 0; i < 9; i++) {
        const g = new THREE.Group();
        const body = new THREE.Mesh(new THREE.ConeGeometry(0.16, 0.9, 5), gullMat);
        body.rotation.x = Math.PI / 2;
        g.add(body);
        const wings = [-1, 1].map(s => {
            const pivot = new THREE.Group();
            const w = new THREE.Mesh(new THREE.BoxGeometry(0.9, 0.03, 0.32), gullMat);
            w.position.x = s * 0.45;
            const tip = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.031, 0.22), gullTip);
            tip.position.x = s * 0.95;
            pivot.add(w, tip);
            g.add(pivot);
            return { pivot, s };
        });
        const near = i < 3;
        g.userData = { wings, r: near ? 25 + rand() * 15 : 95 + rand() * 40, a: rand() * 6.28, y: near ? 12 + rand() * 6 : 18 + rand() * 14, speed: (0.05 + rand() * 0.05) * (rand() < 0.5 ? 1 : -1), phase: rand() * 10,
            cx: near ? PLACES.pier.x + PLACES.pier.dirX * 30 : 0, cz: near ? PLACES.pier.z + PLACES.pier.dirZ * 30 : 0 };
        g.scale.setScalar(1.3);
        scene.add(g);
        gulls.push(g);
    }
    updates.push((dt, t, env) => {
        for (const g of gulls) {
            const u = g.userData;
            u.a += u.speed * dt * (u.r < 60 ? 2.2 : 1);
            const x = u.cx + Math.cos(u.a) * u.r, z = u.cz + Math.sin(u.a) * u.r, y = u.y + Math.sin(t * 0.3 + u.phase) * 2;
            g.lookAt(x + -Math.sin(u.a) * Math.sign(u.speed), y, z + Math.cos(u.a) * Math.sign(u.speed));
            g.position.set(x, y, z);
            g.rotation.z += -Math.sign(u.speed) * 0.35;
            // Flap in bursts, then glide.
            const flapping = Math.sin(t * 0.5 + u.phase) > 0.2;
            for (const w of u.wings) w.pivot.rotation.z = w.s * (flapping ? Math.sin(t * 9 + u.phase) * 0.55 : 0.08);
            g.visible = env.night < 0.7;
        }
    });

    // ---- Butterflies over the meadows by day ----
    const flyColors = [0xffb347, 0xffffff, 0x8fc8ff, 0xffe066, 0xff8fb3];
    const flies = [];
    const wingGeo = new THREE.PlaneGeometry(0.22, 0.18);
    wingGeo.translate(0.11, 0, 0);
    for (let i = 0; i < (quality === 'low' ? 10 : 22); i++) {
        let x = 0, z = 0;
        for (let k = 0; k < 200; k++) { x = (rand() * 2 - 1) * 70; z = (rand() * 2 - 1) * 55; if (isMeadow(x, z)) break; }
        const g = new THREE.Group();
        const m = new THREE.MeshBasicMaterial({ color: flyColors[i % flyColors.length], side: THREE.DoubleSide });
        const wl = new THREE.Mesh(wingGeo, m), wr = new THREE.Mesh(wingGeo, m);
        wr.scale.x = -1;
        g.add(wl, wr);
        g.userData = { home: new THREE.Vector3(x, 0, z), wl, wr, seed: rand() * 100 };
        scene.add(g);
        flies.push(g);
    }
    updates.push((dt, t, env) => {
        const show = env.night < 0.4;
        for (const f of flies) {
            f.visible = show;
            if (!show) continue;
            const u = f.userData, s = u.seed;
            const x = u.home.x + Math.sin(t * 0.31 + s) * 4 + Math.sin(t * 0.83 + s * 2) * 1.5;
            const z = u.home.z + Math.cos(t * 0.27 + s) * 4 + Math.cos(t * 0.71 + s) * 1.5;
            const y = heightAt(x, z) + 0.7 + Math.sin(t * 1.7 + s) * 0.35;
            f.lookAt(x + Math.cos(t * 0.27 + s) * 0.31, y, z - Math.sin(t * 0.31 + s) * 0.27);
            f.position.set(x, y, z);
            const flap = Math.sin(t * 22 + s) * 1.1;
            u.wl.rotation.y = flap;
            u.wr.rotation.y = -flap;
        }
    });

    // ---- Fireflies at night ----
    const ffCount = quality === 'low' ? 120 : 260;
    const ffPos = new Float32Array(ffCount * 3), ffSeed = new Float32Array(ffCount);
    for (let i = 0; i < ffCount; i++) {
        let x = 0, z = 0;
        for (let k = 0; k < 200; k++) { x = (rand() * 2 - 1) * 85; z = (rand() * 2 - 1) * 65; if (isMeadow(x, z)) break; }
        ffPos.set([x, heightAt(x, z) + 0.5 + rand() * 1.8, z], i * 3);
        ffSeed[i] = rand() * 100;
    }
    const ffGeo = new THREE.BufferGeometry();
    ffGeo.setAttribute('position', new THREE.BufferAttribute(ffPos, 3));
    ffGeo.setAttribute('seed', new THREE.BufferAttribute(ffSeed, 1));
    const ffMat = new THREE.ShaderMaterial({
        uniforms: { uTime: { value: 0 }, uOpacity: { value: 0 }, uScale: { value: innerHeight / 2 } },
        vertexShader: `attribute float seed; uniform float uTime, uScale; varying float vBlink;
            void main(){
                vec3 p = position + vec3(sin(uTime * 0.4 + seed) * 1.2, sin(uTime * 0.7 + seed * 1.7) * 0.4, cos(uTime * 0.35 + seed) * 1.2);
                vec4 mv = modelViewMatrix * vec4(p, 1.0);
                gl_Position = projectionMatrix * mv;
                vBlink = smoothstep(0.3, 1.0, sin(uTime * (0.8 + fract(seed) * 1.2) + seed * 6.0));
                gl_PointSize = 0.35 * uScale / -mv.z;
            }`,
        fragmentShader: `uniform float uOpacity; varying float vBlink;
            void main(){ float d = length(gl_PointCoord - 0.5); float a = smoothstep(0.5, 0.0, d);
                gl_FragColor = vec4(vec3(0.85, 1.0, 0.45) * (0.6 + a), a * vBlink * uOpacity); }`,
        transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    });
    const fireflies = new THREE.Points(ffGeo, ffMat);
    fireflies.frustumCulled = false;
    scene.add(fireflies);
    updates.push((dt, t, env) => {
        ffMat.uniforms.uTime.value = t;
        ffMat.uniforms.uOpacity.value = Math.max(0, env.night - 0.3) * 1.4;
        ffMat.uniforms.uScale.value = innerHeight / 2;
        fireflies.visible = env.night > 0.3;
    });

    // ---- Fish that jump now and then off the pier and the coast ----
    const fishMesh = new THREE.Mesh(new THREE.IcosahedronGeometry(0.25, 0), mat(0xb8c7d6, { metalness: 0.6, roughness: 0.3 }));
    fishMesh.scale.set(0.5, 0.6, 1.4);
    fishMesh.visible = false;
    scene.add(fishMesh);
    let jump = null, nextJump = 4;
    updates.push((dt, t) => {
        nextJump -= dt;
        if (!jump && nextJump <= 0) {
            const p = anchors.pierEnd, a = rand() * Math.PI * 2, r = 6 + rand() * 18;
            const x = p.x + Math.cos(a) * r, z = p.z + Math.sin(a) * r;
            if (heightAt(x, z) < -2) {
                const dir = rand() * Math.PI * 2;
                jump = { x, z, dx: Math.cos(dir), dz: Math.sin(dir), k: 0 };
                splash(x, z, 0.6, t);
            }
            nextJump = 5 + rand() * 9;
        }
        if (jump) {
            jump.k += dt / 0.9;
            const s = jump.k * 2.2;
            fishMesh.visible = true;
            fishMesh.position.set(jump.x + jump.dx * s, Math.sin(Math.PI * jump.k) * 1.3, jump.z + jump.dz * s);
            fishMesh.rotation.set(-Math.cos(Math.PI * jump.k) * 0.9, Math.atan2(jump.dx, jump.dz), 0);
            if (jump.k >= 1) { splash(fishMesh.position.x, fishMesh.position.z, 0.7, t); fishMesh.visible = false; jump = null; }
        }
    });

    // ---- Chimney smoke ----
    const smokeMat = new THREE.MeshStandardMaterial({ color: 0xe8e4de, flatShading: true, transparent: true, opacity: 0.5, depthWrite: false });
    const puffs = [];
    for (let i = 0; i < 12; i++) {
        const m = new THREE.Mesh(new THREE.IcosahedronGeometry(0.4, 0), smokeMat.clone());
        m.userData.age = i / 12 * 6;
        scene.add(m);
        puffs.push(m);
    }
    updates.push((dt, t, env) => {
        for (const p of puffs) {
            p.userData.age = (p.userData.age + dt) % 6;
            const a = p.userData.age / 6;
            p.position.copy(anchors.chimney).add(new THREE.Vector3(a * 3.5 * (env.gust ?? 1) + Math.sin(t + a * 6) * 0.3, a * 7, a * 1.2));
            p.scale.setScalar(0.5 + a * 2.2);
            p.rotation.set(a * 2, a * 3, 0);
            p.material.opacity = Math.sin(Math.PI * a) * 0.45;
            p.material.color.setHex(env.night > 0.5 ? 0x7d8394 : 0xeeeae4);
        }
    });

    // ---- Leaves drifting down from the old tree ----
    const leafGeo = new THREE.PlaneGeometry(0.16, 0.1);
    const leafMat = new THREE.MeshStandardMaterial({ color: 0x8fbf4a, side: THREE.DoubleSide });
    const leaves = [];
    const sw = anchors.swing;
    const tree = { x: sw.pivot.x - Math.sin(sw.yaw) * 4.3, z: sw.pivot.z - Math.cos(sw.yaw) * 4.3 };
    for (let i = 0; i < 16; i++) {
        const l = new THREE.Mesh(leafGeo, i % 4 ? leafMat : mat(0xe0a33a, { side: THREE.DoubleSide }));
        l.userData = { seed: rand() * 100, y: rand() * 6 };
        scene.add(l);
        leaves.push(l);
    }
    updates.push((dt, t) => {
        for (const l of leaves) {
            const u = l.userData;
            u.y -= dt * 0.55;
            if (u.y < 0) u.y = 7 + rand();
            const r = 1.5 + (u.seed % 4);
            const x = tree.x + Math.cos(u.seed) * r + Math.sin(t * 0.8 + u.seed) * 0.6;
            const z = tree.z + Math.sin(u.seed) * r + Math.cos(t * 0.6 + u.seed) * 0.6;
            l.position.set(x, groundAt(x, z) + u.y, z);
            l.rotation.set(t * 1.3 + u.seed, t * 0.7, Math.sin(t * 2 + u.seed));
        }
    });

    // ---- Shooting stars ----
    const streakMat = new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending, fog: false });
    const streak = new THREE.Mesh(new THREE.CylinderGeometry(0.0, 0.6, 40, 4, 1, true), streakMat);
    streak.frustumCulled = false;
    scene.add(streak);
    let star = null, nextStar = 6;
    updates.push((dt, t, env, eye) => {
        if (env.night < 0.7) { streakMat.opacity = 0; star = null; return; }
        nextStar -= dt * (env.stargazing ? 2.2 : 1);
        if (!star && nextStar <= 0) {
            const a = rand() * Math.PI * 2;
            const from = new THREE.Vector3(Math.cos(a) * 300, 180 + rand() * 80, Math.sin(a) * 300);
            const dir = new THREE.Vector3(-Math.cos(a) + (rand() - 0.5), -0.35, -Math.sin(a) + (rand() - 0.5)).normalize();
            star = { from, dir, k: 0 };
            nextStar = 9 + rand() * 14;
        }
        if (star) {
            star.k += dt / 0.9;
            const p = star.from.clone().addScaledVector(star.dir, star.k * 160).add(eye);
            streak.position.copy(p);
            streak.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), star.dir.clone().negate());
            streakMat.opacity = Math.sin(Math.PI * Math.min(1, star.k)) * 0.9;
            if (star.k >= 1) { star = null; streakMat.opacity = 0; }
        }
    });

    // ---- Sky lanterns ----
    const lanterns = [];
    const paper = new THREE.MeshStandardMaterial({ color: 0xffd9a0, emissive: 0xff9a3c, emissiveIntensity: 1.6, flatShading: true, transparent: true, opacity: 0.95 });
    function releaseLantern(pos) {
        const g = new THREE.Group();
        const body = new THREE.Mesh(new THREE.CylinderGeometry(0.32, 0.22, 0.6, 6), paper.clone());
        g.add(body);
        const halo = glow(0xffa64a, 2.4);
        g.add(halo);
        g.position.copy(pos);
        scene.add(g);
        lanterns.push({ g, body, halo, age: 0, seed: Math.random() * 10 });
        if (lanterns.length > 40) { const old = lanterns.shift(); scene.remove(old.g); }
    }
    updates.push((dt, t) => {
        for (let i = lanterns.length - 1; i >= 0; i--) {
            const l = lanterns[i];
            l.age += dt;
            const lift = Math.min(1, l.age / 3);
            l.g.position.y += dt * (0.25 + lift * 0.9);
            l.g.position.x += dt * (0.6 + Math.sin(t * 0.3 + l.seed) * 0.4);
            l.g.position.z += dt * (0.3 + Math.cos(t * 0.25 + l.seed) * 0.3);
            l.g.rotation.y += dt * 0.2;
            const flicker = 0.85 + Math.sin(t * 9 + l.seed) * 0.1;
            const fade = Math.max(0, 1 - Math.max(0, l.age - 90) / 30);
            l.body.material.emissiveIntensity = 1.6 * flicker * fade;
            l.body.material.opacity = 0.95 * fade;
            l.halo.material.opacity = flicker * fade;
            if (fade <= 0) { scene.remove(l.g); lanterns.splice(i, 1); }
        }
    });

    // ---- The cat ----
    const cat = createCat();
    scene.add(cat.group);
    cat.group.position.set(anchors.porch.x, anchors.porch.y, anchors.porch.z);

    function update(dt, t, env, eye, player) {
        for (const u of updates) u(dt, t, env, eye);
        cat.update(dt, t, env, player);
        for (let i = rings.length - 1; i >= 0; i--) {
            const r = rings[i];
            r.life -= dt;
            const k = 1 - r.life / 1.4;
            r.m.scale.setScalar((0.3 + k * 2.6) * r.size);
            r.m.material.opacity = (1 - k) * 0.7;
            if (r.life <= 0) { scene.remove(r.m); r.m.material.dispose(); rings.splice(i, 1); }
        }
        let n = 0;
        for (let i = drops.length - 1; i >= 0; i--) {
            const d = drops[i];
            d.life -= dt;
            d.v.y -= 9.8 * dt;
            d.p.addScaledVector(d.v, dt);
            if (d.life <= 0) { drops.splice(i, 1); continue; }
            if (n < 120) dropPos.set([d.p.x, d.p.y, d.p.z], n++ * 3);
        }
        dropGeo.setDrawRange(0, n);
        dropGeo.attributes.position.needsUpdate = true;
        for (let i = floaters.length - 1; i >= 0; i--) {
            const f = floaters[i];
            f.life -= dt;
            f.s.position.y += f.rise * dt;
            f.s.position.x += f.drift * dt;
            f.s.material.opacity = Math.min(1, f.life / (f.max * 0.5));
            if (f.life <= 0) { scene.remove(f.s); f.s.material.dispose(); floaters.splice(i, 1); }
        }
    }

    return {
        update, splash, float, releaseLantern, cat,
        get shootingStar() { return star !== null && star.k < 0.9; },
        get lanternCount() { return lanterns.length; },
    };

    // ---------- cat ----------
    function createCat() {
        const g = new THREE.Group();
        const fur = mat(0xe08a3c), stripe = mat(0xb8662a), white = mat(0xfaf3e6), dark = mat(0x2a1f18);
        const body = new THREE.Group();
        body.position.y = 0.32;
        g.add(body);
        const torso = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.3, 0.7), fur);
        torso.castShadow = true;
        body.add(torso);
        for (let i = 0; i < 3; i++) body.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.35, 0.05, 0.08), stripe), 0, 0.13, -0.2 + i * 0.18));
        body.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.26, 0.12, 0.3), white), 0, -0.1, 0.18));
        const headG = new THREE.Group();
        headG.position.set(0, 0.2, 0.38);
        const headM = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.26, 0.26), fur);
        headM.castShadow = true;
        headG.add(headM);
        for (const s of [-1, 1]) {
            const ear = new THREE.Mesh(new THREE.ConeGeometry(0.07, 0.14, 4), fur);
            ear.position.set(s * 0.09, 0.17, -0.02);
            headG.add(ear);
            headG.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.05, 0.02), dark), s * 0.07, 0.03, 0.135));
        }
        headG.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.08, 0.06), white), 0, -0.06, 0.14));
        headG.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.03, 0.02), mat(0xe98a8a)), 0, -0.03, 0.175));
        body.add(headG);
        const legs = [];
        for (const [x, z] of [[-0.1, 0.25], [0.1, 0.25], [-0.1, -0.25], [0.1, -0.25]]) {
            const leg = new THREE.Group();
            leg.position.set(x, -0.12, z);
            leg.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.22, 0.08), fur), 0, -0.1, 0));
            leg.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.05, 0.1), white), 0, -0.2, 0.01));
            body.add(leg);
            legs.push(leg);
        }
        const tail = [];
        let parent = body;
        for (let i = 0; i < 4; i++) {
            const seg = new THREE.Group();
            seg.position.set(0, i === 0 ? 0.08 : 0.14, i === 0 ? -0.35 : 0);
            seg.add(at(new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.16, 0.07), i === 3 ? stripe : fur), 0, 0.07, 0));
            parent.add(seg);
            parent = seg;
            tail.push(seg);
        }
        g.scale.setScalar(1.25);

        const zzz = glyphSprite('💤', 0.5);
        zzz.position.set(0, 0.9, 0.3);
        g.add(zzz);

        const state = { mode: 'sleep', followUntil: 0, target: new THREE.Vector3(), wanderAt: 0, heading: 0, speed: 0, purr: 0, petCount: 0 };
        const home = new THREE.Vector3(anchors.porch.x, anchors.porch.y, anchors.porch.z);

        function update(dt, t, env, player) {
            const p = g.position;
            let goal = null, maxSpeed = 1.2;
            if (state.mode === 'follow' && t > state.followUntil) state.mode = 'wander';
            if (state.mode === 'follow' && player) {
                const d = p.distanceTo(player.position);
                if (d > 2.6) { goal = player.position; maxSpeed = d > 8 ? 5.5 : 3.2; }
            } else if (state.mode === 'wander') {
                if (t > state.wanderAt) {
                    state.wanderAt = t + 6 + Math.random() * 10;
                    if (Math.random() < 0.3) state.mode = 'sleep';
                    else state.target.set(home.x + (Math.random() - 0.5) * 16, 0, home.z + 2 + Math.random() * 10);
                }
                goal = state.target;
            } else if (state.mode === 'sleep') {
                goal = p.distanceTo(home) > 0.5 ? home : null;
                if (t > state.wanderAt && env.night < 0.6 && Math.random() < dt * 0.02) { state.mode = 'wander'; state.wanderAt = 0; }
            }
            if (goal) {
                const dx = goal.x - p.x, dz = goal.z - p.z, d = Math.hypot(dx, dz);
                if (d > 0.3) {
                    state.speed = Math.min(maxSpeed, state.speed + dt * 4, d * 1.5);
                    const want = Math.atan2(dx, dz);
                    let diff = want - state.heading;
                    diff = Math.atan2(Math.sin(diff), Math.cos(diff));
                    state.heading += diff * Math.min(1, dt * 6);
                    p.x += Math.sin(state.heading) * state.speed * dt;
                    p.z += Math.cos(state.heading) * state.speed * dt;
                } else state.speed = 0;
            } else state.speed = Math.max(0, state.speed - dt * 4);
            p.y = groundAt(p.x, p.z);
            g.rotation.y = state.heading;
            const sleeping = state.mode === 'sleep' && state.speed < 0.1;
            const sitting = !sleeping && state.speed < 0.1;
            // Pose: curled up asleep, sitting upright, or trotting.
            body.position.y = THREE.MathUtils.lerp(body.position.y, sleeping ? 0.16 : 0.32, dt * 4);
            body.rotation.x = THREE.MathUtils.lerp(body.rotation.x, sitting ? -0.45 : 0, dt * 5);
            headG.rotation.x = sleeping ? 0.5 : Math.sin(t * 0.7) * 0.1;
            headG.rotation.y = sleeping ? 0.6 : Math.sin(t * 0.4) * 0.4;
            const gait = state.speed > 0.1 ? t * (6 + state.speed * 3) : 0;
            legs.forEach((l, i) => { l.rotation.x = state.speed > 0.1 ? Math.sin(gait + (i % 3 ? Math.PI : 0)) * 0.6 : (sleeping ? 1.3 * (i < 2 ? -1 : 1) : 0); });
            tail.forEach((s, i) => {
                s.rotation.x = sleeping ? 0.5 : -0.5 + i * 0.25 + (sitting ? -0.3 : 0);
                s.rotation.z = Math.sin(t * (state.purr > 0 ? 4 : 1.6) + i * 0.6) * (sleeping ? 0.15 : 0.35);
            });
            zzz.visible = sleeping;
            zzz.position.y = 0.9 + Math.sin(t * 1.5) * 0.08;
            state.purr = Math.max(0, state.purr - dt);
        }

        return {
            group: g, state, update,
            pet(t) {
                state.petCount++;
                state.purr = 3;
                const following = state.mode === 'follow';
                state.mode = following ? 'wander' : 'follow';
                state.followUntil = t + 240;
                state.wanderAt = t + 4;
                float('♥', g.position.clone().add(new THREE.Vector3(0, 1, 0)), { size: 0.5 });
                setTimeout(() => float('♥', g.position.clone().add(new THREE.Vector3(0.2, 1.1, 0)), { size: 0.4 }), 300);
                return !following;
            },
            get following() { return state.mode === 'follow'; },
            get purring() { return state.purr > 0; },
        };
    }
}

