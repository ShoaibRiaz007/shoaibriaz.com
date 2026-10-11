// The sea: a sum of Gerstner waves computed on the GPU with analytic normals, on a radial grid that is dense
// under the camera and sparse toward the horizon (it follows the camera, so detail is always where you look).
// Waves calm down in the shallows (a baked depth map), short waves fade with distance so they never alias,
// and the surface picks up the sky at grazing angles (fresnel), a turquoise glow through the crests, and foam
// on the crests and along the shoreline, broken up with noise.
import * as THREE from 'three';
import { heightAt as terrainHeight } from './terrain.js';

// direction (x, z), steepness, wavelength (m). Steepness sums well under 1, so crests never loop.
const WAVES = [
    [1, 0.35, 0.07, 38], [0.75, 0.95, 0.07, 21], [-0.35, 1, 0.07, 11], [0.95, -0.45, 0.06, 6.5], [0.2, -1, 0.05, 3.8],
];
const SPEED = 0.6;

function radialGrid(quality) {
    const growth = quality === 'low' ? 1.065 : 1.04, segs = quality === 'low' ? 96 : 160, max = 700;
    const radii = [0];
    for (let r = 0.6; r < max; r *= growth) radii.push(r);
    radii.push(max);
    const pos = [], idx = [];
    pos.push(0, 0, 0);
    for (let i = 1; i < radii.length; i++) for (let j = 0; j < segs; j++) {
        const a = (j / segs) * Math.PI * 2;
        pos.push(Math.cos(a) * radii[i], 0, Math.sin(a) * radii[i]);
    }
    const ring = i => 1 + (i - 1) * segs;
    for (let j = 0; j < segs; j++) idx.push(0, ring(1) + ((j + 1) % segs), ring(1) + j);
    for (let i = 1; i < radii.length - 1; i++) for (let j = 0; j < segs; j++) {
        const a = ring(i) + j, b = ring(i) + ((j + 1) % segs), c = ring(i + 1) + j, d = ring(i + 1) + ((j + 1) % segs);
        idx.push(a, b, c, b, d, c);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(new Float32Array(pos.length).map((_, i) => (i % 3 === 1 ? 1 : 0)), 3));
    g.setIndex(idx);
    g.boundingSphere = new THREE.Sphere(new THREE.Vector3(), max);
    return g;
}

