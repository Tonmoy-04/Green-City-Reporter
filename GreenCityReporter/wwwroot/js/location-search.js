/* Photon supports search as you type: https://github.com/komoot/photon */
function initLocationSearch({ input, dropdown, selectLocation, isInsideBounds }) {
    let timer;
    let request;
    let generation = 0;
    let activeIndex = -1;
    let choices = [];
    const cache = new Map();
    const status = document.createElement('div');
    status.className = 'visually-hidden';
    status.setAttribute('role', 'status');
    input.parentElement.appendChild(status);

    function hide() {
        dropdown.classList.add('d-none');
        dropdown.classList.remove('d-block');
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
        activeIndex = -1;
    }
    function cancel() {
        clearTimeout(timer);
        generation++;
        if (request) request.abort();
        hide();
    }
    function show() {
        dropdown.classList.remove('d-none');
        dropdown.classList.add('d-block');
        input.setAttribute('aria-expanded', 'true');
    }
    function message(text) {
        choices = [];
        dropdown.replaceChildren();
        const row = document.createElement('div');
        row.className = 'small text-muted py-2 px-3';
        row.textContent = text;
        dropdown.appendChild(row);
        status.textContent = text;
        show();
    }
    function choose(index) {
        const place = choices[index];
        if (place && selectLocation(place.lat, place.lng, place.address)) {
            input.value = place.address;
            cancel();
            input.focus();
            status.textContent = 'Location selected: ' + place.address;
        }
    }
    function render(results) {
        hide();
        choices = results;
        dropdown.replaceChildren();
        if (!results.length) {
            message('No matching places in Dhaka. Try another area or landmark.');
            return;
        }
        results.forEach((place, index) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.id = 'location-suggestion-' + index;
            button.className = 'dropdown-item search-suggestion-item text-wrap small py-2 px-3 border-bottom';
            button.setAttribute('role', 'option');
            button.setAttribute('aria-selected', 'false');
            button.tabIndex = -1;
            button.textContent = place.address;
            button.addEventListener('click', () => choose(index));
            dropdown.appendChild(button);
        });
        status.textContent = results.length + ' locations found. Use arrow keys to select.';
        show();
    }
    input.addEventListener('input', () => {
        cancel();
        const query = input.value.trim();
        if (query.length < 3) return;
        const currentGeneration = generation;
        timer = setTimeout(async () => {
            if (cache.has(query)) {
                render(cache.get(query));
                return;
            }
            message('Searching locations...');
            const controller = new AbortController();
            request = controller;
            const timeout = setTimeout(() => controller.abort(), 10000);
            try {
                const params = new URLSearchParams({ q: query, limit: '6', bbox: '90.25,23.60,90.55,23.95', lat: '23.8103', lon: '90.4125' });
                const response = await fetch('https://photon.komoot.io/api/?' + params, { signal: controller.signal });
                if (!response.ok) throw new Error('Location search unavailable');
                const data = await response.json();
                if (currentGeneration !== generation) return;
                const seen = new Set();
                const results = (data.features || []).flatMap(feature => {
                    const [lng, lat] = feature.geometry.coordinates;
                    const p = feature.properties;
                    const address = [...new Set([p.name, [p.housenumber, p.street].filter(Boolean).join(' '), p.district, p.city, p.state, p.country].filter(Boolean))].join(', ');
                    const key = lat + ',' + lng + ',' + address;
                    if (!address || !isInsideBounds(lat, lng) || seen.has(key)) return [];
                    seen.add(key);
                    return [{ lat, lng, address }];
                });
                if (cache.size >= 30) cache.delete(cache.keys().next().value);
                cache.set(query, results);
                render(results);
            } catch (error) {
                if (currentGeneration === generation) message('Location search is unavailable. Try again or choose a spot on the map.');
            } finally {
                clearTimeout(timeout);
            }
        }, 500);
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'Escape') { cancel(); return; }
        if (event.key === 'Enter') {
            event.preventDefault();
            if (activeIndex >= 0) choose(activeIndex);
            return;
        }
        if (!['ArrowDown', 'ArrowUp'].includes(event.key) || !choices.length || dropdown.classList.contains('d-none')) return;
        event.preventDefault();
        activeIndex = event.key === 'ArrowDown' ? (activeIndex + 1) % choices.length : (activeIndex <= 0 ? choices.length - 1 : activeIndex - 1);
        [...dropdown.children].forEach((button, index) => {
            button.classList.toggle('active', index === activeIndex);
            button.setAttribute('aria-selected', String(index === activeIndex));
        });
        const selected = dropdown.children[activeIndex];
        input.setAttribute('aria-activedescendant', selected.id);
        selected.scrollIntoView({ block: 'nearest' });
    });
    document.addEventListener('click', event => {
        if (event.target !== input && !dropdown.contains(event.target)) cancel();
    });
    input.addEventListener('blur', () => setTimeout(() => {
        if (!dropdown.contains(document.activeElement)) cancel();
    }, 150));
    document.getElementById('clear-location-btn').addEventListener('click', () => {
        cancel();
        choices = [];
        input.value = '';
    });
}