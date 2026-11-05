// Message in a Bottle & Treasure Digging: driven by an explicit, lightweight State Machine.
// States: BOTTLE -> CLUE -> DIGGING -> REVEALED -> (loop to next treasure) -> IDLE.
// Invariant: clue and treasure bottle positions persist discovery status across reloads in save.journal.
import * as THREE from 'three';
import { groundAt } from './terrain.js';
import { box, mat } from './builders.js';

export const TREASURES = [
    {
        id: 'floppy',
        name: 'Vintage 3.5" Floppy Disk',
        icon: '💾',
        subtitle: "Shoaib's First C# Program (2012)",
        bottlePos: { x: -44.0, z: 32.0 },
        digPos: { x: -36.0, z: 38.0 },
        clueTitle: 'Tideline Message',
        clue: '“To whoever finds this bottle: seek where wooden pilings meet the southern surf. At the shoreward roots of the pier lies the inception of a coding journey.”',
        description: 'A 1.44 MB magnetic disc containing a high school console program. The ink label reads: “Hello World — Shoaib 2012”.',
    },
    {
        id: 'wrench',
        name: 'Master Craftsman’s Wrench',
        icon: '🔧',
        subtitle: '10,000 Pull Requests Later',
        bottlePos: { x: 18.0, z: 36.0 },
        digPos: { x: 38.0, z: -12.0 },
        clueTitle: 'Breezy Parchment',
        clue: '“The four canvas sails catch the northern gale. Step behind the millstones where wild blossoms face the dawn, and unearth the craftsman’s tool.”',
        description: 'A polished chrome-vanadium spanner engraved with clean architecture patterns and zero compiler warnings.',
    },
    {
        id: 'bat',
        name: 'Golden Willow Cricket Bat',
        icon: '🏏',
        subtitle: 'Crafted in Sialkot, Pakistan',
        bottlePos: { x: 42.0, z: 26.0 },
        digPos: { x: -28.0, z: -38.0 },
        clueTitle: 'Highland Scroll',
        clue: '“Climb past the rocky ridges to where the ancient tree watches the setting sun. Beneath the creaking rope swing rests a treasured piece of home.”',
        description: 'Carved from seasoned English willow in Sialkot, Pakistan. Still smells of linseed oil and childhood matches.',
    },
    {
        id: 'compass',
        name: 'The Navigator’s Astrolabe',
        icon: '🧭',
        subtitle: 'Guiding Infinite Ambition',
        bottlePos: { x: -16.0, z: 38.0 },
        digPos: { x: 76.0, z: -32.0 },
        clueTitle: 'Sea-Stained Dispatch',
        clue: '“Follow the rotating beacon to the rugged eastern cape. Where waves crash against the granite bluff, dig beneath the lighthouse beam.”',
        description: 'A brass navigational instrument that always points toward the next frontier in engineering and human creativity.',
    },
];

export const TreasureState = {
    IDLE: 'idle',           // All treasures collected
    BOTTLE: 'bottle',       // Bottle visible on shoreline, waiting for player
    CLUE: 'clue',           // Clue read, chest buried at dig site
    DIGGING: 'digging',     // Dig animation and particle burst in progress
    REVEALED: 'revealed',   // Chest open, reward card displayed
};

