// Moving around: keyboard, touch joystick, drag-to-look and tap-to-walk input; the third-person orbit camera;
// walking and running on the terrain (wading into shallows, blocked by deep water).
import * as THREE from 'three';
import { groundAt, heightAt, surfaceKind } from './terrain.js';
import { animateCharacter, AVATAR_SCALE, createCharacter, setPose, SIT_DROP } from './character.js';
import { ROOM } from './cottage.js';

const WALK = 2.3, RUN = 5.6, RADIUS = 0.34, WADE_LIMIT = -0.8;
const ACCEL = 9, DECEL = 12, TURN_WALK = 9, TURN_RUN = 5.5;      // m/s², rad/s

export const PlayerState = {
    IDLE: 'idle',
    WALK: 'walk',
    RUN: 'run',
    BUSY: 'busy',
};

// ---------------- Input ----------------
export function createInput(canvas, joystickEl) {
    const keys = new Set();
    const listeners = { action: [], tap: [] };
    const state = { look: { dx: 0, dy: 0 }, zoom: 0, dragging: false, lastLook: -10, joy: null };

    const typing = e => e.target instanceof HTMLElement && e.target.closest('input, textarea, select, [contenteditable]');
    addEventListener('keydown', e => {
        if (typing(e) || e.ctrlKey || e.metaKey || e.altKey) return;
        const k = e.key.toLowerCase();
        if (['arrowup', 'arrowdown', 'arrowleft', 'arrowright', ' '].includes(k) && !e.target.closest?.('button, a, input')) e.preventDefault();
        if (!e.repeat) listeners.action.forEach(f => f(k, e));
        keys.add(k);
    });
    addEventListener('keyup', e => keys.delete(e.key.toLowerCase()));
    addEventListener('blur', () => keys.clear());

    // Drag to look; a short press without movement is a tap (walk there / use what's there).
    const pointers = new Map();
    let pinch = 0;
    canvas.addEventListener('pointerdown', e => {
        canvas.setPointerCapture(e.pointerId);
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY, sx: e.clientX, sy: e.clientY, t: performance.now() });
        if (pointers.size === 2) { const [a, b] = [...pointers.values()]; pinch = Math.hypot(a.x - b.x, a.y - b.y); }
    });
    canvas.addEventListener('pointermove', e => {
        const p = pointers.get(e.pointerId);
        if (!p) return;
        if (pointers.size === 2) {
            p.x = e.clientX; p.y = e.clientY;
            const [a, b] = [...pointers.values()];
            const d = Math.hypot(a.x - b.x, a.y - b.y);
            state.zoom += (pinch - d) * 0.04;
            pinch = d;
            return;
        }
        state.look.dx += e.clientX - p.x;
        state.look.dy += e.clientY - p.y;
        p.x = e.clientX; p.y = e.clientY;
        if (Math.hypot(p.x - p.sx, p.y - p.sy) > 6) { state.dragging = true; state.lastLook = performance.now(); }
    });
    const up = e => {
        const p = pointers.get(e.pointerId);
        pointers.delete(e.pointerId);
        if (p && pointers.size === 0 && Math.hypot(e.clientX - p.sx, e.clientY - p.sy) < 7 && performance.now() - p.t < 400) {
            listeners.tap.forEach(f => f(e.clientX, e.clientY));
        }
        if (pointers.size === 0) state.dragging = false;
    };
    canvas.addEventListener('pointerup', up);
    canvas.addEventListener('pointercancel', up);
    canvas.addEventListener('wheel', e => { e.preventDefault(); state.zoom += Math.sign(e.deltaY) * 1.6; }, { passive: false });
    canvas.addEventListener('contextmenu', e => e.preventDefault());

    // Touch joystick: drag inside the circle.
    const stick = joystickEl.querySelector('.stick');
    joystickEl.addEventListener('pointerdown', e => { state.joy = { id: e.pointerId, x: e.clientX, y: e.clientY, dx: 0, dy: 0 }; joystickEl.setPointerCapture(e.pointerId); });
    joystickEl.addEventListener('pointermove', e => {
        const j = state.joy;
        if (!j || e.pointerId !== j.id) return;
        const r = 48;
        let dx = e.clientX - j.x, dy = e.clientY - j.y;
        const len = Math.hypot(dx, dy);
        if (len > r) { dx *= r / len; dy *= r / len; }
        j.dx = dx / r; j.dy = dy / r;
        stick.style.transform = `translate(${dx}px, ${dy}px)`;
    });
    const endJoy = () => { state.joy = null; stick.style.transform = ''; };
    joystickEl.addEventListener('pointerup', endJoy);
    joystickEl.addEventListener('pointercancel', endJoy);

    return {
        state,
        on(kind, f) { listeners[kind].push(f); },
        /** Movement axes: x = right, y = forward, both -1..1; run when Shift is held or the stick is pushed far. */
        axes() {
            const has = (...k) => k.some(x => keys.has(x));
            let x = (has('d', 'arrowright') ? 1 : 0) - (has('a', 'arrowleft') ? 1 : 0);
            let y = (has('w', 'arrowup') ? 1 : 0) - (has('s', 'arrowdown') ? 1 : 0);
            let run = keys.has('shift');
            if (state.joy) { x += state.joy.dx; y -= state.joy.dy; run ||= Math.hypot(state.joy.dx, state.joy.dy) > 0.9; }
            const len = Math.hypot(x, y);
            if (len > 1) { x /= len; y /= len; }
            return { x, y, run, active: len > 0.08 };
        },
        held: k => keys.has(k),
        takeLook() { const l = { ...state.look }; state.look.dx = state.look.dy = 0; return l; },
        takeZoom() { const z = state.zoom; state.zoom = 0; return z; },
        get recentlyLooked() { return performance.now() - state.lastLook < 1800; },
    };
}

