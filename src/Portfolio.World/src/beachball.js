// Beach Ball Keep-Up & Volleyball Mini-Game: procedural 3D beach ball on South Beach
// with floaty bounce physics, elastic sand/water collision, combo volley counter,
// rising pitch Web Audio feedback, and State Machine architecture (BeachBallState).
import * as THREE from 'three';
import { groundAt } from './terrain.js';

export const BeachBallState = {
    IDLE: 'idle',
    JUGGLING: 'juggling',
    DROPPED: 'dropped',
};

const BALL_RADIUS = 0.42;
const GRAVITY = -6.8; // Floaty air-filled vinyl ball gravity
const RESTITUTION = 0.65; // Elastic bounce retention
const AIR_DRAG = 0.28;
const _rollAxis = new THREE.Vector3();

// Create procedural striped beach ball texture
function createBeachBallTexture() {
    const canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 256;
    const ctx = canvas.getContext('2d');

    const colors = ['#f04632', '#ffffff', '#228be6', '#ffd43b', '#ffffff', '#20c997'];
    const w = canvas.width / colors.length;

    colors.forEach((col, i) => {
        ctx.fillStyle = col;
        ctx.fillRect(i * w, 0, w, canvas.height);
    });

    // White pole caps
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.arc(canvas.width / 2, 0, 45, 0, Math.PI * 2);
    ctx.fill();

    ctx.beginPath();
    ctx.arc(canvas.width / 2, canvas.height, 45, 0, Math.PI * 2);
    ctx.fill();

    const tex = new THREE.CanvasTexture(canvas);
    tex.wrapS = THREE.RepeatWrapping;
    tex.wrapT = THREE.ClampToEdgeWrapping;
    return tex;
}

