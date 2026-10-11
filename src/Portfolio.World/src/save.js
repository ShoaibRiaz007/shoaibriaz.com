// Per-visitor progress (journal, planted flowers, settings) kept in this browser only. Storage can be missing
// or throw (private mode, blocked site data), so every access is guarded and the world works without it.
const KEY = 'shoaib-island.v1';

const DEFAULTS = {
    settings: { sound: true, music: true, quality: 'auto', time: { mode: 'cycle', hour: 17.2, speed: 1 } },
    journal: { seen: {}, done: {}, fish: {}, shells: [], lanterns: 0, wishes: 0, bestSkip: 0, planted: 0, pets: 0, catches: 0, photos: 0, seconds: 0, visits: 0, read: {} },
    flowers: [],
    welcomed: false,
};

function merge(base, extra) {
    if (typeof base !== 'object' || base === null || Array.isArray(base)) return extra ?? base;
    const out = { ...base };
    for (const k of Object.keys(extra ?? {})) out[k] = k in base ? merge(base[k], extra[k]) : extra[k];
    return out;
}

export function loadSave() {
    try {
        const raw = localStorage.getItem(KEY);
        return merge(structuredClone(DEFAULTS), raw ? JSON.parse(raw) : {});
    } catch {
        return structuredClone(DEFAULTS);
    }
}

export function writeSave(state) {
    try { localStorage.setItem(KEY, JSON.stringify(state)); } catch { /* storage unavailable: progress lasts this visit */ }
}

export function clearSave() {
    try { localStorage.removeItem(KEY); } catch { /* nothing to clear */ }
}
