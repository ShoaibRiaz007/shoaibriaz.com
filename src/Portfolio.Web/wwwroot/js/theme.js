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

    // Scroll-reveal is opt-in: content is visible by default, and only hidden-until-scrolled when motion is
    // welcome and the browser can observe it. public.js does the revealing and marks 'reveal-ready'; if it
    // never runs (blocked, failed to load), drop the opt-in so nothing stays invisible.
    if ('IntersectionObserver' in window && window.matchMedia
        && matchMedia('(prefers-reduced-motion: no-preference)').matches) {
        root.classList.add('reveal-on');
        setTimeout(function () {
            if (!root.classList.contains('reveal-ready')) root.classList.remove('reveal-on');
        }, 3000);
    }
})();
