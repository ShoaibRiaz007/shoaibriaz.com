// Time of day: a shader sky dome with sun and moon, stars, drifting clouds, and the lights and fog that follow
// them. One `update(hours)` drives the whole mood, from dawn through golden hour to a moonlit night.
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { rng } from './noise.js';

// Keyframes around the clock. Interpolated cyclically, so any hour is valid.
const KEYS = [
    { h: 0, zenith: 0x050b1d, horizon: 0x18284a, sun: 0x000000, sunI: 0, hemiSky: 0x6680c4, hemiGround: 0x243040, hemiI: 0.75, night: 1, exposure: 1.25 },
    { h: 4.6, zenith: 0x0a1430, horizon: 0x2c3360, sun: 0x000000, sunI: 0, hemiSky: 0x6680c4, hemiGround: 0x243040, hemiI: 0.75, night: 1, exposure: 1.25 },
    { h: 5.8, zenith: 0x2d3f78, horizon: 0xf09a7a, sun: 0xff9a66, sunI: 0.6, hemiSky: 0x9aa8d8, hemiGround: 0x4a4a48, hemiI: 0.8, night: 0.35, exposure: 1.15 },
    { h: 7.5, zenith: 0x4d86c6, horizon: 0xf8d2a6, sun: 0xffd2a0, sunI: 2.2, hemiSky: 0xbcd8f2, hemiGround: 0x5d6b4a, hemiI: 1.1, night: 0, exposure: 1.05 },
    { h: 12, zenith: 0x3d8ad8, horizon: 0xc4e4f6, sun: 0xfff4e4, sunI: 3.0, hemiSky: 0xcfe8ff, hemiGround: 0x5f7a4c, hemiI: 1.25, night: 0, exposure: 1.0 },
    { h: 16, zenith: 0x3b7cc8, horizon: 0xd6e2e6, sun: 0xffe6c4, sunI: 2.8, hemiSky: 0xcfe2f6, hemiGround: 0x627a4a, hemiI: 1.2, night: 0, exposure: 1.0 },
    { h: 17.8, zenith: 0x3a62a2, horizon: 0xffc58c, sun: 0xffb26b, sunI: 2.6, hemiSky: 0xb6c6e6, hemiGround: 0x6b5a44, hemiI: 1.05, night: 0, exposure: 1.08 },
    { h: 18.9, zenith: 0x27386e, horizon: 0xff8a62, sun: 0xff7a4a, sunI: 1.3, hemiSky: 0x9a8fc6, hemiGround: 0x4a3c3c, hemiI: 0.85, night: 0.2, exposure: 1.15 },
    { h: 19.8, zenith: 0x111a3c, horizon: 0x5a3e66, sun: 0xff6a4a, sunI: 0.15, hemiSky: 0x7880bc, hemiGround: 0x2c2e40, hemiI: 0.75, night: 0.75, exposure: 1.22 },
    { h: 21, zenith: 0x060c20, horizon: 0x1c2a4e, sun: 0x000000, sunI: 0, hemiSky: 0x6680c4, hemiGround: 0x243040, hemiI: 0.75, night: 1, exposure: 1.25 },
];
const colorKeys = ['zenith', 'horizon', 'sun', 'hemiSky', 'hemiGround'];
const numKeys = ['sunI', 'hemiI', 'night', 'exposure'];

function sample(hours) {
    const h = ((hours % 24) + 24) % 24;
    let i = KEYS.findIndex(k => k.h > h);
    if (i === -1) i = KEYS.length;
    const a = KEYS[(i - 1 + KEYS.length) % KEYS.length], b = KEYS[i % KEYS.length];
    const span = ((b.h - a.h) + 24) % 24 || 24;
    const t = (((h - a.h) + 24) % 24) / span;
    const k = t * t * (3 - 2 * t);
    const out = {};
    for (const key of colorKeys) out[key] = new THREE.Color(a[key]).lerp(new THREE.Color(b[key]), k);
    for (const key of numKeys) out[key] = a[key] + (b[key] - a[key]) * k;
    return out;
}

/** Sun direction for an hour: rises in the east (+x) at ~6:00, arcs over the south, sets in the west ~19:20. */
export function sunDirection(hours, out = new THREE.Vector3()) {
    const t = (hours - 6) / 13.3;                       // 0 at sunrise, 1 at sunset
    const az = Math.PI * t;                             // east → south → west
    const el = Math.sin(Math.PI * t) * THREE.MathUtils.degToRad(62) - 0.05;
    return out.set(Math.cos(az) * Math.cos(el), Math.sin(el), Math.sin(az) * Math.cos(el) * 0.75 + 0.25).normalize();
}

