let calendar;
let editingEventId = null;
let currentDetailEvent = null;
let allEvents = [];
let isDataLoaded = false;

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

function showDetailModal(event) {
    currentDetailEvent = event;

    const titleEl = document.getElementById('detail-title');
    const descEl = document.getElementById('detail-desc');
    const dateEl = document.getElementById('detail-date');
    const locationEl = document.getElementById('detail-location');
    const eventbriteEl = document.getElementById('detail-eventbrite');

    const imageWrapperEl = document.getElementById('detail-image-wrapper');
    const imageEl = document.getElementById('detail-image');

    titleEl.textContent = event.extendedProps.description || '';

    descEl.textContent = (event.title || '').toUpperCase();
    descEl.style.fontWeight = '700';
    descEl.style.marginBottom = '12px';

    dateEl.textContent =
        '📅 ' + formatDate(event.start) + (event.end ? ' → ' + formatDate(event.end) : '');
    dateEl.style.marginBottom = '6px';

    const luogo = event.extendedProps.location;

    if (locationEl) {
        locationEl.textContent = luogo ? '📍 ' + luogo : '';
    }

    if (imageWrapperEl && imageEl) {
        imageWrapperEl.style.display = 'none';
        imageEl.src = '';
    }

    const eventbriteLink = event.extendedProps.eventbriteLink;

    if (eventbriteEl) {
        if (eventbriteLink) {
            eventbriteEl.innerHTML = `
                🎟️ <a href="${eventbriteLink}" target="_blank" rel="noopener noreferrer" class="fw-bold">
                    Info e iscrizioni
                </a>
            `;
        } else {
            eventbriteEl.innerHTML = '';
        }
    }

    if (eventbriteLink && imageWrapperEl && imageEl) {
        fetch(`/api/eventbrite/preview?url=${encodeURIComponent(eventbriteLink)}`)
            .then(res => res.ok ? res.json() : null)
            .then(data => {
                if (data && data.imageUrl) {
                    imageEl.src = data.imageUrl;
                    imageWrapperEl.style.display = 'block';
                }
            })
            .catch(err => {
                console.error('Errore caricamento immagine Eventbrite:', err);
            });
    }

    new bootstrap.Modal(document.getElementById('detailModal')).show();
}

async function saveEvent() {
    const title = document.getElementById('f-title').value.trim();
    const start = document.getElementById('f-start').value;
    if (!title || !start) { alert('Titolo e data inizio sono obbligatori!'); return; }
    const payload = {
        title, start,
        end: document.getElementById('f-end').value || null,
        color: document.getElementById('f-color').value,
        description: document.getElementById('f-description').value,
        allDay: !start.includes('T')
    };
    await fetch(editingEventId ? `/api/events/${editingEventId}` : '/api/events', {
        method: editingEventId ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(editingEventId ? { id: parseInt(editingEventId), ...payload } : payload)
    });
    isDataLoaded = false;
    calendar.refetchEvents();
    bootstrap.Modal.getInstance(document.getElementById('formModal')).hide();
}

async function deleteEvent() {
    if (!confirm('Sei sicuro di voler eliminare questo evento?')) return;
    await fetch(`/api/events/${editingEventId}`, { method: 'DELETE' });
    isDataLoaded = false;
    calendar.refetchEvents();
    bootstrap.Modal.getInstance(document.getElementById('formModal')).hide();
}

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
        li = document.createElement('li');
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
    if (activeRegionTags.length === 0) { container.style.display = 'none'; container.innerHTML = ''; return; }
    container.style.display = 'flex';
    container.innerHTML = '';
    activeRegionTags.forEach(regione => {
        const tag = document.createElement('span');
        tag.className = 'badge bg-secondary d-flex align-items-center gap-2 px-3 py-2 fw-normal rounded-pill shadow-sm';
        tag.style.fontSize = '0.85rem';
        tag.innerHTML = `${regione} <span style="cursor: pointer; font-weight: bold; opacity: 0.7;" onclick="removeRegionTag('${regione}')">✕</span>`;
        container.appendChild(tag);
    });
}

function filterLocalEvents() {
    const query = document.getElementById('search-input').value.toLowerCase().trim();
    return allEvents.filter(e => {
        const matchTesto = query === '' || (e.title && e.title.toLowerCase().includes(query)) || (e.description && e.description.toLowerCase().includes(query));
        const matchRegione = activeRegionTags.length === 0 || activeRegionTags.includes(e.regione);
        return matchTesto && matchRegione;
    });
}

function applyFilters() { calendar.refetchEvents(); }

function clearFilters() {
    document.getElementById('search-input').value = '';
    activeRegionTags = [];
    renderRegionTags();
    applyFilters();
}

function formatDate(date) {
    return new Date(date).toLocaleString('it-IT', { day: '2-digit', month: 'short', year: 'numeric' });
}

document.addEventListener('DOMContentLoaded', function () {
    calendar = new FullCalendar.Calendar(document.getElementById('calendar'), {
        locale: 'it',
        firstDay: 1,
        height: 'calc(100vh - 250px)',
        expandRows: true,
        dayMaxEvents: 2,
        headerToolbar: { left: 'prev,next today', center: 'title', right: '' },
        initialView: 'dayGridMonth',
        eventContent: function (arg) {
            let divEl = document.createElement('div');
            divEl.style = "padding:1px 3px; overflow:hidden; font-size:0.75rem; line-height:1.1;";
            divEl.innerHTML = `<div style="font-weight:bold; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;">${arg.event.title}</div>` +
                (arg.event.extendedProps.location ? `<div style="opacity:0.85; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; margin-top:1px;">📍 ${arg.event.extendedProps.location}</div>` : '');
            return { domNodes: [divEl] };
        },
        events: function (info, successCallback, failureCallback) {
            if (!isDataLoaded) {
                fetch(`/api/events`)
                    .then(res => res.json())
                    .then(data => {
                        allEvents = data.filter((v, i, a) => a.findIndex(t => (t.title === v.title && t.start === v.start && t.regione === v.regione)) === i);
                        isDataLoaded = true;
                        populateRegionFilter();
                        successCallback(filterLocalEvents());
                    })
                    .catch(() => failureCallback());
            } else {
                successCallback(filterLocalEvents());
            }
        },
        eventClick: function (info) { showDetailModal(info.event); }
    });
    calendar.render();
});