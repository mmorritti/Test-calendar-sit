let calendar;
let editingEventId = null;
let currentDetailEvent = null;
let allEvents = [];
let isDataLoaded = false; // <-- NUOVO: Evita di scaricare i dati 100 volte dal server!

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

    document.getElementById('detail-date').textContent = '📅 ' + formatDate(event.start)
        + (event.end ? ' → ' + formatDate(event.end) : '');

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

    isDataLoaded = false; // Costringe a riscaricare i dati dal server per vedere la modifica
    calendar.refetchEvents();
    bootstrap.Modal.getInstance(document.getElementById('formModal')).hide();
}

// ─────────────────────────────────────────
// ELIMINA
// ─────────────────────────────────────────
async function deleteEvent() {
    if (!confirm('Sei sicuro di voler eliminare questo evento?')) return;

    await fetch(`/api/events/${editingEventId}`, { method: 'DELETE' });

    isDataLoaded = false; // Costringe a riscaricare i dati
    calendar.refetchEvents();
    bootstrap.Modal.getInstance(document.getElementById('formModal')).hide();
}

// ─────────────────────────────────────────
// FILTRI AVANZATI (Testo + Tags Regioni)
// ─────────────────────────────────────────

let isRegionFilterPopulated = false;
let activeRegionTags = [];

function populateRegionFilter() {
    if (isRegionFilterPopulated) return;

    const list = document.getElementById('region-dropdown-list');
    if (!list) return;

    list.innerHTML = '';

    const regioniUniche = [...new Set(allEvents.map(e => e.regione).filter(r => r))].sort();

    regioniUniche.forEach(regione => {
        const safeId = regione.replace(/\s+/g, '-');
        const li = document.createElement('li');
        li.innerHTML = `<button class="dropdown-item" id="menu-btn-${safeId}" type="button" onclick="addRegionTag('${regione}')">${regione}</button>`;
        list.appendChild(li);
    });

    isRegionFilterPopulated = true;
}

function addRegionTag(regione) {
    if (!activeRegionTags.includes(regione)) {
        activeRegionTags.push(regione);
        renderRegionTags();
        applyFilters();
    }
}

function removeRegionTag(regione) {
    activeRegionTags = activeRegionTags.filter(r => r !== regione);
    renderRegionTags();
    applyFilters();
}

function renderRegionTags() {
    const container = document.getElementById('region-tags');
    if (!container) return;
    container.innerHTML = '';

    const allMenuBtns = document.querySelectorAll('#region-dropdown-list .dropdown-item');
    allMenuBtns.forEach(btn => btn.classList.remove('disabled'));

    activeRegionTags.forEach(regione => {
        const safeId = regione.replace(/\s+/g, '-');
        const menuBtn = document.getElementById(`menu-btn-${safeId}`);
        if (menuBtn) {
            menuBtn.classList.add('disabled');
        }

        const tag = document.createElement('span');
        tag.className = 'badge bg-secondary d-flex align-items-center gap-2 px-3 py-2 fw-normal rounded-pill shadow-sm';
        tag.style.fontSize = '0.85rem';
        tag.innerHTML = `
            ${regione} 
            <span style="cursor: pointer; font-weight: bold; opacity: 0.7;" onmouseover="this.style.opacity=1" onmouseout="this.style.opacity=0.7" onclick="removeRegionTag('${regione}')">✕</span>
        `;
        container.appendChild(tag);
    });
}

// NUOVO METODO DI FILTRAGGIO (Molto più pulito, previene i duplicati)
function filterLocalEvents() {
    const query = document.getElementById('search-input').value.toLowerCase().trim();

    return allEvents.filter(e => {
        const matchTesto = query === '' ||
            (e.title && e.title.toLowerCase().includes(query)) ||
            (e.description && e.description.toLowerCase().includes(query));

        const matchRegione = activeRegionTags.length === 0 || activeRegionTags.includes(e.regione);

        return matchTesto && matchRegione;
    });
}

function applyFilters() {
    // Ora invece di sovrapporre dati, diciamo solo al calendario di "ricaricarsi"
    // Il calendario richiamerà la funzione "events:" qui sotto, che applicherà il filtro.
    calendar.refetchEvents();
}

function clearFilters() {
    document.getElementById('search-input').value = '';
    activeRegionTags = [];
    renderRegionTags();
    applyFilters();
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

// ─────────────────────────────────────────
// INIZIALIZZAZIONE CALENDARIO
// ─────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {

    calendar = new FullCalendar.Calendar(document.getElementById('calendar'), {
        locale: 'it',
        height: 'calc(100vh - 250px)',
        dayMaxEvents: 2,

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
        editable: false,
        selectMinDistance: 10,
        longPressDelay: 500,

        eventContent: function (arg) {
            let divEl = document.createElement('div');
            divEl.style.padding = '1px 3px';
            divEl.style.overflow = 'hidden';
            divEl.style.fontSize = '0.75rem';
            divEl.style.lineHeight = '1.1';

            let titleStyle = 'font-weight: bold; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;';
            let titleHtml = `<div style="${titleStyle}">${arg.event.title}</div>`;

            let locStyle = 'opacity: 0.85; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin-top: 1px;';
            let locationHtml = arg.event.extendedProps.location
                ? `<div style="${locStyle}">📍 ${arg.event.extendedProps.location}</div>`
                : '';

            divEl.innerHTML = titleHtml + locationHtml;
            return { domNodes: [divEl] };
        },

        events: function (info, successCallback, failureCallback) {
            // IL CUORE DEL FIX: Scarichiamo dal server solo se "isDataLoaded" è falso
            if (!isDataLoaded) {
                fetch(`/api/events?start=${info.startStr}&end=${info.endStr}`)
                    .then(res => res.json())
                    .then(data => {
                        allEvents = data;
                        isDataLoaded = true;
                        populateRegionFilter();

                        // Applica i filtri (se ci sono) prima di stampare
                        successCallback(filterLocalEvents());
                    })
                    .catch(() => failureCallback());
            } else {
                // Se i dati sono già stati scaricati, stampiamo solo quelli filtrati all'istante!
                successCallback(filterLocalEvents());
            }
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