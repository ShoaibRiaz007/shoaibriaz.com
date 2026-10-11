// Things to do on the island. Each activity says where it is, when it's available, what the prompt reads, and
// how it starts, runs and ends. One is active at a time; it owns the avatar's pose and the camera until you
// leave it. The journal tracks which ones you've found and done.
import * as THREE from 'three';
import { setPose, AVATAR_SCALE, SIT_DROP } from './character.js';
import { flowerGeometry, windify } from './vegetation.js';
import { coastPoint, groundAt, heightAt, isBeach, isMeadow, PLACES, surfaceKind } from './terrain.js';
import { mat } from './builders.js';
import { rng } from './noise.js';

export const FISH = [
    { id: 'sardine', name: 'Sardine', rarity: 'Common', size: [12, 20], w: 30, note: 'Small, silver and always in a hurry.' },
    { id: 'mackerel', name: 'Mackerel', rarity: 'Common', size: [25, 40], w: 24, note: 'Striped like the sea at dusk.' },
    { id: 'bream', name: 'Sea Bream', rarity: 'Common', size: [20, 35], w: 20, note: 'A steady, patient fish. Respect.' },
    { id: 'pomfret', name: 'Silver Pomfret', rarity: 'Uncommon', size: [18, 30], w: 12, note: 'Flat, shiny and a little shy.' },
    { id: 'snapper', name: 'Red Snapper', rarity: 'Uncommon', size: [30, 60], w: 10, note: 'Blushes when caught. Or always.' },
    { id: 'tang', name: 'Blue Tang', rarity: 'Uncommon', size: [15, 25], w: 7, day: true, note: 'Seems to have forgotten where it was going.' },
    { id: 'grouper', name: 'Grouper', rarity: 'Uncommon', size: [40, 90], w: 6, note: 'Looked at you like you owed it money.' },
    { id: 'barracuda', name: 'Barracuda', rarity: 'Rare', size: [60, 120], w: 3, note: 'All teeth and no small talk.' },
    { id: 'moonfish', name: 'Moonfish', rarity: 'Rare', size: [30, 50], w: 5, night: true, note: 'Only bites when the moon is up.' },
    { id: 'lanternfish', name: 'Lanternfish', rarity: 'Rare', size: [8, 15], w: 5, night: true, note: 'Glows softly. Brought its own light.' },
    { id: 'mahseer', name: 'Golden Mahseer', rarity: 'Legendary', size: [60, 110], w: 1, note: "Pakistan's national fish. A long way from the Indus." },
    { id: 'boot', name: 'Old Boot', rarity: 'Junk', size: [27, 29], w: 5, note: 'Size 9. No sign of the other one.' },
];

const SHELL_NAMES = ['Pink Scallop', 'Tiger Cowrie', 'Moon Snail', 'Sundial', 'Spiny Murex', 'Sand Dollar', 'Turban Shell', 'Pearly Nautilus', 'Angel Wing', 'Olive Shell', 'Conch', 'Lucky Cockle'];
const SHELL_COLORS = [0xf7b6c2, 0xe8c79a, 0xf4efe6, 0xd9b38c, 0xf2d0a4, 0xeee6d2, 0xc9a07a, 0xf6e3d0, 0xfaf6ee, 0xb59a7a, 0xf3b58e, 0xffd6a8];

/** The journal's checklist. Hints show until you find each one. */
export const ACTIVITIES = [
    { id: 'career', title: 'Walk the career trail', hint: 'It starts behind the cottage and climbs to the lookout.' },
    { id: 'gallery', title: 'Visit the project gallery', hint: 'Follow the path east from the windmill track.' },
    { id: 'mail', title: 'Read the letter in the mailbox', hint: 'The mailbox by the cottage has its flag up.' },
    { id: 'fish', title: 'Go fishing off the pier', hint: 'The end of the pier is a quiet spot.' },
    { id: 'campfire', title: 'Sit by the campfire', hint: 'Follow the smoke on the south beach.' },
    { id: 'hammock', title: 'Rest in the hammock', hint: 'Two palms on the beach hold something comfy.' },
    { id: 'swing', title: 'Swing from the old tree', hint: 'Something creaks on top of the hill.' },
    { id: 'telescope', title: 'Look through the telescope', hint: 'The lookout has a view worth zooming into.' },
    { id: 'lighthouse', title: 'Climb the lighthouse', hint: 'The headland in the north-east.' },
    { id: 'lantern', title: 'Release a sky lantern', hint: 'After sunset, at the campfire or the lookout.' },
    { id: 'wish', title: 'Wish on a shooting star', hint: 'Lie back and watch the night sky.' },
    { id: 'cat', title: 'Make friends with the cat', hint: 'Someone naps on the cottage porch.' },
    { id: 'skip', title: 'Skip a stone', hint: 'Stand at the water’s edge on a beach.' },
    { id: 'plant', title: 'Plant a flower', hint: 'Press F (or 🌱) anywhere on the grass.' },
    { id: 'shells', title: 'Find all 12 seashells', hint: 'Sparkles on the sand. Look closely.' },
    { id: 'drive', title: 'Take the car for a drive', hint: 'It’s parked by the cottage, keys in.' },
    { id: 'bench', title: 'Sit on a bench and watch the world', hint: 'Benches wait at the lookout, windmill and cliffs.' },
    { id: 'photo', title: 'Take a photo', hint: 'Press P for photo mode.' },
];

