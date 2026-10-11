// Public site behaviour: theme toggle, header (compact on scroll, mobile menu, active section), scroll
// reveal, back-to-top, reading progress, projects filter, copy-email, gallery lightbox, and the home page's
// motion (rotating headline, count-up stats, pointer spotlight, card tilt).
// Every feature checks for its markup and does nothing when absent, so one file serves all pages.
(function () {
    const root = document.documentElement;
    const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ---- Theme toggle ----
    const themeBtn = document.getElementById('themeToggle');
    themeBtn?.addEventListener('click', () => {
        const current = root.getAttribute('data-theme')
            || (matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark');
        const next = current === 'light' ? 'dark' : 'light';
        root.setAttribute('data-theme', next);
        try { localStorage.setItem('theme', next); } catch (e) { /* not persisted; still applies now */ }
    });

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
        // Must match the CSS breakpoint where the toggle disappears.
        matchMedia('(min-width: 981px)').addEventListener('change', e => { if (e.matches) setNav(false); });
    }

    // ---- Scroll reveal (opted into by theme.js; content is visible without it) ----
    const reveals = [...document.querySelectorAll('.reveal')];
    let checkReveals = () => {};
    if (root.classList.contains('reveal-on')) {
        const pending = new Set(reveals);
        const show = el => { el.classList.add('in'); pending.delete(el); };
        // Items that arrive together (a row of cards) come in one after another, then lose the delay so hover
        // effects aren't slowed by it.
        const io = new IntersectionObserver(entries => {
            let n = 0;
            entries.forEach(e => {
                if (!e.isIntersecting) return;
                const d = Math.min(n++, 5) * 80;
                if (d) {
                    e.target.style.setProperty('--d', d + 'ms');
                    setTimeout(() => e.target.style.removeProperty('--d'), d + 700);
                }
                show(e.target); io.unobserve(e.target);
            });
        }, { rootMargin: '0px 0px -5% 0px' });
        // Anything in or above the viewport (a deep link, a restored scroll position, a programmatic jump that
        // outruns the observer) is shown right away; only content still below the fold waits.
        checkReveals = () => pending.forEach(el => {
            if (el.getBoundingClientRect().top < innerHeight) { show(el); io.unobserve(el); }
        });
        const revealAll = () => { pending.forEach(show); io.disconnect(); };
        checkReveals();
        pending.forEach(el => io.observe(el));
        addEventListener('beforeprint', revealAll);
        setTimeout(revealAll, 4000); // safety net: nothing stays hidden, whatever the observer does
        root.classList.add('reveal-ready');
    }

    // ---- Home motion. Each piece is decoration over content that is already complete in the HTML, and none of
    //      it runs when the visitor prefers reduced motion. ----
    if (!reducedMotion) {
        // Rotating headline phrase: one shown at a time, the old one slides up and out.
        document.querySelectorAll('[data-rotator]').forEach(rot => {
            const items = [...rot.children];
            if (items.length < 2) return;
            let i = 0;
            rot.classList.add('live');
            items[0].classList.add('on');
            setInterval(() => {
                if (document.hidden) return;
                const prev = items[i];
                i = (i + 1) % items.length;
                prev.classList.replace('on', 'off');
                items.forEach(el => { if (el !== prev) el.classList.remove('off'); });
                items[i].classList.add('on');
            }, 2600);
        });

        // Count-up stats: the leading number in "7+", "28" or "70–80% faster" counts up once, when first seen.
        // The final text is in the HTML, and width is held so nothing shifts while it counts.
        const counters = [...document.querySelectorAll('[data-count]')];
        if (counters.length && 'IntersectionObserver' in window) {
            const run = el => {
                const text = el.textContent;
                const m = text.match(/\d+/);
                if (!m) return;
                const target = +m[0];
                const before = text.slice(0, m.index), after = text.slice(m.index + m[0].length);
                el.style.minWidth = el.offsetWidth + 'px';
                const start = performance.now(), dur = 1200;
                const step = now => {
                    const t = Math.min(1, (now - start) / dur);
                    const eased = 1 - Math.pow(1 - t, 3);
                    el.textContent = before + Math.round(target * eased) + after;
                    if (t < 1) requestAnimationFrame(step); else el.textContent = text;
                };
                requestAnimationFrame(step);
            };
            const cio = new IntersectionObserver(entries => entries.forEach(e => {
                if (e.isIntersecting) { run(e.target); cio.unobserve(e.target); }
            }), { threshold: 0.6 });
            counters.forEach(el => cio.observe(el));
        }

        // Pointer spotlight and card tilt: mouse and pen only (touch has no hover to follow).
        if (matchMedia('(hover: hover) and (pointer: fine)').matches) {
            const follow = (el, fn) => {
                let frame = 0, ev = null;
                el.addEventListener('pointermove', e => {
                    ev = e;
                    if (!frame) frame = requestAnimationFrame(() => { frame = 0; fn(ev); });
                });
            };
            document.querySelectorAll('[data-spotlight]').forEach(el => follow(el, e => {
                const r = el.getBoundingClientRect();
                el.style.setProperty('--mx', (e.clientX - r.left) + 'px');
                el.style.setProperty('--my', (e.clientY - r.top) + 'px');
            }));
            document.querySelectorAll('[data-tilt] .project-card').forEach(card => {
                follow(card, e => {
                    const r = card.getBoundingClientRect();
                    const x = (e.clientX - r.left) / r.width, y = (e.clientY - r.top) / r.height;
                    card.classList.add('tilting');
                    card.style.setProperty('--gx', x * 100 + '%');
                    card.style.setProperty('--gy', y * 100 + '%');
                    card.style.setProperty('--rx', ((0.5 - y) * 6).toFixed(2) + 'deg');
                    card.style.setProperty('--ry', ((x - 0.5) * 6).toFixed(2) + 'deg');
                });
                card.addEventListener('pointerleave', () => {
                    card.classList.remove('tilting');
                    card.style.removeProperty('--rx');
                    card.style.removeProperty('--ry');
                });
            });
        }
    }

    // ---- Scroll-driven bits, one rAF-throttled handler: compact header, back-to-top, reading progress,
    //      active section in the header (home page) and in the case-study "On this page" list ----
    const header = document.getElementById('siteHeader');
    const toTop = document.getElementById('toTop');
    const progress = document.getElementById('readProgress');
    const article = document.querySelector('[data-progress]');
    if (progress && article) progress.classList.add('on');

    const spies = [];
    function addSpy(pairs) {
        const items = pairs.map(([link, id]) => ({ link, target: document.getElementById(id) })).filter(i => i.target);
        if (items.length) spies.push(items);
    }
    if (location.pathname === '/' && nav) {
        // Header links point at "/#section" (data-section); on the home page those sections are right here.
        addSpy([...nav.querySelectorAll('a[data-section]')].map(a => [a, a.dataset.section]));
    }
    document.querySelectorAll('[data-spy]').forEach(n =>
        addSpy([...n.querySelectorAll('a[href^="#"]')].map(a => [a, decodeURIComponent(a.hash.slice(1))])));

    function updateSpy(items) {
        const line = (header?.offsetHeight || 0) + 120;
        const atBottom = innerHeight + scrollY >= document.documentElement.scrollHeight - 4;
        let active = null;
        items.forEach(i => { if (i.target.getBoundingClientRect().top <= line) active = i; });
        if (atBottom && items[items.length - 1].target.getBoundingClientRect().top < innerHeight) active = items[items.length - 1];
        items.forEach(i => {
            i.link.classList.toggle('is-active', i === active);
            if (i === active) i.link.setAttribute('aria-current', 'location');
            else if (i.link.getAttribute('aria-current') === 'location') i.link.removeAttribute('aria-current');
        });
    }

    let ticking = false;
    function onScroll() {
        ticking = false;
        const y = scrollY;
        header?.classList.toggle('scrolled', y > 8);
        toTop?.classList.toggle('show', y > innerHeight * 0.8);
        if (progress && article) {
            const start = article.offsetTop;
            const span = Math.max(1, article.offsetHeight - innerHeight);
            const pct = Math.min(1, Math.max(0, (y - start) / span));
            progress.style.transform = `scaleX(${pct})`;
        }
        spies.forEach(updateSpy);
        checkReveals();
    }
    const requestTick = () => { if (!ticking) { ticking = true; requestAnimationFrame(onScroll); } };
    addEventListener('scroll', requestTick, { passive: true });
    addEventListener('resize', requestTick);
    onScroll();

    // ---- Back to top: scroll up and move focus to the page start rather than leaving it at the footer ----
    toTop?.addEventListener('click', e => {
        e.preventDefault();
        scrollTo({ top: 0, behavior: reducedMotion ? 'auto' : 'smooth' });
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
            let shown = 0;
            cards.forEach(card => {
                const ok = (area === 'all' || card.dataset.category === area) && (!q || card.dataset.search.includes(q));
                if (ok) matching++;
                const visible = ok && matching <= limit;
                if (visible && card.hidden && !reducedMotion) {
                    card.classList.add('pop');
                    card.addEventListener('animationend', () => card.classList.remove('pop'), { once: true });
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
    //      address so the visitor can copy it themselves. ----
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
        const idle = label?.textContent || '';
        let timer;
        btn.classList.add('ready');
        btn.addEventListener('click', () => {
            const text = btn.dataset.copy;
            const fallback = () => {
                if (legacyCopy(text)) { feedback('Copied!', true); return; }
                if (address) getSelection()?.selectAllChildren(address);
                feedback('Press Ctrl+C', false);
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
