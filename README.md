# shoaibriaz.com

Personal portfolio of M. Shoaib Riaz, live at **[www.shoaibriaz.com](https://www.shoaibriaz.com)**.

A server-rendered ASP.NET Core site with an admin panel for all content, cookie-free visit statistics, an editable
privacy policy and an interactive 3D tour of the portfolio.

## Features

- **Public site:** home page, projects with search, area filters and "Show more", project case studies with image
  and video galleries, CV page, privacy policy, sitemap and robots.txt.
- **SEO:** schema.org Person / WebSite / ProfilePage data, Open Graph tags, canonical URLs.
- **Admin panel** (`/Admin`): edit the profile, projects, experience, skills, services, education, certifications,
  testimonials and the privacy policy; reorder and hide items; guided project editor with multi-file uploads, cover
  selection and captions.
- **Activity** (`/Admin/Activity`): anonymous, cookie-free page-view statistics, views per project, and the sign-in
  log, with search and paging. Records are deleted after 180 days.
- **3D tour** (`/world`): three.js scene built with Vite.

## Tech stack

| Layer | Technology |
|---|---|
| Web | ASP.NET Core 10 MVC (Razor views), output caching |
| Data | PostgreSQL ([Neon](https://neon.tech)) via EF Core / Npgsql |
| Media | [Cloudinary](https://cloudinary.com), or local `wwwroot/uploads` when Cloudinary isn't configured |
| 3D tour | three.js + Vite (`src/Portfolio.World`) |
| Hosting | [Render](https://render.com) (Docker, free plan) |
| CI | GitHub Actions |

## Project structure

```
src/
  Portfolio.Domain/          entities
  Portfolio.Application/     facades (use cases), abstractions, models
  Portfolio.Infrastructure/  EF Core, migrations, seeding, file storage
  Portfolio.Web/             controllers, views, static assets, composition root (Program.cs)
  Portfolio.World/           3D tour (three.js + Vite), built into Portfolio.Web/wwwroot/world
  Portfolio.Tests/           xUnit tests
scripts/                     one-off SQL scripts
```

## Motion

The public pages animate through a small declarative system. Markup asks for motion with data attributes;
`wwwroot/css/site-v2.css` says how things look, `wwwroot/css/motion.css` how they move, and `wwwroot/js/motion.js`
applies the attributes. For example, `data-enter="card"` makes a block rise in once when it comes into view,
`data-stagger` on its container makes siblings follow each other, and `data-pointer="glow"` adds a light that
follows the mouse. The full vocabulary is at the top of `motion.js`. Scrolling stays native, content arrives once
and stays, and everything is off when the visitor prefers reduced motion.

## Running locally

Requirements: .NET 10 SDK, Node 24, and a PostgreSQL database.

```bash
# 3D tour (optional; the rest of the site works without it)
cd src/Portfolio.World && npm ci && npm run build && cd ../..

# Configuration (user-secrets keep these out of the repo)
cd src/Portfolio.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=portfolio;Username=postgres"
dotnet user-secrets set "Admin:Password" "<choose a password>"

dotnet run
```

The site listens on `http://localhost:8080` (or `PORT`). Migrations and seed content are applied automatically at
startup. Sign in at `/Account/Login` with username `admin` (or `Admin:Username`).

## Configuration

| Setting | Environment variable | Notes |
|---|---|---|
| Database | `DATABASE_URL` | `postgres://user:pass@host/db` URL or a keyword connection string. Locally, `ConnectionStrings:DefaultConnection` also works. Required. |
| Admin username | `Admin__Username` | Defaults to `admin`. |
| Admin password | `Admin__Password` | Required on first run, to create the admin account. |
| Cloudinary | `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` | Optional; without them uploads are stored on local disk. |
| Telemetry retention | `Telemetry__RetentionDays` | Days to keep visit and sign-in records. Default 180. |
| Port | `PORT` | Default 8080. |

Never commit real credentials; set them as user-secrets locally and as environment variables on the host.

## Tests

```bash
dotnet test Portfolio.slnx
```

## Deployment

- Pushing to `main` deploys to Render automatically, using the `Dockerfile` and `render.yaml`. The Docker build
  compiles the 3D tour, then publishes the site.
- `/health` returns `ok` without touching the database; use it for Render's health check and uptime monitors.
- The **Keep alive** workflow pings `/health` every 10 minutes so the free instance doesn't sleep. Set the repository
  variable `SITE_URL` to change its target.

## CI

The **ci** workflow builds the 3D tour and the solution and runs all tests on every pull request and every push to
`main`, with a test summary on the run page. `main` is protected: pull requests need the `build-and-test` check to
pass before they can merge.

## License

[MIT](LICENSE)
