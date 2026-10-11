// The soundscape, synthesized live with Web Audio (no files to download): surf that swells with your distance
// to the shore, wind that rises on high ground, birdsong by day, crickets at night, the campfire's crackle, a
// slow generative music bed, and small sound effects for everything you do.
export function createAudio() {
    let ctx = null, master, buses, noiseBuf, reverb;
    const layers = {};
    const settings = { sound: true, music: true };
    let chordIndex = 0;

    function noise(seconds = 2) {
        const b = ctx.createBuffer(1, ctx.sampleRate * seconds, ctx.sampleRate);
        const d = b.getChannelData(0);
        let last = 0;
        for (let i = 0; i < d.length; i++) {
            // Pinkish: white noise through a gentle one-pole low-pass, which sounds softer than raw hiss.
            last = last * 0.86 + (Math.random() * 2 - 1) * 0.14;
            d[i] = last * 3.2;
        }
        return b;
    }
    function impulse(seconds = 3.2) {
        const len = ctx.sampleRate * seconds, b = ctx.createBuffer(2, len, ctx.sampleRate);
        for (let c = 0; c < 2; c++) {
            const d = b.getChannelData(c);
            for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 2.6);
        }
        return b;
    }
    function loop(buffer, ...chain) {
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.loop = true;
        let node = src;
        for (const n of chain) { node.connect(n); node = n; }
        src.start(0, Math.random() * buffer.duration);
        return node;
    }
    const gain = v => { const g = ctx.createGain(); g.gain.value = v; return g; };
    const filter = (type, freq, Q = 0.7) => { const f = ctx.createBiquadFilter(); f.type = type; f.frequency.value = freq; f.Q.value = Q; return f; };

    function init() {
        if (ctx) return;
        ctx = new AudioContext();
        master = gain(0.9);
        const comp = ctx.createDynamicsCompressor();
        master.connect(comp).connect(ctx.destination);
        buses = { amb: gain(1), sfx: gain(0.8), music: gain(settings.music ? 0.5 : 0) };
        reverb = ctx.createConvolver();
        reverb.buffer = impulse();
        const wet = gain(0.35);
        reverb.connect(wet).connect(master);
        Object.values(buses).forEach(b => b.connect(master));
        buses.music.connect(reverb);
        buses.sfx.connect(reverb);
        noiseBuf = noise(4);

        // Surf: low rumble plus a brighter wash, each swelling on its own slow cycle.
        layers.surf = gain(0);
        layers.surf.connect(buses.amb);
        loop(noiseBuf, filter('lowpass', 420), gain(0.9)).connect(layers.surf);
        layers.surfWash = gain(0);
        loop(noiseBuf, filter('highpass', 900), filter('lowpass', 3200), layers.surfWash).connect(layers.surf);
        // Wind: band-passed noise with a slowly wandering centre frequency.
        layers.windFilter = filter('bandpass', 500, 0.5);
        layers.wind = gain(0);
        loop(noiseBuf, layers.windFilter, layers.wind).connect(buses.amb);
        // Crickets: a high tone chopped by a fast pulse, gated in slow phrases.
        const cricket = ctx.createOscillator();
        cricket.frequency.value = 4400;
        const chirp = gain(0), gate = gain(0);
        const pulse = ctx.createOscillator();
        pulse.type = 'square';
        pulse.frequency.value = 22;
        pulse.connect(gain(0.5)).connect(chirp.gain);
        cricket.connect(chirp).connect(gate);
        layers.crickets = gain(0);
        gate.connect(layers.crickets).connect(buses.amb);
        cricket.start();
        pulse.start();
        layers.cricketGate = gate;
        // Fire: a low roar; the crackle bursts are scheduled in update().
        layers.fire = gain(0);
        layers.fire.connect(buses.amb);
        loop(noiseBuf, filter('lowpass', 300), gain(0.5)).connect(layers.fire);
        scheduleMusic();
    }

    // ---------------- Music: slow pads and a sparse plucked melody ----------------
    const DAY = [[62, 66, 69, 73, 76], [59, 62, 66, 69, 74], [55, 59, 62, 66, 71], [57, 61, 64, 69, 71]];       // D, Bm, G, A with colour tones
    const NIGHT = [[57, 60, 64, 67, 71], [53, 57, 60, 64, 69], [55, 59, 62, 67, 69], [52, 55, 59, 64, 66]];     // Am, F, G, Em
    const SCALE_DAY = [62, 64, 66, 69, 71, 74, 76, 78, 81], SCALE_NIGHT = [57, 60, 62, 64, 67, 69, 72, 74, 76];
    const hz = n => 440 * Math.pow(2, (n - 69) / 12);
    let nightMood = 0;

    function pad(notes, when, dur) {
        for (const n of notes.slice(0, 4)) {
            const o = ctx.createOscillator();
            o.type = 'triangle';
            o.frequency.value = hz(n - 12);
            o.detune.value = (Math.random() - 0.5) * 8;
            const g = gain(0), f = filter('lowpass', 900 - nightMood * 300);
            o.connect(f).connect(g).connect(buses.music);
            g.gain.setValueAtTime(0, when);
            g.gain.linearRampToValueAtTime(0.045, when + 2.8);
            g.gain.setValueAtTime(0.045, when + dur - 3);
            g.gain.linearRampToValueAtTime(0, when + dur + 1.5);
            o.start(when);
            o.stop(when + dur + 2);
        }
    }
    function pluck(n, when, vol = 0.08) {
        const o = ctx.createOscillator(), o2 = ctx.createOscillator();
        o.frequency.value = hz(n);
        o2.frequency.value = hz(n) * 2.01;
        const g = gain(0), g2 = gain(0.18);
        o.connect(g);
        o2.connect(g2).connect(g);
        g.connect(buses.music);
        g.gain.setValueAtTime(0, when);
        g.gain.linearRampToValueAtTime(vol, when + 0.01);
        g.gain.exponentialRampToValueAtTime(0.0001, when + 2.4);
        o.start(when); o2.start(when);
        o.stop(when + 2.5); o2.stop(when + 2.5);
    }
    function scheduleMusic() {
        const bar = 9;
        const step = () => {
            if (!ctx) return;
            const now = ctx.currentTime + 0.1;
            if (settings.music && settings.sound && ctx.state === 'running') {
                const chords = nightMood > 0.5 ? NIGHT : DAY, scale = nightMood > 0.5 ? SCALE_NIGHT : SCALE_DAY;
                pad(chords[chordIndex % chords.length], now, bar);
                // A few notes per bar around the scale; sometimes a rest.
                let t = now + 1 + Math.random() * 2;
                while (t < now + bar - 1) {
                    if (Math.random() < 0.7) pluck(scale[Math.floor(Math.random() * scale.length)], t, 0.05 + Math.random() * 0.04);
                    t += 0.9 + Math.random() * 2.2;
                }
                chordIndex++;
            }
            setTimeout(step, bar * 1000);
        };
        step();
    }

    // ---------------- Birds ----------------
    let nextBird = 2, nextGull = 6;
    function birdPhrase(pan) {
        const p = ctx.createStereoPanner();
        p.pan.value = pan;
        p.connect(buses.amb);
        let t = ctx.currentTime;
        const base = 2400 + Math.random() * 1800, notes = 2 + Math.floor(Math.random() * 5);
        for (let i = 0; i < notes; i++) {
            const o = ctx.createOscillator(), g = gain(0);
            o.frequency.setValueAtTime(base * (0.9 + Math.random() * 0.3), t);
            o.frequency.exponentialRampToValueAtTime(base * (1.2 + Math.random() * 0.5), t + 0.07);
            o.frequency.exponentialRampToValueAtTime(base * 0.8, t + 0.12);
            g.gain.setValueAtTime(0, t);
            g.gain.linearRampToValueAtTime(0.025, t + 0.01);
            g.gain.exponentialRampToValueAtTime(0.0001, t + 0.13);
            o.connect(g).connect(p);
            o.start(t);
            o.stop(t + 0.15);
            t += 0.1 + Math.random() * 0.12;
        }
    }
    function gull(pan) {
        const p = ctx.createStereoPanner();
        p.pan.value = pan;
        p.connect(buses.amb);
        let t = ctx.currentTime;
        for (let i = 0; i < 2 + Math.floor(Math.random() * 3); i++) {
            const o = ctx.createOscillator(), g = gain(0), f = filter('bandpass', 1400, 2);
            o.type = 'sawtooth';
            o.frequency.setValueAtTime(900, t);
            o.frequency.linearRampToValueAtTime(1500, t + 0.08);
            o.frequency.linearRampToValueAtTime(800, t + 0.32);
            g.gain.setValueAtTime(0, t);
            g.gain.linearRampToValueAtTime(0.03, t + 0.04);
            g.gain.linearRampToValueAtTime(0, t + 0.34);
            o.connect(f).connect(g).connect(p);
            o.start(t);
            o.stop(t + 0.36);
            t += 0.4;
        }
    }

    // ---------------- One-shot effects ----------------
    function burst({ freq = 1000, type = 'bandpass', dur = 0.15, vol = 0.2, Q = 1, sweepTo = null, bus = 'sfx' }) {
        const src = ctx.createBufferSource();
        src.buffer = noiseBuf;
        const f = filter(type, freq, Q), g = gain(0), t = ctx.currentTime;
        if (sweepTo) f.frequency.exponentialRampToValueAtTime(sweepTo, t + dur);
        g.gain.setValueAtTime(vol, t);
        g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        src.connect(f).connect(g).connect(buses[bus]);
        src.start(t, Math.random() * 3);
        src.stop(t + dur + 0.05);
    }
    function tone(freq, { dur = 0.3, vol = 0.12, type = 'sine', to = null, at = 0 } = {}) {
        const o = ctx.createOscillator(), g = gain(0), t = ctx.currentTime + at;
        o.type = type;
        o.frequency.setValueAtTime(freq, t);
        if (to) o.frequency.exponentialRampToValueAtTime(to, t + dur);
        g.gain.setValueAtTime(0, t);
        g.gain.linearRampToValueAtTime(vol, t + 0.01);
        g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        o.connect(g).connect(buses.sfx);
        o.start(t);
        o.stop(t + dur + 0.05);
    }
    const SFX = {
        step(kind) {
            const v = { grass: [700, 0.05], sand: [450, 0.06], path: [900, 0.06], rock: [1600, 0.05], wood: [1200, 0.08], water: [1100, 0.09] }[kind] ?? [800, 0.05];
            burst({ freq: v[0] * (0.85 + Math.random() * 0.3), type: kind === 'water' ? 'bandpass' : 'lowpass', dur: kind === 'water' ? 0.22 : 0.08, vol: v[1] });
            if (kind === 'wood') tone(150 + Math.random() * 30, { dur: 0.08, vol: 0.05 });
        },
        splash(size = 1) { burst({ freq: 1800, dur: 0.35 * size + 0.1, vol: 0.12 * size, sweepTo: 400 }); },
        cast() { burst({ freq: 600, dur: 0.4, vol: 0.08, sweepTo: 3000, Q: 2 }); },
        plip() { tone(900, { dur: 0.12, vol: 0.08, to: 520 }); },
        bite() { tone(700, { dur: 0.09, vol: 0.12, to: 400 }); tone(700, { dur: 0.09, vol: 0.12, to: 400, at: 0.12 }); },
        reel() { for (let i = 0; i < 6; i++) setTimeout(() => ctx && burst({ freq: 3000, dur: 0.02, vol: 0.05, Q: 6 }), i * 45); },
        chime(notes = [72, 76, 79, 84]) { notes.forEach((n, i) => tone(hz(n), { dur: 1.2, vol: 0.07, at: i * 0.09 })); },
        pop() { tone(320, { dur: 0.14, vol: 0.12, to: 700 }); },
        shell() { [88, 91, 95].forEach((n, i) => tone(hz(n), { dur: 0.8, vol: 0.05, at: i * 0.06 })); },
        meow() {
            const o = ctx.createOscillator(), f = filter('bandpass', 900, 3), g = gain(0), t = ctx.currentTime;
            o.type = 'sawtooth';
            o.frequency.setValueAtTime(480, t);
            o.frequency.linearRampToValueAtTime(760, t + 0.18);
            o.frequency.linearRampToValueAtTime(420, t + 0.55);
            f.frequency.setValueAtTime(700, t);
            f.frequency.linearRampToValueAtTime(1500, t + 0.2);
            f.frequency.linearRampToValueAtTime(800, t + 0.55);
            g.gain.setValueAtTime(0, t);
            g.gain.linearRampToValueAtTime(0.09, t + 0.05);
            g.gain.linearRampToValueAtTime(0, t + 0.58);
            o.connect(f).connect(g).connect(buses.sfx);
            o.start(t);
            o.stop(t + 0.6);
        },
        purr() { for (let i = 0; i < 10; i++) setTimeout(() => ctx && burst({ freq: 140, type: 'lowpass', dur: 0.12, vol: 0.12 }), i * 140); },
        creak() { tone(260, { dur: 0.35, vol: 0.03, type: 'sawtooth', to: 180 }); },
        whoosh() { burst({ freq: 400, dur: 0.6, vol: 0.06, sweepTo: 1600, Q: 1.5 }); },
        skip() { burst({ freq: 2200, dur: 0.07, vol: 0.08, Q: 2 }); },
        shutter() { burst({ freq: 3000, dur: 0.04, vol: 0.15 }); setTimeout(() => ctx && burst({ freq: 2000, dur: 0.06, vol: 0.12 }), 70); },
        click() { tone(1200, { dur: 0.05, vol: 0.04 }); },
        wish() { [84, 88, 91, 96, 100].forEach((n, i) => tone(hz(n), { dur: 1.6, vol: 0.04, at: i * 0.12 })); },
        door() { burst({ freq: 300, type: 'lowpass', dur: 0.3, vol: 0.12 }); },
    };

    // Engine hum while driving.
    let engine = null, engineGain = null;
    function ensureEngine() {
        if (engine) return;
        engine = ctx.createOscillator();
        engine.type = 'sawtooth';
        engineGain = gain(0);
        engine.connect(filter('lowpass', 380)).connect(engineGain).connect(buses.sfx);
        engine.start();
    }

    const api = {
        get ready() { return !!ctx; },
        settings,
        /** Must be called from a user gesture (browsers block audio until then). */
        start() { init(); ctx.resume(); api.apply(); },
        apply() {
            if (!ctx) return;
            const t = ctx.currentTime;
            master.gain.setTargetAtTime(settings.sound ? 0.9 : 0, t, 0.2);
            buses.music.gain.setTargetAtTime(settings.music ? 0.5 : 0, t, 0.6);
        },
        play(name, ...args) { if (ctx && settings.sound) SFX[name]?.(...args); },
        /**
         * Per-frame ambience. shore 0..1 = closeness to the surf, height in metres, night 0..1, fire 0..1 =
         * closeness to the campfire, gust 0..1, drive = { speed } while driving.
         */
        update(dt, { shore, height, night, fire, gust = 1, drive = null, muffled = false }) {
            if (!ctx || !settings.sound) return;
            const t = ctx.currentTime;
            nightMood += ((night > 0.5 ? 1 : 0) - nightMood) * Math.min(1, dt * 0.2);
            const swell = 0.6 + Math.sin(t * 0.55) * 0.25 + Math.sin(t * 0.21 + 1) * 0.15;
            layers.surf.gain.setTargetAtTime((0.05 + shore * 0.5) * swell * (muffled ? 0.4 : 1), t, 0.3);
            layers.surfWash.gain.setTargetAtTime(Math.max(0, Math.sin(t * 0.55 - 0.6)) * 0.5 * shore, t, 0.2);
            const windLevel = (0.04 + Math.min(1, Math.max(0, height - 4) / 14) * 0.18) * (0.7 + gust * 0.5);
            layers.wind.gain.setTargetAtTime(windLevel, t, 0.5);
            layers.windFilter.frequency.setTargetAtTime(380 + Math.sin(t * 0.13) * 160 + gust * 120, t, 0.5);
            layers.crickets.gain.setTargetAtTime(Math.max(0, night - 0.4) * 0.05 * (1 - shore * 0.6), t, 1);
            layers.cricketGate.gain.setTargetAtTime(Math.sin(t * 1.3) > 0.1 ? 0.6 : 0, t, 0.05);
            layers.fire.gain.setTargetAtTime(fire * 0.18, t, 0.3);
            if (fire > 0.05 && Math.random() < dt * 9 * fire) burst({ freq: 1500 + Math.random() * 2500, dur: 0.02 + Math.random() * 0.03, vol: 0.12 * fire, Q: 2, bus: 'amb' });
            nextBird -= dt;
            if (nextBird <= 0) {
                if (night < 0.4 && shore < 0.85) birdPhrase(Math.random() * 1.6 - 0.8);
                nextBird = 2.5 + Math.random() * 6;
            }
            nextGull -= dt;
            if (nextGull <= 0) {
                if (night < 0.5 && shore > 0.35) gull(Math.random() * 1.6 - 0.8);
                nextGull = 8 + Math.random() * 14;
            }
            if (drive) {
                ensureEngine();
                const s = Math.abs(drive.speed);
                engine.frequency.setTargetAtTime(42 + s * 5, t, 0.08);
                engineGain.gain.setTargetAtTime(0.03 + s * 0.003, t, 0.15);
            } else if (engineGain) engineGain.gain.setTargetAtTime(0, t, 0.2);
        },
        suspend() { ctx?.suspend(); },
        resume() { ctx?.resume(); },
    };
    return api;
}
