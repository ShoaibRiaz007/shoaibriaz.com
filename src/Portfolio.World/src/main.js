// Entry point: builds the island, shows the title fly-around, then hands control to the visitor. Owns the
// renderer, quality scaling, the clock, saving, and the frame loop that ticks every system.
import * as THREE from 'three';
import './style.css';
import { buildTerrain, bakeDepthTexture, bakeShoreDistance, groundAt, PLACES } from './terrain.js';
import { createSky } from './sky.js';
import { createWater } from './water.js';
import { buildProps } from './props.js';
import { buildShowcase } from './showcase.js';
import { buildVegetation, WIND } from './vegetation.js';
import { createLife } from './life.js';
import { createInput, createPlayer, createRig } from './player.js';
import { createActivities } from './activities.js';
import { createAudio } from './audio.js';
import { createUI, firstNameOf } from './ui.js';
import { clearSave, loadSave, writeSave } from './save.js';
import { reportVisit } from './telemetry.js';

const $ = id => document.getElementById(id);
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
const coarse = matchMedia('(pointer: coarse)').matches;
const debug = new URLSearchParams(location.search).has('debug');
const progress = p => { $('loaderFill').style.width = `${Math.round(p * 100)}%`; };
const nextFrame = () => new Promise(r => setTimeout(r, 0));     // let the loader paint between build steps

function webglAvailable() {
    try { const c = document.createElement('canvas'); return !!(c.getContext('webgl2') || c.getContext('webgl')); } catch { return false; }
}

