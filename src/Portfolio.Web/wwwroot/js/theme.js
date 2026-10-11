// Loaded synchronously in <head> so a saved theme applies before first paint (no flash). External rather
// than inline because the CSP only allows 'self' scripts. Storage can be unavailable (private mode), so the
// site simply follows the OS theme then.
(function () {
    var root = document.documentElement;
    root.classList.remove('no-js');
    root.classList.add('js');
    try {
        var saved = localStorage.getItem('theme');
        if (saved === 'light' || saved === 'dark') root.setAttribute('data-theme', saved);
    } catch (e) { /* follow the OS theme */ }

    // Motion off by choice (the footer switch, remembered) works like the device setting: no motion-on below.
    var motionOff = false;
    try { motionOff = localStorage.getItem('motion') === 'reduced'; } catch (e) { /* follow the device */ }
    if (motionOff) root.setAttribute('data-motion', 'reduced');

    // Entrances are opt-in: content is visible by default, and [data-enter] blocks are only held back until
    // they arrive when motion is welcome and the browser can observe it. motion.js does the revealing and marks
    // 'motion-ready'; if it never runs (blocked, failed to load), drop the opt-in so nothing stays invisible.
    if (!motionOff && 'IntersectionObserver' in window && window.matchMedia
        && matchMedia('(prefers-reduced-motion: no-preference)').matches) {
        root.classList.add('motion-on');
        setTimeout(function () {
            if (!root.classList.contains('motion-ready')) root.classList.remove('motion-on');
        }, 3000);
    }
})();
