// Lightbox for gallery images.
(function () {
    const lb = document.getElementById('lightbox');
    if (!lb) return;
    const img = document.getElementById('lightboxImg');
    document.querySelectorAll('.zoomable').forEach(el => {
        el.addEventListener('click', () => { img.src = el.dataset.full || el.src; lb.classList.add('open'); });
    });
    lb.addEventListener('click', () => lb.classList.remove('open'));
    document.addEventListener('keydown', e => { if (e.key === 'Escape') lb.classList.remove('open'); });
})();

// Slideshow: prev/next, dots, counter, keyboard, autoplay (pauses on hover & while a video plays).
(function () {
    const root = document.getElementById('slideshow');
    if (!root) return;
    const slides = [...root.querySelectorAll('.slide')];
    if (slides.length < 2) return;
    const dots = [...root.querySelectorAll('.dot')];
    const curEl = document.getElementById('slideCur');
    let idx = 0, timer;

    function show(n) {
        idx = (n + slides.length) % slides.length;
        slides.forEach((s, i) => s.classList.toggle('active', i === idx));
        dots.forEach((d, i) => d.classList.toggle('active', i === idx));
        if (curEl) curEl.textContent = idx + 1;
        root.querySelectorAll('video').forEach(v => {
            if (!v.closest('.slide').classList.contains('active')) v.pause();
        });
    }
    const next = () => show(idx + 1);
    const prev = () => show(idx - 1);
    function restart() { clearInterval(timer); timer = setInterval(auto, 6000); }
    function auto() {
        const vid = slides[idx].querySelector('video');
        if (vid && !vid.paused && !vid.ended) return; // don't interrupt a playing video
        next();
    }
    document.getElementById('slideNext')?.addEventListener('click', () => { next(); restart(); });
    document.getElementById('slidePrev')?.addEventListener('click', () => { prev(); restart(); });
    dots.forEach(d => d.addEventListener('click', () => { show(+d.dataset.go); restart(); }));
    document.addEventListener('keydown', e => {
        if (e.key === 'ArrowRight') { next(); restart(); }
        if (e.key === 'ArrowLeft') { prev(); restart(); }
    });
    root.addEventListener('mouseenter', () => clearInterval(timer));
    root.addEventListener('mouseleave', restart);
    restart();
})();
