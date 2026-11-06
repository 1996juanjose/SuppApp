(() => {
    'use strict';
    const root = document.getElementById('waWorkspace');
    if (!root) return;
    const el = id => document.getElementById(id);
    const selected = new Map();
    let state, busy = false, previewIds = [], creationId, popup, messageLoaded = false;
    const endpoint = handler => root.dataset.endpoint + '?handler=' + handler;
    const feedback = (text, error = false) => {
        el('waFeedback').textContent = text;
        el('waFeedback').className = 'small mt-2 ' + (error ? 'text-danger' : 'text-success');
    };
    async function api(handler, body) {
        const response = await fetch(endpoint(handler), {
            method: body === undefined ? 'GET' : 'POST',
            credentials: 'same-origin',
            headers: body === undefined ? {} : { 'Content-Type': 'application/json', 'RequestVerificationToken': root.querySelector('[name="__RequestVerificationToken"]').value },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        if (response.redirected || response.status === 401) throw new Error('La sesión venció. Vuelve a iniciar sesión; el progreso está guardado.');
        const data = await response.json().catch(() => null);
        if (!response.ok || !data) throw new Error(data?.error || 'No se pudo confirmar la operación. Actualiza el progreso antes de continuar.');
        return data;
    }
    function paint() {
        el('waSelectedCount').textContent = selected.size;
        const c = state?.campaign;
        el('waCampaign').hidden = !c;
        if (state) el('waMetrics').textContent = `Números de la empresa: ${state.totalClients} · Con envío: ${state.contacted} · Sin envío: ${state.totalClients - state.contacted} · Con enlace generado: ${state.generated}`;
        if (c) {
            el('waProgress').textContent = `${c.sent} de ${c.total} enviados · ${c.pending} pendientes · ${c.skipped} omitidos${c.status === 'Paused' ? ' · Pausada' : c.status === 'Completed' ? ' · Terminada' : ''}`;
            el('waCampaignId').textContent = 'Reactivación · ' + c.id;
            el('waCampaignMessage').textContent = c.message;
            el('waCurrent').textContent = c.current ? `${c.current.name || 'Registro'} · ${c.current.phone}${!c.current.allowed ? ' · Sin autorización vigente: omitir para continuar' : c.current.openedAt ? ' · Enlace generado, pendiente de confirmar envío' : ''}` : 'Selección terminada. Puedes seleccionar otros registros.';
        }
        for (const id of ['waStart','waSaveMessage','waConfirm','waCancelPreview','waSettingsToggle','waRefresh']) el(id).disabled = busy;
        for (const control of document.querySelectorAll('.wa-single,.wa-select')) {
            if (control.dataset.busyEligible === undefined) control.dataset.busyEligible = control.disabled ? 'no' : 'yes';
            control.disabled = busy || control.dataset.busyEligible === 'no';
        }
        el('waOpen').disabled = busy || !c?.current?.allowed || c.status !== 'Active';
        el('waSent').disabled = busy || !c?.current?.allowed || !c.current.openedAt || c.status !== 'Active';
        el('waSkip').disabled = busy || !c?.current || c.status !== 'Active';
        el('waPause').hidden = c?.status !== 'Active'; el('waPause').disabled = busy;
        el('waContinue').hidden = !c?.pending || c.status !== 'Paused'; el('waContinue').disabled = busy;
        const boxes = [...document.querySelectorAll('.wa-select')].filter(x => x.dataset.busyEligible === 'yes');
        el('waSelectAll').disabled = busy || !boxes.length;
        el('waSelectAll').checked = boxes.length > 0 && boxes.every(x => selected.has(x.dataset.phone));
        el('waSelectAll').indeterminate = !el('waSelectAll').checked && boxes.some(x => selected.has(x.dataset.phone));
    }
    async function run(work) {
        if (busy) return;
        busy = true; paint();
        try { await work(); }
        catch (error) { feedback(error.message, true); }
        finally { busy = false; paint(); }
    }
    async function loadState() {
        state = await api('WhatsAppState');
        if (!messageLoaded) { el('waMessage').value = state.message; messageLoaded = true; }
        paint();
    }
    function syncSelection() {
        document.querySelectorAll('.wa-select').forEach(x => x.checked = selected.has(x.dataset.phone));
        paint();
    }
    document.querySelectorAll('.wa-select').forEach(box => box.addEventListener('change', () => {
        if (box.checked) selected.set(box.dataset.phone, Number(box.value)); else selected.delete(box.dataset.phone);
        el('waPreview').hidden = true; creationId = null; syncSelection();
    }));
    el('waSelectAll').addEventListener('change', event => {
        document.querySelectorAll('.wa-select:not(:disabled)').forEach(box => {
            if (event.target.checked) { if (!selected.has(box.dataset.phone)) selected.set(box.dataset.phone, Number(box.value)); }
            else selected.delete(box.dataset.phone);
        });
        el('waPreview').hidden = true; creationId = null; syncSelection();
    });
    const settings = () => { el('waSettings').open = true; root.scrollIntoView({ behavior: 'smooth', block: 'start' }); };
    el('waSettingsToggle').addEventListener('click', () => { el('waSettings').open = !el('waSettings').open; root.scrollIntoView({ behavior: 'smooth', block: 'start' }); });
    el('waSaveMessage').addEventListener('click', () => run(async () => { await api('WhatsAppMessage', { message: el('waMessage').value }); feedback('Mensaje guardado.'); }));
    async function preview(ids) {
        if (!ids.length) throw new Error('Selecciona al menos un número habilitado del listado.');
        if (state?.campaign?.pending) { root.scrollIntoView({ behavior: 'smooth' }); throw new Error('Continúa la selección pendiente antes de iniciar otra.'); }
        if (!el('waMessage').value.trim()) { settings(); throw new Error('Escribe y guarda el mensaje común.'); }
        previewIds = ids;
        creationId = null;
        const data = await api('WhatsAppPreview', { ids, excludePrevious: el('waExclude').checked });
        el('waPreviewText').textContent = `${data.selected} registros · ${data.duplicates} duplicados · ${data.invalid} inválidos · ${data.withoutConsent} sin autorización vigente · ${data.previouslyContacted} con envíos anteriores. Se incluirán ${data.eligible} números.${data.previouslyContacted && !el('waExclude').checked ? ' Atención: volverás a contactar números con envíos anteriores.' : ''}`;
        el('waPreview').hidden = false;
        root.scrollIntoView({ behavior: 'smooth' });
    }
    el('waStart').addEventListener('click', () => run(() => preview([...selected.values()])));
    document.querySelectorAll('.wa-single').forEach(button => button.addEventListener('click', () => run(() => preview([Number(button.dataset.record)]))));
    el('waCancelPreview').addEventListener('click', () => { el('waPreview').hidden = true; creationId = null; });
    el('waExclude').addEventListener('change', () => { el('waPreview').hidden = true; creationId = null; });
    el('waMessage').addEventListener('input', () => { el('waPreview').hidden = true; creationId = null; });
    // One named browsing context, opened only from explicit clicks; there is no send automation.
    function reserveWindow() {
        if (popup && !popup.closed) return popup;
        popup = window.open('about:blank', 'oldschool_whatsapp_assisted');
        if (popup) popup.opener = null;
        return popup;
    }
    function markRow(phone, status, at) {
        document.querySelectorAll('.wa-row-state').forEach(row => {
            if (row.dataset.waPhone !== phone || status === 'Generado' && row.dataset.sent === 'yes') return;
            row.textContent = `Reactivación · ${status} ${(at || '').replace('T', ' ').slice(0, 16)}`;
            row.className = 'small wa-row-state ' + (status === 'Enviado' ? 'text-success' : 'text-muted');
            if (status === 'Enviado') row.dataset.sent = 'yes';
        });
    }
    async function openCurrent(target) {
        const c = state?.campaign;
        if (!c?.current || c.status !== 'Active') { if (target && !target.closed) target.close(); return; }
        const accepted = await api('WhatsAppAction', { campaignId: c.id, recipientId: c.current.id, action: 'open' });
        markRow(c.current.phone, 'Generado', accepted.at);
        const link = `https://wa.me/${c.current.phone}?text=${encodeURIComponent(c.current.message)}`;
        if (target && !target.closed) { target.location = link; target.focus(); }
        else {
            feedback('Enlace generado. El navegador bloqueó la pestaña; ábrelo aquí: ');
            const anchor = document.createElement('a'); anchor.href = link; anchor.target = 'oldschool_whatsapp_assisted'; anchor.rel = 'noopener'; anchor.textContent = 'Abrir WhatsApp'; el('waFeedback').append(anchor);
        }
        await loadState();
    }
    el('waConfirm').addEventListener('click', () => {
        if (busy) return;
        const target = reserveWindow();
        run(async () => {
            creationId ||= crypto.randomUUID();
            await api('WhatsAppMessage', { message: el('waMessage').value });
            await api('WhatsAppCreate', { campaignId: creationId, ids: previewIds, message: el('waMessage').value, excludePrevious: el('waExclude').checked });
            el('waPreview').hidden = true; selected.clear(); syncSelection();
            await loadState();
            feedback('Selección guardada. Envía manualmente el mensaje en WhatsApp y confirma aquí.');
            await openCurrent(target);
        });
    });
    el('waOpen').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => openCurrent(target)); } });
    async function action(name, target) {
        const c = state?.campaign; if (!c) return;
        const accepted = await api('WhatsAppAction', { campaignId: c.id, recipientId: c.current?.id, action: name });
        if (name === 'sent') markRow(c.current.phone, 'Enviado', accepted.at);
        await loadState();
        feedback(name === 'sent' ? 'Envío declarado guardado permanentemente.' : name === 'skip' ? 'Contacto omitido.' : name === 'pause' ? 'Progreso guardado. Puedes continuar después.' : 'Selección reanudada.');
        if (target) await openCurrent(target);
    }
    el('waSent').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => action('sent', target)); } });
    el('waSkip').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => action('skip', target)); } });
    el('waPause').addEventListener('click', () => run(() => action('pause')));
    el('waContinue').addEventListener('click', () => { if (!busy) { const target = reserveWindow(); run(() => action('continue', target)); } });
    el('waRefresh').addEventListener('click', () => run(loadState));
    document.querySelectorAll('.wa-history').forEach(button => button.addEventListener('click', () => run(async () => {
        const data = await api('WhatsAppHistory&recordId=' + encodeURIComponent(button.dataset.record));
        const body = el('waHistoryBody'); body.replaceChildren();
        if (!data.length) body.textContent = 'Sin historial de reactivación.';
        for (const item of data) {
            const block = document.createElement('div'); block.className = 'border-bottom py-2';
            const heading = document.createElement('div');
            const label = item.status === 'Sent' ? 'Enviado (declarado)' : item.status === 'Skipped' ? 'Omitido' : item.openedAt ? 'Enlace generado' : 'Pendiente';
            heading.textContent = `${label} · Campaña ${item.campaignId} · ${item.phone} · ${item.actorName || 'Sin acción'} · ${(item.declaredSentAt || item.openedAt || '').replace('T', ' ').slice(0, 19)}`;
            const text = document.createElement('div'); text.style.whiteSpace = 'pre-wrap'; text.textContent = item.message;
            block.append(heading, text); body.append(block);
        }
        el('waHistory').hidden = false; el('waHistory').open = true; el('waHistory').scrollIntoView({ behavior: 'smooth' });
    })));
    run(loadState);
})();