async function boot() {
    if (!webglAvailable()) {
        $('loader').querySelector('.loader-title').textContent = 'Your browser can’t show 3D here. Opening the website…';
        setTimeout(() => { location.href = '/'; }, 2500);
        return;
    }
    reportVisit();   // only visitors who actually get the island count as island visits
    const save = loadSave();
    save.journal.visits++;
    progress(0.05);

    // The island works without the API. With it, the profile personalises the title, letter and back link, and
    // the experience and projects become the Career Trail and the Gallery (showcase.js).
    let profile = null, site = null;
    try {
        const res = await fetch('/api/site', { headers: { Accept: 'application/json' } });
        if (res.ok) { site = await res.json(); profile = site.profile ?? null; }
    } catch { /* offline or API down: the island still works */ }
    if (profile?.fullName) {
        const first = firstNameOf(profile.fullName);
        $('titleName').textContent = `${first}’s Island`;
        $('brandName').textContent = profile.fullName;
        document.title = `${first}’s Island — ${profile.fullName}`;
    }

    // ---- Renderer and quality ----
    let quality = save.settings.quality === 'auto' ? (coarse ? 'medium' : 'high') : save.settings.quality;
    const canvas = $('scene');
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: quality !== 'low', powerPreference: 'high-performance' });
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.shadowMap.type = THREE.PCFShadowMap;
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(50, 1, 0.1, 1200);

    const sky = createSky(scene, quality);
    progress(0.15);
    await nextFrame();
    buildTerrain(scene, quality);
    const water = createWater(scene, bakeDepthTexture(), { quality, reducedMotion });
    const shoreDistance = bakeShoreDistance();
    progress(0.35);
    await nextFrame();
    const colliders = [], clear = [];
    const showcase = buildShowcase(scene, { site, colliders, clear });     // first, so paths and plants keep clear of it
    const props = buildProps(scene, { colliders, clear });
    progress(0.5);
    await nextFrame();
    const canopies = props.canopies;
    const veg = buildVegetation(scene, { quality, clear, colliders, canopies });
    progress(0.75);
    await nextFrame();
    const life = createLife(scene, { anchors: props.anchors, water, quality });

    function applyQuality() {
        renderer.setPixelRatio(Math.min(devicePixelRatio, { high: 1.75, medium: 1.25, low: 1 }[quality]));
        renderer.shadowMap.enabled = quality !== 'low';
        sky.sun.castShadow = quality !== 'low';
        const size = quality === 'high' ? 2048 : 1024;
        if (sky.sun.shadow.mapSize.x !== size) { sky.sun.shadow.mapSize.set(size, size); sky.sun.shadow.map?.dispose(); sky.sun.shadow.map = null; }
        Object.assign(sky.sun.shadow.camera, { left: -45, right: 45, top: 45, bottom: -45, near: 1, far: 300 });
        sky.sun.shadow.camera.updateProjectionMatrix();
        veg.setGrassDensity({ high: 1, medium: 0.6, low: 0.35 }[quality]);
        resize();
    }
    const resize = () => {
        renderer.setSize(innerWidth, innerHeight, false);
        camera.aspect = innerWidth / innerHeight;
        camera.updateProjectionMatrix();
    };
    addEventListener('resize', resize);
    applyQuality();

    // ---- Systems ----
    const input = createInput(canvas, $('joystick'));
    const rig = createRig(camera, input, { reducedMotion });
    // Camera blockers: anything solid and tall enough to come between the camera and the player. Thin posts
    // and knee-high things are left out so the camera doesn't twitch past them.
    rig.blockers = [
        ...colliders.filter(c => (c.type === 'box' ? true : c.r >= 0.45) && (c.h ?? 2.4) >= 1.4)
            .map(c => ({ ...c, y0: groundAt(c.x, c.z) - 0.3, h: (c.h ?? 2.4) + 0.3 })),
        ...canopies,
    ];
    const audio = createAudio();
    audio.settings.sound = save.settings.sound;
    audio.settings.music = save.settings.music;
    const player = createPlayer(scene, {
        colliders, rig, input, carObj: props.anchors.car,
        onStep: (kind, speed) => {
            audio.play('step', kind);
            if (kind === 'water' && speed > 1) { const p = player.avatar.position; life.splash(p.x, p.z, 0.35, clock.t); }
        },
    });
    player.place(PLACES.spawn.x, PLACES.spawn.z, PLACES.cottage.facing);
    const ui = createUI({ save, audio, coarse, profile });
    const time = { hours: save.settings.time.hour, fast: false };
    const env = { night: 0, gust: 1, water, hammockSwing: 0.06, stargazing: false, swinging: false };
    const clock = { t: 0 };
    let actionPressed = false;
    const ctx = {
        scene, player, rig, life, audio, ui, anchors: props.anchors, showcase, save, water, time, env, t: 0,
        input: { held: input.held, consumeAction: () => { const a = actionPressed; actionPressed = false; return a; } },
    };
    const activities = createActivities(ctx);
    let entered = false, photoMode = false;

    // ---- Controls ----
    input.on('action', (k, e) => {
        if (!entered) return;
        if (photoMode) { if (k === 'escape' || k === 'p') setPhoto(false); return; }
        if (k === 'j') return ui.toggleJournal();
        if (k === 'm') return ui.toggleMap();
        if (k === 't') return ui.toggleTime();
        if (k === 'h' || k === '?') return ui.toggleHelp();
        if (ui.anyOpen()) return;
        if (k === 'p') return setPhoto(true);
        if (k === 'e' || k === 'enter') { if (e.target.closest?.('button, a')) return; activities.interact(); return; }
        if (k === 'escape' && activities.active) return activities.stop();
        if (k === 'escape' && player.mode === 'drive') return activities.interact();
        if (k === ' ') actionPressed = true;
        if (k === 'f') activities.plant();
    });
    ui.on('interact', () => activities.interact());
    ui.on('plant', () => activities.plant());

    // Tap or click: inside an activity it's the action button; otherwise walk to the ground you clicked.
    const ray = new THREE.Raycaster(), ndc = new THREE.Vector2();
    const marker = new THREE.Mesh(new THREE.RingGeometry(0.25, 0.38, 24).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0, depthWrite: false }));
    scene.add(marker);
    input.on('tap', (x, y) => {
        if (!entered || photoMode || ui.anyOpen()) return;
        if (activities.active) { actionPressed = true; return; }
        if (player.mode !== 'walk') return;
        ndc.set((x / innerWidth) * 2 - 1, -(y / innerHeight) * 2 + 1);
        ray.setFromCamera(ndc, camera);
        const o = ray.ray.origin, d = ray.ray.direction, p = new THREE.Vector3();
        let hit = null;
        for (let s = 0.5; s < 220; s += 0.5) {
            p.copy(o).addScaledVector(d, s);
            if (p.y < Math.max(groundAt(p.x, p.z), 0)) {
                let lo = s - 0.5, hi = s;
                for (let i = 0; i < 12; i++) { const m = (lo + hi) / 2; p.copy(o).addScaledVector(d, m); if (p.y < Math.max(groundAt(p.x, p.z), 0)) hi = m; else lo = m; }
                hit = p.clone();
                break;
            }
        }
        if (!hit || groundAt(hit.x, hit.z) < -0.8) return;
        if (activities.tapNear(hit.x, hit.z)) return;
        player.walkTo(hit.x, hit.z);
        marker.position.set(hit.x, groundAt(hit.x, hit.z) + 0.06, hit.z);
        marker.material.opacity = 0.9;
    });

    // ---- Photo mode ----
    function setPhoto(on) {
        photoMode = on;
        ui.photo(on);
        rig.limits = on ? { min: 1.5, max: 70 } : { min: 2.6, max: 28 };
        if (!on) rig.setView({ distance: Math.min(rig.distance, 28) });
    }
    ui.on('photo', setPhoto);
    ui.on('shutter', () => {
        tick(0);                                   // render a fresh frame, then read it back before it's cleared
        canvas.toBlob(blob => {
            if (!blob) return;
            const a = document.createElement('a');
            const now = new Date(), pad = n => String(n).padStart(2, '0');   // local time: the date the visitor sees
            const stamp = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}-${pad(now.getHours())}-${pad(now.getMinutes())}`;
            a.download = `island-${stamp}.png`;
            a.href = URL.createObjectURL(blob);
            a.click();
            setTimeout(() => URL.revokeObjectURL(a.href), 2000);
        }, 'image/png');
        audio.play('shutter');
        $('fade').style.background = '#fff';
        $('fade').classList.add('on');
        setTimeout(() => { $('fade').classList.remove('on'); setTimeout(() => { $('fade').style.background = ''; }, 450); }, 80);
        save.journal.photos++;
        activities.markDone('photo');
    });

    // ---- Time, travel, settings ----
    ui.on('getHour', () => time.hours);
    ui.on('setHour', h => { time.hours = h; save.settings.time.hour = h; });
    ui.on('timeModeChanged', () => {});
    ui.on('quality', q => { quality = q === 'auto' ? (coarse ? 'medium' : 'high') : q; applyQuality(); });
    ui.on('reset', () => { clearSave(); location.reload(); });
    const a = props.anchors;
    const front = (p, f, d) => ({ x: p.x + Math.sin(f) * d, z: p.z + Math.cos(f) * d, facing: f + Math.PI });
    ui.on('places', () => [
        { name: 'Cottage', icon: '🏡', ...PLACES.spawn, facing: PLACES.cottage.facing },
        { name: 'Pier', icon: '🎣', ...front(a.fishing, a.fishing.facing + Math.PI, 3), facing: a.fishing.facing },
        { name: 'Campfire', icon: '🔥', ...front(a.campfire.center, 0.6, 4.5) },
        { name: 'Hammock', icon: '🌴', x: a.hammock.front.x, z: a.hammock.front.z, facing: a.hammock.yaw },
        { name: 'Windmill', icon: '🌾', ...front(PLACES.windmill, -0.9, 6) },
        { name: 'Lighthouse', icon: '🗼', x: a.lighthouse.door.x, z: a.lighthouse.door.z, facing: a.lighthouse.door.facing + Math.PI },
        { name: 'Lookout', icon: '⛰️', x: a.hillTop.x, z: a.hillTop.z, facing: a.swing.yaw },
        { name: 'Garden', icon: '🥕', ...front(a.garden, PLACES.cottage.facing + Math.PI / 2, 5.5) },
        ...(showcase.career.length ? [{ name: 'Career trail', icon: '🧭', x: showcase.trailStart.x, z: showcase.trailStart.z, facing: Math.atan2(showcase.trailStart.dx, showcase.trailStart.dz) }] : []),
        ...(showcase.gallery.length ? [{ name: 'Gallery', icon: '🖼️', x: showcase.plaza.x - 12, z: showcase.plaza.z, facing: Math.PI / 2 }] : []),
    ]);
    ui.on('travel', place => {
        activities.stop();
        if (player.mode === 'drive') { player.car.speed = 0; player.exitCar(); ui.setControls(null); }
        ui.fade(() => {
            player.place(place.x, place.z, place.facing);
            rig.focus.copy(player.followPoint());
            rig.setView({ yaw: place.facing + Math.PI, pitch: 0.38, distance: 9 }, true);
        });
    });

    function updateTime(dt) {
        const t = save.settings.time;
        if (t.mode === 'local' && !time.fast && !time.frozen) {
            const d = new Date();
            time.hours = d.getHours() + d.getMinutes() / 60 + d.getSeconds() / 3600;
            return;
        }
        const rate = time.fast ? 1.2 : t.mode === 'cycle' && !time.frozen ? 1 / 60 : 0;   // cycle: one in-game hour per real minute
        time.hours = (time.hours + dt * rate) % 24;
        t.hour = time.hours;
    }

    // ---- Title fly-around, then the swoop down to the cottage ----
    let intro = null;
    const titleCam = { a: 0.8 };
    function enter() {
        entered = true;
        const follow = player.followPoint();
        rig.focus.copy(follow);
        rig.setView({ yaw: player.heading - 0.65, pitch: 0.22, distance: 7.5 }, true);    // front three-quarter: he waves hello
        intro = { k: reducedMotion ? 1 : 0, from: camera.position.clone(), look: new THREE.Vector3(0, 6, 0) };
        player.avatar.userData.waveUntil = clock.t + (reducedMotion ? 2 : 4.5);
        if (!save.welcomed) {
            save.welcomed = true;
            ui.setControls(coarse ? ['Joystick to walk · drag to look · tap the ground to walk there'] : ['<kbd>W A S D</kbd> walk · drag to look · <kbd>E</kbd> interact', 'Press <kbd>H</kbd> for help']);
            setTimeout(() => { if (!activities.active && player.mode === 'walk') ui.setControls(null); }, 12000);
            setTimeout(() => ui.toast({ icon: '📖', title: 'Your journal', body: coarse ? 'Tap 📖 to see things to do.' : 'Press J to see things to do.' }), 3500);
            setTimeout(() => ui.toast({ icon: '✉️', title: 'You’ve got mail', body: 'The mailbox by the cottage has its flag up.' }), 9000);
        } else {
            setTimeout(() => ui.toast({ icon: '👋', title: 'Welcome back', body: `Visit ${save.journal.visits}. Your flowers kept growing.` }), 2500);
        }
        lastSave = clock.t;
    }

    // ---- Frame loop ----
    const timer = new THREE.Timer();
    const focus = new THREE.Vector3(), follow = new THREE.Vector3(), wp = new THREE.Vector3();
    let running = true, lastSave = 0, clockShown = -1, fpsFrames = 0, fpsTime = 0, fpsChecked = false;
    document.addEventListener('visibilitychange', () => {
        running = !document.hidden;
        if (running) { timer.reset(); audio.resume(); requestAnimationFrame(frame); }
        else { audio.suspend(); writeSave(save); }
    });
    addEventListener('pagehide', () => { writeSave(save); audio.suspend(); });
    // Some hosts hide the page without telling it (embedded panes, minimised windows): frames simply stop.
    // If no frame has rendered for a moment, go quiet until they resume.
    let lastFrameAt = performance.now(), stalled = false;
    setInterval(() => {
        if (!stalled && performance.now() - lastFrameAt > 700) { stalled = true; audio.suspend(); }
    }, 400);

    function frame(now) {
        if (!running) return;
        lastFrameAt = performance.now();
        if (stalled) { stalled = false; audio.resume(); }
        timer.update(now);
        const dt = Math.min(timer.getDelta(), 1 / 15);
        tick(dt);
        if (entered && !fpsChecked && save.settings.quality === 'auto') {
            fpsFrames++; fpsTime += dt;
            if (fpsTime > 4) {
                fpsChecked = true;
                const fps = fpsFrames / fpsTime;
                if (fps < 42 && quality !== 'low') { quality = quality === 'high' ? 'medium' : 'low'; applyQuality(); fpsChecked = quality === 'low'; fpsFrames = fpsTime = 0; }
            }
        }
        requestAnimationFrame(frame);
    }

    function tick(dt) {
        clock.t += dt;
        const t = clock.t;
        ctx.t = t;
        updateTime(dt);
        env.gust = 0.75 + Math.sin(t * 0.11) * 0.25 + Math.sin(t * 0.37) * 0.1;
        WIND.uTime.value = t;
        WIND.uGust.value = env.gust;

        if (entered) {
            player.update(dt, t);
            activities.update(dt, t);
            rig.update(dt, player.followPoint(follow));
            if (intro && intro.k < 1) {
                intro.k = Math.min(1, intro.k + dt / 3.2);
                const e = 1 - Math.pow(1 - intro.k, 3);
                const lift = Math.sin(Math.PI * e) * 12;
                camera.position.lerpVectors(intro.from, camera.position, e);
                camera.position.y += lift * (1 - e);
                camera.lookAt(wp.lerpVectors(intro.look, rig.focus, e));
            }
            save.journal.seconds += dt;
        } else {
            // Slow orbit around the island behind the title.
            titleCam.a += dt * (reducedMotion ? 0 : 0.025);
            camera.position.set(Math.cos(titleCam.a) * 150, 46, Math.sin(titleCam.a) * 125);
            camera.lookAt(0, 4, 0);
        }
        player.worldPosition(focus);
        if (player.mode !== 'drive') player.avatar.getWorldPosition(WIND.uPlayer.value);
        else WIND.uPlayer.value.set(0, -100, 0);

        const skyState = sky.update(time.hours, entered ? focus : new THREE.Vector3(0, 0, 0), dt, t, camera.position);
        env.night = skyState.night;
        renderer.toneMappingExposure = skyState.exposure;
        water.update(t, camera.position, env.night, skyState.reflect);
        props.update(dt, t, env);
        showcase.update(dt, t, env);
        life.update(dt, t, env, camera.position, player);
        marker.material.opacity = Math.max(0, marker.material.opacity - dt * (player.tapTarget ? 0.3 : 2));

        const listener = entered ? focus : camera.position;
        const fireD = listener.distanceTo(props.anchors.campfire.center);
        audio.update(dt, {
            shore: THREE.MathUtils.clamp(1 - shoreDistance(listener.x, listener.z) / 35, 0, 1),
            height: entered ? camera.position.y : 30,
            night: env.night,
            fire: entered ? THREE.MathUtils.clamp(1 - fireD / 16, 0, 1) : 0,
            gust: env.gust,
            drive: player.mode === 'drive' ? player.car : null,
        });
        const minute = Math.floor(time.hours * 60);
        if (minute !== clockShown) { clockShown = minute; ui.setClock(time.hours); }
        ui.updateMapMarker(focus);
        ui.refreshJournalIfOpen();
        if (entered && t - lastSave > 5) { lastSave = t; writeSave(save); }
        renderer.render(scene, camera);
    }

    // ?debug: step the simulation by hand (screenshots and checks while the tab isn't painting).
    if (debug) {
        window.__island = {
            step(seconds, hold = {}) {
                const keys = Object.keys(hold);
                const realHeld = input.held;
                ctx.input.held = k => keys.includes(k) || realHeld(k);
                const n = Math.ceil(seconds * 30);
                for (let i = 0; i < n; i++) tick(1 / 30);
                ctx.input.held = realHeld;
            },
            enter() { $('enterBtn').click(); },
            move(seconds, { x = 0, y = 1, run = false } = {}) {
                const real = input.axes;
                input.axes = () => ({ x, y, run, active: true });
                this.step(seconds);
                input.axes = real;
            },
            hour(h) { time.hours = h; time.frozen = true; },        // debug only: never saved
            go(x, z, facing = 0) { player.place(x, z, facing); rig.focus.copy(player.followPoint()); },
            view(yaw, pitch, distance) { rig.setView({ yaw, pitch, distance }, true); },
            interact: () => activities.interact(),
            action: () => { actionPressed = true; },
            places: () => ui.toggleMap(),
            get state() {
                return { quality, hours: time.hours, mode: player.mode, active: activities.active?.id ?? null, pos: { ...player.avatar.position }, calls: renderer.info.render.calls, tris: renderer.info.render.triangles };
            },
            anchors: props.anchors, showcase, PLACES, camera, rig, player, activities,
        };
    }

    progress(1);
    $('loader').classList.add('done');
    setTimeout(() => $('loader').remove(), 700);
    ui.showTitle(enter);
    requestAnimationFrame(frame);
}

boot();
