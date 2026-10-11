// Everything on screen that isn't the 3D world: title screen, HUD, interaction prompt, toasts and cards, the
// journal, map, time-of-day and help panels, the letter, photo mode, fades. All text is set with textContent
// (or fixed markup we write), never from API data as HTML.
import { ACTIVITIES, FISH } from './activities.js';
import { groundColor, heightAt, pathDistance } from './terrain.js';

const $ = id => document.getElementById(id);

/** The name people call you: skips initials such as "M." in "M. Shoaib Riaz". */
export function firstNameOf(full) {
    return (full || '').split(/\s+/).find(p => p.length > 1 && !p.endsWith('.')) || 'Friend';
}

export function createUI({ save, audio, coarse, profile }) {
    const handlers = {};
    const ui = { holdingTime: false };
    const journal = save.journal;
    const firstName = firstNameOf(profile?.fullName);

    // ---------- prompt, controls, toasts, cards ----------
    const promptEl = $('prompt');
    let lastPrompt = '';
    ui.prompt = p => {
        const key = p ? `${p.key}|${p.label}|${p.dim}` : '';
        if (key === lastPrompt) return;
        lastPrompt = key;
        promptEl.hidden = !p;
        if (!p) return;
        $('promptKey').hidden = !p.key;
        $('promptKey').textContent = coarse ? 'Tap' : p.key ?? '';
        $('promptLabel').textContent = p.label;
        promptEl.classList.toggle('dim', !!p.dim);
        promptEl.disabled = !!p.dim;
    };
    promptEl.addEventListener('click', () => handlers.interact?.());

    let lastControls = '';
    ui.setControls = items => {
        const key = items ? items.join('|') : '';
        if (key === lastControls) return;
        lastControls = key;
        const el = $('controls');
        el.hidden = !items;
        el.replaceChildren(...(items ?? []).map(html => {
            const s = document.createElement('span');
            s.innerHTML = coarse ? html.replace(/<kbd>E<\/kbd>/g, 'Tap the prompt to').replace(/<kbd>Space<\/kbd>/g, 'tap') : html;   // fixed strings from our code only
            return s;
        }));
        $('holdBtn').hidden = !(coarse && items && key.includes('time pass'));
        if (coarse && items) {
            // On touch, leaving an activity is a tap on the prompt button.
            ui.prompt({ key: '✕', label: 'Leave' });
        }
    };

    ui.toast = ({ icon = '•', title, body = '' }) => {
        const box = $('toasts');
        const t = document.createElement('div');
        t.className = 'toast';
        const i = document.createElement('div');
        i.className = 'toast-icon';
        i.textContent = icon;
        const text = document.createElement('div');
        const b = document.createElement('b');
        b.textContent = title;
        const s = document.createElement('span');
        s.textContent = body;
        text.append(b, s);
        t.append(i, text);
        box.append(t);
        while (box.children.length > 3) box.firstChild.remove();
        setTimeout(() => { t.classList.add('out'); setTimeout(() => t.remove(), 450); }, 3400);
    };

    let cardTimer;
    ui.card = ({ icon, kicker, title, subtitle, body, foot = '' }) => {
        $('cardIcon').textContent = icon;
        $('cardKicker').textContent = kicker;
        $('cardTitle').textContent = title;
        $('cardSub').textContent = subtitle;
        $('cardBody').textContent = body;
        $('cardFoot').textContent = foot;
        $('card').hidden = false;
        clearTimeout(cardTimer);
        cardTimer = setTimeout(() => { $('card').hidden = true; }, 5200);
    };
    $('card').addEventListener('click', () => { $('card').hidden = true; });

    ui.fade = fn => {
        const f = $('fade');
        f.classList.add('on');
        setTimeout(() => { fn(); setTimeout(() => f.classList.remove('on'), 120); }, 470);
    };
    ui.setVignette = on => { $('scope').hidden = !on; };
    ui.setPlantable = on => { $('plantBtn').hidden = !on || !coarse; };
    $('plantBtn').addEventListener('click', () => handlers.plant?.());
    const hold = $('holdBtn');
    const holdOn = e => { e.preventDefault(); ui.holdingTime = true; hold.classList.add('on'); };
    const holdOff = () => { ui.holdingTime = false; hold.classList.remove('on'); };
    hold.addEventListener('pointerdown', holdOn);
    ['pointerup', 'pointercancel', 'pointerleave'].forEach(e => hold.addEventListener(e, holdOff));

    // ---------- panels ----------
    const dialogs = ['journal', 'map', 'timePanel', 'help', 'letter', 'info'].map($);
    dialogs.forEach(d => {
        d.addEventListener('click', e => { if (e.target === d || e.target.closest('[data-close]')) d.close(); });
        d.addEventListener('close', () => handlers.panelClosed?.());
    });
    ui.anyOpen = () => dialogs.some(d => d.open);
    ui.closeAll = () => dialogs.forEach(d => d.open && d.close());
    function toggle(d, render) {
        if (d.open) { d.close(); return; }
        ui.closeAll();
        render?.();
        d.showModal();
        audio.play('click');
    }

    // Journal
    let journalDirty = false;
    ui.journalChanged = () => {
        journalDirty = true;
        const unseen = ACTIVITIES.some(a => journal.seen[a.id] && !journal.done[a.id]);
        $('journalBadge').hidden = !unseen || $('journal').open;
    };
    function renderJournal() {
        const done = ACTIVITIES.filter(a => journal.done[a.id]).length;
        $('journalTitle').textContent = `Journal · ${done}/${ACTIVITIES.length}`;
        $('todoList').replaceChildren(...ACTIVITIES.map(a => {
            const li = document.createElement('li');
            const isDone = !!journal.done[a.id], seen = !!journal.seen[a.id];
            li.className = isDone ? 'done' : seen ? '' : 'unseen';
            const mark = document.createElement('span');
            mark.className = 'mark';
            mark.textContent = isDone ? '✓' : '';
            const text = document.createElement('div');
            const t = document.createElement('div');
            t.className = 't';
            t.textContent = a.id === 'shells' ? `${a.title} (${journal.shells.length}/12)` : a.title;
            const h = document.createElement('div');
            h.className = 'h';
            h.textContent = isDone ? '' : a.hint;
            text.append(t, h);
            li.append(mark, text);
            return li;
        }));
        $('fishList').replaceChildren(...FISH.map(f => {
            const rec = journal.fish[f.id];
            const li = document.createElement('li');
            li.className = rec ? '' : 'missing';
            const n = document.createElement('div');
            n.className = 'n';
            n.textContent = rec ? f.name : '???';
            const r = document.createElement('div');
            r.className = 'r';
            r.textContent = f.rarity + (f.night ? ' · night' : f.day ? ' · day' : '');
            const s = document.createElement('div');
            s.className = 's';
            s.textContent = rec ? (f.id === 'boot' ? `Caught ${rec.count}×. Why.` : `Best ${rec.best} cm · caught ${rec.count}×`) : 'Not caught yet';
            li.append(n, r, s);
            return li;
        }));
        const mins = Math.round(journal.seconds / 60);
        const stats = [
            ['Time on the island', mins < 60 ? `${mins} min` : `${Math.floor(mins / 60)} h ${mins % 60} min`],
            ['Visits', journal.visits],
            ['Fish caught', journal.catches],
            ['Kinds of fish', `${Object.keys(journal.fish).length} / ${FISH.length}`],
            ['Seashells', `${journal.shells.length} / 12`],
            ['Flowers planted', journal.planted],
            ['Sky lanterns', journal.lanterns],
            ['Wishes', journal.wishes],
            ['Best stone skip', journal.bestSkip],
            ['Cat pets', journal.pets],
            ['Photos', journal.photos],
        ];
        $('statsList').replaceChildren(...stats.flatMap(([k, v]) => {
            const dt = document.createElement('dt'), dd = document.createElement('dd');
            dt.textContent = k;
            dd.textContent = String(v);
            return [dt, dd];
        }));
        journalDirty = false;
        $('journalBadge').hidden = true;
    }
    document.querySelectorAll('.tab').forEach(tab => tab.addEventListener('click', () => {
        document.querySelectorAll('.tab').forEach(t => t.setAttribute('aria-selected', String(t === tab)));
        document.querySelectorAll('.tab-body').forEach(b => { b.hidden = b.dataset.body !== tab.dataset.tab; });
    }));
    $('resetBtn').addEventListener('click', () => {
        if (confirm('Clear your journal, planted flowers and settings in this browser?')) handlers.reset?.();
    });
    $('journalBtn').addEventListener('click', () => toggle($('journal'), renderJournal));

    // Map: painted once from the terrain, with pins for travel and a live marker for you.
    const MAP = { x0: -115, x1: 115, z0: -86, z1: 95 };       // same aspect as the canvas; reaches the pier's end
    const mapCanvas = $('mapCanvas');
    let mapPainted = false;
    function paintMap() {
        const g = mapCanvas.getContext('2d'), W = mapCanvas.width, H = mapCanvas.height;
        const img = g.createImageData(W, H);
        for (let j = 0; j < H; j++) for (let i = 0; i < W; i++) {
            const x = MAP.x0 + (i / W) * (MAP.x1 - MAP.x0), z = MAP.z0 + (j / H) * (MAP.z1 - MAP.z0);
            const h = heightAt(x, z);
            let r, gg, b;
            if (h < 0) {
                const d = Math.min(1, -h / 7);
                r = 140 - d * 70; gg = 205 - d * 70; b = 214 - d * 40;
                if (h > -0.4) { r = 220; gg = 238; b = 236; }                    // foam line
            } else {
                const col = groundColor(x, z, h);
                const shade = Math.max(0.7, Math.min(1.25, 1 + (heightAt(x - 1, z - 1) - h) * 0.35));     // hill shading from the north-west
                r = Math.pow(col.r, 1 / 2.2) * 255 * shade; gg = Math.pow(col.g, 1 / 2.2) * 255 * shade; b = Math.pow(col.b, 1 / 2.2) * 255 * shade;
                if (pathDistance(x, z) < 1.2) { r = 236; gg = 214; b = 168; }
            }
            // Warm paper tint so it reads as a drawn map, not a satellite photo.
            const k = (j * W + i) * 4;
            img.data[k] = r * 0.85 + 255 * 0.15 * 0.98;
            img.data[k + 1] = gg * 0.85 + 255 * 0.15 * 0.94;
            img.data[k + 2] = b * 0.85 + 255 * 0.15 * 0.85;
            img.data[k + 3] = 255;
        }
        g.putImageData(img, 0, 0);
        mapPainted = true;
    }
    const toMap = (x, z) => ({ left: `${((x - MAP.x0) / (MAP.x1 - MAP.x0)) * 100}%`, top: `${((z - MAP.z0) / (MAP.z1 - MAP.z0)) * 100}%` });
    const me = document.createElement('div');
    me.className = 'map-me';
    function renderMap() {
        if (!mapPainted) paintMap();
        const pins = $('mapPins');
        pins.replaceChildren(me);
        for (const p of handlers.places?.() ?? []) {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'map-pin';
            b.textContent = `${p.icon} ${p.name}`;
            Object.assign(b.style, toMap(p.x, p.z));
            b.addEventListener('click', () => { $('map').close(); handlers.travel?.(p); });
            pins.append(b);
        }
    }
    ui.updateMapMarker = pos => { if ($('map').open) Object.assign(me.style, toMap(pos.x, pos.z)); };
    $('mapBtn').addEventListener('click', () => toggle($('map'), renderMap));

    // Time and settings
    const fmt = h => { const hh = Math.floor(h) % 24, mm = Math.floor((h % 1) * 60); return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`; };
    function renderTime() {
        const t = save.settings.time;
        $('timeSlider').value = String(handlers.getHour?.() ?? t.hour);
        $('timeLabel').textContent = fmt(handlers.getHour?.() ?? t.hour);
        document.querySelectorAll('input[name="timeMode"]').forEach(r => { r.checked = r.value === t.mode; });
        $('musicToggle').checked = save.settings.music;
        $('qualitySelect').value = save.settings.quality;
    }
    $('timeSlider').addEventListener('input', e => { const h = Number(e.target.value); handlers.setHour?.(h); $('timeLabel').textContent = fmt(h); if (save.settings.time.mode === 'local') { save.settings.time.mode = 'fixed'; renderTime(); } });
    document.querySelectorAll('.presets [data-hour]').forEach(b => b.addEventListener('click', () => {
        handlers.setHour?.(Number(b.dataset.hour));
        if (save.settings.time.mode === 'local') save.settings.time.mode = 'cycle';
        renderTime();
    }));
    document.querySelectorAll('input[name="timeMode"]').forEach(r => r.addEventListener('change', () => { save.settings.time.mode = r.value; handlers.timeModeChanged?.(); renderTime(); }));
    $('musicToggle').addEventListener('change', e => { save.settings.music = e.target.checked; audio.settings.music = e.target.checked; audio.apply(); });
    $('qualitySelect').addEventListener('change', e => { save.settings.quality = e.target.value; handlers.quality?.(e.target.value); });
    $('timeBtn').addEventListener('click', () => toggle($('timePanel'), renderTime));
    ui.setClock = h => { $('clock').textContent = `${h >= 6 && h < 19 ? '☀️' : '🌙'} ${fmt(h)}`; };

    // Help
    const helpRows = coarse
        ? [['Move', 'Joystick, bottom left'], ['Look around', 'Drag anywhere'], ['Zoom', 'Pinch'], ['Walk somewhere', 'Tap the ground'], ['Do things', 'Tap the white prompt'], ['Plant a flower', '🌱 button on grass']]
        : [['Move', '<kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> or arrows'], ['Run', 'Hold <kbd>Shift</kbd>'], ['Look around', 'Drag with the mouse'], ['Zoom', 'Scroll'],
            ['Walk somewhere', 'Click the ground'], ['Do things', '<kbd>E</kbd> or click the prompt'], ['Plant a flower', '<kbd>F</kbd> on grass'],
            ['Journal · Map', '<kbd>J</kbd> · <kbd>M</kbd>'], ['Time · Photo', '<kbd>T</kbd> · <kbd>P</kbd>'], ['Close / leave', '<kbd>Esc</kbd>']];
    $('helpList').replaceChildren(...helpRows.map(([k, v]) => {
        const li = document.createElement('li');
        const b = document.createElement('b');
        b.textContent = k;
        const s = document.createElement('span');
        s.innerHTML = v;      // fixed strings above
        li.append(b, s);
        return li;
    }));
    $('helpBtn').addEventListener('click', () => toggle($('help')));
    $('titleControls').innerHTML = coarse
        ? 'Joystick to walk · drag to look · tap the ground to walk there'
        : '<kbd>WASD</kbd> walk · <kbd>Shift</kbd> run · drag to look · <kbd>E</kbd> interact · click the ground to walk there';

    // The letter: a short, warm note with real contact links from the site's profile.
    ui.letter = () => {
        const body = $('letterBody');
        const p = (text, cls) => { const el = document.createElement('p'); el.textContent = text; if (cls) el.className = cls; return el; };
        const links = document.createElement('div');
        links.className = 'letter-links';
        const link = (label, href) => {
            if (!href || !/^(https?:|mailto:)/i.test(href)) return;
            const a = document.createElement('a');
            a.className = 'chip';
            a.href = href;
            a.textContent = label;
            if (!href.startsWith('mailto:')) { a.target = '_blank'; a.rel = 'noopener'; }
            links.append(a);
        };
        link('✉️ Email', profile?.email ? `mailto:${profile.email}` : '');
        link('in LinkedIn', profile?.linkedIn);
        link('GitHub', profile?.gitHub);
        link('🏠 Website', '/');
        body.replaceChildren(
            p('Hello, and welcome.'),
            p(`I’m ${firstName}. I made this little island as a place to slow down for a minute. There’s no score here and nowhere you have to be.`),
            p('Fish off the pier, sit by the fire, swing from the old tree on the hill, or just watch the sun go down. If you plant a flower, it’ll still be here when you come back.'),
            p('And if you’d like to say hello, my door is always open.'),
            p(`— ${firstName}`, 'sig'),
            links,
        );
        ui.closeAll();
        $('letter').showModal();
    };

    /** Reading panel for a company or a project. `build(el, h)` fills it using the safe element helper `h`. */
    ui.info = ({ kicker, title, build }) => {
        $('infoKicker').textContent = kicker;
        $('infoTitle').textContent = title;
        const body = $('infoBody');
        body.replaceChildren();
        build(body, h);
        ui.closeAll();
        $('info').showModal();
        body.scrollTop = 0;
    };
    ui.infoOpen = () => $('info').open;
    /** Tiny DOM builder: h('p', { class }, 'text', child…). Text is always set as text. */
    function h(tag, attrs = {}, ...kids) {
        const el = document.createElement(tag);
        for (const [k, v] of Object.entries(attrs)) {
            if (k === 'class') el.className = v;
            else if (k === 'href') { if (/^(https?:|mailto:|\/)/i.test(v)) el.href = v; }
            else el.setAttribute(k, v);
        }
        for (const kid of kids.flat()) if (kid !== null && kid !== undefined && kid !== '') el.append(kid instanceof Node ? kid : document.createTextNode(String(kid)));
        return el;
    }

    // ---------- photo mode ----------
    ui.photo = on => {
        document.body.classList.toggle('photo', on);
        $('photoBar').hidden = !on;
        if (on) ui.closeAll();
    };
    $('photoBtn').addEventListener('click', () => handlers.photo?.(true));
    $('photoExit').addEventListener('click', () => handlers.photo?.(false));
    $('shutterBtn').addEventListener('click', () => handlers.shutter?.());

    // ---------- sound ----------
    const soundBtn = $('soundBtn');
    ui.syncSound = () => {
        soundBtn.setAttribute('aria-pressed', String(save.settings.sound));
        $('soundIcon').textContent = save.settings.sound ? '🔊' : '🔇';
    };
    soundBtn.addEventListener('click', () => {
        save.settings.sound = !save.settings.sound;
        audio.settings.sound = save.settings.sound;
        if (save.settings.sound) audio.start();
        audio.apply();
        ui.syncSound();
    });

    // ---------- title ----------
    ui.showTitle = onEnter => {
        $('title').hidden = false;
        $('titleSound').checked = save.settings.sound;
        $('enterBtn').focus();
        $('enterBtn').addEventListener('click', () => {
            save.settings.sound = $('titleSound').checked;
            audio.settings.sound = save.settings.sound;
            audio.settings.music = save.settings.music;
            audio.start();
            audio.apply();
            ui.syncSound();
            $('title').hidden = true;
            $('hud').hidden = false;
            $('joystick').hidden = !coarse;
            onEnter();
        }, { once: true });
    };

    ui.on = (name, fn) => { handlers[name] = fn; };
    ui.toggleJournal = () => toggle($('journal'), renderJournal);
    ui.toggleMap = () => toggle($('map'), renderMap);
    ui.toggleTime = () => toggle($('timePanel'), renderTime);
    ui.toggleHelp = () => toggle($('help'));
    ui.refreshJournalIfOpen = () => { if ($('journal').open && journalDirty) renderJournal(); };
    ui.syncSound();
    return ui;
}
