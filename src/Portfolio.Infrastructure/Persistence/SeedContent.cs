using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;

namespace Portfolio.Infrastructure.Persistence;

// Generated from the owner's LinkedIn profile (Oct 2026). The same content is applied to the existing
// production database by scripts/content-refresh-2026-10.sql; keep the two in sync if either changes.
public static class SeedContent
{
    public static Bio Bio() => new()
    {
        FullName = "Shoaib Riaz",
        Headline = "Team Lead · C# / .NET · AI/LLM · Office Add-ins & Unity",
        Tagline = "I build AI-powered .NET software, from backend APIs to desktop add-ins and real-time Unity clients, and lead the team that ships it.",
        About = "Senior Software Engineer and Team Lead with 7+ years across C#, .NET and Unity.\nI started in game development and grew into full-stack .NET work, so I'm comfortable on both sides of the wire: the backend serving the data and the real-time client consuming it. Multiplayer games talking to ASP.NET Core APIs, AR/VR apps syncing through Firebase, Unity clients driven by backend features.\nToday I lead the Lahore engineering team at Lucen Software, building AI-powered Microsoft Office add-ins: .NET 9 desktop apps with React frontends, integrating the Claude API for AI-driven diagram generation inside PowerPoint. Before that, I built an AI-assisted level creation pipeline for Fruit Ninja Adventure that cut level production time from 5–15 days to 1–5.",
        Location = "Lahore, Punjab, Pakistan",
        YearsExperience = "7+",
        Email = "riazshoaib17@gmail.com",
        LinkedIn = "https://www.linkedin.com/in/mshoaibriaz",
        GitHub = "https://github.com/ShoaibRiaz007",
        Website = "https://shoaibriaz.com",
    };

