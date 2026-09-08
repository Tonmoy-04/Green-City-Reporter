(() => {
    const search = document.getElementById('report-search');
    if (!search) return;
    const status = document.getElementById('status-filter');
    const priority = document.getElementById('priority-filter');
    const rows = Array.from(document.querySelectorAll('[data-report-row]'));
    const entries = rows.map(row => ({ row, text: row.textContent.toLocaleLowerCase() }));
    const filter = () => {
        const query = search.value.trim().toLocaleLowerCase();
        let count = 0;
        entries.forEach(({ row, text }) => {
            const matches = text.includes(query) && (!status.value || row.dataset.status === status.value) && (!priority.value || row.dataset.priority === priority.value);
            row.hidden = !matches;
            if (matches) count++;
        });
        document.getElementById('no-results').hidden = count !== 0;
        document.getElementById('report-count').textContent = `Showing ${count} of ${rows.length} reports`;
    };
    document.getElementById('queue-filters').hidden = false;
    search.addEventListener('input', filter);
    status.addEventListener('change', filter);
    priority.addEventListener('change', filter);
    document.getElementById('clear-filters').addEventListener('click', () => {
        search.value = ''; status.value = ''; priority.value = '';
        filter();
        search.focus();
    });
})();