const skyVert = /* glsl */`
varying vec3 vDir;
void main() {
    vDir = normalize(position);
    vec4 p = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    gl_Position = p.xyww;   // pinned to the far plane
}`;
const skyFrag = /* glsl */`
uniform vec3 uZenith, uHorizon, uSunColor, uSunDir, uMoonDir;
uniform float uNight;
varying vec3 vDir;
void main() {
    vec3 d = normalize(vDir);
    float h = d.y;
    // Horizon band is tight, the upper sky a long gentle gradient.
    vec3 col = mix(uHorizon, uZenith, pow(smoothstep(-0.02, 0.85, h), 0.55));
    // Warm scatter around the sun, strongest near the horizon.
    float s = max(dot(d, uSunDir), 0.0);
    col += uSunColor * (pow(s, 6.0) * 0.35 + pow(s, 48.0) * 0.5) * (1.0 - smoothstep(0.0, 0.6, h) * 0.6);
    col += uSunColor * smoothstep(0.9993, 0.9997, s) * 2.0;            // sun disc
    float m = max(dot(d, uMoonDir), 0.0);
    col += vec3(0.85, 0.9, 1.0) * smoothstep(0.9990, 0.9994, m) * uNight * 1.4;   // moon disc
    col += vec3(0.4, 0.5, 0.8) * pow(m, 30.0) * 0.12 * uNight;                 // moon halo
    // Below the horizon the sky fades into the sea haze.
    col = mix(col, uHorizon * 0.85, smoothstep(0.0, -0.15, h));
    gl_FragColor = vec4(col, 1.0);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
}`;

