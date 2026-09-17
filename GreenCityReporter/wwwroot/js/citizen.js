// Presentation only: no form, network, storage or application-state handlers.
(() => {
    if (!document.body.classList.contains('gcr-citizen')) return;
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (reducedMotion.matches || !('IntersectionObserver' in window) || !Element.prototype.animate) return;

    const animations = new Set();
    const observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (!entry.isIntersecting) return;
            observer.unobserve(entry.target);
            // A focused form or link should never move while being used.
            if (reducedMotion.matches || entry.target.contains(document.activeElement)) return;
            const animation = entry.target.animate([
                { opacity: .65, transform: 'translateY(10px)' },
                { opacity: 1, transform: 'translateY(0)' }
            ], { duration: 240, easing: 'ease-out' });
            animations.add(animation);
            animation.addEventListener('finish', () => animations.delete(animation));
            animation.addEventListener('cancel', () => animations.delete(animation));
        });
    }, { threshold: .08 });

    document.querySelectorAll('main .card, main .feature-card').forEach(card => {
        // Avoid nested motion, map surfaces, sticky/fixed descendants and active forms.
        if (!card.parentElement.closest('.card') && !card.querySelector('form, #location-picker-map')) observer.observe(card);
    });
    const stopMotion = () => {
        observer.disconnect();
        animations.forEach(animation => animation.cancel());
        animations.clear();
    };
    reducedMotion.addEventListener('change', event => { if (event.matches) stopMotion(); });
    window.addEventListener('pagehide', stopMotion, { once: true });
})();
