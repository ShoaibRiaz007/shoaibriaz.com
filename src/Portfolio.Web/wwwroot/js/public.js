// Public site behaviour: theme toggle, header (compact on scroll, mobile menu, active section with a gliding
// underline, tucked away on phones while scrolling down), section rail, back-to-top, reading progress, projects
// filter, copy-email, gallery lightbox, the footer motion switch. Motion (entrances, pointer effects,
// scroll-linked animation) is motion.js.
// Every feature checks for its markup and does nothing when absent, so one file serves all pages.
(function () {
    const root = document.documentElement;
    // Motion is off when theme.js says so (device setting or the footer switch, kept live there).
    const motionOff = () => root.dataset.motion === 'reduced';
    // Breakpoints, matching site-v2.css: the full header nav, and phone widths.
    const wideNav = matchMedia('(min-width: 981px)');
    const phone = matchMedia('(max-width: 860px)');

    // ---- Theme toggle ----
    const themeBtn = document.getElementById('themeToggle');
    themeBtn?.addEventListener('click', () => {
        const current = root.getAttribute('data-theme')
            || (matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark');
        const next = current === 'light' ? 'dark' : 'light';
        root.setAttribute('data-theme', next);
        try { localStorage.setItem('theme', next); } catch (e) { /* not persisted; still applies now */ }
    });

    // ---- Motion switch (footer): off applies at once and is remembered; on reloads, so entrances and effects
    //      start from a fresh page. Hidden while the device itself asks for reduced motion, since the switch
    //      can't override that. Follows the state theme.js keeps, including changes made in other tabs. ----
    const motionBtn = document.getElementById('motionToggle');
    if (motionBtn) {
        const sync = () => {
            motionBtn.hidden = root.dataset.motionBy === 'device';
            motionBtn.setAttribute('aria-pressed', String(!motionOff()));   // pressed: animations on
        };
        sync();
        new MutationObserver(sync).observe(root, { attributes: true, attributeFilter: ['data-motion', 'data-motion-by'] });
        motionBtn.addEventListener('click', () => {
            if (motionOff()) {
                try { localStorage.removeItem('motion'); } catch (e) { /* on for this page only */ }
                location.reload();
                return;
            }
            // Off for this page only when storage can't keep it; theme.js honours that marker.
            try { localStorage.setItem('motion', 'reduced'); } catch (e) { root.setAttribute('data-motion-chosen', ''); }
            root.setAttribute('data-motion', 'reduced');
            root.setAttribute('data-motion-by', 'choice');
            root.classList.remove('motion-on');   // nothing is left waiting to arrive
        });
    }

    // ---- Mobile nav: closes on Esc, link click, outside click, and when the viewport grows past it ----
    const navBtn = document.getElementById('navToggle');
    const nav = document.getElementById('siteNav');
    if (navBtn && nav) {
        const isOpen = () => nav.classList.contains('open');
        const setNav = open => {
            nav.classList.toggle('open', open);
            navBtn.setAttribute('aria-expanded', String(open));
            navBtn.setAttribute('aria-label', open ? 'Close menu' : 'Open menu');
        };
        navBtn.addEventListener('click', () => setNav(!isOpen()));
        nav.querySelectorAll('a').forEach(a => a.addEventListener('click', () => setNav(false)));
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && isOpen()) { setNav(false); navBtn.focus(); }
        });
        document.addEventListener('click', e => {
            if (isOpen() && !nav.contains(e.target) && !navBtn.contains(e.target)) setNav(false);
        });
        wideNav.addEventListener('change', e => { if (e.matches) setNav(false); });
    }

    // ---- Scroll-driven bits, one rAF-throttled handler: compact header, back-to-top, reading progress,
    //      active section in the header (home page) and in the case-study "On this page" list ----
    const header = document.getElementById('siteHeader');
    const toTop = document.getElementById('toTop');
    const progress = document.getElementById('readProgress');
    const article = document.querySelector('[data-progress]');
    if (progress && article) progress.classList.add('on');

    const spies = [];
    // within: the line must be inside a section for it to count (home sections sit edge to edge, and some have
    // no header link, which should then mark none); otherwise the last section above the line stays active,
    // which suits the case-study list with gaps between its blocks. Either way, at the very bottom of the page
    // the last link wins, since its section may be too short to ever reach the line.
    function addSpy(pairs, within = false) {
        const items = pairs.map(([link, id]) => ({ link, target: document.getElementById(id) })).filter(i => i.target);
        if (items.length) spies.push({ items, within });
    }
    if (location.pathname === '/' && nav) {
        // Header links point at "/#section" (data-section); on the home page those sections are right here.
        addSpy([...nav.querySelectorAll('a[data-section]')].map(a => [a, a.dataset.section]), true);
    }
    document.querySelectorAll('[data-spy]').forEach(n =>
        addSpy([...n.querySelectorAll('a[href^="#"]')].map(a => [a, decodeURIComponent(a.hash.slice(1))]), n.dataset.spy === 'within'));

    // The home page's section rail appears once the hero is mostly scrolled past.
    const rail = document.querySelector('.section-rail');

    // Split in two so a frame does all its measuring before any of its writes (one layout per frame).
    function measureSpy({ items, within }, line, atBottom) {
        let active = null;
        items.forEach(i => {
            const r = i.target.getBoundingClientRect();
            if (r.top <= line && (!within || r.bottom > line)) active = i;
        });
        if (atBottom && items[items.length - 1].target.getBoundingClientRect().top < innerHeight) active = items[items.length - 1];
        return active;
    }
    function markSpy({ items }, active) {
        items.forEach(i => {
            i.link.classList.toggle('is-active', i === active);
            if (i === active) i.link.setAttribute('aria-current', 'location');
            else if (i.link.getAttribute('aria-current') === 'location') i.link.removeAttribute('aria-current');
        });
    }

    // ---- Header underline: one indicator glides to the active link on wide screens (the per-link underline is
    //      hidden while it's there). Phones: the header tucks away while scrolling down and returns on the way
    //      up, unless the menu is open or focus is inside it. ----
    let indicator = null;
    if (nav) {
        indicator = document.createElement('span');
        indicator.className = 'nav-indicator';
        indicator.setAttribute('aria-hidden', 'true');
        nav.appendChild(indicator);
        nav.classList.add('has-indicator');
    }
    // Measured only when the active link changes, the window resizes or the web font arrives (which changes the
    // links' widths); not on every scroll frame.
    let indicatorPlaced = false, indicatorFor = null;
    function moveIndicator(force) {
        if (!indicator) return;
        const a = wideNav.matches ? nav.querySelector('a.is-active, a[aria-current="page"]') : null;
        if (a === indicatorFor && !force) return;
        indicatorFor = a;
        if (!a) { indicator.style.opacity = '0'; return; }
        if (!indicatorPlaced) { indicator.style.transition = 'none'; }
        indicator.style.transform = `translateX(${a.offsetLeft + 10}px) scaleX(${Math.max(1, a.offsetWidth - 20)})`;
        indicator.style.opacity = '1';
        if (!indicatorPlaced) { void indicator.offsetWidth; indicator.style.transition = ''; indicatorPlaced = true; }
    }
    document.fonts?.ready.then(() => moveIndicator(true));
    let lastTuckY = scrollY;
    function tuckHeader(y) {
        if (!header || Math.abs(y - lastTuckY) < 6) return;
        const tuck = phone.matches && y > 240 && y > lastTuckY
            && !nav?.classList.contains('open') && !header.contains(document.activeElement);
        header.classList.toggle('tucked', tuck);
        lastTuckY = y;
    }
    // Keyboard focus moving into a tucked header brings it back, so the focused control is never hidden.
    header?.addEventListener('focusin', () => header.classList.remove('tucked'));

    let ticking = false, resized = true;
    function onScroll() {
        ticking = false;
        // Measure everything first...
        const y = scrollY;
        let pct = 0;
        if (progress && article) pct = Math.min(1, Math.max(0, (y - article.offsetTop) / Math.max(1, article.offsetHeight - innerHeight)));
        const line = (header?.offsetHeight || 0) + 120;
        const atBottom = innerHeight + y >= document.documentElement.scrollHeight - 4;
        const actives = spies.map(sp => measureSpy(sp, line, atBottom));
        // ...then write.
        header?.classList.toggle('scrolled', y > 8);
        toTop?.classList.toggle('show', y > innerHeight * 0.8);
        rail?.classList.toggle('show', y > innerHeight * 0.6);
        if (progress && article) progress.style.transform = `scaleX(${pct})`;
        spies.forEach((sp, k) => markSpy(sp, actives[k]));
        moveIndicator(resized);
        resized = false;
        tuckHeader(y);
    }
    const requestTick = () => { if (!ticking) { ticking = true; requestAnimationFrame(onScroll); } };
    addEventListener('scroll', requestTick, { passive: true });
    addEventListener('resize', () => { resized = true; requestTick(); });
    onScroll();

    // ---- Back to top: scroll up and move focus to the page start rather than leaving it at the footer ----
    toTop?.addEventListener('click', e => {
        e.preventDefault();
        scrollTo({ top: 0, behavior: motionOff() ? 'auto' : 'smooth' });
        document.getElementById('main')?.focus({ preventScroll: true });
    });

    // ---- Projects filter: area chips (links, so they work without JS) + search, mirrored in the URL ----
    const grid = document.getElementById('projectGrid');
    if (grid) {
        const cards = [...grid.querySelectorAll('[data-category]')];
        const chips = [...document.querySelectorAll('[data-filter]')];
        const search = document.getElementById('projectSearch');
        const count = document.getElementById('projectCount');
        const empty = document.getElementById('projectEmpty');
        const areas = new Set(chips.map(c => c.dataset.filter));
        const more = document.getElementById('projectMore');
        const moreButton = more?.querySelector('button');
        const PAGE = 12;
        let area = 'all';
        let limit = PAGE;   // how many matching cards are shown; grows by a page with "Show more"

        function readUrl() {
            const params = new URLSearchParams(location.search);
            const a = params.get('area');
            area = a && areas.has(a) ? a : 'all';
            if (search) search.value = params.get('q') || '';
        }
        function writeUrl(push) {
            const url = new URL(location.href);
            if (area === 'all') url.searchParams.delete('area'); else url.searchParams.set('area', area);
            const q = (search?.value || '').trim();
            if (q) url.searchParams.set('q', q); else url.searchParams.delete('q');
            // The shown-card count travels with the entry (see "Show more"), so it is rewritten even when the URL isn't.
            if (url.href !== location.href) history[push ? 'pushState' : 'replaceState']({ limit }, '', url);
            else try { history.replaceState({ ...(history.state || {}), limit }, ''); } catch { /* state is a nicety */ }
        }
        function apply() {
            const q = (search?.value || '').trim().toLowerCase();
            let matching = 0;
            let shown = 0, returning = 0;
            cards.forEach(card => {
                const ok = (area === 'all' || card.dataset.category === area) && (!q || card.dataset.search.includes(q));
                if (ok) matching++;
                const visible = ok && matching <= limit;
                // A card that had already arrived and is shown again replays its own entrance once, in order with
                // the others coming back. One that never arrived waits for the viewport like any other block.
                if (visible && card.hidden && card.classList.contains('in') && root.classList.contains('motion-on')) {
                    card.hidden = false;
                    card.classList.remove('in');
                    card.style.setProperty('--i', Math.min(returning++, 5));
                    void card.offsetWidth;
                    card.classList.add('in');
                }
                card.hidden = !visible;
                if (visible) shown++;
            });
            chips.forEach(c => {
                if (c.dataset.filter === area) c.setAttribute('aria-current', 'true'); else c.removeAttribute('aria-current');
            });
            const noun = matching === 1 ? 'project' : 'projects';
            if (count) {
                count.textContent = shown < matching ? `Showing ${shown} of ${matching} ${noun}`
                    : matching === cards.length ? `${matching} ${noun}` : `${matching} of ${cards.length} projects`;
            }
            if (empty) empty.hidden = matching !== 0;
            if (more && moreButton) {
                const left = matching - shown;
                more.hidden = left <= 0;
                moreButton.textContent = `Show ${Math.min(PAGE, left)} more (${left} left)`;
            }
        }

        moreButton?.addEventListener('click', () => {
            const firstNew = cards.filter(c => !c.hidden).length;
            limit += PAGE;
            apply();
            // Remembered in this history entry, so Back from a project shows the same cards again.
            try { history.replaceState({ ...(history.state || {}), limit }, ''); } catch { /* state is a nicety */ }
            // Keyboard and screen-reader users continue from the first card just shown.
            const next = cards.filter(c => !c.hidden)[firstNew];
            next?.querySelector('a')?.focus({ preventScroll: false });
        });

        chips.forEach(chip => chip.addEventListener('click', e => {
            if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return; // new tab etc.
            e.preventDefault();
            area = chip.dataset.filter;
            limit = PAGE;
            apply();
            writeUrl(true);
        }));
        search?.addEventListener('input', () => { limit = PAGE; apply(); writeUrl(false); });
        empty?.querySelector('[data-clear]')?.addEventListener('click', e => {
            e.preventDefault();
            area = 'all';
            limit = PAGE;
            if (search) search.value = '';
            apply();
            writeUrl(true);
            search?.focus();
        });
        const savedLimit = state => Math.max(PAGE, Number(state && state.limit) || PAGE);
        addEventListener('popstate', e => { readUrl(); limit = savedLimit(e.state); apply(); });
        readUrl();
        limit = savedLimit(history.state);
        apply();
    }

    // ---- Copy email (the button is hidden until this runs). The async Clipboard API can be denied (insecure
    //      origin, embedded frame, permissions), so fall back to execCommand, and failing that select the
    //      address beside the button so the visitor can copy it themselves, or, with none beside it, show the
    //      address on the button. ----
    function legacyCopy(text) {
        const ta = document.createElement('textarea');
        ta.value = text;
        ta.setAttribute('readonly', '');
        ta.className = 'visually-hidden';
        document.body.appendChild(ta);
        ta.select();
        let ok = false;
        try { ok = document.execCommand('copy'); } catch (e) { /* unsupported */ }
        ta.remove();
        return ok;
    }
    document.querySelectorAll('[data-copy]').forEach(btn => {
        const label = btn.querySelector('[data-copy-label]');
        const status = btn.parentElement.querySelector('[data-copy-status]');
        const address = btn.parentElement.querySelector('a');
        let idle = label?.textContent || '';
        let timer;
        btn.classList.add('ready');
        btn.addEventListener('click', () => {
            const text = btn.dataset.copy;
            const fallback = () => {
                if (legacyCopy(text)) { feedback('Copied!', true); return; }
                if (address) { getSelection()?.selectAllChildren(address); feedback('Press Ctrl+C', false); return; }
                // No address shown beside this button (the hero's): put it on the button, and keep it there
                // (it becomes the label the button returns to).
                clearTimeout(timer);
                idle = text;
                btn.classList.remove('copied');
                if (label) label.textContent = text;
                if (status) status.textContent = `Couldn't copy. The email address is ${text}`;
            };
            if (navigator.clipboard?.writeText) navigator.clipboard.writeText(text).then(() => feedback('Copied!', true), fallback);
            else fallback();
        });
        function feedback(text, ok) {
            if (label) label.textContent = text;
            if (status) status.textContent = ok ? 'Email address copied to clipboard' : 'Email address selected; press Ctrl+C to copy';
            btn.classList.toggle('copied', ok);
            clearTimeout(timer);
            timer = setTimeout(() => {
                if (label) label.textContent = idle;
                if (status) status.textContent = '';
                btn.classList.remove('copied');
            }, 2200);
        }
    });

    // ---- Gallery lightbox (native <dialog>: focus trap, Esc and backdrop come for free) ----
    const dialog = document.getElementById('lightbox');
    const thumbs = [...document.querySelectorAll('[data-lightbox]')];
    if (dialog && thumbs.length && typeof dialog.showModal === 'function') {
        const img = dialog.querySelector('img');
        const caption = dialog.querySelector('[data-caption]');
        const counter = dialog.querySelector('[data-counter]');
        let index = 0;
        let opener = null;   // the control that opened the viewer gets focus back

        function show(i) {
            index = (i + thumbs.length) % thumbs.length;
            const t = thumbs[index];
            img.src = t.dataset.lightbox;
            img.alt = t.dataset.alt || '';
            caption.textContent = t.dataset.alt || '';
            counter.textContent = `${index + 1} / ${thumbs.length}`;
        }
        thumbs.forEach((t, i) => t.addEventListener('click', () => { opener = t; show(i); dialog.showModal(); }));
        dialog.querySelector('[data-prev]')?.addEventListener('click', () => show(index - 1));
        dialog.querySelector('[data-next]')?.addEventListener('click', () => show(index + 1));
        dialog.querySelector('[data-close]')?.addEventListener('click', () => dialog.close());
        dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });
        dialog.addEventListener('keydown', e => {
            if (e.key === 'ArrowRight') show(index + 1);
            if (e.key === 'ArrowLeft') show(index - 1);
        });
        dialog.addEventListener('close', () => (opener || thumbs[index])?.focus());
        // The hero cover opens the viewer on the same screenshot.
        document.querySelectorAll('[data-lightbox-open]').forEach(b => b.addEventListener('click', () => {
            const i = thumbs.findIndex(t => t.dataset.lightbox === b.dataset.lightboxOpen);
            if (i >= 0) { opener = b; show(i); dialog.showModal(); }
        }));
    }

    // ---- Print (CV page) ----
    document.querySelectorAll('[data-print]').forEach(b => b.addEventListener('click', () => window.print()));
})();
