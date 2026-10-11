// Anonymous, cookie-free visit beacon. Sends one small JSON report per page load to /api/t (same origin): the
// path, the referrer, the browser's time zone and language, the screen width and any utm_* parameters. Nothing is
// stored in the browser, and nothing is sent when the visitor has Do Not Track or Global Privacy Control on.
(() => {
    'use strict';
    const nav = navigator;
    if (nav.doNotTrack === '1' || window.doNotTrack === '1' || nav.globalPrivacyControl === true) return;

    try {
        const params = new URLSearchParams(location.search);
        const utm = name => params.get(`utm_${name}`) || undefined;
        let tz;
        try { tz = Intl.DateTimeFormat().resolvedOptions().timeZone; } catch { /* very old browsers */ }
        const body = JSON.stringify({
            path: location.pathname,
            referrer: document.referrer || undefined,
            tz,
            lang: nav.language,
            sw: screen.width,
            utm: { source: utm('source'), medium: utm('medium'), campaign: utm('campaign') },
        });

        // sendBeacon survives the page being closed; fall back to a keepalive fetch where it's missing, refuses or
        // throws (older Chromium threw for a JSON-typed Blob).
        const url = '/api/t';
        try { if (nav.sendBeacon && nav.sendBeacon(url, new Blob([body], { type: 'application/json' }))) return; } catch { /* use fetch */ }
        fetch(url, { method: 'POST', body, headers: { 'Content-Type': 'application/json' }, keepalive: true, credentials: 'same-origin' })
            .catch(() => { /* analytics must never surface errors */ });
    } catch { /* analytics must never break the page */ }
})();
