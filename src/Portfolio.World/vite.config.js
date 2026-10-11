import { defineConfig } from 'vite';

// Built into the ASP.NET app's wwwroot so it ships in the same container and is served at /world
// (same origin: no CORS, and the site's CSP of script-src 'self' covers it).
export default defineConfig({
    base: '/world/',
    build: {
        outDir: '../Portfolio.Web/wwwroot/world',
        emptyOutDir: true,
        target: 'es2022',
        chunkSizeWarningLimit: 800,
    },
    server: {
        // `npm run dev` against a locally running site (dotnet run on :5099) for live content.
        proxy: { '/api': 'http://localhost:5099' },
    },
});
