-- One-time content refresh for the EXISTING production database (Oct 2026), from the LinkedIn profile.
-- Generated together with src/Portfolio.Infrastructure/Persistence/SeedContent.cs (fresh databases).
--
-- Run AFTER deploying the build containing migration ContentV2PublishDatesEducationCerts
-- (the app migrates on startup). Review first; it runs in one transaction.
--
-- What it does:
--   Bio          : updates name, headline, tagline, about, location, years, links. Keeps phone, photo, CV.
--   Experience   : REPLACES all rows with the 9 LinkedIn roles (admin edits to experience are lost).
--   Skills       : REPLACES all rows.
--   Services     : REPLACES all rows.
--   Education    : adds GIFT University if missing.
--   Certs        : adds the two Anthropic certifications if missing.
--   Projects     : upserts the 8 core projects by slug (keeps their images, links and case-study text);
--                  keeps 7 InvoGames titles visible and HIDES the rest (nothing is deleted).
--   Testimonials : untouched.
--   Aborts with no changes if the resulting counts are not 9 roles / 15 visible projects.
--
-- psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/content-refresh-2026-10.sql

BEGIN;

UPDATE "Bios" SET
    "FullName" = 'Shoaib Riaz',
    "Headline" = 'Team Lead · C# / .NET · AI/LLM · Office Add-ins & Unity',
    "Tagline" = 'I build AI-powered .NET software, from backend APIs to desktop add-ins and real-time Unity clients, and lead the team that ships it.',
    "About" = 'Senior Software Engineer and Team Lead with 7+ years across C#, .NET and Unity.
I started in game development and grew into full-stack .NET work, so I''m comfortable on both sides of the wire: the backend serving the data and the real-time client consuming it. Multiplayer games talking to ASP.NET Core APIs, AR/VR apps syncing through Firebase, Unity clients driven by backend features.
Today I lead the Lahore engineering team at Lucen Software, building AI-powered Microsoft Office add-ins: .NET 9 desktop apps with React frontends, integrating the Claude API for AI-driven diagram generation inside PowerPoint. Before that, I built an AI-assisted level creation pipeline for Fruit Ninja Adventure that cut level production time from 5–15 days to 1–5.',
    "Location" = 'Lahore, Punjab, Pakistan',
    "YearsExperience" = '7+',
    "LinkedIn" = 'https://www.linkedin.com/in/mshoaibriaz',
    "GitHub" = 'https://github.com/ShoaibRiaz007',
    "Website" = 'https://shoaibriaz.com';

