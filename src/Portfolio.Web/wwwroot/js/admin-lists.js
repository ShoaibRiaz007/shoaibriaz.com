// Admin content lists: drag-and-drop (mouse) and ▲▼ buttons (keyboard / touch) reorder rows, then persist the
// new order; search and the Live/Hidden filter narrow the list. No library: the CSP only allows 'self' scripts.
(function () {
    const tokenInput = document.querySelector('#reorder-token input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;

    // Long enough for the admin to read why the list is about to reset.
    const RELOAD_AFTER_ERROR_MS = 1500;

    // admin.js loads first and provides the toasts; the lists still work without it.
    function toast(message, type) {
        if (window.AdminUI) window.AdminUI.toast(message, type);
    }

    function idsOf(tbody) {
        return [...tbody.querySelectorAll('tr[data-id]')].map(tr => tr.dataset.id);
    }

    // While a search or filter hides rows, the visible order isn't the whole order, so moves are refused.
    function isLocked(tbody) {
        return tbody.hasAttribute('data-reorder-locked');
    }

    function createSaver(tbody) {
        const status = tbody.closest('section').querySelector('.reorder-status');
        let inFlight = false;
        let pending = false;
        let failed = false;

        function fail(message) {
            // Reload so the list reflects what the server actually has, not the unsaved order. Until then the
            // saver stays closed: a move made on top of the rejected order must not be sent.
            failed = true;
            pending = false;
            status.textContent = message;
            toast(message, 'error');
            setTimeout(() => location.reload(), RELOAD_AFTER_ERROR_MS);
        }

        // One request at a time per list; moves made while one is in flight are coalesced into a single
        // follow-up carrying the latest order, so requests can never commit out of order.
        return async function save() {
            if (failed) return;
            if (inFlight) { pending = true; return; }
            inFlight = true;
            status.textContent = 'Saving order…';
            const body = new FormData();
            body.append('kind', tbody.dataset.reorderKind);
            idsOf(tbody).forEach(id => body.append('ids', id));
            try {
                const res = await fetch(tbody.dataset.reorderUrl, {
                    method: 'POST',
                    body,
                    // X-Requested-With makes an expired session answer 401 instead of redirecting to the login
                    // page, which fetch would follow and report as a 200.
                    headers: { 'RequestVerificationToken': tokenInput.value, 'X-Requested-With': 'XMLHttpRequest' },
                    credentials: 'same-origin',
                });
                if (res.status === 401 || res.redirected) return fail('Your session has expired — reloading to sign in…');
                if (!res.ok) return fail('Could not save the new order — reloading…');
            } catch (e) {
                return fail('Could not save the new order — reloading…');
            } finally {
                inFlight = false;
            }
            if (pending) { pending = false; return save(); }
            status.textContent = 'Order saved.';
            toast('Order saved.', 'success');
        };
    }

    document.querySelectorAll('tbody[data-reorder-kind]').forEach(tbody => {
        const save = createSaver(tbody);
        let dragged = null;
        let rowsBefore = null;

        tbody.addEventListener('dragstart', e => {
            // Only the row itself is a reorder handle; dragging the Edit link or selected text keeps its
            // native behaviour (and a Text-node target has no .matches).
            const target = e.target;
            if (!(target instanceof Element) || !target.matches('tr[data-id]')) return;
            if (isLocked(tbody)) { e.preventDefault(); return; }
            dragged = target;
            rowsBefore = [...tbody.children];
            dragged.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
        });

        tbody.addEventListener('dragover', e => {
            if (!dragged) return;
            e.preventDefault();
            const over = e.target.closest && e.target.closest('tr[data-id]');
            if (!over || over === dragged) return;
            const rect = over.getBoundingClientRect();
            const ref = e.clientY > rect.top + rect.height / 2 ? over.nextSibling : over;
            if (ref !== dragged && ref !== dragged.nextSibling) tbody.insertBefore(dragged, ref);
        });

        // A drop must be accepted for dragend to report dropEffect "move" rather than "none".
        tbody.addEventListener('drop', e => { if (dragged) e.preventDefault(); });

        tbody.addEventListener('dragend', e => {
            if (!dragged) return;
            dragged.classList.remove('dragging');
            const before = rowsBefore;
            dragged = null;
            rowsBefore = null;
            if (e.dataTransfer.dropEffect === 'none') {
                before.forEach(tr => tbody.appendChild(tr)); // cancelled (Esc / dropped outside): undo the preview
                return;
            }
            if (before.every((tr, i) => tbody.children[i] === tr)) return; // dropped where it started
            save();
        });

        tbody.addEventListener('click', e => {
            const btn = e.target.closest('[data-move]');
            if (!btn || isLocked(tbody)) return;
            const row = btn.closest('tr');
            const sibling = btn.dataset.move === 'up' ? row.previousElementSibling : row.nextElementSibling;
            if (!sibling) return;
            if (btn.dataset.move === 'up') tbody.insertBefore(row, sibling);
            else tbody.insertBefore(sibling, row);
            // Paging may have to turn to the moved row's new page before focus can stay on it.
            tbody.dispatchEvent(new CustomEvent('list:moved', { detail: row }));
            btn.focus(); // keep keyboard focus on the moved row's button
            save();
        });
    });

    // ---- Search + Live/Hidden filter (list pages) ----
    const tools = document.querySelector('[data-list-tools]');
    const tbody = document.querySelector('tbody[data-reorder-kind]');
    if (!tools || !tbody) return;
    const search = tools.querySelector('[data-list-search]');
    const radios = [...tools.querySelectorAll('input[name="list-filter"]')];
    const lockNote = document.querySelector('[data-list-lock-note]');
    const empty = document.querySelector('[data-list-empty]');
    const pager = document.querySelector('[data-list-pager]');
    const pageStatus = pager && pager.querySelector('[data-page-status]');
    const PAGE_SIZE = 15;
    let page = 0;   // zero-based; paging only shows and hides rows, so the full order stays in the DOM for saving
    let announceTimer = 0;

    function currentFilter() {
        const checked = radios.find(r => r.checked);
        return checked ? checked.value : 'all';
    }

    function apply(announce) {
        const query = search.value.trim().toLowerCase();
        const filter = currentFilter();
        const rows = [...tbody.querySelectorAll('tr[data-id]')];
        let shown = 0;
        let live = 0;
        const matching = [];
        rows.forEach(tr => {
            const published = tr.dataset.published === 'true';
            if (published) live++;
            const match = (!query || tr.dataset.search.includes(query))
                && (filter === 'all' || (filter === 'live') === published);
            if (match) { matching.push(tr); shown++; }
        });
        const pages = Math.max(1, Math.ceil(matching.length / PAGE_SIZE));
        page = Math.min(page, pages - 1);
        const onPage = new Set(matching.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE));
        rows.forEach(tr => { tr.hidden = !onPage.has(tr); });
        const first = page * PAGE_SIZE + 1;
        const last = Math.min(matching.length, (page + 1) * PAGE_SIZE);
        if (pager) {
            pager.hidden = pages <= 1;
            if (pageStatus) pageStatus.textContent = `${first}–${last} of ${matching.length}`;
            pager.querySelector('[data-page="prev"]').disabled = page === 0;
            pager.querySelector('[data-page="next"]').disabled = page >= pages - 1;
        }

        const locked = query !== '' || filter !== 'all';
        tbody.toggleAttribute('data-reorder-locked', locked);
        rows.forEach(tr => { tr.draggable = !locked; });
        tbody.querySelectorAll('[data-move]').forEach(b => { b.disabled = locked; });
        if (lockNote) lockNote.hidden = !locked;
        if (empty) empty.hidden = shown !== 0;

        const counts = { all: rows.length, live: live, hidden: rows.length - live };
        tools.querySelectorAll('[data-count]').forEach(el => { el.textContent = counts[el.dataset.count]; });

        if (announce && window.AdminUI) {
            // Debounced so typing doesn't produce an announcement per keystroke.
            clearTimeout(announceTimer);
            announceTimer = setTimeout(() => {
                window.AdminUI.announce(pages > 1
                    ? `Showing ${first}–${last} of ${matching.length}${locked ? ' matches' : ''}.`
                    : locked ? `${shown} of ${rows.length} shown.` : `All ${rows.length} shown.`);
            }, 500);
        }
    }

    search.addEventListener('input', () => { page = 0; apply(true); });
    radios.forEach(r => r.addEventListener('change', () => { page = 0; apply(true); }));
    search.addEventListener('keydown', e => {
        if (e.key === 'Escape' && search.value) { search.value = ''; page = 0; apply(true); }
    });
    if (pager) pager.addEventListener('click', e => {
        const btn = e.target.closest('[data-page]');
        if (!btn || btn.disabled) return;
        page += btn.dataset.page === 'next' ? 1 : -1;
        apply(false);
        tbody.closest('section').scrollIntoView({ block: 'start' });
        const firstRow = tbody.querySelector('tr[data-id]:not([hidden])');
        if (firstRow) firstRow.querySelector('a')?.focus({ preventScroll: true });
    });
    // A row moved past the edge of the page: turn to the page it is now on.
    tbody.addEventListener('list:moved', e => {
        const visible = [...tbody.querySelectorAll('tr[data-id]')];
        const index = visible.indexOf(e.detail);
        if (index >= 0) page = Math.floor(index / PAGE_SIZE);
        apply(false);
    });
    const reset = document.querySelector('[data-list-reset]');
    if (reset) reset.addEventListener('click', () => {
        search.value = '';
        radios.forEach(r => { r.checked = r.value === 'all'; });
        page = 0;
        apply(true);
        search.focus();
    });
    // A visibility toggle (admin.js) changes which rows the filter matches and the counts.
    document.addEventListener('admin:visibility-changed', () => {
        apply(false);
        // The toggled row may now be filtered out; don't leave keyboard focus on an invisible button.
        const active = document.activeElement;
        if (active && active.closest('tr[hidden]')) search.focus();
    });
    apply(false); // the browser may restore a search value on Back
})();
