# The Island

A small, peaceful three.js world served by the ASP.NET site at `/world/`: walk around as the site owner's
avatar, fish off the pier, sit by the campfire, swing from the old tree, watch the stars. Everything is generated
in code (no models, textures or audio files), and the island is deterministic, so it looks the same every visit.

```bash
npm ci
npm run dev     # live dev server; proxies /api to a local site on http://localhost:5099
npm run build   # writes ../Portfolio.Web/wwwroot/world (git-ignored; Docker and CI build it)
```

## Modules

| File | What it owns |
|---|---|
| `terrain.js` | Island shape as functions (`heightAt`, `groundAt`, `surfaceKind`), places, paths, walkable decks, terrain mesh, baked depth and shore-distance maps |
| `sky.js` | Time of day: shader sky with sun and moon, stars, clouds, sun/moon light, hemisphere light, fog |
| `water.js` | Faceted low-poly sea with shallows tint and shoreline foam; follows the camera |
| `vegetation.js` | Instanced trees, palms, bushes, rocks, grass and flowers; shared wind shader (grass parts around the player) |
| `props.js` | Cottage, garden, pier and rowboat, campfire, hammock, lighthouse, windmill, the old tree and swing, benches, lanterns, signpost, car; registers colliders, decks and activity anchors |
| `life.js` | Gulls, butterflies, fireflies, jumping fish, chimney smoke, leaves, shooting stars, sky lanterns, the cat |
| `character.js` | The owner's low-poly avatar and its poses (walk, run, sit, lie, fish, swing, crouch…) |
| `player.js` | Input (keys, joystick, drag, pinch, tap), the orbit/first-person camera rig, walking, wading and driving |
| `activities.js` | Everything to do, its prompts, and the journal checklist; fish and shell collections |
| `audio.js` | Synthesized ambience (surf, wind, birds, crickets, fire), generative music, sound effects |
| `showcase.js` | The owner's work in the world: the Career Trail of milestones up the lookout path and the project Gallery, from `/api/site` |
| `builders.js` | Shared building blocks: cached flat-shaded materials, boxes, glow sprites, canvas text, static-mesh merging, the car |
| `noise.js` | Seeded random numbers and smooth noise, so the island looks the same on every visit |
| `ui.js` | Title, HUD, prompts, toasts, journal, map, time and settings, letter, photo mode |
| `save.js` | Journal, planted flowers and settings in `localStorage` (guarded; works without it) |
| `telemetry.js` | One anonymous, cookie-free visit report to `/api/t` at boot (skipped under Do Not Track / GPC), like the site's beacon |
| `main.js` | Boot, renderer and quality, clock, frame loop |

## Notes

- `/api/site` supplies the profile (name for the title, contact links in the mailbox letter), the experience for
  the Career Trail and the projects for the Gallery. The island runs without the API; the trail and gallery are
  then left out.
- The only other request is the visit report to `/api/t` (see `telemetry.js`).
- Quality is automatic (drops a level if the first seconds run under ~42 fps) and can be set in Time & settings.
- `?debug` exposes `window.__island` (`enter`, `step`, `hour`, `go`, `view`, `interact`, `action`, `state`) to
  step the simulation by hand when the tab isn't painting (headless checks, screenshots).
