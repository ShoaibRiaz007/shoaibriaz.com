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
    g.fillRect(0, 0, width, Math.max(4, Math.round(height * 0.03)));

    const activeLines = lines.filter(Boolean);
    if (height <= 96 && activeLines.length <= 1) {
        const size = Math.round(height * 0.45);
        g.font = `800 ${size}px system-ui, -apple-system, "Segoe UI", sans-serif`;
        g.textAlign = 'center';
        g.textBaseline = 'middle';
        g.fillStyle = fg;
        g.fillText(String(activeLines[0] || ''), width / 2, height / 2 + 2);
        const tex = new THREE.CanvasTexture(canvas);
        tex.colorSpace = THREE.SRGBColorSpace;
        tex.anisotropy = 4;
        return tex;
    }

    const pad = Math.min(28, Math.round(width * 0.06));
    let y = pad + 18;
    activeLines.forEach((line, i) => {
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

/** Directional wooden fingerboard texture with carved/painted text, icon, and directional arrow. */
export function signboardTexture({ label, icon = '', arrow = 'right', width = 512, height = 128, bg = '#fbf3de', fg = '#261609', accent = '#ea580c' } = {}) {
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const g = canvas.getContext('2d');

    // Sunlit cream painted wood plank
    g.fillStyle = bg;
    g.fillRect(0, 0, width, height);

    // Weathered board border bevel
    g.strokeStyle = 'rgba(255, 255, 255, 0.9)';
    g.lineWidth = 4;
    g.strokeRect(2, 2, width - 4, height - 4);

    g.strokeStyle = 'rgba(80, 50, 25, 0.35)';
    g.lineWidth = 4;
    g.strokeRect(6, 6, width - 12, height - 12);

    // Subtle wood grain lines
    g.strokeStyle = 'rgba(160, 120, 80, 0.12)';
    g.lineWidth = 2;
    for (let y = 20; y < height; y += 22) {
        g.beginPath();
        g.moveTo(10, y);
        g.lineTo(width - 10, y);
        g.stroke();
    }

    g.textBaseline = 'middle';
    const text = label.toUpperCase();
    const arrowSymbol = arrow === 'left' ? '◀' : '▶';

    g.font = '900 36px "Nunito", system-ui, -apple-system, sans-serif';

    // Highlight drop shadow
    g.fillStyle = 'rgba(255, 255, 255, 0.7)';
    if (arrow === 'left') {
        g.textAlign = 'left';
        g.fillText(`${arrowSymbol}  ${text}`, 24, height / 2 + 2);
        if (icon) {
            g.textAlign = 'right';
            g.fillText(icon, width - 24, height / 2 + 2);
        }
    } else {
        g.textAlign = 'left';
        const prefix = icon ? `${icon}  ` : '';
        g.fillText(prefix + text, 24, height / 2 + 2);
        g.textAlign = 'right';
        g.fillText(arrowSymbol, width - 24, height / 2 + 2);
    }

    // Crisp foreground text & colored arrow
    if (arrow === 'left') {
        g.textAlign = 'left';
        g.fillStyle = accent;
        g.fillText(arrowSymbol, 24, height / 2);
        const arrowWidth = g.measureText(arrowSymbol + '  ').width;
        g.fillStyle = fg;
        g.fillText(text, 24 + arrowWidth, height / 2);
        if (icon) {
            g.textAlign = 'right';
            g.fillText(icon, width - 24, height / 2);
        }
    } else {
        g.textAlign = 'left';
        let xOffset = 24;
        if (icon) {
            g.fillStyle = fg;
            g.fillText(icon, xOffset, height / 2);
            xOffset += g.measureText(icon + '  ').width;
        }
        g.fillStyle = fg;
        g.fillText(text, xOffset, height / 2);
        g.textAlign = 'right';
        g.fillStyle = accent;
        g.fillText(arrowSymbol, width - 24, height / 2);
    }

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