// Synthesize pleasant rising pitch tones for consecutive volleys
let sharedAudioCtx = null;
function playVolleyTone(combo, save) {
    if (save && save.settings && !save.settings.sound) return;
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return;
    try {
        if (!sharedAudioCtx) sharedAudioCtx = new AudioCtx();
        if (sharedAudioCtx.state === 'suspended') sharedAudioCtx.resume();
        const actx = sharedAudioCtx;
        const osc = actx.createOscillator();
        const g = actx.createGain();
        const scale = [261.63, 293.66, 329.63, 349.23, 392.00, 440.00, 493.88, 523.25, 587.33, 659.25, 783.99, 880.00];
        const freq = scale[Math.min(Math.max(0, combo - 1), scale.length - 1)];

        osc.type = 'sine';
        osc.frequency.setValueAtTime(freq, actx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(freq * 1.35, actx.currentTime + 0.08);

        g.gain.setValueAtTime(0.25, actx.currentTime);
        g.gain.exponentialRampToValueAtTime(0.001, actx.currentTime + 0.22);

        osc.connect(g).connect(actx.destination);
        osc.start();
        osc.stop(actx.currentTime + 0.22);
    } catch (err) {
        // audio context could be blocked by browser autoplay policy before gesture
    }
}

export function createBeachBall(scene, { player, audio, ui, save, life, water, anchors }) {
    const avatar = player.avatar;
    const campfire = anchors?.campfire?.center ?? { x: 14, y: 0.8, z: 38 };

    // Initial ball spawn point on the sunny south beach
    const spawnPos = new THREE.Vector3(campfire.x + 6.2, groundAt(campfire.x + 6.2, campfire.z + 4.5) + BALL_RADIUS, campfire.z + 4.5);

    // 3D Beach Ball Group
    const ballGroup = new THREE.Group();
    ballGroup.position.copy(spawnPos);
    scene.add(ballGroup);

    // Ball Mesh
    const ballGeo = new THREE.SphereGeometry(BALL_RADIUS, 24, 16);
    const ballMat = new THREE.MeshStandardMaterial({
        map: createBeachBallTexture(),
        roughness: 0.35,
        metalness: 0.05,
    });
    const ballMesh = new THREE.Mesh(ballGeo, ballMat);
    ballMesh.castShadow = true;
    ballGroup.add(ballMesh);

    // Projected Contact Drop Shadow
    const shadowGeo = new THREE.RingGeometry(0, BALL_RADIUS * 1.15, 24);
    shadowGeo.rotateX(-Math.PI / 2);
    const shadowMat = new THREE.MeshBasicMaterial({
        color: 0x1a1610,
        transparent: true,
        opacity: 0.38,
        depthWrite: false,
    });
    const shadowMesh = new THREE.Mesh(shadowGeo, shadowMat);
    scene.add(shadowMesh);

    // Physics State
    const pos = ballGroup.position;
    const vel = new THREE.Vector3(0, 0, 0);

    // DOM HUD elements
    const hud = document.getElementById('ballHud');
    const hudCombo = document.getElementById('ballHudCombo');
    const hudBest = document.getElementById('ballHudBest');

    let lastRenderedCombo = -1;
    let lastRenderedBest = -1;
    function updateHUD(combo) {
        if (!hud) return;
        const best = save.journal.bestKeepUp ?? 0;
        const targetBest = Math.max(best, combo);
        if (combo === lastRenderedCombo && targetBest === lastRenderedBest) return;
        lastRenderedCombo = combo;
        lastRenderedBest = targetBest;
        if (hudCombo) hudCombo.textContent = combo;
        if (hudBest) hudBest.textContent = `Best: ${targetBest}`;
    }

    // Shared State Machine Context
    const fsmContext = {
        combo: 0,
        dropCooldown: 0,
        restTimer: 0,
    };

    // ---------------- State Handlers ----------------
    const states = {
        [BeachBallState.IDLE]: {
            enter(ctx) {
                ctx.combo = 0;
                if (hud) hud.hidden = true;
                const gy = groundAt(pos.x, pos.z);
                pos.y = gy + BALL_RADIUS;
                vel.set(0, 0, 0);
            },
            update(ctx, dt, t) {
                // Gentle visual idle bobbing without mutating physics position
                ballMesh.position.y = Math.sin(t * 2.2) * 0.02;
            },
            exit() {
                ballMesh.position.y = 0;
            },
        },

        [BeachBallState.JUGGLING]: {
            enter(ctx) {
                if (hud) hud.hidden = false;
                updateHUD(ctx.combo);
            },
            update(ctx, dt, t) {
                updateHUD(ctx.combo);
            },
            exit() {},
        },

        [BeachBallState.DROPPED]: {
            enter(ctx) {
                ctx.dropCooldown = 2.4;
                const score = ctx.combo;
                const prevBest = save.journal.bestKeepUp ?? 0;

                if (score > 0) {
                    if (score > prevBest) {
                        save.journal.bestKeepUp = score;
                        audio.play('chime', [65, 69, 72, 77]);
                        ui.toast({
                            icon: '🏆',
                            title: 'New Keep-Up Record!',
                            body: `${score} consecutive volleys. Incredible balance!`,
                        });
                    } else {
                        ui.toast({
                            icon: '🏐',
                            title: 'Volley Dropped',
                            body: `${score} keep-ups. Personal best: ${prevBest}.`,
                        });
                    }

                    if (score >= 5 && !save.journal.done.beachball) {
                        save.journal.done.beachball = true;
                        ui.toast({ icon: '✨', title: 'Journal', body: 'Play beach ball keep-up completed!' });
                    }
                    save.journal.seen.beachball = true;
                    ui.journalChanged();
                }

                audio.play('pop');
                ctx.combo = 0;
            },
            update(ctx, dt, t) {
                ctx.dropCooldown -= dt;
                if (ctx.dropCooldown <= 0) {
                    machine.transition(BeachBallState.IDLE);
                }
            },
            exit(ctx, next) {
                if (next !== BeachBallState.JUGGLING) {
                    ctx.combo = 0;
                    if (hud) hud.hidden = true;
                }
            },
        },
    };

    // State Machine Engine
    function createStateMachine(initialState, stateMap, context) {
        let current = initialState;
        stateMap[current]?.enter?.(context);

        return {
            get current() { return current; },
            transition(next, ...args) {
                if (current === next) return;
                stateMap[current]?.exit?.(context, next);
                current = next;
                stateMap[current]?.enter?.(context, ...args);
            },
            update(dt, t) {
                stateMap[current]?.update?.(context, dt, t);
            },
        };
    }

    const machine = createStateMachine(BeachBallState.IDLE, states, fsmContext);

    // Avatar strike / header execution
    function volleyHit(power = 1.0) {
        const ap = player.worldPosition();
        const dx = pos.x - ap.x;
        const dz = pos.z - ap.z;
        const dist = Math.hypot(dx, dz) || 1;

        // Upward loft with forward impulse
        vel.y = (4.8 + Math.min(fsmContext.combo * 0.12, 2.2)) * power;
        const fwdX = Math.sin(player.heading);
        const fwdZ = Math.cos(player.heading);

        vel.x = (dx / dist) * 1.6 + fwdX * 2.2 * power;
        vel.z = (dz / dist) * 1.6 + fwdZ * 2.2 * power;

        fsmContext.combo++;
        playVolleyTone(fsmContext.combo, save);

        // Visual sparkles
        life?.float?.('✨', pos.clone().add(new THREE.Vector3(0, 0.25, 0)), { size: 0.45, rise: 0.35, life: 0.8 });

        if (machine.current !== BeachBallState.JUGGLING) {
            machine.transition(BeachBallState.JUGGLING);
        } else {
            updateHUD(fsmContext.combo);
        }
    }

    return {
        update(dt, t, env) {
            machine.update(dt, t);

            // ---- Ball Physics Integration ----
            vel.y += GRAVITY * dt;
            vel.x -= vel.x * AIR_DRAG * dt;
            vel.z -= vel.z * AIR_DRAG * dt;

            // Island wind gentle drift
            const gust = env?.gust ?? 1;
            vel.x += Math.sin(t * 0.4) * 0.35 * gust * dt;
            vel.z += Math.cos(t * 0.35) * 0.35 * gust * dt;

            pos.x += vel.x * dt;
            pos.y += vel.y * dt;
            pos.z += vel.z * dt;

            // Ball rotation matches horizontal movement via world-axis rotation to prevent gimbal twist
            const rollSpeed = Math.hypot(vel.x, vel.z);
            if (rollSpeed > 0.001) {
                _rollAxis.set(-vel.z / rollSpeed, 0, vel.x / rollSpeed);
                ballMesh.rotateOnWorldAxis(_rollAxis, rollSpeed * dt * 2.5);
            }

            // Sand terrain collision
            const gy = groundAt(pos.x, pos.z);
            const waterY = water?.heightAt?.(pos.x, pos.z, t) ?? -10;
            const surfaceY = Math.max(gy, waterY);

            if (pos.y - BALL_RADIUS <= surfaceY) {
                pos.y = surfaceY + BALL_RADIUS;

                if (vel.y < -0.8) {
                    vel.y = -vel.y * RESTITUTION;
                    vel.x *= 0.82;
                    vel.z *= 0.82;

                    if (machine.current === BeachBallState.JUGGLING) {
                        machine.transition(BeachBallState.DROPPED);
                    }
                } else {
                    vel.y = 0;
                    vel.x *= 0.88;
                    vel.z *= 0.88;
                    if (machine.current === BeachBallState.JUGGLING) {
                        machine.transition(BeachBallState.DROPPED);
                    }
                }
            }

            // Contact drop shadow positioning
            shadowMesh.position.set(pos.x, gy + 0.02, pos.z);
            const heightAbove = Math.max(0, pos.y - BALL_RADIUS - gy);
            shadowMesh.scale.setScalar(1 + heightAbove * 0.35);
            shadowMat.opacity = Math.max(0.06, 0.36 / (1 + heightAbove * 0.75));

            // ---- Avatar Proximity Collision / Automatic Header Bump ----
            if (player.mode === 'walk') {
                const ap = avatar.position;
                const d = Math.hypot(pos.x - ap.x, pos.z - ap.z);
                const heightDiff = pos.y - ap.y;

                if (d < BALL_RADIUS + 0.55 && heightDiff >= 0.2 && heightDiff <= 1.95 && vel.y <= 1.0) {
                    volleyHit(1.0);
                }
            }
        },

        get state() { return machine.current; },
        get combo() { return fsmContext.combo; },
        get pos() { return pos; },

        // Candidate provider for activities.js interaction list
        candidate(avatarPos) {
            const d = Math.hypot(avatarPos.x - pos.x, avatarPos.z - pos.z);
            if (d < 2.2) {
                return {
                    id: 'beachball',
                    pos,
                    d,
                    label: fsmContext.combo > 0 ? 'Volley beach ball' : 'Kick beach ball',
                    run: () => volleyHit(1.2),
                };
            }
            return null;
        },

        strike() {
            const ap = player.worldPosition();
            const d = Math.hypot(ap.x - pos.x, ap.z - pos.z);
            if (d < 2.5) {
                volleyHit(1.25);
                return true;
            }
            return false;
        },
    };
}
