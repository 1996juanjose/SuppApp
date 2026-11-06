(() => {
    'use strict';
    const root = document.getElementById('waWorkspace');
    if (!root) return;
    const el = id => document.getElementById(id);
    const selected = new Map();
    const records = new Map([...document.querySelectorAll('.wa-select')].map(box => [Number(box.value), { recordId: Number(box.value), phone: box.dataset.phone, name: box.dataset.name || 'Registro', status: 'Pending' }]));
    let state, campaign, busy = false, creationId, popup, loaded = false;
    const endpoint = handler => root.dataset.endpoint + '?handler=' + handler;
    function feedback(text, error = false) {
        el('waFeedback').textContent = text;
        el('waFeedback').className = 'small mb-3 ' + (error ? 'text-danger' : 'text-success');
    }
    function showModal() { bootstrap.Modal.getOrCreateInstance(root).show(); }
    async function api(handler, body) {
        const response = await fetch(endpoint(handler), {
            method: body === undefined ? 'GET' : 'POST', credentials: 'same-origin',
            headers: body === undefined ? {} : { 'Content-Type': 'application/json', 'RequestVerificationToken': root.querySelector('[name="__RequestVerificationToken"]').value },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        if (response.redirected || response.status === 401) throw new Error('La sesión venció. Inicia sesión de nuevo; el progreso está guardado.');
        const data = await response.json().catch(() => null);
        if (!response.ok || !data) throw new Error(data?.error || 'No se pudo confirmar la acción. Cierra y vuelve a abrir este modal para actualizar el progreso.');
        return data;
    }
    function renderContacts() {
        const items = campaign ? campaign.contacts : [...selected.values()].map(id => records.get(id));
        const list = el('waContacts'); list.replaceChildren();
        el('waListCount').textContent = items.length;
        if (!items.length) { list.textContent = 'Marca los contactos en el listado para comenzar.'; return; }
        for (const item of items) {
            const row = document.createElement('div'); row.className = 'list-group-item d-flex justify-content-between align-items-center gap-2';
            const current = item.id && item.id === campaign?.current?.id;
            if (current) { row.classList.add('border-success', 'bg-success-subtle'); row.setAttribute('aria-current', 'true'); }
            const text = document.createElement('div'); text.className = 'text-break';
            const name = document.createElement('div'); name.className = 'fw-semibold small'; name.textContent = item.name || 'Registro';
            const phone = document.createElement('div'); phone.className = 'small text-muted'; phone.textContent = item.phone;
            const badge = document.createElement('span'); badge.className = 'badge ' + (item.status === 'Sent' ? 'text-bg-success' : 'text-bg-light');
            badge.textContent = item.status === 'Sent' ? 'Enviado' : item.status === 'Skipped' ? 'Omitido' : current ? 'Actual' : 'Pendiente';
            text.append(name, phone); row.append(text, badge); list.append(row);
        }
        const currentRow = list.querySelector('[aria-current="true"]');
        if (currentRow && list.clientHeight) {
            const offset = currentRow.getBoundingClientRect().top - list.getBoundingClientRect().top;
            if (offset < 0 || offset + currentRow.clientHeight > list.clientHeight) list.scrollTop += offset - list.clientHeight / 3;
        }
    }
    function paint() {
        el('waStart').textContent = campaign?.pending && !selected.size ? `Continuar WhatsApp (${campaign.pending} pendientes)` : `WhatsApp masivo (${selected.size})`;
        const c = campaign, pending = !!c?.pending, active = pending && c.status === 'Active';
        el('waProgress').textContent = c ? `${c.sent} de ${c.total} enviados · ${c.pending} pendientes${c.skipped ? ` · ${c.skipped} omitidos` : ''}${c.status === 'Paused' ? ' · Pausado' : c.status === 'Completed' ? ' · Completado' : ''}` : `${selected.size} contactos seleccionados`;
        el('waCurrentPanel').hidden = !c?.current;
        el('waCurrent').textContent = c?.current ? `${c.current.name || 'Registro'} · ${c.current.phone}${!c.current.allowed ? ' · No disponible: omitir para continuar' : ''}` : '';
        const dirty = pending && el('waMessage').value.trim() !== c.message;
        el('waMessage').disabled = busy;
        el('waConfirm').hidden = pending; el('waConfirm').disabled = busy || !selected.size;
        el('waConfirm').textContent = c?.status === 'Completed' ? 'Comenzar nueva selección' : 'Comenzar envíos';
        el('waOpen').hidden = !active; el('waOpen').disabled = busy || dirty || !c?.current?.allowed;
        el('waSent').hidden = !active; el('waSent').disabled = busy || dirty || !c?.current?.allowed || !c?.current?.openedAt;
        el('waSkip').hidden = !active; el('waSkip').disabled = busy;
        el('waPause').hidden = !active; el('waPause').disabled = busy;
        el('waContinue').hidden = !pending || c.status !== 'Paused'; el('waContinue').disabled = busy;
        el('waSaveMessage').hidden = false; el('waSaveMessage').disabled = busy;
        el('waSaveMessage').textContent = pending ? 'Guardar mensaje para los pendientes' : 'Guardar mensaje para después';
        el('waStart').disabled = busy;
        for (const box of document.querySelectorAll('.wa-select')) {
            if (box.dataset.selectable === undefined) box.dataset.selectable = box.disabled ? 'no' : 'yes';
            box.disabled = busy || box.dataset.selectable === 'no'; box.checked = selected.has(box.dataset.phone);
        }
        const boxes = [...document.querySelectorAll('.wa-select')].filter(x => x.dataset.selectable === 'yes');
        el('waSelectAll').disabled = busy || !boxes.length;
        el('waSelectAll').checked = boxes.length > 0 && boxes.every(x => selected.has(x.dataset.phone));
        el('waSelectAll').indeterminate = !el('waSelectAll').checked && boxes.some(x => selected.has(x.dataset.phone));
        document.querySelectorAll('.wa-single').forEach(button => { if (button.dataset.selectable === undefined) button.dataset.selectable = button.disabled ? 'no' : 'yes'; button.disabled = busy || button.dataset.selectable === 'no'; });
        renderContacts();
    }
    async function run(work) {
        if (busy) return;
        busy = true; paint();
        try { await work(); } catch (error) { feedback(error.message, true); }
        finally { busy = false; paint(); }
    }
    async function loadState(showCompleted = true) {
        state = await api('WhatsAppState');
        campaign = state.campaign?.pending || showCompleted ? state.campaign : null;
        if (!loaded || campaign?.pending) el('waMessage').value = campaign?.pending ? campaign.message : state.message;
        loaded = true;
        paint();
    }
    function changedSelection() { creationId = null; if (!campaign?.pending) campaign = null; paint(); }
    document.querySelectorAll('.wa-select').forEach(box => box.addEventListener('change', () => {
        if (box.checked) selected.set(box.dataset.phone, Number(box.value)); else selected.delete(box.dataset.phone);
        changedSelection();
    }));
    el('waSelectAll').addEventListener('change', event => {
        document.querySelectorAll('.wa-select:not(:disabled)').forEach(box => {
            if (event.target.checked) { if (!selected.has(box.dataset.phone)) selected.set(box.dataset.phone, Number(box.value)); }
            else selected.delete(box.dataset.phone);
        }); changedSelection();
    });
    async function openSelection() {
        feedback(''); el('waHistory').hidden = true;
        await loadState(false);
        if (campaign?.pending && selected.size) feedback('Continúa los pendientes guardados. Al terminarlos podrás iniciar otra selección.');
    }
    el('waStart').addEventListener('click', () => { showModal(); run(openSelection); });
    document.querySelectorAll('.wa-single').forEach(button => button.addEventListener('click', () => {
        const id = Number(button.dataset.record), record = records.get(id);
        selected.clear(); selected.set(record.phone, id); changedSelection(); showModal(); run(openSelection);
    }));
    el('waMessage').addEventListener('input', () => { creationId = null; paint(); });
    el('waSaveMessage').addEventListener('click', () => run(async () => { const editing = !!campaign?.pending; await api('WhatsAppMessage', { campaignId: editing ? campaign.id : undefined, message: el('waMessage').value }); await loadState(); feedback(editing ? 'Mensaje actualizado. Abre WhatsApp Web nuevamente para usarlo.' : 'Mensaje guardado.'); }));
    function reserveWindow() {
        if (popup && !popup.closed) return popup;
        popup = window.open('about:blank', 'oldschool_whatsapp_assisted'); if (popup) popup.opener = null;
        return popup;
    }
    function markReactivated(phone, at) {
        document.querySelectorAll('.wa-row-state').forEach(row => {
            if (row.dataset.waPhone !== phone) return;
            row.textContent = `Reactivado: Sí · ${(at || '').replace('T', ' ').slice(0, 16)}`;
            row.className = 'small wa-row-state text-success'; row.dataset.sent = 'yes';
        });
    }
    async function openCurrent(target) {
        const c = campaign;
        if (!c?.current || c.status !== 'Active') return;
        await api('WhatsAppAction', { campaignId: c.id, recipientId: c.current.id, action: 'open' });
        const link = `https://web.whatsapp.com/send?phone=${c.current.phone}&text=${encodeURIComponent(c.current.message)}`;
        if (target && !target.closed) { target.location = link; target.focus(); feedback('WhatsApp abierto. Envía el mensaje y vuelve para confirmar.'); }
        else {
            feedback('El navegador bloqueó la pestaña. Abre el enlace: ');
            const anchor = document.createElement('a'); anchor.href = link; anchor.target = 'oldschool_whatsapp_assisted'; anchor.rel = 'noopener'; anchor.textContent = 'Abrir WhatsApp'; el('waFeedback').append(anchor);
        }
        await loadState();
    }
    el('waConfirm').addEventListener('click', () => {
        if (busy) return;
        if (!el('waMessage').value.trim()) { feedback('Escribe el mensaje para comenzar.', true); el('waMessage').focus(); return; }
        const target = reserveWindow();
        run(async () => {
            creationId ||= crypto.randomUUID();
            await api('WhatsAppMessage', { message: el('waMessage').value });
            await api('WhatsAppCreate', { campaignId: creationId, ids: [...selected.values()], message: el('waMessage').value, excludePrevious: false });
            selected.clear(); await loadState(); feedback('Envía el mensaje en WhatsApp y vuelve para confirmar.'); await openCurrent(target);
        });
    });
    el('waOpen').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => openCurrent(target)); } });
    async function action(name, target) {
        const c = campaign; if (!c) return;
        const accepted = await api('WhatsAppAction', { campaignId: c.id, recipientId: c.current?.id, action: name });
        if (name === 'sent') markReactivated(c.current.phone, accepted.at);
        await loadState(); feedback(name === 'sent' ? 'Reactivación guardada.' : name === 'skip' ? 'Contacto omitido.' : name === 'pause' ? 'Pendientes guardados. Puedes continuar después.' : 'Envíos reanudados.');
        if (target) await openCurrent(target);
    }
    el('waSent').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => action('sent', target)); } });
    el('waSkip').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => action('skip', target)); } });
    el('waPause').addEventListener('click', () => run(() => action('pause')));
    el('waContinue').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => action('continue', target)); } });
    document.querySelectorAll('.wa-history').forEach(button => button.addEventListener('click', () => {
        showModal(); run(async () => {
            await loadState(false);
            const data = await api('WhatsAppHistory&recordId=' + encodeURIComponent(button.dataset.record));
            const body = el('waHistoryBody'); body.replaceChildren();
            if (!data.length) body.textContent = 'Sin reactivaciones registradas.';
            for (const item of data) {
                const block = document.createElement('div'); block.className = 'border-bottom py-2';
                const heading = document.createElement('div'); heading.textContent = `${item.status === 'Sent' ? 'Enviado' : item.status === 'Skipped' ? 'Omitido' : 'Pendiente'} · ${item.phone} · ${item.actorName || ''} · ${(item.declaredSentAt || item.openedAt || '').replace('T', ' ').slice(0, 19)}`;
                const message = document.createElement('div'); message.style.whiteSpace = 'pre-wrap'; message.textContent = item.message;
                block.append(heading, message); body.append(block);
            }
            el('waHistory').hidden = false; el('waHistory').open = true;
        });
    }));
    run(() => loadState(false));
})();
