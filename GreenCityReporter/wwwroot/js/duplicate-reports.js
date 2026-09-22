(() => {
    const form = document.getElementById('report-review-form');
    if (!form || !window.fetch || !window.AbortController) return;
    const results = document.getElementById('duplicate-results');
    const check = document.getElementById('check-duplicates');
    const confirm = document.getElementById('confirm-report');
    const category = document.getElementById('SelectedCategoryId');
    let pending;

    function showMessage(text) {
        const paragraph = document.createElement('p');
        paragraph.className = 'small text-muted mb-0';
        paragraph.textContent = text;
        results.replaceChildren(paragraph);
    }

    async function refresh() {
        pending?.abort();
        const request = new AbortController();
        pending = request;
        const timeout = window.setTimeout(() => request.abort(), 15000);
        // Remove the previous category's ticket, choices and override before starting a new check.
        showMessage('Checking nearby reports…');
        results.setAttribute('aria-busy', 'true');
        check.disabled = true;
        confirm.disabled = true;
        try {
            const response = await fetch(form.dataset.checkUrl, {
                method: 'POST', body: new FormData(form), credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: request.signal
            });
            if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('text/html'))
                throw new Error('Check unavailable');
            const html = await response.text();
            if (pending === request) results.innerHTML = html;
        } catch (error) {
            if (pending === request)
                showMessage('We could not check nearby reports. Try again, or continue: we will check once more before saving your report.');
        } finally {
            window.clearTimeout(timeout);
            if (pending === request) {
                results.removeAttribute('aria-busy');
                check.disabled = false;
                confirm.disabled = false;
                pending = null;
            }
        }
    }

    form.addEventListener('submit', event => {
        if (event.submitter === check) {
            event.preventDefault();
            refresh();
        }
    });
    category?.addEventListener('change', refresh);
})();
