// Admin shell behaviour: toasts, the sidebar drawer, the delete confirmation dialog, in-place visibility toggles,
// and form helpers (unsaved-changes guard, character counters, upload drop zones, password reveal).
// External file because the CSP only allows 'self' scripts; nothing here is required for a form to submit.
(function () {
    'use strict';
    document.documentElement.classList.remove('no-js');

    // Long enough for the admin to read why the page is about to reload.
    const RELOAD_AFTER_ERROR_MS = 1500;
    const TOAST_MS = 5000;

    // ---------------------------------------------------------------- Toasts + announcements
    const politeRegion = document.getElementById('toast-polite');
    const assertiveRegion = document.getElementById('toast-assertive');
    const stack = document.getElementById('toast-stack');

    function announce(message, assertive) {
        const region = assertive ? assertiveRegion : politeRegion;
        if (!region) return;
        // Clearing first makes a repeated identical message announce again.
        region.textContent = '';
        setTimeout(() => { region.textContent = message; }, 60);
    }

    function toast(message, type) {
        const isError = type === 'error';
        announce(message, isError);
        if (!stack) return;
        const item = document.createElement('div');
        item.className = 'toast-item ' + (isError ? 'toast-error' : 'toast-success');
        const text = document.createElement('p');
        text.textContent = message;
        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'toast-close';
        close.setAttribute('aria-label', 'Dismiss notification');
        close.textContent = '×';
        item.append(text, close);
        stack.appendChild(item);

        let timer = 0;
        const dismiss = () => {
            clearTimeout(timer);
            item.classList.add('is-leaving');
            setTimeout(() => item.remove(), 200);
        };
        // Errors stay until dismissed; success toasts pause while hovered or focused so they can be read.
        const arm = () => { if (!isError) timer = setTimeout(dismiss, TOAST_MS); };
        close.addEventListener('click', dismiss);
        item.addEventListener('mouseenter', () => clearTimeout(timer));
        item.addEventListener('mouseleave', arm);
        item.addEventListener('focusin', () => clearTimeout(timer));
        item.addEventListener('focusout', arm);
        arm();
    }

    window.AdminUI = { toast: toast, announce: announce };

    const flash = document.getElementById('flash-message');
    if (flash) toast(flash.textContent.trim(), flash.dataset.type);

    // ---------------------------------------------------------------- Sidebar drawer (narrow screens)
    const toggle = document.querySelector('[data-sidebar-toggle]');
    const sidebar = document.getElementById('admin-sidebar');
    const scrim = document.querySelector('.sidebar-scrim');
    const main = document.querySelector('.shell-main');
    const drawerQuery = window.matchMedia('(max-width: 991.98px)');

    function setDrawer(open, restoreFocus) {
        if (!toggle || !sidebar) return;
        document.body.classList.toggle('nav-open', open);
        toggle.setAttribute('aria-expanded', String(open));
        if (scrim) scrim.hidden = !open;
        // The rest of the page is inert while the drawer covers it, which also keeps Tab inside the drawer.
        if (main) main.inert = open;
        if (open) {
            const first = sidebar.querySelector('[aria-current="page"]') || sidebar.querySelector('a, button');
            if (first) first.focus();
        } else if (restoreFocus) {
            toggle.focus();
        }
    }

    if (toggle) {
        toggle.addEventListener('click', () => setDrawer(!document.body.classList.contains('nav-open'), false));
        document.querySelectorAll('[data-sidebar-close]').forEach(el => el.addEventListener('click', () => setDrawer(false, true)));
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && document.body.classList.contains('nav-open')) setDrawer(false, true);
        });
        drawerQuery.addEventListener('change', () => { if (!drawerQuery.matches) setDrawer(false, false); });
    }

    // ---------------------------------------------------------------- Confirmation dialog
    const dialog = document.getElementById('confirm-dialog');
    let pendingForm = null;
    let opener = null;

    document.addEventListener('submit', e => {
        const form = e.target;
        const message = form.getAttribute('data-confirm');
        if (!message) return;
        e.preventDefault();
        if (!dialog || typeof dialog.showModal !== 'function') {
            if (window.confirm(message)) form.submit();
            return;
        }
        dialog.querySelector('#confirm-title').textContent = form.dataset.confirmTitle || 'Are you sure?';
        dialog.querySelector('#confirm-message').textContent = message;
        dialog.querySelector('#confirm-ok').textContent = form.dataset.confirmAction || 'Delete';
        pendingForm = form;
        opener = e.submitter || document.activeElement;
        dialog.returnValue = '';
        dialog.showModal();
        // The safe choice gets focus, so Enter right away doesn't delete anything.
        dialog.querySelector('button[value="cancel"]').focus();
    });

    // Acts on the button click itself rather than the dialog's async "close" event, which a backgrounded tab
    // may not deliver promptly.
    function settleDialog(confirmed) {
        const form = pendingForm;
        const returnTo = opener;
        pendingForm = null;
        opener = null;
        if (dialog.open) dialog.close(confirmed ? 'confirm' : 'cancel');
        if (confirmed && form) {
            // submit() skips the submit event, so this doesn't loop back into the dialog.
            form.submit();
            return;
        }
        if (returnTo && document.contains(returnTo)) returnTo.focus();
    }

    if (dialog) {
        dialog.querySelector('#confirm-ok').addEventListener('click', e => { e.preventDefault(); settleDialog(true); });
        dialog.querySelector('button[value="cancel"]').addEventListener('click', e => { e.preventDefault(); settleDialog(false); });
        // Esc fires "cancel"; a click on the backdrop (the dialog element itself) cancels too.
        dialog.addEventListener('cancel', e => { e.preventDefault(); settleDialog(false); });
        dialog.addEventListener('click', e => {
            if (e.target !== dialog) return;
            const r = dialog.getBoundingClientRect();
            const outside = e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom;
            if (outside) settleDialog(false); // the dialog's own padding also targets the dialog element
        });
    }

    // ---------------------------------------------------------------- Live / Hidden toggles
    document.addEventListener('submit', async e => {
        const form = e.target.closest('[data-visibility-form]');
        if (!form || e.defaultPrevented) return;
        e.preventDefault();
        if (form.dataset.busy) return;
        form.dataset.busy = '1';
        const button = form.querySelector('button');
        const row = form.closest('tr');
        const flag = form.querySelector('input[name="isPublished"]');
        const makeLive = flag.value === 'true';
        const title = row ? row.dataset.title : 'Item';
        button.setAttribute('aria-busy', 'true');
        try {
            const res = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                // Same contract as admin-lists.js: an expired session answers 401 instead of a login redirect.
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin',
            });
            if (res.status === 401 || res.redirected) {
                toast('Your session has expired — reloading to sign in…', 'error');
                setTimeout(() => location.reload(), RELOAD_AFTER_ERROR_MS);
                return;
            }
            if (!res.ok) throw new Error(String(res.status));
            flag.value = String(!makeLive);
            button.classList.toggle('is-live', makeLive);
            button.classList.toggle('is-hidden', !makeLive);
            button.querySelector('.vis-text').textContent = makeLive ? 'Live' : 'Hidden';
            button.setAttribute('aria-label', makeLive ? `Live. Hide ${title} from the site` : `Hidden. Show ${title} on the site`);
            if (row) {
                row.dataset.published = String(makeLive);
                row.classList.toggle('row-hidden', !makeLive);
            }
            toast(makeLive ? `“${title}” is now live on the site.` : `“${title}” is hidden from the site.`, 'success');
            document.dispatchEvent(new CustomEvent('admin:visibility-changed'));
        } catch (err) {
            toast(`Couldn't change the visibility of “${title}”. Please try again.`, 'error');
        } finally {
            delete form.dataset.busy;
            button.removeAttribute('aria-busy');
        }
    });

    // ---------------------------------------------------------------- Unsaved-changes guard
    const guarded = [...document.querySelectorAll('form[data-dirty-guard]')];
    let leavingBySubmit = false;

    function snapshot(form) {
        return JSON.stringify([...new FormData(form)]
            .filter(([name, value]) => typeof value === 'string' && name !== '__RequestVerificationToken'));
    }

    guarded.forEach(form => {
        // A form re-rendered with validation errors holds edits that were never saved, so it starts dirty.
        const rejected = form.querySelector('.input-validation-error, .validation-summary-errors');
        form.dataset.initial = rejected ? '' : snapshot(form);
        const indicator = form.querySelector('[data-dirty-indicator]');
        const update = () => {
            const dirty = snapshot(form) !== form.dataset.initial;
            form.classList.toggle('is-dirty', dirty);
            if (indicator) indicator.hidden = !dirty;
        };
        form.addEventListener('input', update);
        form.addEventListener('change', update);
        update();
    });

    // Registered on document so it runs after jQuery validation, which cancels an invalid submit.
    document.addEventListener('submit', e => {
        if (guarded.includes(e.target) && !e.defaultPrevented) leavingBySubmit = true;
    });
    window.addEventListener('pageshow', () => { leavingBySubmit = false; });
    window.addEventListener('beforeunload', e => {
        if (leavingBySubmit) return;
        if (guarded.some(form => snapshot(form) !== form.dataset.initial)) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

    // ---------------------------------------------------------------- Character counters
    const counted = 'input[maxlength]:not([type="hidden"]):not([type="file"]):not([type="number"]):not([type="month"]), textarea[maxlength]';
    document.querySelectorAll('.admin-form').forEach(form => form.querySelectorAll(counted).forEach(field => {
        const max = field.maxLength;
        if (!(max > 0) || !field.id) return;
        const counter = document.createElement('span');
        counter.className = 'char-count';
        counter.id = field.id + '-count';
        const describedBy = (field.getAttribute('aria-describedby') || '').split(' ').filter(Boolean);
        field.setAttribute('aria-describedby', describedBy.concat(counter.id).join(' '));
        const host = field.closest('.field');
        const meta = host && host.querySelector('.field-meta');
        if (meta) meta.appendChild(counter); else field.insertAdjacentElement('afterend', counter);

        let warned = false;
        const update = () => {
            const len = field.value.length;
            counter.textContent = `${len.toLocaleString()} / ${max.toLocaleString()}`;
            const near = len >= max * 0.9;
            counter.classList.toggle('is-near', near && len < max);
            counter.classList.toggle('is-full', len >= max);
            // One announcement when crossing into the last 10%, not one per keystroke.
            if (near && !warned) announce(`${(max - len).toLocaleString()} characters left`);
            warned = near;
        };
        field.addEventListener('input', update);
        warned = field.value.length >= max * 0.9;
        update();
    }));

    // ---------------------------------------------------------------- Validation state for assistive tech
    function syncValidation(root) {
        root.querySelectorAll('[data-valmsg-for]').forEach(msg => {
            const field = root.querySelector(`[name="${CSS.escape(msg.dataset.valmsgFor)}"]`);
            if (!field) return;
            if (!msg.id) msg.id = (field.id || field.name) + '-error';
            const ids = (field.getAttribute('aria-describedby') || '').split(' ').filter(Boolean);
            if (!ids.includes(msg.id)) field.setAttribute('aria-describedby', ids.concat(msg.id).join(' '));
        });
        root.querySelectorAll('input, textarea, select').forEach(field => {
            if (field.classList.contains('input-validation-error')) field.setAttribute('aria-invalid', 'true');
            else field.removeAttribute('aria-invalid');
        });
    }

    document.querySelectorAll('form').forEach(form => {
        syncValidation(form);
        new MutationObserver(() => syncValidation(form))
            .observe(form, { subtree: true, attributes: true, attributeFilter: ['class'] });
    });
    // A server-side validation failure re-renders the form: put focus on the first problem.
    const firstInvalid = document.querySelector('.admin-form .input-validation-error, .auth-card .input-validation-error');
    const summary = document.querySelector('.validation-summary-errors');
    if (firstInvalid) firstInvalid.focus();
    else if (summary) { summary.setAttribute('tabindex', '-1'); summary.focus(); }

    // ---------------------------------------------------------------- Upload drop zones
    function formatSize(bytes) {
        return bytes >= 1048576 ? (bytes / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(bytes / 1024)) + ' KB';
    }

    function accepts(input, file) {
        const accept = (input.getAttribute('accept') || '').split(',').map(s => s.trim().toLowerCase()).filter(Boolean);
        if (!accept.length) return true;
        const name = file.name.toLowerCase();
        const type = (file.type || '').toLowerCase();
        return accept.some(a => a.startsWith('.') ? name.endsWith(a)
            : a.endsWith('/*') ? type.startsWith(a.slice(0, -1)) : type === a);
    }

    document.querySelectorAll('[data-dropzone]').forEach(zone => {
        const input = zone.querySelector('input[type="file"]');
        const preview = zone.querySelector('.dz-preview');
        const thumb = zone.querySelector('.dz-thumb');
        const fileName = zone.querySelector('.dz-name');
        const error = zone.querySelector('.dz-error');
        const clear = zone.querySelector('[data-dz-clear]');
        const maxBytes = Number(zone.dataset.maxMb || 0) * 1048576;
        if (!input) return;

        const list = zone.querySelector('.dz-list');
        const form = input.form;
        const uploadLabel = form && form.querySelector('[data-upload-label]');

        function reset() {
            input.value = '';
            zone.classList.remove('has-file');
            if (preview) preview.hidden = true;
            if (thumb) { thumb.removeAttribute('src'); thumb.hidden = true; }
            if (list) { list.replaceChildren(); list.hidden = true; }
            if (uploadLabel) uploadLabel.textContent = 'Upload';
        }

        // Several files: list them by name and size, check the batch against the per-upload limit.
        function showMany(files) {
            showError('');
            const bad = files.find(f => !accepts(input, f));
            if (bad) { reset(); showError(`“${bad.name}” isn't a supported file type.`); return; }
            const total = files.reduce((sum, f) => sum + f.size, 0);
            if (maxBytes && total > maxBytes) {
                reset();
                showError(`These files add up to ${formatSize(total)}; one upload can be ${zone.dataset.maxMb} MB. Send them in smaller batches.`);
                return;
            }
            zone.classList.add('has-file');
            if (thumb) thumb.hidden = true;
            if (fileName) fileName.textContent = `${files.length} files · ${formatSize(total)}`;
            if (preview) preview.hidden = false;
            if (list) {
                list.replaceChildren(...files.map(f => {
                    const li = document.createElement('li');
                    li.textContent = `${f.name} · ${formatSize(f.size)}`;
                    return li;
                }));
                list.hidden = false;
            }
            if (uploadLabel) uploadLabel.textContent = `Upload ${files.length} files`;
            announce(`${files.length} files selected. Press the upload button to send them.`);
        }

        function pick(files) {
            if (!files.length) { reset(); return; }
            if (input.multiple && files.length > 1) { showMany(files); return; }
            if (list) { list.replaceChildren(); list.hidden = true; }
            if (uploadLabel) uploadLabel.textContent = 'Upload';
            show(files[0]);
        }

        function showError(message) {
            if (!error) return;
            error.textContent = message;
            error.hidden = !message;
            if (message) announce(message, true);
        }

        function show(file) {
            showError('');
            if (!accepts(input, file)) { reset(); showError(`“${file.name}” isn't a supported file type.`); return; }
            if (maxBytes && file.size > maxBytes) { reset(); showError(`“${file.name}” is ${formatSize(file.size)}; the limit is ${zone.dataset.maxMb} MB.`); return; }
            zone.classList.add('has-file');
            if (fileName) fileName.textContent = `${file.name} · ${formatSize(file.size)}`;
            if (preview) preview.hidden = false;
            // data: URLs rather than blob: ones, which the CSP's img-src doesn't allow.
            if (thumb && file.type.startsWith('image/')) {
                const reader = new FileReader();
                reader.onload = () => { thumb.src = reader.result; thumb.hidden = false; };
                reader.readAsDataURL(file);
            } else if (thumb) {
                thumb.hidden = true;
            }
            announce(`${file.name} selected. Press the upload button to send it.`);
        }

        input.addEventListener('change', () => pick(input.files ? [...input.files] : []));
        ['dragenter', 'dragover'].forEach(type => zone.addEventListener(type, e => {
            if (!e.dataTransfer || ![...e.dataTransfer.types].includes('Files')) return;
            e.preventDefault();
            zone.classList.add('is-over');
        }));
        ['dragleave', 'dragend'].forEach(type => zone.addEventListener(type, e => {
            if (!zone.contains(e.relatedTarget)) zone.classList.remove('is-over');
        }));
        zone.addEventListener('drop', e => {
            zone.classList.remove('is-over');
            if (!e.dataTransfer || !e.dataTransfer.files.length) return;
            e.preventDefault();
            const dropped = input.multiple ? [...e.dataTransfer.files] : [e.dataTransfer.files[0]];
            const dt = new DataTransfer();
            dropped.forEach(f => dt.items.add(f));
            input.files = dt.files;
            pick([...input.files]);
        });
        if (clear) clear.addEventListener('click', () => { reset(); showError(''); input.focus(); });
    });

    // Uploads can take a while: show that the button is working. Nothing is locked: a repeat submit adds nothing, since
    // a file already in the gallery is skipped. The state is cleared when the page is shown again from history.
    const uploadForms = [...document.querySelectorAll('[data-upload-form]')];
    uploadForms.forEach(form => form.addEventListener('submit', e => {
        if (e.defaultPrevented) return;
        const button = form.querySelector('[data-upload-button]');
        const label = button && button.querySelector('[data-upload-label]');
        if (button) button.setAttribute('aria-busy', 'true');
        if (label) { label.dataset.idle = label.textContent; label.textContent = 'Uploading…'; }
        announce('Uploading. This can take a moment for videos.');
    }));
    window.addEventListener('pageshow', () => uploadForms.forEach(form => {
        const button = form.querySelector('[data-upload-button]');
        const label = button && button.querySelector('[data-upload-label]');
        if (button) button.removeAttribute('aria-busy');
        if (label && label.dataset.idle) label.textContent = label.dataset.idle;
    }));

    // An edited, unsaved caption is highlighted (its own form; the page-level unsaved-changes guard belongs to the
    // project form, and guarding these too would switch that warning off whenever a caption is saved).
    document.querySelectorAll('[data-caption-form]').forEach(form => {
        const input = form.querySelector('input[name="caption"]');
        if (!input) return;
        const saved = input.value;
        input.addEventListener('input', () => form.classList.toggle('is-edited', input.value !== saved));
    });

    // ---------------------------------------------------------------- Project card preview
    document.querySelectorAll('[data-preview-text]').forEach(input => {
        const targets = document.querySelectorAll(`[data-preview="${input.dataset.previewText}"]`);
        if (!targets.length) return;
        const update = () => targets.forEach(t => {
            const value = input.value.trim();
            t.textContent = value || t.dataset.empty || '';
            t.classList.toggle('is-placeholder', !value && !!t.dataset.empty);
        });
        input.addEventListener('input', update);
    });
    const coverInput = document.querySelector('#ImageUrl');
    const coverPreview = document.querySelector('[data-preview-cover]');
    if (coverInput && coverPreview) {
        const empty = coverPreview.parentElement.querySelector('.card-preview-empty');
        const update = () => {
            const value = coverInput.value.trim();
            const ok = /^(https:\/\/|\/)/i.test(value);
            if (ok) coverPreview.src = value; else coverPreview.removeAttribute('src');
            coverPreview.hidden = !ok;
            if (empty) empty.hidden = ok;
        };
        coverPreview.addEventListener('error', () => { coverPreview.hidden = true; if (empty) empty.hidden = false; });
        coverInput.addEventListener('change', update);
    }

    // A file dropped outside a drop zone would otherwise navigate away from the form.
    window.addEventListener('dragover', e => { if (!e.target.closest || !e.target.closest('[data-dropzone]')) e.preventDefault(); });
    window.addEventListener('drop', e => { if (!e.target.closest || !e.target.closest('[data-dropzone]')) e.preventDefault(); });

    // ---------------------------------------------------------------- Image URL preview
    document.querySelectorAll('[data-url-preview]').forEach(input => {
        const img = document.querySelector(input.dataset.urlPreview);
        if (!img) return;
        const update = () => {
            const value = input.value.trim();
            if (/^(https:\/\/|\/)/i.test(value)) { img.src = value; img.hidden = false; }
            else { img.removeAttribute('src'); img.hidden = true; }
        };
        img.addEventListener('error', () => { img.hidden = true; });
        input.addEventListener('change', update);
        input.addEventListener('blur', update);
        update();
    });

    // ---------------------------------------------------------------- Password reveal + Caps Lock hint
    document.querySelectorAll('[data-password-toggle]').forEach(btn => {
        const input = document.getElementById(btn.getAttribute('aria-controls'));
        if (!input) return;
        btn.hidden = false;
        btn.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.setAttribute('aria-pressed', String(show));
            input.focus();
        });
        // Never leave the password readable in the page if the form is submitted while revealed.
        if (input.form) input.form.addEventListener('submit', () => { input.type = 'password'; btn.setAttribute('aria-pressed', 'false'); });
    });

    document.querySelectorAll('[data-capslock-hint]').forEach(hint => {
        const input = document.getElementById(hint.dataset.capslockHint);
        if (!input) return;
        const check = e => { if (e.getModifierState) hint.hidden = !e.getModifierState('CapsLock'); };
        input.addEventListener('keyup', check);
        input.addEventListener('keydown', check);
        input.addEventListener('blur', () => { hint.hidden = true; });
    });
})();
