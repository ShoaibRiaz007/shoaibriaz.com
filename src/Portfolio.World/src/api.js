// Dynamic API client for Portfolio.World.
// Fetches live projects, career milestones, and project case studies from ASP.NET Core endpoints.
// Includes resilient fallback to high-fidelity seed data if offline or unreachable.
import { FALLBACK_SITE } from './seed.js';

const cache = {
    site: null,
    projects: null,
    experience: null,
    details: new Map(),
};

async function fetchJson(url, timeoutMs = 3500) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
        const res = await fetch(url, {
            headers: { Accept: 'application/json' },
            signal: controller.signal,
        });
        clearTimeout(timer);
        if (!res.ok) return null;
        return await res.json();
    } catch {
        clearTimeout(timer);
        return null;
    }
}

/**
 * Loads full site data (profile, experience, projects) from /api/site.
 * Falls back to /api/projects + /api/experience, or embedded high-fidelity seed data if offline.
 */
export async function loadSiteData() {
    if (cache.site) return cache.site;

    // 1. Primary: aggregated /api/site endpoint
    const site = await fetchJson('/api/site');
    if (site && (site.projects?.length || site.experience?.length)) {
        cache.site = site;
        return site;
    }

    // 2. Secondary: parallel fetch of /api/projects, /api/experience, and /api/profile
    const [projects, experience, profile] = await Promise.all([
        fetchJson('/api/projects'),
        fetchJson('/api/experience'),
        fetchJson('/api/profile'),
    ]);

    if (projects || experience || profile) {
        cache.site = {
            profile: profile ?? FALLBACK_SITE.profile,
            experience: experience ?? FALLBACK_SITE.experience,
            projects: projects ?? FALLBACK_SITE.projects,
        };
        return cache.site;
    }

    // 3. Fallback: offline / standalone seed data
    cache.site = FALLBACK_SITE;
    return cache.site;
}

/**
 * Loads detailed case study for a project exhibit from /api/projects/{slug}.
 * Returns full Description, Challenge, Solution, Outcome, MyRole, Highlights, and Media.
 */
export async function fetchProjectDetail(slug) {
    if (!slug) return null;
    if (cache.details.has(slug)) return cache.details.get(slug);

    const detail = await fetchJson(`/api/projects/${encodeURIComponent(slug)}`);
    if (detail) {
        cache.details.set(slug, detail);
        return detail;
    }

    // Return structured fallback based on the project's base info if available
    const site = cache.site || FALLBACK_SITE;
    const base = site.projects?.find(p => p.slug === slug);
    if (!base) return null;

    const fallbackDetail = {
        project: base,
        description: [base.summary, base.description || base.summary].filter(Boolean),
        challenge: base.challenge ? [base.challenge] : ['High-performance real-time synchronization and robust client-server architecture.'],
        solution: base.solution ? [base.solution] : ['Engineered modular systems with clean architecture, strict telemetry, and zero-downtime deployment pipelines.'],
        outcome: base.outcome ? [base.outcome] : [`Shipped to production on schedule with ${base.metric || 'positive user feedback'}.`],
        myRole: base.myRole || 'Senior Software Engineer / Team Lead',
        highlights: base.techStack?.length ? [`Built with ${base.techStack.join(', ')}.`] : [],
        media: base.imageUrl ? [{ type: 'image', url: base.imageUrl, caption: base.title }] : []
    };
    cache.details.set(slug, fallbackDetail);
    return fallbackDetail;
}