// ---------------- Camera rig ----------------
export function createRig(camera, input, { reducedMotion }) {
    const rig = {
        yaw: 0.6, pitch: 0.42, distance: 9, focus: new THREE.Vector3(),
        override: null,         // { focus, yaw?, pitch?, distance?, minD?, maxD? } set by activities
        firstPerson: null,      // { eye, yaw, pitch, fov } for the telescope
        limits: { min: 2.2, max: 28 },
        blockers: [],           // camera collision shapes (see castCamera)
        camDistance: 9,         // the distance actually used after collision
    };
    const want = { yaw: rig.yaw, pitch: rig.pitch, distance: rig.distance };
    const tmp = new THREE.Vector3(), dir = new THREE.Vector3();
    let fov = 50;

    rig.setView = (v, instant = false) => {
        Object.assign(want, v);
        if (instant || reducedMotion) Object.assign(rig, v);
    };

    rig.update = (dt, followPoint) => {
        const look = input.takeLook();
        const zoom = input.takeZoom();
        if (rig.firstPerson) {
            const fp = rig.firstPerson;
            fp.yaw -= look.dx * 0.0016 * (fp.fov / 50);
            if (fp.baseFacing !== undefined) {
                const maxArc = fp.maxArc ?? 1.22;
                const diff = Math.atan2(Math.sin(fp.yaw - fp.baseFacing), Math.cos(fp.yaw - fp.baseFacing));
                fp.yaw = fp.baseFacing + THREE.MathUtils.clamp(diff, -maxArc, maxArc);
            }
            fp.pitch = THREE.MathUtils.clamp(fp.pitch - look.dy * 0.0016 * (fp.fov / 50), -0.5, fp.maxPitch ?? 0.9);
            fp.fov = THREE.MathUtils.clamp(fp.fov + zoom * 0.8, 6, 45);
            camera.position.copy(fp.eye);
            tmp.set(-Math.sin(fp.yaw) * Math.cos(fp.pitch), Math.sin(fp.pitch), -Math.cos(fp.yaw) * Math.cos(fp.pitch)).add(fp.eye);
            camera.lookAt(tmp);
            fov += (fp.fov - fov) * Math.min(1, dt * 6);
            camera.fov = fov;
            camera.updateProjectionMatrix();
            return;
        }
        if (Math.abs(fov - 50) > 0.05) { fov += (50 - fov) * Math.min(1, dt * 5); camera.fov = fov; camera.updateProjectionMatrix(); }
        want.yaw -= look.dx * 0.0055;
        want.pitch = THREE.MathUtils.clamp(want.pitch + look.dy * 0.0045, -0.05, 1.35);
        const lim = rig.override?.limits ?? rig.limits;
        want.distance = THREE.MathUtils.clamp(want.distance + zoom, lim.min, lim.max);
        const k = 1 - Math.pow(0.0008, dt);
        rig.yaw += (want.yaw - rig.yaw) * k;
        rig.pitch += (want.pitch - rig.pitch) * k;
        rig.distance += (want.distance - rig.distance) * k;
        const target = rig.override?.focus ?? followPoint;
        rig.focus.lerp(target, 1 - Math.pow(rig.override ? 0.02 : 0.0001, dt));

        // Pull the camera in when something stands between it and the player, then ease back out.
        const cp = Math.cos(rig.pitch);
        dir.set(Math.sin(rig.yaw) * cp, Math.sin(rig.pitch), Math.cos(rig.yaw) * cp);
        const hit = castCamera(rig.focus, dir, rig.distance, rig.blockers);
        const allowed = Math.max(0.9, Math.min(rig.distance, hit - 0.35));
        if (allowed < rig.camDistance) rig.camDistance = allowed;                          // snap in: never show the wall
        else rig.camDistance += (allowed - rig.camDistance) * (1 - Math.exp(-dt * 2.5));   // ease back out
        camera.position.copy(rig.focus).addScaledVector(dir, rig.camDistance);
        // Keep the camera out of hills: lift it wherever the ground along the view ray pokes through.
        for (let i = 1; i <= 5; i++) {
            const f = i / 5;
            tmp.lerpVectors(rig.focus, camera.position, f);
            const need = Math.max(groundAt(tmp.x, tmp.z), 0) + 0.45;
            if (tmp.y < need) camera.position.y += (need - tmp.y) / f;
        }
        // When inside the cottage interior room, clamp camera within room walls:
        if (Math.hypot(rig.focus.x - ROOM.x, rig.focus.z - ROOM.z) < 6.0) {
            const padX = 0.35, padZ = 0.35;
            const halfInnerW = ROOM.w / 2 - 0.7;
            const halfInnerD = ROOM.d / 2 - 0.2;
            camera.position.x = THREE.MathUtils.clamp(camera.position.x, ROOM.x - halfInnerW + padX, ROOM.x + halfInnerW - padX);
            camera.position.z = THREE.MathUtils.clamp(camera.position.z, ROOM.z - halfInnerD + padZ, ROOM.z + halfInnerD - padZ);
            camera.position.y = THREE.MathUtils.clamp(camera.position.y, ROOM.y + 0.35, ROOM.y + ROOM.h - 0.25);
        }
        camera.lookAt(rig.focus);
    };

    /** Points the camera behind a heading (used when driving and when entering the world). */
    rig.behind = (heading, k = 1) => {
        let target = heading + Math.PI;
        let diff = target - want.yaw;
        diff = Math.atan2(Math.sin(diff), Math.cos(diff));
        want.yaw += diff * k;
    };
    return rig;
}

