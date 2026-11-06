
(function () {
    'use strict';

    const csrfToken = document.querySelector('meta[name="csrf-token"]')?.content ?? '';

    
    async function api(url, { method = 'GET', body } = {}) {
        const headers = { 'Accept': 'application/json' };
        if (method !== 'GET') headers['X-CSRF-TOKEN'] = csrfToken;
        if (body !== undefined) headers['Content-Type'] = 'application/json';

        const response = await fetch(url, {
            method,
            headers,
            credentials: 'same-origin',
            body: body === undefined ? undefined : JSON.stringify(body)
        });

        let data = null;
        try { data = await response.json(); } catch { /* empty body */ }

        if (!response.ok) {
            const message = data?.error || data?.title ||
                (response.status === 401 ? 'Please sign in to play.' : `Request failed (${response.status}).`);
            const error = new Error(message);
            error.status = response.status;
            throw error;
        }
        return data;
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, c =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function toast(message, kind = 'info', ms = 4000) {
        const area = document.getElementById('toast-area');
        if (!area) return;
        const el = document.createElement('div');
        el.className = `geo-toast geo-toast-${kind}`;
        el.setAttribute('role', kind === 'error' ? 'alert' : 'status');
        el.textContent = message;
        area.appendChild(el);
        setTimeout(() => el.classList.add('hide'), ms);
        setTimeout(() => el.remove(), ms + 400);
    }


    document.querySelectorAll('[data-country-picker]').forEach(form => {
        const search = form.querySelector('[data-country-search]');
        const select = form.querySelector('select');
        if (!search || !select) return;
        search.addEventListener('input', () => {
            const term = search.value.trim().toLowerCase();
            let firstVisible = null;
            for (const option of select.options) {
                const match = !term || option.dataset.name.includes(term);
                option.hidden = !match;
                if (match && !firstVisible) firstVisible = option;
            }
            if (firstVisible && (select.selectedOptions[0]?.hidden ?? true)) firstVisible.selected = true;
        });
    });

    window.geo = { api, escapeHtml, toast };
})();
