// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const modalElement = document.getElementById('callReminderModal');
    if (!modalElement) {
        return;
    }

    const metaElement = document.getElementById('callReminderMeta');
    const reminderItems = Array.from(modalElement.querySelectorAll('[data-call-scheduled-at]'));
    const summaryElement = document.getElementById('callReminderSummary');
    const countdownElement = document.getElementById('callReminderCountdown');
    const markConcreteButton = document.getElementById('btnMarkCallConcrete');
    const shownKey = 'oldschoollab-records-call-reminder-shown';
    const signature = reminderItems.map(item => `${item.dataset.callId}:${item.dataset.callScheduledAt}`).join('|');
    let refreshTimeoutId = null;
    let reloadIntervalId = null;

    function openModalOnce() {
        if (window.sessionStorage.getItem(shownKey) === signature) {
            return;
        }

        window.sessionStorage.setItem(shownKey, signature);
        bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    function getTimes() {
        return reminderItems
            .map(item => ({
                id: item.dataset.callId,
                when: new Date(item.dataset.callScheduledAt).getTime()
            }))
            .filter(x => !Number.isNaN(x.when))
            .sort((a, b) => a.when - b.when);
    }

    function scheduleRefresh(nextCallAtIso) {
        if (refreshTimeoutId) {
            clearTimeout(refreshTimeoutId);
            refreshTimeoutId = null;
        }

        if (!nextCallAtIso) {
            return;
        }

        const nextCallAt = new Date(nextCallAtIso).getTime();
        if (Number.isNaN(nextCallAt)) {
            return;
        }

        const waitMs = Math.max(1000, nextCallAt - Date.now() + 1000);
        refreshTimeoutId = setTimeout(() => window.location.reload(), waitMs);
    }

    function updateTimer() {
        const now = Date.now();
        const times = getTimes();
        const dueItems = times.filter(x => x.when <= now);
        const futureItems = times.filter(x => x.when > now);

        if (dueItems.length > 0) {
            if (summaryElement) {
                summaryElement.textContent = `Tienes ${dueItems.length} llamada(s) vencida(s) por atender.`;
            }

            openModalOnce();
            return;
        }

        if (futureItems.length > 0) {
            const nearest = futureItems[0].when - now;
            const minutes = Math.max(0, Math.ceil(nearest / 60000));

            if (summaryElement) {
                summaryElement.textContent = `La próxima llamada se activará en aproximadamente ${minutes} minuto(s).`;
            }

            if (countdownElement) {
                countdownElement.textContent = `Próxima alerta automática en ${minutes} minuto(s).`;
            }

            if (nearest <= 0) {
                window.location.reload();
                return;
            }
            scheduleRefresh(metaElement?.dataset.nextCallScheduledAt);
        }
    }

    window.addEventListener('DOMContentLoaded', () => {
        updateTimer();
        setInterval(updateTimer, 30000);
    });
})();