/**
 * Distance along a ray from the camera focus to the first camera blocker: vertical cylinders and oriented boxes
 * (the walk colliders, given heights) and spheres (tree canopies, big rocks). Shapes that contain the focus are
 * ignored, so standing under a canopy doesn't pin the camera.
 */
export function castCamera(o, d, maxT, blockers) {
    let best = maxT;
    for (const b of blockers) {
        let t = Infinity;
        if (b.type === 'sphere') {
            const ox = o.x - b.x, oy = o.y - b.y, oz = o.z - b.z;
            const c = ox * ox + oy * oy + oz * oz - b.r * b.r;
            if (c < 0) continue;
            const bb = ox * d.x + oy * d.y + oz * d.z, disc = bb * bb - c;
            if (disc < 0 || bb > 0) continue;
            t = -bb - Math.sqrt(disc);
        } else if (b.type === 'box') {
            const cs = Math.cos(b.rot), sn = Math.sin(b.rot);
            const rx = o.x - b.x, rz = o.z - b.z;
            const lo = [rx * cs - rz * sn, o.y - b.y0, rx * sn + rz * cs];
            const ld = [d.x * cs - d.z * sn, d.y, d.x * sn + d.z * cs];
            const mn = [-b.w / 2, 0, -b.d / 2], mx = [b.w / 2, b.h, b.d / 2];
            if (lo.every((v, i) => v > mn[i] && v < mx[i])) continue;
            let t0 = 0, t1 = maxT;
            for (let i = 0; i < 3 && t0 <= t1; i++) {
                if (Math.abs(ld[i]) < 1e-6) { if (lo[i] < mn[i] || lo[i] > mx[i]) t0 = Infinity; continue; }
                let e0 = (mn[i] - lo[i]) / ld[i], e1 = (mx[i] - lo[i]) / ld[i];
                if (e0 > e1) [e0, e1] = [e1, e0];
                t0 = Math.max(t0, e0);
                t1 = Math.min(t1, e1);
            }
            if (t0 <= t1) t = t0;
        } else {
            const ox = o.x - b.x, oz = o.z - b.z;
            const a = d.x * d.x + d.z * d.z, bb = ox * d.x + oz * d.z, c = ox * ox + oz * oz - b.r * b.r;
            if (c < 0 || a < 1e-8) continue;
            const disc = bb * bb - a * c;
            if (disc < 0) continue;
            const tt = (-bb - Math.sqrt(disc)) / a;
            const y = o.y + d.y * tt;
            if (tt > 0 && y >= b.y0 && y <= b.y0 + b.h) t = tt;
        }
        if (t > 0 && t < best) best = t;
    }
    return best;
}