export function createTreasureHunt(scene, { player, audio, ui, save }) {
    const group = new THREE.Group();
    group.name = 'treasure-hunt';
    scene.add(group);

    // Ensure save data schema
    save.journal.treasures ??= [];

    // ---- 3D Bottle Construction ----
    const bottleGroup = new THREE.Group();
    const glassMat = new THREE.MeshStandardMaterial({
        color: 0x5eead4,
        roughness: 0.15,
        metalness: 0.1,
        transparent: true,
        opacity: 0.72,
    });
    const corkMat = mat(0x926641, { roughness: 0.9 });
    const parchmentMat = mat(0xfef3c7, { roughness: 0.8 });

    // Glass body, neck, and cork
    const bottleBody = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 0.42, 12), glassMat);
    bottleBody.position.y = 0.21;
    const bottleNeck = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.08, 0.14, 10), glassMat);
    bottleNeck.position.y = 0.49;
    const cork = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.045, 0.08, 8), corkMat);
    cork.position.y = 0.58;
    const scroll = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 0.28, 8), parchmentMat);
    scroll.position.set(0, 0.22, 0);
    scroll.rotation.z = 0.15;

    bottleGroup.add(bottleBody, bottleNeck, cork, scroll);
    bottleGroup.rotation.z = Math.PI / 2.3; // Tilts resting on sand
    bottleGroup.visible = false;
    group.add(bottleGroup);

    // Orbiting bottle sparkles
    const sparkleGroup = new THREE.Group();
    const sparkleMat = new THREE.MeshBasicMaterial({ color: 0xffe066 });
    for (let i = 0; i < 4; i++) {
        const sp = new THREE.Mesh(new THREE.OctahedronGeometry(0.05, 0), sparkleMat);
        const ang = (i / 4) * Math.PI * 2;
        sp.position.set(Math.cos(ang) * 0.35, 0.2 + (i % 2) * 0.15, Math.sin(ang) * 0.35);
        sparkleGroup.add(sp);
    }
    bottleGroup.add(sparkleGroup);

    // ---- 3D Treasure Chest Construction ----
    const chestGroup = new THREE.Group();
    chestGroup.visible = false;

    const chestWood = mat(0x422a1d, { roughness: 0.8 });
    const chestBronze = mat(0xc2883f, { metalness: 0.7, roughness: 0.35 });
    const chestGold = mat(0xffd700, { emissive: 0xffaa00, emissiveIntensity: 1.2, metalness: 0.8 });

    // Chest Base
    const chestBase = box(0.85, 0.42, 0.52, 0x422a1d);
    chestBase.position.y = 0.21;
    chestGroup.add(chestBase);

    // Bronze straps
    for (const sx of [-0.3, 0.3]) {
        const strap = box(0.07, 0.43, 0.53, 0xc2883f);
        strap.position.set(sx, 0.21, 0);
        chestGroup.add(strap);
    }

    // Hinged Lid (pivot positioned at back edge)
    const lidPivot = new THREE.Group();
    lidPivot.position.set(0, 0.42, -0.26);
    const lidMesh = box(0.86, 0.14, 0.54, 0x422a1d);
    lidMesh.position.set(0, 0.07, 0.27);
    const latch = box(0.12, 0.12, 0.04, 0xc2883f);
    latch.position.set(0, 0.02, 0.54);
    lidPivot.add(lidMesh, latch);
    chestGroup.add(lidPivot);

    // Glowing treasure inside
    const treasureRelic = new THREE.Mesh(new THREE.DodecahedronGeometry(0.14, 0), chestGold);
    treasureRelic.position.set(0, 0.3, 0);
    const chestLight = new THREE.PointLight(0xffb536, 0, 4);
    chestLight.position.set(0, 0.35, 0);
    chestGroup.add(treasureRelic, chestLight);

    // Secret Dig Site Sparkle Marker
    const digMarkerGroup = new THREE.Group();
    digMarkerGroup.visible = false;
    const digRing = new THREE.Mesh(
        new THREE.RingGeometry(0.3, 0.5, 16).rotateX(-Math.PI / 2),
        new THREE.MeshBasicMaterial({ color: 0xffd27a, transparent: true, opacity: 0.65, depthWrite: false })
    );
    const digSparkle = new THREE.Mesh(new THREE.OctahedronGeometry(0.1, 0), chestGold);
    digSparkle.position.y = 0.25;
    digMarkerGroup.add(digRing, digSparkle);
    group.add(digMarkerGroup, chestGroup);

    // Dig Dust Particle System
    const dustCount = 18;
    const dustGeo = new THREE.BufferGeometry();
    const dustPos = new Float32Array(dustCount * 3);
    const dustVel = Array.from({ length: dustCount }, () => new THREE.Vector3());
    dustGeo.setAttribute('position', new THREE.BufferAttribute(dustPos, 3));
    const dustMat = new THREE.PointsMaterial({ color: 0xd6b788, size: 0.15, transparent: true, opacity: 0 });
    const dustPoints = new THREE.Points(dustGeo, dustMat);
    group.add(dustPoints);

    function triggerDust(x, y, z) {
        dustMat.opacity = 0.85;
        for (let i = 0; i < dustCount; i++) {
            dustPos[i * 3] = x + (Math.random() - 0.5) * 0.4;
            dustPos[i * 3 + 1] = y + 0.1;
            dustPos[i * 3 + 2] = z + (Math.random() - 0.5) * 0.4;
            dustVel[i].set((Math.random() - 0.5) * 2.2, 1.2 + Math.random() * 2.0, (Math.random() - 0.5) * 2.2);
        }
        dustGeo.attributes.position.needsUpdate = true;
    }

    // Helper to pick the next undiscovered treasure
    function getNextTreasure() {
        return TREASURES.find(t => !save.journal.treasures.includes(t.id)) ?? null;
    }

    // Shared State Machine Context
    const fsmContext = {
        activeTreasure: getNextTreasure(),
        digTimer: 0,
        revealTimer: 0,
        baseGroundY: 0,
    };

    // ---- State Machine Handlers ----
    const states = {
        [TreasureState.IDLE]: {
            enter(ctx) {
                bottleGroup.visible = false;
                chestGroup.visible = false;
                digMarkerGroup.visible = false;
                ctx.activeTreasure = null;
            },
            update() {},
            exit() {},
        },

        [TreasureState.BOTTLE]: {
            enter(ctx) {
                if (!ctx.activeTreasure) {
                    machine.transition(TreasureState.IDLE);
                    return;
                }
                const bp = ctx.activeTreasure.bottlePos;
                const gy = groundAt(bp.x, bp.z);
                ctx.baseGroundY = gy;
                bottleGroup.position.set(bp.x, gy + 0.08, bp.z);
                bottleGroup.visible = true;
                chestGroup.visible = false;
                digMarkerGroup.visible = false;
            },
            update(ctx, dt, t) {
                if (!bottleGroup.visible) return;
                bottleGroup.position.y = ctx.baseGroundY + 0.08 + Math.sin(t * 2.6) * 0.04;
                sparkleGroup.rotation.y += dt * 1.6;
            },
            exit() {
                bottleGroup.visible = false;
            },
        },

        [TreasureState.CLUE]: {
            enter(ctx) {
                bottleGroup.visible = false;
                const dp = ctx.activeTreasure.digPos;
                const gy = groundAt(dp.x, dp.z);
                ctx.baseGroundY = gy;

                // Position dig marker and underground chest
                digMarkerGroup.position.set(dp.x, gy + 0.04, dp.z);
                digMarkerGroup.visible = true;

                chestGroup.position.set(dp.x, gy - 0.55, dp.z);
                chestGroup.visible = true;
                lidPivot.rotation.x = 0;
                chestLight.intensity = 0;
            },
            update(ctx, dt, t) {
                digRing.material.opacity = 0.45 + Math.sin(t * 4.5) * 0.25;
                digSparkle.position.y = 0.25 + Math.sin(t * 3.0) * 0.08;
                digSparkle.rotation.y += dt * 2.0;
            },
            exit() {
                digMarkerGroup.visible = false;
            },
        },

        [TreasureState.DIGGING]: {
            enter(ctx) {
                ctx.digTimer = 1.35;
                digMarkerGroup.visible = false;
                player.setBusy?.(true, 'crouch');
                audio.play('step', 'sand');
                const dp = ctx.activeTreasure.digPos;
                triggerDust(dp.x, ctx.baseGroundY, dp.z);
            },
            update(ctx, dt, t) {
                ctx.digTimer -= dt;
                // Animate dust particles
                if (dustMat.opacity > 0) {
                    dustMat.opacity = Math.max(0, dustMat.opacity - dt * 0.7);
                    for (let i = 0; i < dustCount; i++) {
                        dustPos[i * 3] += dustVel[i].x * dt;
                        dustPos[i * 3 + 1] += dustVel[i].y * dt;
                        dustPos[i * 3 + 2] += dustVel[i].z * dt;
                        dustVel[i].y -= 4.0 * dt; // gravity
                    }
                    dustGeo.attributes.position.needsUpdate = true;
                }

                // Rise chest from underground
                const progress = Math.min(1, 1 - ctx.digTimer / 1.35);
                chestGroup.position.y = ctx.baseGroundY - 0.55 + progress * 0.55;

                if (ctx.digTimer <= 0) {
                    machine.transition(TreasureState.REVEALED);
                }
            },
            exit() {
                player.setBusy?.(false);
            },
        },

        [TreasureState.REVEALED]: {
            enter(ctx) {
                ctx.revealTimer = 8.5;
                chestGroup.position.y = ctx.baseGroundY;
                lidPivot.rotation.x = -Math.PI * 0.42; // Pop open lid
                chestLight.intensity = 2.5;

                audio.play('creak');
                setTimeout(() => audio.play('wish'), 250);

                const t = ctx.activeTreasure;
                save.journal.currentClue = null;
                if (!save.journal.treasures.includes(t.id)) {
                    save.journal.treasures.push(t.id);
                }

                // Celebration Card
                ui.card({
                    icon: t.icon,
                    kicker: `Island Treasure ${save.journal.treasures.length} of ${TREASURES.length}`,
                    title: t.name,
                    subtitle: t.subtitle,
                    body: t.description,
                    foot: 'Safely catalogued in your island journal.',
                });

                if (save.journal.treasures.length >= TREASURES.length) {
                    save.journal.done.treasure = true;
                    ui.toast({ icon: '🏆', title: 'Island Archaeologist!', body: 'Discovered all 4 island treasures.' });
                } else {
                    save.journal.seen.treasure = true;
                    ui.toast({ icon: '🧭', title: 'Treasure Unearthed!', body: `${save.journal.treasures.length} of ${TREASURES.length} found.` });
                }
                ui.journalChanged();
            },
            update(ctx, dt, t) {
                ctx.revealTimer -= dt;
                treasureRelic.rotation.y += dt * 1.5;
                chestLight.intensity = 2.0 + Math.sin(t * 5.0) * 0.6;

                if (ctx.revealTimer <= 0) {
                    chestGroup.visible = false;
                    const next = getNextTreasure();
                    if (next) {
                        ctx.activeTreasure = next;
                        machine.transition(TreasureState.BOTTLE);
                    } else {
                        machine.transition(TreasureState.IDLE);
                    }
                }
            },
            exit() {
                chestGroup.visible = false;
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
                stateMap[current]?.exit?.(context);
                current = next;
                stateMap[current]?.enter?.(context, ...args);
            },
            update(dt, t) {
                stateMap[current]?.update?.(context, dt, t);
            },
        };
    }

    const initial = fsmContext.activeTreasure
        ? (save.journal.currentClue && save.journal.currentClue === fsmContext.activeTreasure.clue ? TreasureState.CLUE : TreasureState.BOTTLE)
        : TreasureState.IDLE;
    const machine = createStateMachine(initial, states, fsmContext);

    // Interaction handlers called when player interacts near object
    function openBottle() {
        const t = fsmContext.activeTreasure;
        if (!t) return;
        player.setBusy?.(true, 'crouch');
        audio.play('pop');
        save.journal.seen.treasure = true;
        save.journal.currentClue = t.clue;
        ui.journalChanged();

        setTimeout(() => {
            if (player.mode === 'busy' && machine.current !== TreasureState.DIGGING) {
                player.setBusy?.(false);
            }
        }, 400);

        ui.info({
            kicker: 'Message in a Bottle · Island Clue',
            title: t.clueTitle,
            build(el, h) {
                el.append(
                    h('p', { class: 'parchment-clue' }, t.clue),
                    h('p', { class: 'info-progress' }, 'This clue has been saved to your Island Journal (J).'),
                    h('div', { class: 'info-actions' },
                        h('button', {
                            class: 'btn-primary',
                            type: 'button',
                            'data-close': '',
                            autofocus: 'true'
                        }, 'Close & Search Island → [E / Space]')
                    )
                );
            },
        });
        machine.transition(TreasureState.CLUE);
    }

    function digTreasure() {
        machine.transition(TreasureState.DIGGING);
    }

    return {
        update(dt, t) {
            machine.update(dt, t);
        },
        get state() { return machine.current; },
        get isBusy() { return machine.current === TreasureState.DIGGING; },
        get activeTreasure() { return fsmContext.activeTreasure; },
        // Candidate provider for activities.js interaction list
        candidate(pos) {
            if (machine.current === TreasureState.BOTTLE) {
                const bp = fsmContext.activeTreasure?.bottlePos;
                if (!bp) return null;
                const d = Math.hypot(pos.x - bp.x, pos.z - bp.z);
                if (d < 2.5) {
                    return { id: 'treasure', pos: bp, d, label: 'Read message in bottle', run: openBottle };
                }
            } else if (machine.current === TreasureState.CLUE) {
                const dp = fsmContext.activeTreasure?.digPos;
                if (!dp) return null;
                const d = Math.hypot(pos.x - dp.x, pos.z - dp.z);
                if (d < 2.6) {
                    return { id: 'treasure', pos: dp, d, label: 'Dig for treasure', run: digTreasure };
                }
            }
            return null;
        },
    };
}
