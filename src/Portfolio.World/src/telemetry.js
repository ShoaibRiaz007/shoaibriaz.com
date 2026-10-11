// One anonymous, cookie-free visit report for the island, to the same endpoint and under the same rules as the
// site's wwwroot/js/telemetry.js (skipped under Do Not Track / Global Privacy Control, which /api/t also enforces).
// Keep the two in step. Never throws.
export function reportVisit() {
    try {
        if (navigator.doNotTrack === '1' || window.doNotTrack === '1' || navigator.globalPrivacyControl === true) return;
        const p = new URLSearchParams(location.search);
        const utm = name => p.get(`utm_${name}`) || undefined;
        let tz;
        try { tz = Intl.DateTimeFormat().resolvedOptions().timeZone; } catch { /* report without it */ }
        const body = JSON.stringify({
            path: '/world/',
            referrer: document.referrer || undefined,
            tz,
            lang: navigator.language,
            sw: screen.width,
            utm: { source: utm('source'), medium: utm('medium'), campaign: utm('campaign') },
        });
        try { if (navigator.sendBeacon?.('/api/t', new Blob([body], { type: 'application/json' }))) return; } catch { /* use fetch */ }
        fetch('/api/t', { method: 'POST', body, headers: { 'Content-Type': 'application/json' }, keepalive: true, credentials: 'same-origin' })
            .catch(() => {});
    } catch { /* analytics must never break the island */ }
}
