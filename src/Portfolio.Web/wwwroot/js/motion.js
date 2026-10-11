// Motion: everything on the public site that plays on arrival, follows the pointer or answers the scroll.
// Markup asks for it with data attributes; site-v2.css says how things look, motion.css how they move, and this
// file only adds classes and custom properties that motion.css reads.
//
//   data-enter="rise|card|deal|flip|slide-left|slide-right|tilt-left|tilt-right|widen|heading|hero"
//       Plays once, when the element first comes into view (empty means rise). "hero" plays on load, CSS only.
//       "deal" sends each element in from the middle of its container, so it suits a row of cards.
//   data-stagger                      On a container: children that arrive together follow each other (--i).
//   data-enter-item="pop|spin|dot|slide-right"
//       Inside an entering block: plays after the block, one after another (--j).
//   data-scroll="lift|recede|fade|depth-far|depth-near|ambient|draw"
//       Tied to the scroll position. CSS only, where the browser supports scroll-driven animations.
//   data-pointer="spotlight|glow|tilt|lean|magnet"
//       Follows a mouse or pen. data-pointer-each on a container applies the same effect to each child. lean
//       moves the element's [data-lean] descendants; a magnet follows the pointer anywhere in its closest
//       data-pointer-area (or its parent, without one);
//       data-badge (with tilt) adds a label that rides under the pointer.
//   data-hover-focus                  On a group: the hovered child stands forward, its siblings quiet down.
//   data-count                        Its leading whole number counts up once, when seen.
//   data-rotator                      Its children are phrases shown one at a time.
//   data-velocity                     Looping animations inside speed up with the scroll, backwards going up.
//   data-pause                        A button that pauses every animation inside its closest [data-pausable].
//   data-idle                         A region whose decorative loops pause while it is off screen.
//
// Content is complete without any of it. theme.js hides [data-enter] blocks only when this script can reveal them
// (html.motion-on), and drops that if the script never marks itself ready. Motion is off when
// html[data-motion="reduced"] is set, which theme.js keeps for both the device setting and the footer switch;
// effects check it live, so switching off takes hold at once (switching on reloads the page).
(function () {
    const root = document.documentElement;
    const off = () => root.dataset.motion === 'reduced';

    // rAF-throttled handler: runs at most once a frame however often the event fires.
    const perFrame = fn => {
        let queued = false;
        return () => { if (!queued) { queued = true; requestAnimationFrame(() => { queued = false; fn(); }); } };
    };

    // ---- Enter: each block plays its entrance once, as it comes into view ----
    // Order inside blocks: entrance items follow each other (--j).
    document.querySelectorAll('[data-enter]').forEach(block => {
        [...block.querySelectorAll('[data-enter-item]')]
            .filter(item => item.parentElement.closest('[data-enter]') === block)
            .forEach((item, j) => item.style.setProperty('--j', Math.min(j, 12)));
    });

    if (root.classList.contains('motion-on')) {
        const pending = new Set(document.querySelectorAll('[data-enter]:not([data-enter="hero"])'));
        // Blocks that arrive in the same moment: in a [data-stagger] container they follow each other, in
        // document order (an explicit --i in the markup wins).
        const reveal = arriving => {
            const groups = new Map();
            arriving.forEach(el => {
                const list = groups.get(el.parentElement) || [];
                list.push(el);
                groups.set(el.parentElement, list);
            });
            groups.forEach((els, parent) => els.forEach((el, k) => {
                if (parent.hasAttribute('data-stagger') && !el.style.getPropertyValue('--i')) el.style.setProperty('--i', Math.min(k, 5));
                // deal: how many card widths the element sits from its container's middle (+ when left of it),
                // so a row is dealt outward from the centre and a single column simply rises.
                if (el.dataset.enter === 'deal') {
                    const p = parent.getBoundingClientRect(), r = el.getBoundingClientRect();
                    const offset = r.width ? ((p.left + p.width / 2) - (r.left + r.width / 2)) / r.width : 0;
                    el.style.setProperty('--deal', Math.max(-1.5, Math.min(1.5, offset)).toFixed(2));
                }
                el.classList.add('in');
                pending.delete(el);
            }));
            // Done watching the scroll once everything has arrived. Hidden cards stay pending: the filter can
            // show them later, and a fast jump past them still needs this catch-up.
            if (!pending.size) removeEventListener('scroll', onScrollCatchUp);
        };
        let observed = false;
        const io = new IntersectionObserver(entries => {
            observed = true;
            const arriving = entries.filter(e => e.isIntersecting).map(e => e.target);
            arriving.forEach(el => io.unobserve(el));
            if (arriving.length) reveal(arriving);
        }, { rootMargin: '0px 0px -5% 0px' });
        // Anything already in or above the viewport (a deep link, a restored position, a jump that outruns the
        // observer) is shown at once; only content still below waits. Hidden elements (display: none, e.g. cards
        // the projects filter leaves out) have no box and keep waiting until they are shown and seen.
        const catchUp = () => {
            const past = [...pending].filter(el => el.getClientRects().length && el.getBoundingClientRect().top < innerHeight);
            past.forEach(el => io.unobserve(el));
            if (past.length) reveal(past);
        };
        const onScrollCatchUp = perFrame(catchUp);
        catchUp();
        pending.forEach(el => io.observe(el));
        if (pending.size) addEventListener('scroll', onScrollCatchUp, { passive: true });
        addEventListener('beforeprint', () => reveal([...pending]));
        // If the observer never reports (it always does once on observe), show everything rather than leave it
        // hidden. Revealing on a timer regardless would play entrances off-screen.
        setTimeout(() => { if (!observed) reveal([...pending]); }, 4000);
        root.classList.add('motion-ready');
    }

    // Motion off: skip the cross-page transition too (CSS can't, the at-rule ignores attributes).
    addEventListener('pageswap', e => { if (off()) e.viewTransition?.skipTransition(); });

    // ---- Budget: decorative loops in a [data-idle] region pause while it is off screen ----
    const idle = new IntersectionObserver(entries => entries.forEach(e => e.target.classList.toggle('idle-paused', !e.isIntersecting)));
    document.querySelectorAll('[data-idle]').forEach(el => idle.observe(el));

    if (off()) return;

    // ---- Rotator: one phrase at a time; the old one slides up and out, the next rises from below ----
    document.querySelectorAll('[data-rotator]').forEach(rot => {
        const items = [...rot.children];
        if (items.length < 2) return;
        let i = 0;
        rot.classList.add('live');
        items[0].classList.add('on');
        setInterval(() => {
            if (document.hidden || off()) return;
            const prev = items[i];
            i = (i + 1) % items.length;
            const next = items[i];
            prev.classList.replace('on', 'off');
            items.forEach(el => { if (el !== prev && el !== next) el.classList.remove('off'); });
            // With two phrases the incoming one is still parked above: move it below without animating.
            if (next.classList.contains('off')) {
                next.style.transition = 'none';
                next.classList.remove('off');
                void next.offsetWidth;
                next.style.transition = '';
            }
            next.classList.add('on');
        }, 2600);
    });

    // ---- Count-up: the leading whole number in "7+", "28" or "70–80%" counts up once, when seen. The final
    //      text is in the HTML; "1,200" or "2.5x" stay as written, since counting their first digits would show
    //      wrong numbers on the way. ----
    const counters = new IntersectionObserver(entries => entries.forEach(e => {
        if (!e.isIntersecting) return;
        counters.unobserve(e.target);
        if (off()) return;
        const el = e.target, text = el.textContent, m = text.match(/\d+/);
        if (!m || /^[.,]\d/.test(text.slice(m.index + m[0].length))) return;
        const before = text.slice(0, m.index), after = text.slice(m.index + m[0].length), target = +m[0];
        const start = performance.now();
        const step = now => {
            const t = Math.min(1, (now - start) / 1200);
            el.textContent = before + Math.round(target * (1 - Math.pow(1 - t, 3))) + after;
            if (t < 1) requestAnimationFrame(step); else el.textContent = text;
        };
        requestAnimationFrame(step);
    }), { threshold: 0.6 });
    document.querySelectorAll('[data-count]').forEach(el => counters.observe(el));

    // ---- Pause buttons (WCAG 2.2.2: moving content that runs on needs a way to stop it) ----
    document.querySelectorAll('[data-pause]').forEach(btn => {
        const area = btn.closest('[data-pausable]');
        if (!area) return;
        btn.hidden = false;
        btn.addEventListener('click', () => btn.setAttribute('aria-pressed', String(area.classList.toggle('paused'))));
    });

    // ---- Velocity: looping animations inside speed up with the scroll, run backwards going up, then settle.
    //      A paused animation ignores its rate, so pausing still wins. ----
    const looping = document.getAnimations
        ? [...document.querySelectorAll('[data-velocity]')].flatMap(el => el.getAnimations({ subtree: true })) : [];
    if (looping.length) {
        let boost = 0, lastY = scrollY, lastT = performance.now(), settling = false;
        const settle = () => {
            boost *= 0.92;
            if (Math.abs(boost) < 0.02) boost = 0;
            looping.forEach(a => { a.playbackRate = 1 + boost; });
            if (boost) requestAnimationFrame(settle); else settling = false;
        };
        const regions = [...document.querySelectorAll('[data-velocity]')];
        addEventListener('scroll', perFrame(() => {
            if (off()) { boost = 0; looping.forEach(a => { a.playbackRate = 1; }); return; }
            // Nothing to speed up while the moving content is off screen or paused (sampling still moves on, so
            // the first reading afterwards isn't measured across the gap).
            const now = performance.now();
            if (regions.every(r => r.classList.contains('idle-paused') || r.classList.contains('paused'))) { lastY = scrollY; lastT = now; return; }
            boost = Math.max(-5, Math.min(8, boost + (scrollY - lastY) / Math.max(16, now - lastT) * 3));
            lastY = scrollY; lastT = now;
            if (!settling) { settling = true; requestAnimationFrame(settle); }
        }), { passive: true });
    }

    // ---- Pointer effects: mouse and pen only (touch has no hover to follow) ----
    if (!matchMedia('(hover: hover) and (pointer: fine)').matches) return;

    const clamp01 = v => Math.min(1, Math.max(0, v));
    const effects = {
        // Custom properties in px from the element's corner, for a glow centred on the pointer.
        spotlight: { move(el, e, r) { el.style.setProperty('--mx', e.clientX - r.left + 'px'); el.style.setProperty('--my', e.clientY - r.top + 'px'); } },
        glow: { move(el, e, r) { el.style.setProperty('--gx', e.clientX - r.left + 'px'); el.style.setProperty('--gy', e.clientY - r.top + 'px'); } },
        // -1..1 from the centre; whatever reads --px/--py leans with it, and settles back on leave.
        lean: {
            move(el, e, r) {
                el.style.setProperty('--px', (clamp01((e.clientX - r.left) / r.width) * 2 - 1).toFixed(3));
                el.style.setProperty('--py', (clamp01((e.clientY - r.top) / r.height) * 2 - 1).toFixed(3));
            },
            leave(el) { el.style.removeProperty('--px'); el.style.removeProperty('--py'); },
        },
        // A few degrees toward the pointer, with a glow (--gx/--gy as %) and the badge while .tilting.
        tilt: {
            move(el, e, r) {
                const x = clamp01((e.clientX - r.left) / r.width), y = clamp01((e.clientY - r.top) / r.height);
                el.classList.add('tilting');
                el.style.setProperty('--gx', x * 100 + '%');
                el.style.setProperty('--gy', y * 100 + '%');
                el.style.setProperty('--rx', ((0.5 - y) * 6).toFixed(2) + 'deg');
                el.style.setProperty('--ry', ((x - 0.5) * 6).toFixed(2) + 'deg');
            },
            leave(el) { el.classList.remove('tilting'); el.style.removeProperty('--rx'); el.style.removeProperty('--ry'); },
        },
        // Drawn up to ~10px toward the pointer while it is anywhere in the element's area.
        magnet: {
            area: el => el.closest('[data-pointer-area]') || el.parentElement,
            move(el, e, r) {
                const dx = e.clientX - (r.left + r.width / 2), dy = e.clientY - (r.top + r.height / 2);
                const pull = Math.max(0, 1 - Math.hypot(dx, dy) / 260);
                el.style.setProperty('--tx', (dx * pull * 0.12).toFixed(1) + 'px');
                el.style.setProperty('--ty', (dy * pull * 0.18).toFixed(1) + 'px');
            },
            leave(el) { el.style.removeProperty('--tx'); el.style.removeProperty('--ty'); },
        },
    };
    // At most once a frame; leaving drops any frame still queued, so a fast exit can't re-apply after the reset.
    const bind = (el, name) => {
        const fx = effects[name];
        if (!fx) return;
        const area = fx.area ? fx.area(el) : el;
        let frame = 0, last = null;
        area.addEventListener('pointermove', e => {
            last = e;
            if (!frame) frame = requestAnimationFrame(() => { frame = 0; if (off()) fx.leave?.(el); else fx.move(el, last, el.getBoundingClientRect()); });
        });
        area.addEventListener('pointerleave', () => { cancelAnimationFrame(frame); frame = 0; fx.leave?.(el); });
    };
    document.querySelectorAll('[data-pointer]').forEach(el => bind(el, el.dataset.pointer));
    document.querySelectorAll('[data-pointer-each]').forEach(list => [...list.children].forEach(el => {
        if (list.dataset.badge) el.dataset.badge = list.dataset.badge;
        bind(el, list.dataset.pointerEach);
    }));
})();
