let calendar;
let editingEventId = null;
let currentDetailEvent = null;
let allEvents = [];

// ─────────────────────────────────────────
// APRI MODAL CREAZIONE
// ─────────────────────────────────────────
function openCreateModal(start = '', end = '') {
    editingEventId = null;

    document.getElementById('modalTitle').textContent = '✨ Nuovo Evento';
    document.getElementById('f-title').value = '';
    document.getElementById('f-start').value = start ? start.substring(0, 16) : '';
    document.getElementById('f-end').value = end ? end.substring(0, 16) : '';
    document.getElementById('f-color').value = '#3788d8';
    document.getElementById('f-description').value = '';
    document.getElementById('btnDelete').style.display = 'none';

    new bootstrap.Modal(document.getElementById('formModal')).show();
}

// ─────────────────────────────────────────
// MOSTRA DETTAGLIO EVENTO
// ─────────────────────────────────────────
function showDetailModal(event) {
    currentDetailEvent = event;

    document.getElementById('detail-title').textContent = event.title;

    // Mostra data
    document.getElementById('detail-date').textContent = '📅 ' + formatDate(event.start)
        + (event.end ? ' → ' + formatDate(event.end) : '');

    // Mostra il Luogo
    const luogo = event.extendedProps.location;
    const locationEl = document.getElementById('detail-location');
    if (locationEl) {
        locationEl.innerHTML = luogo ? '📍 ' + luogo : '';
    }

    document.getElementById('detail-desc').textContent = event.extendedProps.description || '(nessuna descrizione)';

    new bootstrap.Modal(document.getElementById('detailModal')).show();
}

// ─────────────────────────────────────────
// SALVA (crea nuovo evento)
// ─────────────────────────────────────────
async function saveEvent() {
    const title = document.getElementById('f-title').value.trim();
    const start = document.getElementById('f-start').value;

    if (!title || !start) {
        alert('Titolo e data inizio sono obbligatori!');
        return;
    }

    const payload = {
        title,
        start,
        end: document.getElementById('f-end').value || null,
        color: document.getElementById('f-color').value,
        description: document.getElementById('f-description').value,
        allDay: !start.includes('T')
    };

    if (editingEventId) {
        await fetch(`/api/events/${editingEventId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: parseInt(editingEventId), ...payload })
        });
    } else {
        await fetch('/api/events', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
    }

    calendar.refetchEvents();
    bootstrap.Modal.getInstance(document.getElementById('formModal')).hide();
}

// ─────────────────────────────────────────
// ELIMINA
// ─────────────────────────────────────────
async function deleteEvent() {
    if (!confirm('Sei sicuro di voler eliminare questo evento?')) return;

    await fetch(`/api/events/${editingEventId}`, { method: 'DELETE' });

    calendar.refetchEvents();
    bootstrap.Modal.getInstance(document.getElementById('formModal')).hide();
}

// ─────────────────────────────────────────
// RICERCA EVENTI (Aggiornata con Regione)
// ─────────────────────────────────────────
function searchEvents(query) {
    const q = query.toLowerCase().trim();

    const filtered = q === ''
        ? allEvents
        : allEvents.filter(e =>
            (e.title && e.title.toLowerCase().includes(q)) ||
            (e.description && e.description.toLowerCase().includes(q)) ||
            (e.regione && e.regione.toLowerCase().includes(q)) 
        );

    calendar.removeAllEvents();
    calendar.addEventSource(filtered);
}

function clearSearch() {
    document.getElementById('search-input').value = '';
    calendar.removeAllEvents();
    calendar.addEventSource(allEvents);
}

// ─────────────────────────────────────────
// HELPER
// ─────────────────────────────────────────
function formatDate(date) {
    return new Date(date).toLocaleString('it-IT', {
        day: '2-digit', month: 'short', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

function toLocalInput(date) {
    const d = new Date(date);
    d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
    return d.toISOString().substring(0, 16);
}

// ─────────────────────────────────────────
// INIZIALIZZAZIONE CALENDARIO (per ultimo!)
// ─────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {

    calendar = new FullCalendar.Calendar(document.getElementById('calendar'), {
        locale: 'it',
        height: 'auto',

        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,multiMonthYear'
        },

        initialView: 'dayGridMonth',

        views: {
            multiMonthYear: { type: 'multiMonth', duration: { years: 1 }, buttonText: 'Anno' },
            timeGridWeek: { buttonText: 'Settimana' },
            dayGridMonth: { buttonText: 'Mese' }
        },

        selectable: true,
        editable: false, // <-- IMPORTANTE: Disabilita il drag & drop (Sola lettura)
        selectMinDistance: 10,
        longPressDelay: 500,

        // NUOVO: Stampa il luogo nel blocchetto colorato dell'evento
        eventContent: function (arg) {
            let divEl = document.createElement('div');
            divEl.style.padding = '2px 4px';
            divEl.style.overflow = 'hidden';

            let titleHtml = `<b>${arg.event.title}</b>`;
            let locationHtml = arg.event.extendedProps.location
                ? `<br><small style="opacity: 0.9">📍 ${arg.event.extendedProps.location}</small>`
                : '';

            divEl.innerHTML = titleHtml + locationHtml;
            return { domNodes: [divEl] };
        },

        events: function (info, successCallback, failureCallback) {
            fetch(`/api/events?start=${info.startStr}&end=${info.endStr}`)
                .then(res => res.json())
                .then(data => {
                    allEvents = data;
                    successCallback(data);
                })
                .catch(() => failureCallback());
        },

        dateClick: function () { },

        select: function (info) {
            if (info.startStr === info.endStr) return;
            openCreateModal(info.startStr, info.endStr);
            calendar.unselect();
        },

        eventClick: function (info) { showDetailModal(info.event); }
    });

    calendar.render();
});