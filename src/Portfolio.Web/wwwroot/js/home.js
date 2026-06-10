// Mobile nav toggle, scroll-reveal animations, and skill-bar fill on the home page.
const toggle = document.getElementById('navToggle');
const links = document.getElementById('navLinks');
toggle?.addEventListener('click', () => links.classList.toggle('open'));
links?.querySelectorAll('a').forEach(a => a.addEventListener('click', () => links.classList.remove('open')));

const io = new IntersectionObserver((entries) => {
    entries.forEach(e => {
        if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); }
    });
}, { threshold: 0.12 });
document.querySelectorAll('.reveal').forEach(el => io.observe(el));

const skillsSec = document.getElementById('skills');
if (skillsSec) {
    const sIo = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting) {
                document.querySelectorAll('.skill-fill').forEach(f => f.style.width = f.dataset.level + '%');
                sIo.disconnect();
            }
        });
    }, { threshold: 0.2 });
    sIo.observe(skillsSec);
}
