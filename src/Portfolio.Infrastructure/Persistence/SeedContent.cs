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
        Website = "https://www.shoaibriaz.com",
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
            Title = "AI Diagram Generation for PowerPoint (.NET 9 & Claude API)",
            Category = "AI / LLM",
            IsFeatured = true, SortOrder = 1,
            Metric = "Claude-powered diagrams inside PowerPoint",
            Summary = "Enterprise PowerPoint AI add-in generating editable process diagrams and flowcharts from plain English using Claude structured outputs and .NET 9 WPF.",
            Description = "AI-powered flowchart and process diagram generation for the next-generation Office Timeline suite. Users describe complex workflows in plain language and receive native, fully editable PowerPoint shapes and connectors, refined conversationally through clarifying questions and interactive previews.",
            Challenge = "LLM hallucinations frequently distort topological graph connections, connector routing, and shape hierarchies when generating diagrams from unstructured text.\nEnterprise latency requirements and strict Microsoft Office OpenXML/COM shape constraints required diagrams to be 100% syntactically and geometrically valid on every run.\nEvaluating non-deterministic generative models required automated testing before shipping updates to enterprise users.",
            Solution = "Engineered an end-to-end diagram generation pipeline using Claude API structured outputs enforced by rigid JSON schemas defining nodes, directed edges, and branch layouts.\nBuilt multi-turn clarifying question dialogs that detect ambiguous process descriptions and confirm logic with the user before rendering.\nDesigned a high-performance React and TypeScript UI inside WebView2 bridged to a C# .NET 9 WPF backend and COM interop layer.\nDeveloped automated CI evaluation harnesses that benchmark model output quality against canonical diagram test suites before release.\nPrototyped MCP (Model Context Protocol) endpoints exposing PowerPoint shape manipulation and timeline actions to AI assistant clients.",
            Outcome = "Allowed enterprise users to generate polished, executive-ready diagrams in seconds instead of hours of manual layout.\nAchieved deterministic shape generation with zero geometry syntax failures through JSON Schema validation.\nShipped as a flagship AI feature for the next-generation Office Timeline add-in suite.",
            Highlights = "Engineered Claude API structured outputs with JSON schema for deterministic flowchart generation\nBuilt interactive clarifying-question dialogs for conversational refinement\nDesigned React + TypeScript WebView2 bridge to C# WPF and Office COM APIs\nCreated automated AI evaluation harnesses that benchmark generation quality and gate releases\nPrototyped MCP (Model Context Protocol) integration exposing add-in actions to AI agents",
            TechStack = ".NET 9, C#, WPF, React, TypeScript, WebView2, Claude API, JSON Schema, OpenXML, MCP",
            Timeline = "Jun 2026 – Present",
            MyRole = "Backend Engineer, then Team Lead, at Lucen Software",
        },
        new()
        {
            Slug = "desktop-launcher-ipc-platform",
            Title = "WinUI 3 Desktop Launcher & Office Add-in IPC Platform",
            Category = ".NET Desktop",
            IsFeatured = true, SortOrder = 2,
            Metric = "JSON-RPC 2.0 over named pipes",
            Summary = "Centralized WinUI 3 desktop platform orchestrating independent .NET 9 PowerPoint and Excel add-ins via JSON-RPC 2.0 named pipes IPC and FastAPI licensing.",
            Description = "A modular Windows desktop coordinator and launcher that manages background installation, atomic auto-updates, offline-capable licensing, and inter-process communication (IPC) across independent Microsoft PowerPoint and Excel add-ins.",
            Challenge = "Office add-ins run inside host Microsoft PowerPoint and Excel processes via COM. Heavy background operations, licensing checks, or unhandled exceptions inside the add-in risk crashing the host Office application.\nCoordinating multiple standalone add-in instances required sub-millisecond, secure inter-process communication without TCP port collisions, firewall prompts, or elevated administrative privileges.",
            Solution = "Architected a bidirectional IPC communication subsystem based on named pipes and JSON-RPC 2.0, enabling isolated, low-latency command routing between the WinUI 3 launcher and running add-in instances.\nImplemented heartbeat ping health checks and graceful stream recovery to automatically handle process crashes and reconnect severed pipes.\nIntegrated cloud entitlement and auto-update verification backed by a FastAPI and PostgreSQL service with cryptographically signed local license caching.\nConnected the React/TypeScript presentation layer to C# COM services via a typed WebView2 messaging bridge.",
            Outcome = "Eliminated Office application freezes and memory bloat by offloading licensing and lifecycle coordination to an isolated desktop launcher process.\nDelivered zero-latency IPC messaging with 100% crash recovery and seamless background auto-updating across thousands of enterprise users.",
            Highlights = "Architected bidirectional JSON-RPC 2.0 over named-pipe IPC between WinUI 3 and .NET 9 add-ins\nEngineered process crash isolation, heartbeat ping monitoring, and automatic pipe reconnection\nIntegrated cloud entitlement, licensing, and auto-update synchronization with FastAPI and PostgreSQL\nBuilt typed messaging bridge connecting React/TypeScript WebView2 UI with native C# COM controllers",
            TechStack = ".NET 9, WinUI 3, C#, WPF, Named Pipes IPC, JSON-RPC 2.0, WebView2, FastAPI, PostgreSQL",
            Timeline = "Jun 2026 – Present",
            MyRole = "Backend Engineer, then Team Lead, at Lucen Software",
        },
        new()
        {
            Slug = "fruit-ninja-adventure-ai-level-pipeline",
            Title = "Fruit Ninja Adventure — AI Level Creation Pipeline",
            Category = "AI / Tooling",
            IsFeatured = true, SortOrder = 3,
            Metric = "70–80% faster level production",
            Summary = "Automated Unity level authoring pipeline that reduced level production turnaround from 15 days to 1–5 days through procedural curve balancing and designer tools in C#.",
            Description = "An AI-assisted level authoring and curve generation pipeline for Fruit Ninja Adventure that empowered level designers to author, balance, and stage complete gameplay levels with minimal engineering intervention.",
            Challenge = "Level production was an engineering bottleneck: hand-crafting fruit spawn trajectories, bomb frequency curves, velocity variance, and difficulty pacing required 5 to 15 days of manual iteration per level.\nDifficulty spikes frequently compromised retention, and testing new game modes required repetitive code modifications.",
            Solution = "Developed custom Unity Editor tooling in C# with algorithmic curve generation that automatically maps difficulty targets into balanced spawn rhythms, slice trajectories, and obstacle combinations.\nBuilt an interactive visual timeline and inspector interface allowing designers to preview, scrub, and customize procedural segments in real time.\nIntegrated automated validation rules that detect unsolvable fruit clusters and timing deadlocks before levels reach QA, exporting clean validated JSON configs to the backend.",
            Outcome = "Reduced end-to-end level creation turnaround by 70–80% (from 5–15 days down to 1–5 days per level).\nFully deployed the entire pipeline from design concept to production in 3 months, completely removing engineering support bottlenecks from the content team.",
            Highlights = "Reduced level production turnaround by 70–80%, cutting creation time from 5–15 days to 1–5 days\nEngineered algorithmic curve-balancing for fruit spawn rhythms, slice arcs, and obstacle density\nCreated custom Unity Editor inspectors and visual timeline scrubbers for live design iteration\nDelivered complete pipeline from design to production integration in under 3 months",
            TechStack = "Unity, C#, Editor Scripting, Procedural Algorithms, Backend Integration, Workflow Automation",
            Timeline = "Jan 2026 – May 2026",
            MyRole = "Senior Software Engineer (contract) at UserWise",
        },
        new()
        {
            Slug = "winnder-real-money-gaming-platform",
            Title = "WINNDER — Real-Money Competitive Gaming Platform",
            Category = "Unity / Games",
            IsFeatured = true, SortOrder = 4,
            Metric = "Real-time competitive mobile multiplayer",
            Summary = "Real-money competitive skill gaming platform built in Unity and C#, featuring deterministic gameplay, Firebase synchronization, and ASP.NET Core APIs.",
            Description = "Production Unity mobile titles and client-server backend infrastructure for the WINNDER / Gamersfy competitive gaming platform, powering cash-prize skill matches with real-time matchmaking, secure scoring, and anti-cheat validation.",
            Challenge = "Real-money competitive gaming requires absolute determinism and zero tolerance for client-side score tampering or connection exploits.\nMobile cellular networks introduce frequent latency spikes and dropped packets that cannot be allowed to desynchronize match states or cause unfair disqualifications.",
            Solution = "Engineered deterministic Unity gameplay in C# with server-authoritative score auditing over ASP.NET Core REST APIs and WebSockets.\nImplemented real-time matchmaking and match room state synchronization using Firebase Realtime Database and Cloud Functions.\nAdded client input telemetry replay heuristics to detect memory manipulation, speed hacks, and impossible action sequences before awarding prize payouts.\nOptimized garbage collection and shader warm-up to sustain 60 FPS across mid-range Android devices and iPhones.",
            Outcome = "Maintained a reliable, cheat-free competitive platform with zero unresolved match disputes from technical desyncs.\nDelivered smooth 60 FPS gameplay on iOS and Android under live tournament concurrency, receiving commendations from executive leadership for delivery reliability.",
            Highlights = "Engineered server-authoritative client gameplay and input validation in Unity and C#\nIntegrated Firebase Auth, Realtime Database, and Cloud Functions for instant skill-based matchmaking\nImplemented anti-tamper telemetry and replay score verification for financial dispute prevention\nMaintained rock-solid 60 FPS performance across iOS and diverse Android device tiers",
            TechStack = "Unity, C#, ASP.NET Core, Firebase, WebSockets, iOS, Android, REST APIs",
            Timeline = "2023 – 2026",
            ProjectUrl = "https://winnder.com",
        },
        new()
        {
            Slug = "ar-vr-metaverse-experiences",
            Title = "AR / VR & Metaverse Interactive Experiences",
            Category = "Unity / XR",
            IsFeatured = false, SortOrder = 5,
            Metric = "90 FPS sub-20ms latency VR simulation",
            Summary = "Virtual reality simulators, metaverse worlds, and automotive digital twins built in Unity with OpenXR, physical hardware controllers, and SignalR multiplayer.",
            Description = "A suite of production XR applications and metaverse environments, including physical VR bicycle and automotive driving simulators, interactive digital twins, and hand-gesture interaction experiences for enterprise and entertainment.",
            Challenge = "Achieving rock-solid 90+ FPS and under 20ms motion-to-photon latency on standalone VR headsets (Meta Quest) to prevent simulator sickness, while simultaneously interfacing with external physical simulator hardware and syncing multiplayer physics.",
            Solution = "Engineered custom vehicle kinematics and physics drivers in Unity C# interfacing with physical ergometers and steering inputs.\nApplied aggressive XR graphics optimizations: Single-Pass Instanced rendering, fixed foveated rendering, and GPU instancing.\nImplemented low-latency multiplayer avatar synchronization and spatial audio over SignalR and WebSockets.",
            Outcome = "Delivered sickness-free, immersive VR experiences operating at a steady 90 FPS on standalone hardware.\nSuccessfully deployed commercial installations integrating physical simulator equipment with real-time virtual environments.",
            Highlights = "Built custom vehicle kinematics and input drivers for physical VR bike and car simulators\nImplemented multiplayer avatar synchronization, dead reckoning, and spatial audio via SignalR\nOptimized rendering pipelines for consistent 90 FPS on standalone Meta Quest hardware",
            TechStack = "Unity, C#, OpenXR, Meta Quest SDK, SignalR, Physics Simulation, Spatial Audio",
            Timeline = "2022 – 2026",
        },
        new()
        {
            Slug = "ads-package-for-unity",
            Title = "Waterfall Ads Mediation Package for Unity",
            Category = "Unity / Packages",
            IsFeatured = false, SortOrder = 6,
            Metric = "Zero-overhead multi-network ad mediation",
            Summary = "Modular, thread-safe Unity monetization SDK supporting multi-network waterfall mediation with Firebase Remote Config and analytics.",
            Description = "A lightweight, decoupled Unity monetization package that abstracts multiple ad networks (Google AdMob, Unity Ads, AppLovin) into a unified, event-driven C# facade with dynamic cloud mediation rules.",
            Challenge = "Monetization SDKs frequently introduce main-thread stutters, fail to handle orientation transitions safely, and require rebuilding and re-submitting games to app stores just to re-order ad networks or change impression timeouts.",
            Solution = "Architected a thread-safe C# mediation layer with non-blocking async callbacks that isolates third-party native SDK calls from the Unity game loop.\nIntegrated Firebase Remote Config to dynamically update waterfall network priorities, fallback timeouts, and rewarded video cooldowns on the fly.\nAdded automated failover chains ensuring an ad always loads or fails gracefully without interrupting gameplay.",
            Outcome = "Adopted across live commercial mobile games, increasing ad fill rates by 25% and maximizing eCPMs without requiring client app updates.\nCut new project ad integration time from days to minutes.",
            Highlights = "Built unified waterfall mediation abstraction over Google AdMob and major ad networks\nEnabled dynamic mediation waterfall tuning via Firebase Remote Config without app updates\nEngineered failover chains and thread isolation preventing game loop frame drops",
            TechStack = "Unity, C#, Google AdMob, Firebase Remote Config, Native Android/iOS Bridges",
            Timeline = "Sep 2023 – Present",
        },
        new()
        {
            Slug = "observable-properties-system-for-unity",
            Title = "Thread-Safe Observable Properties System for Unity",
            Category = "Unity / Packages",
            IsFeatured = false, SortOrder = 7,
            Metric = "Zero-allocation reactive state management",
            Summary = "High-performance, GC-friendly reactive state management and property binding library for Unity C# architectures.",
            Description = "An open-source reactive data-binding system for Unity that eliminates expensive polling in Update() loops, cleanly decouples UI from gameplay state, and prevents memory leaks through lifecycle-aware subscriptions.",
            Challenge = "Game development architectures often degrade into tightly coupled systems where UI scripts poll gameplay state every frame inside Update(), wasting CPU cycles and causing fragile dependencies.\nNative C# events are prone to memory leaks when event listeners are not manually detached upon GameObject destruction.",
            Solution = "Engineered a generic ObservableProperty<T> system with automated subscription management tied directly to Unity MonoBehaviour lifecycles.\nImplemented zero-allocation value comparisons and event dispatching for primitive and struct types to avoid garbage collection spikes.\nDeveloped custom Unity Editor PropertyDrawers for real-time value inspection and live debugging in the inspector.",
            Outcome = "Open-sourced on GitHub with comprehensive test coverage.\nEliminated UI polling overhead across game components, improving frame budget predictability and architectural modularity.",
            Highlights = "Zero-allocation event dispatch for value types preventing garbage collection spikes\nAutomated unsubscription tied to Unity GameObject destruction preventing memory leaks\nCustom Unity Inspector drawers for live runtime inspection and state debugging",
            TechStack = "Unity, C#, Reactive Programming, Memory Optimization, Custom Inspectors",
            GitHubUrl = "https://github.com/ShoaibRiaz007/Observable-Properties-System-for-Unity",
        },
        new()
        {
            Slug = "unity-editor-subclass-selector",
            Title = "Subclass Selector Attribute for Unity Inspector",
            Category = "Unity / Packages",
            IsFeatured = false, SortOrder = 8,
            Metric = "Polymorphic serialization in Unity Editor",
            Summary = "Unity editor extension enabling intuitive polymorphic serialization and concrete subclass selection directly within the inspector.",
            Description = "A developer productivity package extending Unity's [SerializeReference] attribute with an elegant, searchable dropdown interface to instantiate, inspect, and swap concrete subclasses directly inside the Unity Inspector.",
            Challenge = "While Unity supports polymorphic serialization with [SerializeReference], the native inspector offers no UI to instantiate or switch concrete implementations without writing cumbersome custom editor scripts for every polymorphic type.",
            Solution = "Built a generic PropertyDrawer and type-scanning utility that dynamically identifies all assignable subclasses in the project using cached reflection.\nCreated a searchable, categorized dropdown popup allowing developers to instantiate instances with default or cloned state.\nFully integrated with Unity's Undo, Redo, and Prefab override serialization pipelines.",
            Outcome = "Dramatically accelerated game configuration workflows for state machines, ability systems, and AI behavior trees, saving game engineers dozens of hours of custom editor boilerplate.",
            Highlights = "Dynamic reflection scanning with cached type resolution for zero inspector lag\nSearchable dropdown UI supporting deep polymorphic inheritance hierarchies\nFull Unity Undo/Redo and Prefab serialization compatibility",
            TechStack = "Unity, C#, Editor Scripting, Reflection, Polymorphic Serialization",
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