const isNight = h => h >= 18.6 || h < 5.4;

export function createActivities(ctx) {
    const { scene, player, rig, life, audio, ui, anchors, save, water, time, showcase } = ctx;
    const avatar = player.avatar;
    const S = AVATAR_SCALE;
    const v3 = () => new THREE.Vector3();
    let active = null;             // the running activity
    let busyUntil = 0;             // short actions (throw, plant) lock input briefly

    const journal = save.journal;
    const markSeen = id => { if (!journal.seen[id]) { journal.seen[id] = true; ui.journalChanged(); } };
    const markDone = id => {
        if (journal.done[id]) return;
        journal.done[id] = journal.seen[id] = true;
        const a = ACTIVITIES.find(x => x.id === id);
        ui.toast({ icon: '✓', title: 'Journal', body: a?.title ?? id });
        audio.play('chime', [76, 79, 83]);
        ui.journalChanged();
    };

    // ---------- shared helpers ----------
    const facingVec = f => new THREE.Vector3(Math.sin(f), 0, Math.cos(f));
    function seat(anchor, pose = 'sit') {
        const back = facingVec(anchor.facing).multiplyScalar(-0.08);
        scene.attach(avatar);
        avatar.position.set(anchor.x + back.x, anchor.y - SIT_DROP, anchor.z + back.z);
        avatar.rotation.set(0, anchor.facing, 0);
        player.heading = anchor.facing;
        setPose(avatar, pose);
    }
    function standAt(x, z, facing = player.heading) {
        scene.attach(avatar);
        avatar.scale.setScalar(S);
        player.place(x, z, facing);
        setPose(avatar, 'idle');
    }
    function lookFrom(focus, yaw, pitch = 0.25, distance = 6, limits = { min: 2.5, max: 16 }) {
        rig.override = { focus, limits };
        rig.setView({ yaw, pitch, distance });
    }
    function release() { rig.override = null; rig.firstPerson = null; ui.setControls(null); ui.setVignette(false); time.fast = false; }

    /** Ends the active activity (if any) and gives control back. */
    function stop() {
        if (!active) return;
        const a = active;
        active = null;
        a.end?.();
        release();
        if (player.mode === 'busy') player.setBusy(false);
    }
    function begin(def) {
        stop();
        pending = null;
        ctx.input.consumeAction();          // a stale tap or Space press shouldn't fire the first action
        active = def;
        player.setBusy(true);
        def.start();
    }

    // ---------- seats: benches and campfire logs ----------
    function sitActivity(anchor, { id, label, camera, after }) {
        return {
            id, label,
            start() {
                seat(anchor);
                camera();
                ui.setControls(['Hold <kbd>Space</kbd> to let time pass', '<kbd>E</kbd> stand up']);
                markDone(id);
                after?.();
            },
            update() { time.fast = ctx.input.held(' ') || ui.holdingTime; },
            end() {
                const f = anchor.front ?? (() => { const d = facingVec(anchor.facing); return { x: anchor.x + d.x * 1.1, z: anchor.z + d.z * 1.1 }; })();
                standAt(f.x, f.z, anchor.facing);
            },
        };
    }

    // ---------- fishing ----------
    const bobber = new THREE.Group();
    bobber.add(new THREE.Mesh(new THREE.SphereGeometry(0.09, 8, 6), mat(0xffffff)));
    const bobTop = new THREE.Mesh(new THREE.SphereGeometry(0.09, 8, 6, 0, Math.PI * 2, 0, Math.PI / 2), mat(0xe8402f));
    bobber.add(bobTop);
    bobber.visible = false;
    scene.add(bobber);
    const lineGeo = new THREE.BufferGeometry().setFromPoints(Array.from({ length: 16 }, () => v3()));
    const line = new THREE.Line(lineGeo, new THREE.LineBasicMaterial({ color: 0xf2f2f2, transparent: true, opacity: 0.7 }));
    line.frustumCulled = false;
    line.visible = false;
    scene.add(line);
    const tip = v3(), bobPos = v3();
    function drawLine(sag) {
        avatar.userData.rodTip.getWorldPosition(tip);
        const p = lineGeo.attributes.position;
        for (let i = 0; i < 16; i++) {
            const k = i / 15;
            p.setXYZ(i, tip.x + (bobPos.x - tip.x) * k, tip.y + (bobPos.y - tip.y) * k - Math.sin(Math.PI * k) * sag, tip.z + (bobPos.z - tip.z) * k);
        }
        p.needsUpdate = true;
    }
    function rollFish() {
        const night = isNight(time.hours);
        const pool = FISH.filter(f => (!f.night || night) && (!f.day || !night));
        let r = Math.random() * pool.reduce((s, f) => s + f.w, 0);
        const fish = pool.find(f => (r -= f.w) < 0) ?? pool[0];
        const size = Math.round(fish.size[0] + Math.random() * Math.random() * (fish.size[1] - fish.size[0]));
        return { fish, size };
    }
    const fishing = {
        id: 'fish', label: 'Go fishing',
        state: 'ready', timer: 0, castT: 0,
        start() {
            const a = anchors.fishing;
            standAt(a.x, a.z, a.facing);
            player.setBusy(true);
            avatar.userData.rod.visible = true;
            setPose(avatar, 'fish');
            this.state = 'ready';
            lookFrom(new THREE.Vector3(a.x, a.y + 1.0, a.z).addScaledVector(facingVec(a.facing), 5), a.facing + Math.PI + 0.6, 0.32, 12);
            ui.setControls(['<kbd>Space</kbd> or tap to cast', '<kbd>E</kbd> put the rod away']);
        },
        action() {
            const a = anchors.fishing;
            if (this.state === 'ready') {
                this.state = 'cast';
                this.castT = 0;
                setPose(avatar, 'cast');
                audio.play('cast');
                const d = 9 + Math.random() * 6, side = (Math.random() - 0.5) * 6, f = facingVec(a.facing);
                this.target = new THREE.Vector3(a.x + f.x * d + f.z * side, 0, a.z + f.z * d - f.x * side);
            } else if (this.state === 'bite') {
                const { fish, size } = rollFish();
                this.state = 'reel';
                this.timer = 0.9;
                setPose(avatar, 'reel');
                audio.play('reel');
                life.splash(bobPos.x, bobPos.z, 0.8, ctx.t);
                const rec = journal.fish[fish.id] ?? { count: 0, best: 0 };
                const isNew = rec.count === 0;
                rec.count++;
                const best = size > rec.best;
                rec.best = Math.max(rec.best, size);
                journal.fish[fish.id] = rec;
                journal.catches++;
                setTimeout(() => {
                    ui.card({
                        icon: fish.id === 'boot' ? '🥾' : '🐟',
                        kicker: isNew ? `New · ${fish.rarity}` : fish.rarity,
                        title: fish.name,
                        subtitle: fish.id === 'boot' ? 'Well, it counts.' : `${size} cm${best && !isNew ? ' · personal best' : ''}`,
                        body: fish.note,
                        foot: `${Object.keys(journal.fish).length} of ${FISH.length} in your journal · released back to the sea`,
                    });
                    audio.play('chime', isNew ? [72, 76, 79, 84, 88] : [76, 79, 84]);
                }, 700);
                markDone('fish');
                ui.journalChanged();
            } else if (this.state === 'wait') {
                // Reeling in early: nothing on the line.
                this.state = 'ready';
                setPose(avatar, 'fish');
                bobber.visible = line.visible = false;
                ui.setControls(['<kbd>Space</kbd> or tap to cast', '<kbd>E</kbd> put the rod away']);
            }
        },
        update(dt, t) {
            if (ctx.input.consumeAction()) this.action();
            const a = anchors.fishing;
            if (this.state === 'cast') {
                this.castT += dt / 0.7;
                if (this.castT > 0.35 && !bobber.visible) { bobber.visible = line.visible = true; setPose(avatar, 'fish'); }
                if (bobber.visible) {
                    const k = Math.min(1, (this.castT - 0.35) / 0.65);
                    avatar.userData.rodTip.getWorldPosition(tip);
                    bobPos.lerpVectors(tip, this.target, k);
                    bobPos.y = THREE.MathUtils.lerp(tip.y, water.heightAt(this.target.x, this.target.z, t), k) + Math.sin(Math.PI * k) * 3;
                }
                if (this.castT >= 1) {
                    this.state = 'wait';
                    this.timer = 3 + Math.random() * 8;
                    life.splash(this.target.x, this.target.z, 0.4, t);
                    audio.play('plip');
                    ui.setControls(['Wait for a bite…', '<kbd>Space</kbd> reel in', '<kbd>E</kbd> put the rod away']);
                }
            } else if (this.state === 'wait' || this.state === 'bite') {
                this.timer -= dt;
                const wy = water.heightAt(this.target.x, this.target.z, t);
                bobPos.set(this.target.x, wy + Math.sin(t * 2.2) * 0.03, this.target.z);
                if (this.state === 'wait' && this.timer <= 0) {
                    this.state = 'bite';
                    this.timer = 1.4;
                    audio.play('bite');
                    life.float('❗', bobPos.clone().add(new THREE.Vector3(0, 0.8, 0)), { size: 0.9, rise: 0.3, life: 1.2 });
                    ui.setControls(['<b>A bite! Press <kbd>Space</kbd> now</b>']);
                }
                if (this.state === 'bite') {
                    bobPos.y -= 0.12 + Math.abs(Math.sin(t * 18)) * 0.08;
                    if (this.timer <= 0) {
                        this.state = 'wait';
                        this.timer = 3 + Math.random() * 7;
                        ui.toast({ icon: '〰️', title: 'It got away', body: 'Patience. Another will come.' });
                        ui.setControls(['Wait for a bite…', '<kbd>Space</kbd> reel in', '<kbd>E</kbd> put the rod away']);
                    }
                }
            } else if (this.state === 'reel') {
                this.timer -= dt;
                avatar.userData.rodTip.getWorldPosition(tip);
                bobPos.lerp(tip, Math.min(1, dt * 4));
                if (this.timer <= 0) {
                    this.state = 'ready';
                    bobber.visible = line.visible = false;
                    setPose(avatar, 'fish');
                    ui.setControls(['<kbd>Space</kbd> or tap to cast', '<kbd>E</kbd> put the rod away']);
                }
            }
            bobber.position.copy(bobPos);
            if (line.visible) drawLine(this.state === 'wait' ? 0.6 : 0.2);
        },
        end() {
            avatar.userData.rod.visible = false;
            bobber.visible = line.visible = false;
            setPose(avatar, 'idle');
            player.setBusy(false);
        },
    };

    // ---------- hammock (stargazing, wishes) ----------
    const hammock = {
        id: 'hammock', label: 'Lie in the hammock',
        start() {
            const h = anchors.hammock;
            h.sway.attach(avatar);
            avatar.position.set(-0.95, -0.42, 0);
            avatar.rotation.set(-Math.PI / 2, 0, -Math.PI / 2, 'XYZ');
            setPose(avatar, 'lie');
            ctx.env.hammockSwing = 0.1;
            ctx.env.stargazing = true;
            const eye = new THREE.Vector3(h.x, h.y + 0.9, h.z);
            rig.firstPerson = { eye, yaw: h.lookYaw, pitch: 0.18, fov: 60, maxPitch: 1.35 };     // out over the sea, sky above
            ui.setControls([isNight(time.hours) ? 'Watch for shooting stars' : 'Watch the clouds drift by', 'Drag to look · hold <kbd>Space</kbd> to let time pass', '<kbd>E</kbd> get up']);
            markDone('hammock');
        },
        update(dt, t) {
            const h = anchors.hammock;
            rig.firstPerson.eye.set(h.x, h.y + 0.75 + Math.sin(t * 0.9) * 0.05, h.z);
            const star = life.shootingStar;
            if (star && !this.prompted) { this.prompted = true; ui.setControls(['<b>✨ A shooting star! Press <kbd>W</kbd> to make a wish</b>', '<kbd>E</kbd> get up']); }
            if (!star && this.prompted) { this.prompted = false; ui.setControls(['Watch for shooting stars', 'Hold <kbd>Space</kbd> to let time pass', '<kbd>E</kbd> get up']); }
            if (star && ctx.input.held('w') && !this.wished) {
                this.wished = true;
                journal.wishes++;
                audio.play('wish');
                ui.toast({ icon: '✨', title: 'Wish made', body: 'We won’t tell. ' + (journal.wishes > 1 ? `(${journal.wishes} wishes so far)` : '') });
                markDone('wish');
            }
            if (!star) this.wished = false;
            time.fast = (ctx.input.held(' ') || ui.holdingTime);
        },
        end() {
            ctx.env.hammockSwing = 0.06;
            ctx.env.stargazing = false;
            const f = anchors.hammock.front;
            standAt(f.x, f.z, anchors.hammock.yaw + Math.PI);
        },
    };

    // ---------- rope swing ----------
    const swing = {
        id: 'swing', label: 'Swing from the old tree',
        amp: 0.05, phase: 0, lastSide: 0,
        start() {
            const s = anchors.swing;
            s.group.attach(avatar);
            avatar.position.set(0, -s.seatDrop + 0.06 - SIT_DROP, -0.08);
            avatar.rotation.set(0, 0, 0);
            setPose(avatar, 'swing');
            ctx.env.swinging = true;
            this.amp = Math.abs(s.group.rotation.x) + 0.05;
            this.phase = 0;
            lookFrom(s.pivot.clone().add(new THREE.Vector3(0, -3.2, 0)), s.yaw + Math.PI / 2 + 0.25, 0.12, 9.5, { min: 4, max: 18 });
            ui.setControls(['Hold <kbd>W</kbd> or <kbd>Space</kbd> to swing higher', '<kbd>E</kbd> hop off']);
            markDone('swing');
        },
        update(dt) {
            const s = anchors.swing;
            const pumping = ctx.input.held('w') || ctx.input.held(' ') || ctx.input.held('arrowup') || ui.holdingTime;
            this.amp += pumping ? dt * 0.1 * (0.68 - this.amp) * 1.6 : -dt * 0.03 * this.amp;
            this.amp = THREE.MathUtils.clamp(this.amp, 0, 0.62);
            this.phase += dt * Math.sqrt(9.8 / s.seatDrop);
            s.group.rotation.x = Math.sin(this.phase) * this.amp;
            avatar.userData.body.rotation.x = -Math.cos(this.phase) * this.amp * 0.4;
            const side = Math.sign(Math.cos(this.phase));
            if (side !== this.lastSide && this.amp > 0.25) audio.play('creak');
            this.lastSide = side;
        },
        end() {
            ctx.env.swinging = false;
            const s = anchors.swing;
            const d = facingVec(s.yaw);
            standAt(s.pivot.x - d.x * 1.2, s.pivot.z - d.z * 1.2, s.yaw);
            s.group.rotation.x = 0;
        },
    };

    // ---------- telescope ----------
    const telescope = {
        id: 'telescope', label: 'Look through the telescope',
        start() {
            const tp = anchors.telescope;
            const back = facingVec(tp.facing).multiplyScalar(-0.8);
            standAt(tp.x + back.x, tp.z + back.z, tp.facing);
            player.setBusy(true);
            setPose(avatar, 'look');
            rig.firstPerson = { eye: tp.eye.clone(), yaw: tp.facing + Math.PI, pitch: 0.02, fov: 14, maxPitch: 0.9 };
            ui.setVignette(true);
            ui.setControls(['Drag to look · scroll to zoom', '<kbd>E</kbd> step back']);
            markDone('telescope');
        },
        end() { ui.setVignette(false); setPose(avatar, 'idle'); },
    };

    // ---------- lighthouse ----------
    const lighthouse = {
        id: 'lighthouse', label: 'Climb the lighthouse',
        start() {
            const L = anchors.lighthouse;
            audio.play('door');
            const ticket = (this.ticket = (this.ticket ?? 0) + 1);
            ui.fade(() => {
                if (active !== lighthouse || ticket !== this.ticket) return;     // cancelled while fading
                L.climbed = true;
                standAt(L.gallery.x, L.gallery.z, L.galleryFacing);
                player.setBusy(true);
                avatar.position.y = L.gallery.y;
                lookFrom(L.top.clone().addScaledVector(facingVec(L.galleryFacing), 3), L.galleryFacing + Math.PI + 0.55, 0.14, 10, { min: 4, max: 30 });
                ui.setControls(['Drag to look around · scroll to zoom', '<kbd>E</kbd> climb down']);
                markDone('lighthouse');
            });
        },
        end() {
            const L = anchors.lighthouse;
            this.ticket = (this.ticket ?? 0) + 1;
            ui.fade(() => {
                L.climbed = false;
                standAt(L.door.x, L.door.z, L.door.facing + Math.PI);
                rig.setView({ distance: 9, pitch: 0.42 }, true);
            });
        },
    };

    // ---------- short actions: lantern, stone, flower, shell, cat ----------
    function quick(pose, seconds, then) {
        player.setBusy(true);
        setPose(avatar, pose);
        busyUntil = ctx.t + seconds;
        pending = then;
    }
    let pending = null;

    function releaseLantern() {
        const f = facingVec(player.heading);
        quick('release', 1.4, () => {});
        setTimeout(() => {
            const p = avatar.position.clone().add(new THREE.Vector3(f.x * 0.4, 1.9, f.z * 0.4));
            life.releaseLantern(p);
            audio.play('whoosh');
            journal.lanterns++;
            markDone('lantern');
            if (journal.lanterns > 1) ui.toast({ icon: '🏮', title: 'Lantern released', body: `${journal.lanterns} lanterns sent up so far.` });
        }, 600);
    }

    const stone = new THREE.Mesh(new THREE.DodecahedronGeometry(0.07, 0), mat(0x8d8a86));
    stone.scale.y = 0.45;
    stone.visible = false;
    scene.add(stone);
    let skipping = null;
    function skipStone() {
        quick('throw', 0.5, () => {});
        const f = facingVec(player.heading);
        // Mostly 2–6 skips; a lucky throw goes much further.
        const r = Math.random();
        const skips = r > 0.97 ? 12 + Math.floor(Math.random() * 6) : r > 0.85 ? 7 + Math.floor(Math.random() * 4) : 1 + Math.floor(Math.random() * 6);
        setTimeout(() => {
            stone.visible = true;
            skipping = { pos: avatar.position.clone().add(new THREE.Vector3(f.x * 0.5, 1.1, f.z * 0.5)), dir: f, hop: 0, skips, k: 0, len: 4.5, h: 0.9 };
            audio.play('whoosh');
        }, 260);
    }
    function updateStone(dt, t) {
        if (!skipping) return;
        const s = skipping;
        s.k += dt / (0.25 + s.len * 0.05);
        const k = Math.min(1, s.k);
        stone.position.set(s.pos.x + s.dir.x * s.len * k, s.pos.y + (water.heightAt(s.pos.x, s.pos.z, t) - s.pos.y) * k + Math.sin(Math.PI * k) * s.h, s.pos.z + s.dir.z * s.len * k);
        stone.rotation.y += dt * 25;
        if (s.k >= 1) {
            s.pos.copy(stone.position);
            s.hop++;
            const wet = heightAt(stone.position.x, stone.position.z) < -0.15;
            if (s.hop > s.skips || !wet) {
                life.splash(stone.position.x, stone.position.z, wet ? 0.5 : 0.2, t);
                audio.play(wet ? 'plip' : 'step', 'sand');
                stone.visible = false;
                const n = s.hop - 1;
                const best = n > journal.bestSkip;
                journal.bestSkip = Math.max(journal.bestSkip, n);
                ui.toast({ icon: '🪨', title: n === 0 ? 'Plop.' : `${n} skip${n === 1 ? '' : 's'}!`, body: best && n > 0 ? 'A new personal best.' : `Best so far: ${journal.bestSkip}` });
                if (n > 0) markDone('skip');
                skipping = null;
                return;
            }
            life.splash(stone.position.x, stone.position.z, 0.35, t);
            audio.play('skip');
            s.k = 0;
            s.len *= 0.82;
            s.h *= 0.7;
            s.pos.y = water.heightAt(s.pos.x, s.pos.z, t);
        }
    }
    function nearShoreline() {
        const p = avatar.position, f = facingVec(player.heading);
        const here = surfaceKind(p.x, p.z);
        if (here !== 'sand' && here !== 'water' && here !== 'wood') return false;
        return heightAt(p.x + f.x * 5, p.z + f.z * 5) < -0.3 && heightAt(p.x + f.x * 10, p.z + f.z * 10) < -0.6;
    }

    // Planted flowers: kept per visitor, re-grown on load.
    const planted = new THREE.InstancedMesh(flowerGeometry(true), windify(new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 0.8 }), { height: 0.8, sway: 0.1, push: true }), 120);
    planted.count = 0;
    planted.castShadow = true;
    scene.add(planted);
    const PETALS = [0xff7aa8, 0xffd84a, 0xffffff, 0xb48cff, 0xff8f5a, 0x7ec8ff, 0xff5a6e];
    const growing = [];
    const dummy = new THREE.Object3D();
    function setFlower(i, [x, z, c], scale) {
        dummy.position.set(x, heightAt(x, z) - 0.02, z);
        dummy.rotation.set(0, (x * 13.7 + z) % 6.28, 0);
        dummy.scale.setScalar(scale);
        dummy.updateMatrix();
        planted.setMatrixAt(i, dummy.matrix);
        planted.setColorAt(i, new THREE.Color(PETALS[c % PETALS.length]));
        planted.instanceMatrix.needsUpdate = true;
        planted.instanceColor.needsUpdate = true;
    }
    save.flowers.slice(-120).forEach((f, i) => setFlower(i, f, 1));
    planted.count = Math.min(120, save.flowers.length);
    planted.computeBoundingSphere();
    function canPlant() {
        const p = avatar.position;
        return player.mode === 'walk' && isMeadow(p.x, p.z);
    }
    function plantFlower() {
        const f = facingVec(player.heading);
        const x = avatar.position.x + f.x * 0.7, z = avatar.position.z + f.z * 0.7;
        quick('crouch', 0.9, () => {
            const c = journal.planted % PETALS.length;
            save.flowers.push([Math.round(x * 100) / 100, Math.round(z * 100) / 100, c]);
            if (save.flowers.length > 120) save.flowers.shift();
            const i = Math.min(planted.count, 119);
            planted.count = Math.min(120, planted.count + 1);
            growing.push({ i, f: [x, z, c], k: 0 });
            journal.planted++;
            audio.play('pop');
            life.float('✨', new THREE.Vector3(x, heightAt(x, z) + 0.8, z), { size: 0.5 });
            planted.computeBoundingSphere();
            if (journal.planted === 1 || journal.planted % 10 === 0) ui.toast({ icon: '🌷', title: 'Flower planted', body: journal.planted === 1 ? 'It’ll be here next time you visit.' : `${journal.planted} flowers in your garden.` });
            markDone('plant');
        });
    }

    // Seashells: fixed spots on the sand (deterministic, so they're the same on every visit).
    const shells = [];
    {
        const rand = rng(1234);
        const scallop = new THREE.CylinderGeometry(0.16, 0.16, 0.05, 9, 1, false, 0, Math.PI);
        const spiral = new THREE.ConeGeometry(0.09, 0.26, 7);
        let tries = 0;
        while (shells.length < 12 && tries++ < 4000) {
            const c = coastPoint(rand() * 360);
            const inward = 1.5 + rand() * 5;
            const x = c.x - c.nx * inward, z = c.z - c.nz * inward, h = heightAt(x, z);
            if (h < 0.08 || h > 1.2 || !isBeach(x, z)) continue;
            if (shells.some(s => (s.x - x) ** 2 + (s.z - z) ** 2 < 400)) continue;
            if (Math.hypot(x - PLACES.pier.x, z - PLACES.pier.z) < 5) continue;
            const i = shells.length;
            const m = new THREE.Mesh(i % 2 ? scallop : spiral, mat(SHELL_COLORS[i], { roughness: 0.5 }));
            m.position.set(x, h + 0.05, z);
            m.rotation.set(i % 2 ? 0 : Math.PI / 2.3, rand() * 6, 0);
            m.castShadow = true;
            const collected = journal.shells.includes(i);
            m.visible = !collected;
            scene.add(m);
            shells.push({ i, x, z, mesh: m, collected, next: rand() * 4 });
        }
    }
    function updateShells(dt, t) {
        for (const s of shells) {
            if (s.collected) continue;
            s.next -= dt;
            if (s.next <= 0) {
                s.next = 2.5 + Math.random() * 3;
                if (avatar.position.distanceToSquared(s.mesh.position) < 50 * 50) life.float('✨', s.mesh.position.clone().add(new THREE.Vector3(0, 0.25, 0)), { size: 0.35, rise: 0.25, life: 1.1 });
            }
        }
    }
    function collectShell(s) {
        quick('crouch', 0.6, () => {
            s.collected = true;
            s.mesh.visible = false;
            journal.shells.push(s.i);
            audio.play('shell');
            const n = journal.shells.length;
            ui.card({ icon: '🐚', kicker: `Seashell ${n} of 12`, title: SHELL_NAMES[s.i], subtitle: n === 12 ? 'You found every one!' : 'Added to your journal', body: n === 12 ? 'A full collection. The island is impressed.' : 'There are more hiding along the beaches.' });
            if (n === 12) markDone('shells');
            else markSeen('shells');
            ui.journalChanged();
        });
    }

    function petCat() {
        const cat = life.cat;
        const cp = cat.group.position;
        player.heading = Math.atan2(cp.x - avatar.position.x, cp.z - avatar.position.z);
        avatar.rotation.y = player.heading;
        quick('crouch', 1.3, () => {});
        const following = cat.pet(ctx.t);
        audio.play('meow');
        setTimeout(() => audio.play('purr'), 500);
        journal.pets++;
        if (following) ui.toast({ icon: '🐈', title: 'The cat likes you', body: 'It’ll follow you around for a while. Pet it again to let it nap.' });
        markDone('cat');
    }

    function readLetter() {
        quick('idle', 0.2, () => {});
        audio.play('door');
        anchors.mailbox.flag.rotation.z = -1.4;
        ui.letter();
        markDone('mail');
    }

    // ---------- career trail and gallery ----------
    journal.read ??= {};
    function face(spot) {
        player.heading = spot.facing;
        avatar.rotation.y = spot.facing;
    }
    function readCompany(spot) {
        face(spot);
        quick('read', 0.3, () => {});
        audio.play('click');
        const co = spot.company;
        ui.info({
            kicker: `Chapter ${spot.index + 1} of ${spot.total} · ${co.period}`,
            title: co.company,
            build(el, h) {
                for (const r of co.roles) {
                    el.append(h('div', { class: 'info-role' },
                        h('h3', {}, r.role),
                        h('div', { class: 'info-meta' }, [r.employmentType, r.location, r.period].filter(Boolean).join(' · ')),
                        r.highlights?.length ? h('ul', {}, r.highlights.map(x => h('li', {}, x))) : null));
                }
                const next = spot.index + 1 < spot.total ? 'The next chapter is further up the path.' : 'You’ve reached today. The view from the lookout is worth it.';
                el.append(h('p', { class: 'info-progress' }, next), h('div', { class: 'info-actions' }, h('a', { class: 'chip', href: '/cv' }, 'Full CV')));
            },
        });
        journal.read[`career:${co.company}`] = true;
        const read = showcase.career.filter(c => journal.read[`career:${c.company.company}`]).length;
        if (read === showcase.career.length) markDone('career');
        else { markSeen('career'); ui.toast({ icon: '🧭', title: `${read} of ${showcase.career.length} chapters read`, body: 'Keep climbing.' }); }
    }
    function viewProject(spot) {
        face(spot);
        quick('look', 0.3, () => {});
        audio.play('click');
        const p = spot.project;
        ui.info({
            kicker: `Exhibit ${spot.index + 1} of ${spot.total} · ${p.category}`,
            title: p.title,
            build(el, h) {
                if (p.timeline) el.append(h('div', { class: 'info-meta' }, p.timeline));
                if (p.metric) el.append(h('div', { class: 'info-metric' }, p.metric));
                if (p.summary) el.append(h('p', {}, p.summary));
                if (p.techStack?.length) el.append(h('ul', { class: 'info-chips' }, p.techStack.map(t => h('li', {}, t))));
                const actions = h('div', { class: 'info-actions' }, h('a', { class: 'btn-primary', href: `/projects/${encodeURIComponent(p.slug)}` }, 'Read the case study →'));
                if (p.projectUrl) actions.append(h('a', { class: 'chip', href: p.projectUrl, target: '_blank', rel: 'noopener' }, 'Live'));
                if (p.gitHubUrl) actions.append(h('a', { class: 'chip', href: p.gitHubUrl, target: '_blank', rel: 'noopener' }, 'Source'));
                el.append(actions);
            },
        });
        journal.read[`project:${p.slug}`] = true;
        const seen = showcase.gallery.filter(g => journal.read[`project:${g.project.slug}`]).length;
        if (seen === showcase.gallery.length) markDone('gallery');
        else markSeen('gallery');
    }

    function drive() {
        stop();
        player.enterCar();
        ui.setControls(['<kbd>W A S D</kbd> drive · <kbd>Space</kbd> brake', '<kbd>E</kbd> get out (when stopped)']);
        markDone('drive');
    }

    // ---------- prompts ----------
    const near = (pos, r) => (avatar.position.x - pos.x) ** 2 + (avatar.position.z - pos.z) ** 2 < r * r;
    const lanternSpot = () => near(anchors.campfire.center, 7) || near(anchors.hillTop, 9);

    /** Every interactable that could be offered right now, with its distance. */
    function candidates() {
        const list = [];
        const p = avatar.position;
        const add = (id, pos, radius, label, run, unavailable) => {
            const d = Math.hypot(p.x - pos.x, p.z - pos.z);
            if (d < radius) list.push({ id, d, label, run, unavailable, pos });
        };
        add('mail', anchors.mailbox, 2.4, 'Read the letter', readLetter);
        for (const c of showcase.career) add('career', c, 2.4, `Read: ${c.company.company}`, () => readCompany(c));
        for (const g of showcase.gallery) add('gallery', g, 2.2, `View: ${g.project.title}`, () => viewProject(g));
        add('fish', anchors.fishing, 3, 'Go fishing', () => begin(fishing));
        anchors.campfire.seats.forEach(s => add('campfire', s, 1.8, 'Sit by the fire', () => begin(sitActivity(s, {
            id: 'campfire', label: 'Sit by the fire',
            camera: () => lookFrom(anchors.campfire.center.clone().add(new THREE.Vector3(0, 0.8, 0)), s.facing + Math.PI + 0.75, 0.3, 8.5),
        }))));
        add('hammock', anchors.hammock, 2.6, 'Lie in the hammock', () => begin(hammock));
        add('swing', { x: anchors.swing.pivot.x, z: anchors.swing.pivot.z }, 2.2, 'Sit on the swing', () => begin(swing));
        add('telescope', anchors.telescope, 2, 'Look through the telescope', () => begin(telescope));
        add('lighthouse', anchors.lighthouse.door, 2.6, 'Climb the lighthouse', () => begin(lighthouse));
        anchors.benches.forEach(b => add('bench', b, 1.8, 'Sit on the bench', () => begin(sitActivity(b, {
            id: 'bench', label: 'Sit on the bench',
            camera: () => lookFrom(new THREE.Vector3(b.x, b.y + 0.9, b.z).addScaledVector(facingVec(b.facing), 2.5), b.facing + Math.PI + 0.4, 0.16, 5.5),
        }))));
        if (lanternSpot()) {
            const pos = near(anchors.campfire.center, 7) ? anchors.campfire.center : anchors.hillTop;
            if (isNight(time.hours)) add('lantern', avatar.position, 1, 'Release a sky lantern', releaseLantern);
            else add('lantern', pos, 9, 'Come back after sunset to release a lantern', null, true);
        }
        add('cat', life.cat.group.position, 1.8, life.cat.following ? 'Pet the cat (let it nap)' : 'Pet the cat', petCat);
        add('drive', anchors.car.position, 3.4, 'Take the car for a drive', drive);
        for (const s of shells) if (!s.collected) add('shells', s.mesh.position, 1.6, 'Pick up the shell', () => collectShell(s));
        if (nearShoreline() && !skipping) add('skip', avatar.position, 1, 'Skip a stone', skipStone);
        // Ambient options (skipping stones, the lantern hint) yield to anything placed nearby.
        const rank = c => (c.unavailable ? 2 : 0) + (c.id === 'skip' || c.id === 'lantern' ? 1 : 0);
        return list.sort((a, b) => rank(a) - rank(b) || a.d - b.d);
    }

    let offered = null, stuckFor = 0;
    function update(dt, t) {
        updateStone(dt, t);
        updateShells(dt, t);
        for (let i = growing.length - 1; i >= 0; i--) {
            const g = growing[i];
            g.k = Math.min(1, g.k + dt / 1.1);
            const e = 1 - Math.pow(1 - g.k, 3);
            setFlower(g.i, g.f, e * (1 + Math.sin(g.k * Math.PI) * 0.15));
            if (g.k >= 1) growing.splice(i, 1);
        }
        if (pending && t >= busyUntil) {
            const then = pending;
            pending = null;
            setPose(avatar, 'idle');
            player.setBusy(false);
            then();
        }
        if (active) {
            active.update?.(dt, t);
            offered = null;
            ui.prompt(null);
            return;
        }
        if (player.mode === 'drive') {
            const slow = Math.abs(player.car.speed) < 3;
            ui.prompt(slow ? { key: 'E', label: 'Get out' } : null);
            offered = slow ? { run: () => { if (player.exitCar()) ui.setControls(null); } } : null;
            return;
        }
        if (player.mode === 'busy' && !pending && !document.getElementById('fade').classList.contains('on')) {
            stuckFor += dt;
            if (stuckFor > 0.6) { stuckFor = 0; standAt(avatar.position.x, avatar.position.z); release(); player.setBusy(false); }
        } else stuckFor = 0;
        if (player.mode !== 'walk') { ui.prompt(null); return; }
        const list = candidates();
        for (const c of list) markSeen(c.id);
        offered = list[0] ?? null;
        ui.prompt(offered ? { key: offered.unavailable ? null : 'E', label: offered.label, dim: !!offered.unavailable } : null);
        ui.setPlantable(canPlant());
    }

    return {
        update,
        get active() { return active; },
        /** E / the prompt button: use what's offered, or leave the current activity. */
        interact() {
            if (active) { stop(); return true; }
            if (offered && !offered.unavailable && offered.run) { offered.run(); return true; }
            return false;
        },
        plant() { if (canPlant()) plantFlower(); },
        stop,
        /** Tap on the ground: if it's next to something to do, walk over and do it. */
        tapNear(x, z) {
            if (active || player.mode !== 'walk') return false;
            const spots = [
                ['fish', anchors.fishing], ['mail', anchors.mailbox], ['hammock', anchors.hammock], ['telescope', anchors.telescope],
                ['lighthouse', anchors.lighthouse.door], ['drive', anchors.car.position], ['swing', anchors.swing.pivot], ['cat', life.cat.group.position],
                ...anchors.benches.map(b => ['bench', b]), ...anchors.campfire.seats.map(s => ['campfire', s]),
                ...showcase.career.map(c => ['career', c]), ...showcase.gallery.map(g => ['gallery', g]),
                ...shells.filter(s => !s.collected).map(s => ['shells', s.mesh.position]),
            ];
            let best = null;
            for (const [id, pos] of spots) {
                const d = Math.hypot(pos.x - x, pos.z - z);
                if (d < 3 && (!best || d < best.d)) best = { id, pos, d };
            }
            if (!best) return false;
            player.walkTo(best.pos.x, best.pos.z, () => {
                const c = candidates().find(c => c.id === best.id && !c.unavailable);
                c?.run?.();
            });
            return true;
        },
        shells,
        markDone,
    };
}
