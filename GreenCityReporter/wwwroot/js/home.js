document.addEventListener('DOMContentLoaded', () => {
    const home = document.querySelector('.home-page');
    if (!home) return;

    const animatedElements = home.parentElement.querySelectorAll('.home-entrance, .home-reveal');
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reduceMotion || !('IntersectionObserver' in window)) {
        animatedElements.forEach(element => element.classList.add('is-visible'));
    } else {
        const revealObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -36px' });

        animatedElements.forEach(element => revealObserver.observe(element));
    }

    const navbar = document.querySelector('.gcr-navbar');
    const updateNavbar = () => navbar?.classList.toggle('is-scrolled', window.scrollY > 12);
    updateNavbar();
    window.addEventListener('scroll', updateNavbar, { passive: true });
});