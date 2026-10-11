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

    // Motion off: one state for the whole site, html[data-motion="reduced"], set here for both causes (the device
    // asks for reduced motion, or the visitor chose it with the footer switch, remembered), with
    // data-motion-by saying which. Scripts read only that attribute; motion.css keys its motion-off rules on it
    // too, with the device media query kept as the fallback for when JavaScript doesn't run. It follows a device
    // setting change, the choice made in another tab, and a page restored from the back/forward cache. Turning
    // off applies at once; turning back on reloads a page that runs motion.js, since it only sets up its effects
    // on load.
    var deviceReduced = window.matchMedia ? matchMedia('(prefers-reduced-motion: reduce)') : null;
    // Storage is the source of truth. When it can't be read, an off choice made on this page still counts
    // (data-motion-chosen, which the footer switch sets only when storage couldn't keep the choice).
    function chosenOff() {
        try { return localStorage.getItem('motion') === 'reduced' || root.hasAttribute('data-motion-chosen'); }
        catch (e) { return root.hasAttribute('data-motion-chosen'); }
    }
    var started = false;
    function applyMotion() {
        var byDevice = !!(deviceReduced && deviceReduced.matches), byChoice = chosenOff();
        if (byDevice || byChoice) {
            root.setAttribute('data-motion', 'reduced');
            root.setAttribute('data-motion-by', byDevice ? 'device' : 'choice');
            root.classList.remove('motion-on');   // nothing is left waiting to arrive
        } else if (root.hasAttribute('data-motion')) {
            // A page with motion.js reloads so its effects start; any other page (the admin, with forms that
            // may hold unsaved input) only drops the attribute.
            if (started && document.querySelector('script[src*="/js/motion.js"]')) { location.reload(); return; }
            root.removeAttribute('data-motion');
            root.removeAttribute('data-motion-by');
        }
    }
    applyMotion();
    started = true;
    if (deviceReduced && deviceReduced.addEventListener) deviceReduced.addEventListener('change', applyMotion);
    window.addEventListener('storage', function (e) { if (e.key === 'motion') applyMotion(); });
    window.addEventListener('pageshow', function (e) { if (e.persisted) applyMotion(); });

    // Entrances are opt-in: content is visible by default, and [data-enter] blocks are only held back until
    // they arrive when motion is welcome and the browser can observe it. motion.js does the revealing and marks
    // 'motion-ready'; if it never runs (blocked, failed to load), drop the opt-in so nothing stays invisible.
    if (!root.hasAttribute('data-motion') && 'IntersectionObserver' in window) {
        root.classList.add('motion-on');
        setTimeout(function () {
            if (!root.classList.contains('motion-ready')) root.classList.remove('motion-on');
        }, 3000);
    }
})();
