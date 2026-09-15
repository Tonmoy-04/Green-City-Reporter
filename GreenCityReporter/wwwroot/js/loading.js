(() => {
    const overlay = document.getElementById('site-loading');
    if (!overlay) return;
    const title = document.getElementById('site-loading-title');
    const message = document.getElementById('site-loading-message');
    const slow = document.getElementById('site-loading-slow');
    const tasks = new Map();
    let showTimer, slowTimer, previousFocus;
    function hide() {
        clearTimeout(showTimer);
        clearTimeout(slowTimer);
        showTimer = null;
        const wasVisible = !overlay.hidden;
        overlay.hidden = true;
        slow.hidden = true;
        if (wasVisible && overlay.contains(document.activeElement) && previousFocus?.isConnected) previousFocus.focus();
    }
    function reset() { tasks.clear(); hide(); }
    function begin(heading = 'Loading...', description = 'Please wait while we prepare your page.') {
        const id = Symbol('loading');
        tasks.set(id, true);
        title.textContent = heading;
        message.textContent = description;
        if (overlay.hidden && !showTimer) {
            showTimer = setTimeout(() => {
                showTimer = null;
                if (!tasks.size) return;
                previousFocus = document.activeElement;
                overlay.hidden = false;
                slowTimer = setTimeout(() => { slow.hidden = false; }, 15000);
            }, 150);
        }
        return () => { tasks.delete(id); if (!tasks.size) hide(); };
    }
    window.GreenCityLoading = { begin, reset };
    document.getElementById('site-loading-dismiss').addEventListener('click', reset);
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && !overlay.hidden) reset(); });
    window.addEventListener('pageshow', reset);
    window.addEventListener('pagehide', reset);
    window.addEventListener('load', reset);
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        const button = event.submitter;
        // Wait for native, jQuery and page-specific validation/cancellation handlers.
        setTimeout(() => {
            const target = button?.getAttribute('formtarget') || form.target;
            if (event.defaultPrevented || (target && target !== '_self') || form.method === 'dialog' || form.hasAttribute('data-no-loading')) return;
            if (!form.noValidate && !button?.formNoValidate && !form.checkValidity()) return;
            const path = new URL(button?.formAction || form.action, location.href).pathname.toLowerCase();
            const report = path.includes('/report/');
            begin(report ? 'Processing your report...' : 'Processing your request...',
                report ? 'Please wait while we save or review your report. Analysis and photo uploads may take a little longer.' : 'Please wait. Your request is being processed.');
        }, 0);
    }, true);
    document.addEventListener('click', event => {
        const link = event.target.closest?.('a[href]');
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        if (link.hasAttribute('download') || link.hasAttribute('data-no-loading') || (link.target && link.target !== '_self')) return;
        const url = new URL(link.href, location.href);
        if (!['http:', 'https:'].includes(url.protocol) || url.origin !== location.origin) return;
        if (url.pathname === location.pathname && url.search === location.search && url.hash) return;
        setTimeout(() => { if (!event.defaultPrevented) begin(); }, 0);
    }, true);
    // Existing jQuery write requests also release the overlay on success or failure.
    if (window.jQuery) {
        const requests = new WeakMap();
        window.jQuery(document).ajaxSend((event, xhr, settings) => {
            if (!['GET', 'HEAD'].includes((settings.type || 'GET').toUpperCase()) && settings.globalLoading !== false)
                requests.set(xhr, begin('Processing your request...', 'Please wait while your changes are saved.'));
        }).ajaxComplete((event, xhr) => { requests.get(xhr)?.(); requests.delete(xhr); });
    }
})();