export function createSky(scene, quality) {
    const uniforms = {
        uZenith: { value: new THREE.Color() }, uHorizon: { value: new THREE.Color() }, uSunColor: { value: new THREE.Color() },
        uSunDir: { value: new THREE.Vector3(0, 1, 0) }, uMoonDir: { value: new THREE.Vector3(0, 1, 0) }, uNight: { value: 0 },
    };
    const dome = new THREE.Mesh(new THREE.SphereGeometry(500, 32, 20),
        new THREE.ShaderMaterial({ uniforms, vertexShader: skyVert, fragmentShader: skyFrag, side: THREE.BackSide, depthWrite: false, fog: false }));
    dome.renderOrder = -10;
    dome.frustumCulled = false;
    scene.add(dome);

    // Stars: twinkle comes from size jitter per star, faded in at night.
    const rand = rng(7);
    const starCount = quality === 'low' ? 700 : 1500;
    const pos = new Float32Array(starCount * 3), sizes = new Float32Array(starCount);
    for (let i = 0; i < starCount; i++) {
        const a = rand() * Math.PI * 2, y = 0.08 + rand() * 0.92, r = Math.sqrt(1 - y * y);
        pos.set([Math.cos(a) * r * 450, y * 450, Math.sin(a) * r * 450], i * 3);
        sizes[i] = 0.6 + rand() * rand() * 2.6;
    }
    const starGeo = new THREE.BufferGeometry();
    starGeo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    starGeo.setAttribute('size', new THREE.BufferAttribute(sizes, 1));
    const starMat = new THREE.ShaderMaterial({
        uniforms: { uOpacity: { value: 0 }, uTime: { value: 0 } },
        vertexShader: `attribute float size; uniform float uTime; varying float vTw;
            void main(){ vec4 mv = modelViewMatrix * vec4(position,1.0); gl_Position = projectionMatrix * mv;
            vTw = 0.65 + 0.35 * sin(uTime * (1.0 + fract(position.x) * 3.0) + position.z);
            gl_PointSize = size * (0.8 + vTw * 0.4); }`,
        fragmentShader: `uniform float uOpacity; varying float vTw;
            void main(){ float d = length(gl_PointCoord - 0.5); if (d > 0.5) discard;
            gl_FragColor = vec4(vec3(1.0, 0.97, 0.9), (1.0 - d * 2.0) * uOpacity * vTw); }`,
        transparent: true, depthWrite: false, fog: false, blending: THREE.AdditiveBlending,
    });
    const stars = new THREE.Points(starGeo, starMat);
    stars.frustumCulled = false;
    scene.add(stars);

    // Clouds: soft low-poly puffs; their colour follows the sky so they blush at sunset and go slate at night.
    const cloudMat = new THREE.MeshStandardMaterial({ color: 0xffffff, flatShading: true, roughness: 1, emissive: 0xffffff, emissiveIntensity: 0.25, fog: false });
    const clouds = [];
    const puff = new THREE.IcosahedronGeometry(1, 1);
    for (let i = 0; i < 14; i++) {
        const n = 4 + Math.floor(rand() * 4), parts = [];
        for (let j = 0; j < n; j++) {
            const s = 5 + rand() * 6 * (1 - Math.abs(j - n / 2) / n);
            parts.push(puff.clone().scale(s * 1.3, s * 0.7, s).translate((j - n / 2) * 6.5 + rand() * 2, rand() * 2.5, (rand() - 0.5) * 6));
        }
        const c = new THREE.Mesh(mergeGeometries(parts), cloudMat);     // one draw call per cloud
        const r = 140 + rand() * 170, a = rand() * Math.PI * 2;
        c.userData = { r, a, y: 70 + rand() * 45, speed: 0.004 + rand() * 0.006 };
        c.rotation.y = rand() * Math.PI;
        scene.add(c);
        clouds.push(c);
    }

    const hemi = new THREE.HemisphereLight(0xffffff, 0x444444, 1);
    const sun = new THREE.DirectionalLight(0xffffff, 2);
    sun.shadow.bias = -0.0004;
    sun.shadow.normalBias = 0.04;
    const target = new THREE.Object3D();
    sun.target = target;
    scene.add(hemi, sun, target);
    scene.fog = new THREE.Fog(0x9fc3e0, 120, 330);

    const sunDir = new THREE.Vector3(), moonDir = new THREE.Vector3();
    const state = { hours: 17.5, night: 0, sunDir, moonDir, sunVisible: true, exposure: 1, reflect: new THREE.Color() };

    /** Re-lights the world for an hour of the day. `focus` is where shadows should be sharpest (the player). */
    function update(hours, focus, dt, t, eye = focus) {
        const k = sample(hours);
        sunDirection(hours, sunDir);
        moonDir.copy(sunDir).negate();
        moonDir.y = Math.abs(moonDir.y) * 0.8 + 0.25;
        moonDir.normalize();
        uniforms.uZenith.value.copy(k.zenith);
        uniforms.uHorizon.value.copy(k.horizon);
        uniforms.uSunColor.value.copy(k.sun);
        uniforms.uSunDir.value.copy(sunDir);
        uniforms.uMoonDir.value.copy(moonDir);
        uniforms.uNight.value = k.night;

        hemi.color.copy(k.hemiSky);
        hemi.groundColor.copy(k.hemiGround);
        hemi.intensity = k.hemiI;
        // One directional light: the sun by day, the moon by night, crossfading through twilight.
        const sunUp = THREE.MathUtils.smoothstep(sunDir.y, -0.02, 0.08);
        const dir = sunUp > 0.02 ? sunDir : moonDir;
        if (sunUp > 0.02) { sun.color.copy(k.sun); sun.intensity = k.sunI * sunUp; }
        else { sun.color.setHex(0xa8bcff); sun.intensity = 0.85 * k.night; }
        sun.position.copy(focus).addScaledVector(dir, 120);
        target.position.copy(focus);

        scene.fog.color.copy(k.horizon).lerp(k.zenith, 0.25);
        starMat.uniforms.uOpacity.value = Math.max(0, k.night - 0.15);
        starMat.uniforms.uTime.value = t;
        cloudMat.color.copy(k.horizon).lerp(new THREE.Color(0xffffff), 0.55 - k.night * 0.35);
        cloudMat.emissive.copy(k.horizon).lerp(k.zenith, k.night * 0.6);
        cloudMat.emissiveIntensity = 0.35 - k.night * 0.2;
        for (const c of clouds) {
            const u = c.userData;
            u.a += u.speed * dt * 0.1;
            c.position.set(Math.cos(u.a) * u.r + focus.x * 0.6, u.y, Math.sin(u.a) * u.r + focus.z * 0.6);
        }
        dome.position.copy(eye);
        stars.position.copy(eye);
        state.hours = hours;
        state.night = k.night;
        state.exposure = k.exposure;
        state.reflect.copy(k.horizon).lerp(k.zenith, 0.35);          // what the sea mirrors at grazing angles
        state.sunVisible = sunUp > 0.02;
        return state;
    }

    return { update, sun, hemi, state };
}
