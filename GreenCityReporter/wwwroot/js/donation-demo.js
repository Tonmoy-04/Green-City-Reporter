(() => {
    const number = document.getElementById('demo-card-number');
    const expiry = document.getElementById('demo-expiry');
    function validateCard() {
        if (!number) return;
        const digits = number.value.replace(/ /g, '');
        number.setCustomValidity(/^\d{13,19}$/.test(digits) ? '' : 'Enter a sample card number containing 13 to 19 digits.');
        const parts = /^(0[1-9]|1[0-2])\/(\d{2})$/.exec(expiry.value);
        const now = new Date();
        const valid = parts && (2000 + Number(parts[2]) > now.getFullYear() ||
            (2000 + Number(parts[2]) === now.getFullYear() && Number(parts[1]) >= now.getMonth() + 1));
        expiry.setCustomValidity(valid ? '' : 'Enter a current or future expiry date in MM/YY format.');
    }
    number?.addEventListener('input', validateCard);
    expiry?.addEventListener('input', validateCard);
    document.querySelector('#demo-payment-form button[value="success"]')?.addEventListener('click', validateCard);
})();