// ---------------- Player ----------------
function collide(p, colliders, radius) {
    for (const c of colliders) {
        if (c.type === 'box') {
            const dx = p.x - c.x, dz = p.z - c.z, cs = Math.cos(c.rot), sn = Math.sin(c.rot);
            const lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
            const hx = c.w / 2, hz = c.d / 2;
            const qx = THREE.MathUtils.clamp(lx, -hx, hx), qz = THREE.MathUtils.clamp(lz, -hz, hz);
            let ox = lx - qx, oz = lz - qz;
            const d = Math.hypot(ox, oz);
            if (d >= radius) continue;
            if (d < 1e-5) {
                // Centre inside the box: push out through the nearest face.
                if (hx - Math.abs(lx) < hz - Math.abs(lz)) { ox = (Math.sign(lx) || 1) * (hx + radius) - lx; oz = 0; }
                else { oz = (Math.sign(lz) || 1) * (hz + radius) - lz; ox = 0; }
            } else {
                const push = (radius - d) / d;
                ox *= push; oz *= push;
            }
            // Back from the box's frame to world space.
            p.x += ox * cs + oz * sn;
            p.z += -ox * sn + oz * cs;
        } else {
            const dx = p.x - c.x, dz = p.z - c.z, min = c.r + radius, d2 = dx * dx + dz * dz;
            if (d2 < min * min) {
                const d = Math.sqrt(d2) || 1;
                p.x = c.x + (dx / d) * min;
                p.z = c.z + (dz / d) * min;
            }
        }
    }
}

/** Can you stand at (x, z) coming from height `fromY`? Deep water and tall ledges say no. */
function walkable(x, z, fromY) {
    const g = groundAt(x, z);
    if (g < WADE_LIMIT) return false;
    return g - fromY < 0.7;
}