    public static List<Experience> Experiences() => new()
    {
        Role(1, "Team Lead", "Lucen Software", "Full-time", "Lahore, Pakistan · On-site", new DateOnly(2026, 7, 1), null,
            "Lead the Lahore team building the next-generation Office Timeline suite: AI-powered PowerPoint and Excel add-ins coordinated by a shared desktop launcher.\nOwn sprint priorities and coordinate releases with the wider engineering organization.\nRun code reviews and technical design discussions, enforcing architecture boundaries: presentation-only UI, launcher as coordinator, independent products, strictly shared libraries.\nConsolidated team coding standards into shared CLAUDE.md conventions, one source of truth for engineers and AI agents across the monorepo.\nIntroduced a structured bug-investigation workflow (root cause proven before any code change, confidence gate, minimal-fix rule) that turns Jira tickets into agent-ready task briefs.\nRemain hands-on in C#/.NET, WPF and React/TypeScript on AI diagram generation and the React ↔ .NET bridge layer."),
        Role(2, "Backend Engineer", "Lucen Software", "Full-time", "Lahore, Pakistan · Hybrid", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1),
            "Built LLM-driven diagram and flowchart generation inside PowerPoint using Claude structured outputs, clarifying-question flows and conversational refinement.\nBuilt evaluation harnesses that measure AI generation quality and gate changes before release.\nPrototyped MCP integration exposing add-in actions to AI assistants.\nDesigned JSON-RPC 2.0 over named-pipe IPC between the WinUI 3 launcher and the .NET 9 add-ins.\nOwned launcher install, auto-update and licensing flows, integrated with FastAPI/PostgreSQL entitlement services."),
        Role(3, "Senior Software Engineer", "UserWise", "Contract", "Lahore, Pakistan · Hybrid", new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 1),
            "Built an AI-assisted level creation pipeline for Fruit Ninja Adventure, letting designers create and polish levels with minimal engineering support.\nCut level production time 70–80% (5–15 days → 1–5 days).\nTook the system from design to production in 3 months (C#, Unity, backend integration, workflow automation)."),
        Role(4, "Senior Game Engineer", "InvoZone", "Part-time", "Remote", new DateOnly(2025, 12, 1), new DateOnly(2026, 5, 1),
            "Continued client gamification projects part-time."),
        Role(5, "Senior Game Engineer", "InvoZone", "Full-time", "Remote", new DateOnly(2025, 6, 1), new DateOnly(2025, 12, 1),
            "Client-facing technical lead for gamification and game projects, turning business goals into shipped interactive products.\nLed discovery meetings to define goals, KPIs and scope, and translated them into game mechanics and technical plans.\nGamified business processes (training, onboarding, engagement) with progression, rewards, challenges and leaderboards.\nProduced estimates, milestones and technical proposals, and presented progress directly to stakeholders.\nBuilt the Unity clients and backend integrations behind these experiences (C#, ASP.NET Core APIs, Firebase, analytics).\nMentored engineers and reviewed code to keep delivery on schedule."),
        Role(6, "Software Engineer", "InvoZone", "Full-time", "Lahore, Pakistan · Hybrid", new DateOnly(2023, 8, 1), new DateOnly(2025, 6, 1),
            "Developed ASP.NET Core backend services, REST APIs and web application features for client projects.\nWorked on SQL Server, Entity Framework Core, authentication, API design and system integrations.\nImproved backend performance, maintainability and reliability using clean code practices.\nSupported project delivery from planning to deployment."),
        Role(7, "Game Developer", "InvoZone", "Full-time", "Lahore, Pakistan · Hybrid", new DateOnly(2022, 7, 1), new DateOnly(2023, 8, 1),
            "Developed Unity-based gameplay systems, AR/VR experiences and real-time interactive applications.\nIntegrated Unity clients with backend APIs, Firebase, analytics, ads and third-party services.\nWorked on multiplayer, metaverse and production game features across multiple projects.\nImproved performance, stability and gameplay quality by debugging complex technical issues."),
        Role(8, "Game Developer", "OZI Technology", "Full-time", "Lahore, Pakistan · On-site", new DateOnly(2022, 1, 1), new DateOnly(2022, 7, 1),
            "Shipped live Unity game updates with Firebase Auth, Realtime Database, Analytics and AdMob monetization."),
        Role(9, "Game Developer", "Survival Tech Valley", "Full-time", "Lahore, Pakistan · On-site", new DateOnly(2020, 3, 1), new DateOnly(2021, 12, 1),
            "Built gameplay features and maintained legacy Unity/C# codebases through iterative releases."),
    };

    private static Experience Role(int order, string role, string company, string type, string location,
        DateOnly start, DateOnly? end, string description) => new()
    {
        Role = role, Company = company, EmploymentType = type, Location = location,
        StartDate = start, EndDate = end, Period = PeriodFormatter.Format(start, end),
        Description = description, SortOrder = order,
    };

    public static List<Education> Educations() => new()
    {
        new() { Institution = "GIFT University", Degree = "Bachelor's degree", FieldOfStudy = "Computer Software Engineering", SortOrder = 1 },
    };

    public static List<Certification> Certifications() => new()
    {
        new() { Name = "Claude 101", Issuer = "Anthropic", IssuedOn = new DateOnly(2026, 8, 1), CredentialId = "Claude 101",
            CredentialUrl = "https://verify.skilljar.com/c/cmtotja3k4wa", SortOrder = 1 },
        new() { Name = "Claude Code 101", Issuer = "Anthropic", IssuedOn = new DateOnly(2026, 8, 1), CredentialId = "Claude Code 101",
            CredentialUrl = "https://verify.skilljar.com/c/q8jreimrtkyx", SortOrder = 2 },
    };

    public static List<Skill> Skills() => new()
    {
        new() { Name = "C#", Category = "Backend", Level = 95, SortOrder = 1 },
        new() { Name = "ASP.NET Core / Web API", Category = "Backend", Level = 92, SortOrder = 2 },
        new() { Name = "Entity Framework Core", Category = "Backend", Level = 88, SortOrder = 3 },
        new() { Name = "SQL Server / PostgreSQL", Category = "Backend", Level = 85, SortOrder = 4 },
        new() { Name = "SignalR / WebSockets", Category = "Backend", Level = 82, SortOrder = 5 },
        new() { Name = ".NET 9 · WPF · WinUI 3", Category = "Desktop", Level = 88, SortOrder = 6 },
        new() { Name = "COM interop & Office add-ins", Category = "Desktop", Level = 85, SortOrder = 7 },
        new() { Name = "React / TypeScript", Category = "Frontend", Level = 75, SortOrder = 8 },
        new() { Name = "Claude API / LLM integration", Category = "AI", Level = 88, SortOrder = 9 },
        new() { Name = "AI evaluation harnesses", Category = "AI", Level = 80, SortOrder = 10 },
        new() { Name = "Unity", Category = "Game Dev", Level = 93, SortOrder = 11 },
        new() { Name = "Firebase", Category = "Integration", Level = 85, SortOrder = 12 },
        new() { Name = "Clean Architecture", Category = "Practices", Level = 88, SortOrder = 13 },
        new() { Name = "Technical leadership & code review", Category = "Practices", Level = 85, SortOrder = 14 },
    };

    public static List<Service> Services() => new()
    {
        new() { Title = "AI & LLM Integration", Icon = "🤖", SortOrder = 1,
            Description = "Structured-output LLM features, conversational refinement and evaluation harnesses that keep AI quality measurable before release." },
        new() { Title = "Backend & API Development", Icon = "🧩", SortOrder = 2,
            Description = "ASP.NET Core Web APIs and services with clean architecture, authentication and scalable client-server design." },
        new() { Title = "Desktop & Office Add-ins", Icon = "🖥️", SortOrder = 3,
            Description = ".NET 9 WPF/WinUI apps, COM interop with PowerPoint and Excel, and React UIs bridged through WebView2." },
        new() { Title = "Unity & Real-Time Systems", Icon = "🎮", SortOrder = 4,
            Description = "Unity clients, multiplayer and AR/VR features wired to backend APIs, Firebase and analytics." },
        new() { Title = "Database & EF Core", Icon = "🗄️", SortOrder = 5,
            Description = "Data modeling, SQL Server / PostgreSQL and Entity Framework Core, tuned for performance and maintainability." },
        new() { Title = "Technical Leadership", Icon = "🧭", SortOrder = 6,
            Description = "Sprint ownership, code review, architecture boundaries and agent-ready engineering workflows." },
    };

    /// <summary>Core (non-InvoGames) projects, in display order.</summary>
    public static List<Project> Projects() => new()
    {
        new()
        {
            Slug = "ai-diagram-generation-powerpoint",
            Title = "AI Diagram Generation for PowerPoint",
            Category = "AI / LLM",
            IsFeatured = true, SortOrder = 1,
            Metric = "Claude-powered diagrams inside PowerPoint",
            Summary = "LLM-driven flowchart and diagram generation inside PowerPoint, built on Claude structured outputs.",
            Description = "AI features for the next-generation Office Timeline PowerPoint and Excel add-ins. Users describe a process in plain language and get an editable native diagram, refined through clarifying questions and conversation.",
            Highlights = "Built LLM-driven diagram and flowchart generation using Claude structured outputs\nAdded clarifying-question flows and conversational refinement\nBuilt evaluation harnesses that measure generation quality and gate changes before release\nPrototyped MCP integration exposing add-in actions to AI assistants",
            TechStack = ".NET 9, C#, WPF, React, TypeScript, WebView2, Claude API",
            Timeline = "Jun 2026 – Present",
            MyRole = "Backend Engineer, then Team Lead, at Lucen Software",
        },
        new()
        {
            Slug = "fruit-ninja-adventure-ai-level-pipeline",
            Title = "Fruit Ninja Adventure — AI Level Pipeline",
            Category = "AI / Tooling",
            IsFeatured = true, SortOrder = 2,
            Metric = "70–80% faster level production",
            Summary = "AI-assisted level creation pipeline that cut level production time by 70–80%.",
            Description = "An AI-assisted Unity tooling pipeline that lets designers create and polish levels with minimal engineering support, integrated into production within 3 months.",
            Highlights = "Let designers create and polish levels with minimal engineering support\nCut level production from 5–15 days to 1–5 days\nTook the system from design to production in 3 months",
            TechStack = "Unity, C#, Backend integration, Workflow automation",
            Timeline = "Jan 2026 – May 2026",
            MyRole = "Senior Software Engineer (contract) at UserWise",
        },
        new()
        {
            Slug = "desktop-launcher-ipc-platform",
            Title = "Desktop Launcher & Add-in Platform",
            Category = ".NET Desktop",
            IsFeatured = true, SortOrder = 3,
            Metric = "JSON-RPC 2.0 over named pipes",
            Summary = "The WinUI 3 launcher and IPC layer that installs, updates and coordinates independent .NET 9 Office add-ins.",
            Description = "A shared desktop launcher that installs, updates, licenses and coordinates independent PowerPoint and Excel add-ins, with React UIs bridged to C# and COM services.",
            Highlights = "Designed JSON-RPC 2.0 over named-pipe IPC between the WinUI 3 launcher and the .NET 9 add-ins\nWorked on the bridge connecting React + TypeScript UIs (WebView2) to C#/WPF and COM interop services\nOwned launcher install, auto-update and licensing flows backed by FastAPI/PostgreSQL entitlement services",
            TechStack = ".NET 9, WinUI 3, WPF, COM interop, JSON-RPC, FastAPI, PostgreSQL",
            Timeline = "Jun 2026 – Present",
            MyRole = "Backend Engineer, then Team Lead, at Lucen Software",
        },
        new()
        {
            Slug = "winnder-real-money-gaming-platform",
            Title = "WINNDER — Real-Money Gaming Platform",
            Category = "Unity / Games",
            IsFeatured = true, SortOrder = 4,
            Summary = "Unity games and backend integration for the WINNDER / Gamersfy real-money gaming platform.",
            Description = "Delivered production Unity games and client-server integrations for WINNDER, working closely with the founding team to translate product needs into polished games delivered reliably on deadline.",
            TechStack = "Unity, C#, Backend APIs, Firebase",
            Timeline = "2023 – 2026",
            ProjectUrl = "https://winnder.com",
        },
        new()
        {
            Slug = "ar-vr-metaverse-experiences",
            Title = "AR / VR & Metaverse Experiences",
            Category = "Unity / XR",
            IsFeatured = false, SortOrder = 5,
            Summary = "Immersive AR/VR and metaverse applications with backend-driven gameplay.",
            Description = "AR/VR experiences and metaverse applications, including a VR bike and car controller, integrating real-time interaction with backend services.",
            TechStack = "Unity, C#, AR/VR, SignalR",
            Timeline = "2022 – 2026",
        },
        new()
        {
            Slug = "ads-package-for-unity",
            Title = "Ads Package for Unity",
            Category = "Unity / Packages",
            IsFeatured = false, SortOrder = 6,
            Summary = "Unity ad package supporting multiple advertisers in a waterfall method.",
            Description = "A reusable Unity ad package that supports multiple advertisers using waterfall mediation, simplifying monetization integration across projects.",
            TechStack = ".NET Core, Firebase, Unity, C#",
            Timeline = "Sep 2023 – Present",
        },
        new()
        {
            Slug = "observable-properties-system-for-unity",
            Title = "Observable Properties System for Unity",
            Category = "Unity / Packages",
            IsFeatured = false, SortOrder = 7,
            Summary = "Thread-safe, centralized observable properties and state management for Unity.",
            Description = "A centralized, thread-safe notification system for managing observable properties, enabling efficient updates and communication across components.",
            TechStack = "Unity, C#",
            GitHubUrl = "https://github.com/ShoaibRiaz007/Observable-Properties-System-for-Unity",
        },
        new()
        {
            Slug = "unity-editor-subclass-selector",
            Title = "Unity Editor Subclass Selector",
            Category = "Unity / Packages",
            IsFeatured = false, SortOrder = 8,
            Summary = "Editor extension for selecting and serializing subclasses in the Unity inspector.",
            Description = "A Unity Editor tool that lets developers pick concrete subclasses directly from the inspector, improving serialization workflows and reducing boilerplate.",
            TechStack = "Unity, C#, Editor Tooling",
        },
    };

    /// <summary>InvoGames titles shown publicly, in display order; the others are seeded hidden.</summary>
    public static readonly IReadOnlyList<string> CuratedGameSlugs = new[]
    {
        "rage-war", "atomic-golf", "fps-survival-game", "hoof-clash-rider", "vampire-capitalist", "vr-music-app", "unity-xr-hand-gesture",
    };

    /// <summary>Games sort after the core projects.</summary>
    public const int GamesSortStart = 100;
}