DELETE FROM "Experiences";
INSERT INTO "Experiences" ("Role", "Company", "EmploymentType", "Location", "StartDate", "EndDate", "Period", "Description", "SortOrder", "IsPublished") VALUES
    ('Team Lead', 'Lucen Software', 'Full-time', 'Lahore, Pakistan · On-site', DATE '2026-07-01', NULL, 'Jul 2026 - Present',
     'Lead the Lahore team building the next-generation Office Timeline suite: AI-powered PowerPoint and Excel add-ins coordinated by a shared desktop launcher.
Own sprint priorities and coordinate releases with the wider engineering organization.
Run code reviews and technical design discussions, enforcing architecture boundaries: presentation-only UI, launcher as coordinator, independent products, strictly shared libraries.
Consolidated team coding standards into shared CLAUDE.md conventions, one source of truth for engineers and AI agents across the monorepo.
Introduced a structured bug-investigation workflow (root cause proven before any code change, confidence gate, minimal-fix rule) that turns Jira tickets into agent-ready task briefs.
Remain hands-on in C#/.NET, WPF and React/TypeScript on AI diagram generation and the React ↔ .NET bridge layer.', 1, TRUE),
    ('Backend Engineer', 'Lucen Software', 'Full-time', 'Lahore, Pakistan · Hybrid', DATE '2026-06-01', DATE '2026-07-01', 'Jun 2026 - Jul 2026',
     'Built LLM-driven diagram and flowchart generation inside PowerPoint using Claude structured outputs, clarifying-question flows and conversational refinement.
Built evaluation harnesses that measure AI generation quality and gate changes before release.
Prototyped MCP integration exposing add-in actions to AI assistants.
Designed JSON-RPC 2.0 over named-pipe IPC between the WinUI 3 launcher and the .NET 9 add-ins.
Owned launcher install, auto-update and licensing flows, integrated with FastAPI/PostgreSQL entitlement services.', 2, TRUE),
    ('Senior Software Engineer', 'UserWise', 'Contract', 'Lahore, Pakistan · Hybrid', DATE '2026-01-01', DATE '2026-05-01', 'Jan 2026 - May 2026',
     'Built an AI-assisted level creation pipeline for Fruit Ninja Adventure, letting designers create and polish levels with minimal engineering support.
Cut level production time 70–80% (5–15 days → 1–5 days).
Took the system from design to production in 3 months (C#, Unity, backend integration, workflow automation).', 3, TRUE),
    ('Senior Game Engineer', 'InvoZone', 'Part-time', 'Remote', DATE '2025-12-01', DATE '2026-05-01', 'Dec 2025 - May 2026',
     'Continued client gamification projects part-time.', 4, TRUE),
    ('Senior Game Engineer', 'InvoZone', 'Full-time', 'Remote', DATE '2025-06-01', DATE '2025-12-01', 'Jun 2025 - Dec 2025',
     'Client-facing technical lead for gamification and game projects, turning business goals into shipped interactive products.
Led discovery meetings to define goals, KPIs and scope, and translated them into game mechanics and technical plans.
Gamified business processes (training, onboarding, engagement) with progression, rewards, challenges and leaderboards.
Produced estimates, milestones and technical proposals, and presented progress directly to stakeholders.
Built the Unity clients and backend integrations behind these experiences (C#, ASP.NET Core APIs, Firebase, analytics).
Mentored engineers and reviewed code to keep delivery on schedule.', 5, TRUE),
    ('Software Engineer', 'InvoZone', 'Full-time', 'Lahore, Pakistan · Hybrid', DATE '2023-08-01', DATE '2025-06-01', 'Aug 2023 - Jun 2025',
     'Developed ASP.NET Core backend services, REST APIs and web application features for client projects.
Worked on SQL Server, Entity Framework Core, authentication, API design and system integrations.
Improved backend performance, maintainability and reliability using clean code practices.
Supported project delivery from planning to deployment.', 6, TRUE),
    ('Game Developer', 'InvoZone', 'Full-time', 'Lahore, Pakistan · Hybrid', DATE '2022-07-01', DATE '2023-08-01', 'Jul 2022 - Aug 2023',
     'Developed Unity-based gameplay systems, AR/VR experiences and real-time interactive applications.
Integrated Unity clients with backend APIs, Firebase, analytics, ads and third-party services.
Worked on multiplayer, metaverse and production game features across multiple projects.
Improved performance, stability and gameplay quality by debugging complex technical issues.', 7, TRUE),
    ('Game Developer', 'OZI Technology', 'Full-time', 'Lahore, Pakistan · On-site', DATE '2022-01-01', DATE '2022-07-01', 'Jan 2022 - Jul 2022',
     'Shipped live Unity game updates with Firebase Auth, Realtime Database, Analytics and AdMob monetization.', 8, TRUE),
    ('Game Developer', 'Survival Tech Valley', 'Full-time', 'Lahore, Pakistan · On-site', DATE '2020-03-01', DATE '2021-12-01', 'Mar 2020 - Dec 2021',
     'Built gameplay features and maintained legacy Unity/C# codebases through iterative releases.', 9, TRUE);

DELETE FROM "Skills";
INSERT INTO "Skills" ("Name", "Category", "Level", "SortOrder", "IsPublished") VALUES
    ('C#', 'Backend', 95, 1, TRUE),
    ('ASP.NET Core / Web API', 'Backend', 92, 2, TRUE),
    ('Entity Framework Core', 'Backend', 88, 3, TRUE),
    ('SQL Server / PostgreSQL', 'Backend', 85, 4, TRUE),
    ('SignalR / WebSockets', 'Backend', 82, 5, TRUE),
    ('.NET 9 · WPF · WinUI 3', 'Desktop', 88, 6, TRUE),
    ('COM interop & Office add-ins', 'Desktop', 85, 7, TRUE),
    ('React / TypeScript', 'Frontend', 75, 8, TRUE),
    ('Claude API / LLM integration', 'AI', 88, 9, TRUE),
    ('AI evaluation harnesses', 'AI', 80, 10, TRUE),
    ('Unity', 'Game Dev', 93, 11, TRUE),
    ('Firebase', 'Integration', 85, 12, TRUE),
    ('Clean Architecture', 'Practices', 88, 13, TRUE),
    ('Technical leadership & code review', 'Practices', 85, 14, TRUE);

DELETE FROM "Services";
INSERT INTO "Services" ("Title", "Icon", "Description", "SortOrder", "IsPublished") VALUES
    ('AI & LLM Integration', '🤖', 'Structured-output LLM features, conversational refinement and evaluation harnesses that keep AI quality measurable before release.', 1, TRUE),
    ('Backend & API Development', '🧩', 'ASP.NET Core Web APIs and services with clean architecture, authentication and scalable client-server design.', 2, TRUE),
    ('Desktop & Office Add-ins', '🖥️', '.NET 9 WPF/WinUI apps, COM interop with PowerPoint and Excel, and React UIs bridged through WebView2.', 3, TRUE),
    ('Unity & Real-Time Systems', '🎮', 'Unity clients, multiplayer and AR/VR features wired to backend APIs, Firebase and analytics.', 4, TRUE),
    ('Database & EF Core', '🗄️', 'Data modeling, SQL Server / PostgreSQL and Entity Framework Core, tuned for performance and maintainability.', 5, TRUE),
    ('Technical Leadership', '🧭', 'Sprint ownership, code review, architecture boundaries and agent-ready engineering workflows.', 6, TRUE);

INSERT INTO "Educations" ("Institution", "Degree", "FieldOfStudy", "Description", "SortOrder", "IsPublished")
SELECT 'GIFT University', 'Bachelor''s degree', 'Computer Software Engineering', '', 1, TRUE
WHERE NOT EXISTS (SELECT 1 FROM "Educations" WHERE "Institution" = 'GIFT University');

INSERT INTO "Certifications" ("Name", "Issuer", "IssuedOn", "CredentialId", "CredentialUrl", "SortOrder", "IsPublished")
SELECT 'Claude 101', 'Anthropic', DATE '2026-08-01', 'Claude 101', 'https://verify.skilljar.com/c/cmtotja3k4wa', 1, TRUE
WHERE NOT EXISTS (SELECT 1 FROM "Certifications" WHERE "Name" = 'Claude 101');
INSERT INTO "Certifications" ("Name", "Issuer", "IssuedOn", "CredentialId", "CredentialUrl", "SortOrder", "IsPublished")
SELECT 'Claude Code 101', 'Anthropic', DATE '2026-08-01', 'Claude Code 101', 'https://verify.skilljar.com/c/q8jreimrtkyx', 2, TRUE
WHERE NOT EXISTS (SELECT 1 FROM "Certifications" WHERE "Name" = 'Claude Code 101');

-- Hide everything first, then re-show the core projects and the curated games.
UPDATE "Projects" SET "IsPublished" = FALSE, "IsFeatured" = FALSE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('ai-diagram-generation-powerpoint', 'AI Diagram Generation for PowerPoint', 'AI / LLM', 'LLM-driven flowchart and diagram generation inside PowerPoint, built on Claude structured outputs.', 'AI features for the next-generation Office Timeline PowerPoint and Excel add-ins. Users describe a process in plain language and get an editable native diagram, refined through clarifying questions and conversation.', 'Built LLM-driven diagram and flowchart generation using Claude structured outputs
Added clarifying-question flows and conversational refinement
Built evaluation harnesses that measure generation quality and gate changes before release
Prototyped MCP integration exposing add-in actions to AI assistants', 'Claude-powered diagrams inside PowerPoint', '.NET 9, C#, WPF, React, TypeScript, WebView2, Claude API', 'Jun 2026 – Present', 'Backend Engineer, then Team Lead, at Lucen Software', '', '', '', '', '', '', TRUE, 1, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('fruit-ninja-adventure-ai-level-pipeline', 'Fruit Ninja Adventure — AI Level Pipeline', 'AI / Tooling', 'AI-assisted level creation pipeline that cut level production time by 70–80%.', 'An AI-assisted Unity tooling pipeline that lets designers create and polish levels with minimal engineering support, integrated into production within 3 months.', 'Let designers create and polish levels with minimal engineering support
Cut level production from 5–15 days to 1–5 days
Took the system from design to production in 3 months', '70–80% faster level production', 'Unity, C#, Backend integration, Workflow automation', 'Jan 2026 – May 2026', 'Senior Software Engineer (contract) at UserWise', '', '', '', '', '', '', TRUE, 2, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('desktop-launcher-ipc-platform', 'Desktop Launcher & Add-in Platform', '.NET Desktop', 'The WinUI 3 launcher and IPC layer that installs, updates and coordinates independent .NET 9 Office add-ins.', 'A shared desktop launcher that installs, updates, licenses and coordinates independent PowerPoint and Excel add-ins, with React UIs bridged to C# and COM services.', 'Designed JSON-RPC 2.0 over named-pipe IPC between the WinUI 3 launcher and the .NET 9 add-ins
Worked on the bridge connecting React + TypeScript UIs (WebView2) to C#/WPF and COM interop services
Owned launcher install, auto-update and licensing flows backed by FastAPI/PostgreSQL entitlement services', 'JSON-RPC 2.0 over named pipes', '.NET 9, WinUI 3, WPF, COM interop, JSON-RPC, FastAPI, PostgreSQL', 'Jun 2026 – Present', 'Backend Engineer, then Team Lead, at Lucen Software', '', '', '', '', '', '', TRUE, 3, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('winnder-real-money-gaming-platform', 'WINNDER — Real-Money Gaming Platform', 'Unity / Games', 'Unity games and backend integration for the WINNDER / Gamersfy real-money gaming platform.', 'Delivered production Unity games and client-server integrations for WINNDER, working closely with the founding team to translate product needs into polished games delivered reliably on deadline.', '', '', 'Unity, C#, Backend APIs, Firebase', '2023 – 2026', '', 'https://winnder.com', '', '', '', '', '', TRUE, 4, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('ar-vr-metaverse-experiences', 'AR / VR & Metaverse Experiences', 'Unity / XR', 'Immersive AR/VR and metaverse applications with backend-driven gameplay.', 'AR/VR experiences and metaverse applications, including a VR bike and car controller, integrating real-time interaction with backend services.', '', '', 'Unity, C#, AR/VR, SignalR', '2022 – 2026', '', '', '', '', '', '', '', FALSE, 5, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('ads-package-for-unity', 'Ads Package for Unity', 'Unity / Packages', 'Unity ad package supporting multiple advertisers in a waterfall method.', 'A reusable Unity ad package that supports multiple advertisers using waterfall mediation, simplifying monetization integration across projects.', '', '', '.NET Core, Firebase, Unity, C#', 'Sep 2023 – Present', '', '', '', '', '', '', '', FALSE, 6, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('observable-properties-system-for-unity', 'Observable Properties System for Unity', 'Unity / Packages', 'Thread-safe, centralized observable properties and state management for Unity.', 'A centralized, thread-safe notification system for managing observable properties, enabling efficient updates and communication across components.', '', '', 'Unity, C#', '', '', '', 'https://github.com/ShoaibRiaz007/Observable-Properties-System-for-Unity', '', '', '', '', FALSE, 7, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES ('unity-editor-subclass-selector', 'Unity Editor Subclass Selector', 'Unity / Packages', 'Editor extension for selecting and serializing subclasses in the Unity inspector.', 'A Unity Editor tool that lets developers pick concrete subclasses directly from the inspector, improving serialization workflows and reducing boilerplate.', '', '', 'Unity, C#, Editor Tooling', '', '', '', '', '', '', '', '', FALSE, 8, TRUE)
ON CONFLICT ("Slug") DO UPDATE SET
    "Title" = EXCLUDED."Title",
    "Category" = EXCLUDED."Category",
    "Summary" = EXCLUDED."Summary",
    "Description" = EXCLUDED."Description",
    "Highlights" = EXCLUDED."Highlights",
    "Metric" = EXCLUDED."Metric",
    "TechStack" = EXCLUDED."TechStack",
    "Timeline" = EXCLUDED."Timeline",
    "MyRole" = COALESCE(NULLIF(EXCLUDED."MyRole", ''), "Projects"."MyRole"),
    "ProjectUrl" = COALESCE(NULLIF(EXCLUDED."ProjectUrl", ''), "Projects"."ProjectUrl"),
    "GitHubUrl" = COALESCE(NULLIF(EXCLUDED."GitHubUrl", ''), "Projects"."GitHubUrl"),
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

UPDATE "Projects" SET "IsPublished" = TRUE, "SortOrder" = 100 WHERE "Slug" = 'rage-war';
UPDATE "Projects" SET "IsPublished" = TRUE, "SortOrder" = 101 WHERE "Slug" = 'atomic-golf';
UPDATE "Projects" SET "IsPublished" = TRUE, "SortOrder" = 102 WHERE "Slug" = 'fps-survival-game';
UPDATE "Projects" SET "IsPublished" = TRUE, "SortOrder" = 103 WHERE "Slug" = 'hoof-clash-rider';
UPDATE "Projects" SET "IsPublished" = TRUE, "SortOrder" = 104 WHERE "Slug" = 'vampire-capitalist';
UPDATE "Projects" SET "IsPublished" = TRUE, "SortOrder" = 105 WHERE "Slug" = 'vr-music-app';
UPDATE "Projects" SET "IsPublished" = TRUE, "SortOrder" = 106 WHERE "Slug" = 'unity-xr-hand-gesture';

-- Hidden rows go after everything visible, in their existing order, so they never tie with or sit
-- between the core projects in the admin list (matches how fresh databases are seeded).
UPDATE "Projects" p SET "SortOrder" = 107 + h.rn - 1
FROM (SELECT "Id", row_number() OVER (ORDER BY "SortOrder", "Id") AS rn
      FROM "Projects" WHERE NOT "IsPublished") h
WHERE p."Id" = h."Id";

-- Abort (and roll back everything above) unless the result is exactly what was intended.
DO $$
DECLARE roles int; visible int;
BEGIN
    SELECT count(*) INTO roles FROM "Experiences";
    SELECT count(*) INTO visible FROM "Projects" WHERE "IsPublished";
    IF roles <> 9 OR visible <> 15 THEN
        RAISE EXCEPTION 'Unexpected result: % roles (want 9), % visible projects (want 15). Rolled back.', roles, visible;
    END IF;
END $$;

COMMIT;

-- The site caches public pages for up to 10 minutes and this script bypasses the app, so afterwards
-- restart the service (or save any item in the admin) to show the new content immediately.
