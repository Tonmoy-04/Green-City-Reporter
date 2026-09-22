(() => {
    const form = document.getElementById('donation-form');
    if (!form) return;
    const amount = document.getElementById('Form_Amount');
    const buttons = document.querySelectorAll('.donation-amount');
    const methods = [...form.querySelectorAll('input[name="Form.PaymentMethod"]')];
    const sections = [...form.querySelectorAll('[data-step]')];
    const error = document.getElementById('checkout-error');
    let current = 1;
    const selectedMethod = () => methods.find(method => method.checked && !method.disabled)?.value;
    const showStep = step => {
        current = step;
        sections.forEach(section => { section.hidden = Number(section.dataset.step) !== step; });
        document.querySelectorAll('[data-progress]').forEach(item => {
            if (Number(item.dataset.progress) === step) item.setAttribute('aria-current', 'step');
            else item.removeAttribute('aria-current');
        });
        error.textContent = '';
        if (step === 3) updateMethod();
    };
    const updateAmount = () => {
        buttons.forEach(button => button.setAttribute('aria-pressed', String(Number(button.dataset.amount) === Number(amount.value))));
        document.getElementById('summary-amount').textContent = (Number(amount.value) || 0).toLocaleString('en-BD', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    };
    const updateMethod = () => {
        const method = selectedMethod();
        document.getElementById('summary-method').textContent = method === 'Card' ? 'Credit / Debit Card' : method || 'Not selected';
        form.querySelectorAll('[data-process]').forEach(panel => { panel.hidden = panel.dataset.process !== method; });
    };
    const validateAmount = () => {
        const value = Number(amount.value);
        if (!amount.checkValidity() || value < 50 || value > 100000 || Math.abs(value * 100 - Math.round(value * 100)) > 0.00001) {
            error.textContent = 'Enter an amount between Tk 50 and Tk 100,000 with no more than two decimal places.';
            showStep(1);
            error.textContent = 'Enter a valid donation amount between Tk 50 and Tk 100,000.';
            amount.focus();
            return false;
        }
        return true;
    };
    buttons.forEach(button => button.addEventListener('click', () => { amount.value = button.dataset.amount; updateAmount(); }));
    amount.addEventListener('input', updateAmount);
    methods.forEach(method => method.addEventListener('change', updateMethod));
    form.querySelectorAll('[data-next]').forEach(button => button.addEventListener('click', () => {
        if (!validateAmount()) return;
        if (Number(button.dataset.next) === 3 && !selectedMethod()) { error.textContent = 'Please select an available payment method.'; return; }
        showStep(Number(button.dataset.next));
    }));
    form.querySelectorAll('[data-back]').forEach(button => button.addEventListener('click', () => showStep(Number(button.dataset.back))));
    // With JavaScript disabled, all sections remain visible and native validation still works.
    form.noValidate = true;
    form.addEventListener('submit', event => {
        if (current < 3) { event.preventDefault(); if (validateAmount()) showStep(current + 1); return; }
        if (!validateAmount()) { event.preventDefault(); return; }
        if (!selectedMethod()) { event.preventDefault(); showStep(2); error.textContent = 'Please select an available payment method.'; return; }
        const invalid = [...form.querySelectorAll('input, textarea')].find(input => !input.checkValidity());
        if (invalid) { event.preventDefault(); showStep(3); invalid.reportValidity(); return; }
        if (form.dataset.ready !== 'true') { event.preventDefault(); error.textContent = 'Secure payment setup is temporarily unavailable. Please try again later.'; return; }
        const submit = document.getElementById('donation-submit');
        submit.disabled = true;
        submit.textContent = 'Opening payment screen...';
    });
    window.addEventListener('pageshow', () => {
        const submit = document.getElementById('donation-submit');
        submit.disabled = form.dataset.ready !== 'true';
        submit.textContent = submit.dataset.label || 'Continue to secure payment';
    });
    updateAmount(); updateMethod(); showStep(1);
})();