export function createPlayer(scene, { colliders, rig, input, onStep }) {
    const avatar = createCharacter();
    avatar.scale.setScalar(AVATAR_SCALE);
    scene.add(avatar);
    const player = {
        avatar,
        position: avatar.position,
        heading: 0, speed: 0,
        tapTarget: null, stuck: 0, wading: false,
    };
    const vel = new THREE.Vector3();

    function executeLocomotion(dt, t, { maxSpeed, turnRate }) {
        const a = input.axes();
        let dirX = 0, dirZ = 0, want = 0;
        if (a.active) {
            player.tapTarget = null;
            const fx = -Math.sin(rig.yaw), fz = -Math.cos(rig.yaw);
            dirX = fx * a.y + Math.cos(rig.yaw) * a.x;
            dirZ = fz * a.y - Math.sin(rig.yaw) * a.x;
            const len = Math.hypot(dirX, dirZ);
            dirX /= len; dirZ /= len;
            const push = Math.min(1, Math.hypot(a.x, a.y) * 1.15);
            want = maxSpeed * (maxSpeed === RUN ? push : Math.min(1, push * 1.3));
        } else if (player.tapTarget) {
            const dx = player.tapTarget.x - avatar.position.x, dz = player.tapTarget.z - avatar.position.z, d = Math.hypot(dx, dz);
            if (d < 0.4 || player.stuck > 1.2) {
                const done = player.tapTarget;
                player.tapTarget = null;
                if (d < 1.5) done.onArrive?.();
            } else {
                dirX = dx / d; dirZ = dz / d;
                want = d > 18 ? RUN : maxSpeed;
                want = Math.min(want, 0.6 + d * 1.6);
            }
        }

        if (want > 0) {
            const target = Math.atan2(dirX, dirZ);
            const gap = Math.atan2(Math.sin(target - player.heading), Math.cos(target - player.heading));
            player.heading += THREE.MathUtils.clamp(gap, -turnRate * dt, turnRate * dt);
            want *= THREE.MathUtils.clamp(1.25 - Math.abs(gap) / 1.6, 0.25, 1);
        }

        const p = avatar.position;
        const fwdX = Math.sin(player.heading), fwdZ = Math.cos(player.heading);
        const climb = (groundAt(p.x + fwdX * 0.6, p.z + fwdZ * 0.6) - groundAt(p.x, p.z)) / 0.6;
        want *= (player.wading ? 0.55 : 1) * THREE.MathUtils.clamp(1 - Math.max(0, climb) * 0.45, 0.6, 1);
        const rateV = want > player.speed ? ACCEL : DECEL;
        player.speed += THREE.MathUtils.clamp(want - player.speed, -rateV * dt, rateV * dt);

        vel.set(fwdX, 0, fwdZ).multiplyScalar(player.speed * dt);

        const before = p.clone();
        const next = new THREE.Vector3(p.x + vel.x, 0, p.z + vel.z);
        collide(next, colliders, RADIUS);
        if (walkable(next.x, next.z, p.y)) { p.x = next.x; p.z = next.z; }
        else if (walkable(next.x, p.z, p.y)) p.x = next.x;
        else if (walkable(p.x, next.z, p.y)) p.z = next.z;
        const moved = Math.hypot(p.x - before.x, p.z - before.z);
        player.stuck = want > 0 && moved < want * dt * 0.25 ? player.stuck + dt : 0;
        if (moved < player.speed * dt * 0.5) player.speed = Math.max(moved / dt, player.speed * 0.85);

        const g = groundAt(p.x, p.z);
        p.y = g > p.y + 0.06 ? p.y + Math.min(g - p.y, dt * 6) : g;
        player.wading = heightAt(p.x, p.z) < -0.1 && Math.abs(g - heightAt(p.x, p.z)) < 0.01;
        avatar.rotation.y = player.heading;
        animateCharacter(avatar, dt, t, {
            speed: moved / Math.max(dt, 1e-4),
            heading: player.heading,
            onStep: () => onStep?.(surfaceKind(p.x, p.z), player.speed),
        });
        return { moved, want };
    }

    // ---- Character Controller State Machine ----
    const playerStates = {
        [PlayerState.IDLE]: {
            enter() {
                player.speed = 0;
                setPose(avatar, 'idle');
            },
            update(dt, t) {
                const a = input.axes();
                if (a.active) {
                    machine.transition(a.run ? PlayerState.RUN : PlayerState.WALK);
                    return;
                }
                if (player.tapTarget) {
                    machine.transition(PlayerState.WALK);
                    return;
                }
                const p = avatar.position;
                const g = groundAt(p.x, p.z);
                p.y = g > p.y + 0.06 ? p.y + Math.min(g - p.y, dt * 6) : g;
                player.wading = heightAt(p.x, p.z) < -0.1 && Math.abs(g - heightAt(p.x, p.z)) < 0.01;
                avatar.rotation.y = player.heading;
                animateCharacter(avatar, dt, t, { speed: 0, heading: player.heading });
            },
            exit() {},
        },

        [PlayerState.WALK]: {
            enter() {
                setPose(avatar, 'idle');
            },
            update(dt, t) {
                const a = input.axes();
                if (a.active && a.run) {
                    machine.transition(PlayerState.RUN);
                    return;
                }
                const { moved } = executeLocomotion(dt, t, { maxSpeed: WALK, turnRate: TURN_WALK });
                if (machine.current === PlayerState.WALK && !a.active && !player.tapTarget && player.speed < 0.08) {
                    machine.transition(PlayerState.IDLE);
                }
            },
            exit() {},
        },

        [PlayerState.RUN]: {
            enter() {
                setPose(avatar, 'idle');
            },
            update(dt, t) {
                const a = input.axes();
                if (a.active && !a.run) {
                    machine.transition(PlayerState.WALK);
                    return;
                }
                const { moved } = executeLocomotion(dt, t, { maxSpeed: RUN, turnRate: TURN_RUN });
                if (machine.current === PlayerState.RUN && !a.active && !player.tapTarget && player.speed < 0.08) {
                    machine.transition(PlayerState.IDLE);
                }
            },
            exit() {},
        },

        [PlayerState.BUSY]: {
            enter(pose = 'idle') {
                player.speed = 0;
                player.tapTarget = null;
                setPose(avatar, pose);
            },
            update(dt, t) {
                animateCharacter(avatar, dt, t);
            },
            exit() {
                setPose(avatar, 'idle');
            },
        },
    };

    function createPlayerStateMachine(initialState, stateMap) {
        let current = initialState;
        stateMap[current]?.enter?.();

        return {
            get current() { return current; },
            transition(next, ...args) {
                if (current === next) return;
                stateMap[current]?.exit?.();
                current = next;
                stateMap[current]?.enter?.(...args);
            },
            update(dt, t) {
                stateMap[current]?.update?.(dt, t);
            },
        };
    }

    const machine = createPlayerStateMachine(PlayerState.IDLE, playerStates);
    player.machine = machine;

    player.place = (x, z, heading = player.heading) => {
        if (machine.current !== PlayerState.BUSY) {
            machine.transition(PlayerState.IDLE);
        }
        avatar.position.set(x, groundAt(x, z), z);
        player.heading = heading;
        avatar.rotation.set(0, heading, 0);
        player.speed = 0;
        player.tapTarget = null;
    };

    player.walkTo = (x, z, onArrive) => {
        player.tapTarget = { x, z, onArrive };
        player.stuck = 0;
        if (machine.current === PlayerState.IDLE) {
            machine.transition(PlayerState.WALK);
        }
    };

    player.setBusy = (busy, pose = 'idle') => {
        if (busy) {
            if (machine.current === PlayerState.BUSY) {
                setPose(avatar, pose);
            } else {
                machine.transition(PlayerState.BUSY, pose);
            }
        } else {
            machine.transition(PlayerState.IDLE);
        }
    };

    player.update = (dt, t) => {
        machine.update(dt, t);
    };

    player.followPoint = (out = new THREE.Vector3()) => {
        avatar.getWorldPosition(out);
        out.y += 1.45;
        return out;
    };

    player.worldPosition = (out = new THREE.Vector3()) => {
        return avatar.getWorldPosition(out);
    };

    Object.defineProperty(player, 'mode', {
        get() {
            const s = machine.current;
            if (s === PlayerState.BUSY) return 'busy';
            return 'walk';
        },
        set(m) {
            if (m === 'busy') machine.transition(PlayerState.BUSY);
            else machine.transition(PlayerState.IDLE);
        },
    });

    Object.defineProperty(player, 'state', {
        get() { return machine.current; },
    });
    return player;
}
