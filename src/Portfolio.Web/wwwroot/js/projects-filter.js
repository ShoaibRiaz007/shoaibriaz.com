// Search + category filtering on the all-projects page.
(function () {
    const search = document.getElementById('searchInput');
    const grid = document.getElementById('projectGrid');
    if (!search || !grid) return;
    const cards = Array.from(grid.querySelectorAll('.project-card'));
    const catBtns = Array.from(document.querySelectorAll('.cat-btn'));
    const countEl = document.getElementById('resultCount');
    const noResults = document.getElementById('noResults');
    let activeCat = 'all';

    function apply() {
        const q = search.value.trim().toLowerCase();
        let visible = 0;
        cards.forEach(card => {
            const matchCat = activeCat === 'all' || (card.dataset.category || '') === activeCat;
            const matchText = !q || (card.dataset.search || '').includes(q);
            const show = matchCat && matchText;
            card.classList.toggle('hidden', !show);
            if (show) visible++;
        });
        countEl.textContent = visible + (visible === 1 ? ' project' : ' projects');
        noResults.style.display = visible === 0 ? 'block' : 'none';
    }

    search.addEventListener('input', apply);
    catBtns.forEach(btn => btn.addEventListener('click', () => {
        catBtns.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        activeCat = btn.dataset.cat;
        apply();
    }));
    apply();
})();