export function createWater(scene, depth, { quality, reducedMotion }) {
    const uniforms = {
        uTime: { value: 0 },
        uDepth: { value: depth.texture },
        uExtent: { value: depth.extent },
        uAmp: { value: reducedMotion ? 0.4 : 1 },
        uNight: { value: 0 },
        uEye: { value: new THREE.Vector3() },
        uSky: { value: new THREE.Color(0x9fc3e0) },
        uWaves: { value: WAVES.map(([x, z, s, l]) => new THREE.Vector4(x, z, s, l)) },
    };
    const material = new THREE.MeshStandardMaterial({ color: 0x1f6f8f, roughness: 0.14, metalness: 0, transparent: true });
    material.onBeforeCompile = shader => {
        Object.assign(shader.uniforms, uniforms);
        shader.vertexShader = shader.vertexShader
            .replace('#include <common>', `#include <common>
                uniform float uTime, uExtent, uAmp; uniform sampler2D uDepth; uniform vec3 uEye; uniform vec4 uWaves[5];
                varying vec3 vWorld; varying float vCrest; varying float vDepth;
                float depthAt(vec2 p) {
                    vec2 uv = p / uExtent + 0.5;
                    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return 1.0;
                    return texture2D(uDepth, uv).r;
                }`)
            .replace('#include <beginnormal_vertex>', `
                vec3 w0 = (modelMatrix * vec4(position, 1.0)).xyz;
                float dep = depthAt(w0.xz);
                float damp = smoothstep(0.0, 0.3, dep) * uAmp;              // calm water in the shallows
                float far = distance(w0.xz, uEye.xz);
                vec3 disp = vec3(0.0), T = vec3(1.0, 0.0, 0.0), B = vec3(0.0, 0.0, 1.0);
                for (int i = 0; i < 5; i++) {
                    vec4 w = uWaves[i];
                    float fade = i >= 3 ? 1.0 - smoothstep(45.0, 130.0, far) : 1.0;   // short waves only up close
                    float k = 6.2831853 / w.w, c = sqrt(9.8 / k);
                    vec2 d = normalize(w.xy);
                    float f = k * (dot(d, w0.xz) - c * uTime * ${SPEED.toFixed(2)});
                    float st = w.z * damp * fade, a = st / k, sf = sin(f), cf = cos(f);
                    disp += vec3(d.x * a * cf, a * sf, d.y * a * cf);
                    T += vec3(-d.x * d.x * st * sf, d.x * st * cf, -d.x * d.y * st * sf);
                    B += vec3(-d.x * d.y * st * sf, d.y * st * cf, -d.y * d.y * st * sf);
                }
                vec3 objectNormal = normalize(cross(B, T));
                vCrest = disp.y; vDepth = dep; vWorld = w0 + disp;`)
            .replace('#include <begin_vertex>', 'vec3 transformed = position + disp;');
        shader.fragmentShader = shader.fragmentShader
            .replace('#include <common>', `#include <common>
                uniform float uTime, uNight; uniform vec3 uSky;
                varying vec3 vWorld; varying float vCrest; varying float vDepth;
                float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
                float vnoise(vec2 p) {
                    vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
                    return mix(mix(hash2(i), hash2(i + vec2(1, 0)), f.x), mix(hash2(i + vec2(0, 1)), hash2(i + vec2(1, 1)), f.x), f.y);
                }`)
            .replace('#include <color_fragment>', `#include <color_fragment>
                float d = vDepth;
                vec3 water = mix(vec3(0.22, 0.76, 0.72), vec3(0.06, 0.44, 0.58), smoothstep(0.0, 0.2, d));
                water = mix(water, vec3(0.03, 0.17, 0.31), smoothstep(0.15, 0.85, d));
                water += vec3(0.04, 0.2, 0.18) * smoothstep(0.08, 0.45, vCrest) * (1.0 - uNight * 0.7);   // light through the crests
                water *= 1.0 - uNight * 0.35;
                float n = vnoise(vWorld.xz * 0.35 + uTime * 0.12) * 0.6 + vnoise(vWorld.xz * 1.4 - uTime * 0.25) * 0.4;
                float shore = 1.0 - smoothstep(0.0, 0.03, d + (n - 0.5) * 0.025);
                float bands = smoothstep(0.62, 1.0, sin(d * 150.0 - uTime * 1.3 + n * 3.0)) * (1.0 - smoothstep(0.012, 0.075, d));
                float crest = smoothstep(0.42, 0.7, vCrest + (n - 0.5) * 0.3) * smoothstep(0.1, 0.3, d);
                float foam = clamp(shore + bands * 0.75 + crest * 0.5, 0.0, 1.0);
                diffuseColor.rgb = mix(water, vec3(0.94, 0.97, 1.0), foam * (0.7 + n * 0.3));
                diffuseColor.a = max(mix(0.5, 0.94, smoothstep(0.0, 0.22, d)), foam);`)
            .replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
                float fres = pow(1.0 - clamp(dot(normal, normalize(vViewPosition)), 0.0, 1.0), 4.0);
                totalEmissiveRadiance += uSky * fres * 0.5 * (1.0 - foam * 0.8);`);
    };
    const mesh = new THREE.Mesh(radialGrid(quality), material);
    mesh.receiveShadow = true;
    mesh.renderOrder = 1;
    mesh.frustumCulled = false;
    scene.add(mesh);

    const k = WAVES.map(w => 2 * Math.PI / w[3]);
    return {
        mesh,
        update(t, eye, night, sky) {
            uniforms.uTime.value = t;
            uniforms.uNight.value = night;
            uniforms.uEye.value.copy(eye);
            if (sky) uniforms.uSky.value.copy(sky);
            mesh.position.set(eye.x, 0, eye.z);
        },
        /** Surface height at (x, z): the same waves as the shader (vertical part), for boats, bobbers, splashes. */
        heightAt(x, z, t) {
            const dep = Math.min(1, Math.max(0, -terrainHeight(x, z) / 10));
            const s0 = Math.min(1, dep / 0.3), damp = s0 * s0 * (3 - 2 * s0) * uniforms.uAmp.value;
            let y = 0;
            WAVES.forEach(([dx, dz, st], i) => {
                const len = Math.hypot(dx, dz), c = Math.sqrt(9.8 / k[i]);
                const f = k[i] * ((dx * x + dz * z) / len - c * t * SPEED);
                y += (st * damp / k[i]) * Math.sin(f);
            });
            return y;
        },
    };
}
