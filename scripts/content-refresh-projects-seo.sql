-- Refresh project titles, summaries, descriptions, highlights, metrics, tech stack,
-- and case study sections (Challenge, Solution, Outcome) for SEO, AI, and human readability.
-- Run in psql or the Neon SQL Editor:
-- psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/content-refresh-projects-seo.sql

BEGIN;

INSERT INTO "Projects" ("Slug", "Title", "Category", "Summary", "Description", "Highlights", "Metric", "TechStack", "Timeline", "MyRole", "ProjectUrl", "GitHubUrl", "ImageUrl", "Challenge", "Solution", "Outcome", "IsFeatured", "SortOrder", "IsPublished")
VALUES
('ai-diagram-generation-powerpoint',
 'AI Diagram Generation for PowerPoint (.NET 9 & Claude API)',
 'AI / LLM',
 'Enterprise PowerPoint AI add-in generating editable process diagrams and flowcharts from plain English using Claude structured outputs and .NET 9 WPF.',
 'AI-powered flowchart and process diagram generation for the next-generation Office Timeline suite. Users describe complex workflows in plain language and receive native, fully editable PowerPoint shapes and connectors, refined conversationally through clarifying questions and interactive previews.',
 'Engineered Claude API structured outputs with JSON schema for deterministic flowchart generation
Built interactive clarifying-question dialogs for conversational refinement
Designed React + TypeScript WebView2 bridge to C# WPF and Office COM APIs
Created automated AI evaluation harnesses that benchmark generation quality and gate releases
Prototyped MCP (Model Context Protocol) integration exposing add-in actions to AI agents',
 'Claude-powered diagrams inside PowerPoint',
 '.NET 9, C#, WPF, React, TypeScript, WebView2, Claude API, JSON Schema, OpenXML, MCP',
 'Jun 2026 – Present',
 'Backend Engineer, then Team Lead, at Lucen Software',
 '', '', '',
 'LLM hallucinations frequently distort topological graph connections, connector routing, and shape hierarchies when generating diagrams from unstructured text.
Enterprise latency requirements and strict Microsoft Office OpenXML/COM shape constraints required diagrams to be 100% syntactically and geometrically valid on every run.
Evaluating non-deterministic generative models required automated testing before shipping updates to enterprise users.',
 'Engineered an end-to-end diagram generation pipeline using Claude API structured outputs enforced by rigid JSON schemas defining nodes, directed edges, and branch layouts.
Built multi-turn clarifying question dialogs that detect ambiguous process descriptions and confirm logic with the user before rendering.
Designed a high-performance React and TypeScript UI inside WebView2 bridged to a C# .NET 9 WPF backend and COM interop layer.
Developed automated CI evaluation harnesses that benchmark model output quality against canonical diagram test suites before release.
Prototyped MCP (Model Context Protocol) endpoints exposing PowerPoint shape manipulation and timeline actions to AI assistant clients.',
 'Allowed enterprise users to generate polished, executive-ready diagrams in seconds instead of hours of manual layout.
Achieved deterministic shape generation with zero geometry syntax failures through JSON Schema validation.
Shipped as a flagship AI feature for the next-generation Office Timeline add-in suite.',
 TRUE, 1, TRUE),

('desktop-launcher-ipc-platform',
 'WinUI 3 Desktop Launcher & Office Add-in IPC Platform',
 '.NET Desktop',
 'Centralized WinUI 3 desktop platform orchestrating independent .NET 9 PowerPoint and Excel add-ins via JSON-RPC 2.0 named pipes IPC and FastAPI licensing.',
 'A modular Windows desktop coordinator and launcher that manages background installation, atomic auto-updates, offline-capable licensing, and inter-process communication (IPC) across independent Microsoft PowerPoint and Excel add-ins.',
 'Architected bidirectional JSON-RPC 2.0 over named-pipe IPC between WinUI 3 and .NET 9 add-ins
Engineered process crash isolation, heartbeat ping monitoring, and automatic pipe reconnection
Integrated cloud entitlement, licensing, and auto-update synchronization with FastAPI and PostgreSQL
Built typed messaging bridge connecting React/TypeScript WebView2 UI with native C# COM controllers',
 'JSON-RPC 2.0 over named pipes',
 '.NET 9, WinUI 3, C#, WPF, Named Pipes IPC, JSON-RPC 2.0, WebView2, FastAPI, PostgreSQL',
 'Jun 2026 – Present',
 'Backend Engineer, then Team Lead, at Lucen Software',
 '', '', '',
 'Office add-ins run inside host Microsoft PowerPoint and Excel processes via COM. Heavy background operations, licensing checks, or unhandled exceptions inside the add-in risk crashing the host Office application.
Coordinating multiple standalone add-in instances required sub-millisecond, secure inter-process communication without TCP port collisions, firewall prompts, or elevated administrative privileges.',
 'Architected a bidirectional IPC communication subsystem based on named pipes and JSON-RPC 2.0, enabling isolated, low-latency command routing between the WinUI 3 launcher and running add-in instances.
Implemented heartbeat ping health checks and graceful stream recovery to automatically handle process crashes and reconnect severed pipes.
Integrated cloud entitlement and auto-update verification backed by a FastAPI and PostgreSQL service with cryptographically signed local license caching.
Connected the React/TypeScript presentation layer to C# COM services via a typed WebView2 messaging bridge.',
 'Eliminated Office application freezes and memory bloat by offloading licensing and lifecycle coordination to an isolated desktop launcher process.
Delivered zero-latency IPC messaging with 100% crash recovery and seamless background auto-updating across thousands of enterprise users.',
 TRUE, 2, TRUE),

('fruit-ninja-adventure-ai-level-pipeline',
 'Fruit Ninja Adventure — AI Level Creation Pipeline',
 'AI / Tooling',
 'Automated Unity level authoring pipeline that reduced level production turnaround from 15 days to 1–5 days through procedural curve balancing and designer tools in C#.',
 'An AI-assisted level authoring and curve generation pipeline for Fruit Ninja Adventure that empowered level designers to author, balance, and stage complete gameplay levels with minimal engineering intervention.',
 'Reduced level production turnaround by 70–80%, cutting creation time from 5–15 days to 1–5 days
Engineered algorithmic curve-balancing for fruit spawn rhythms, slice arcs, and obstacle density
Created custom Unity Editor inspectors and visual timeline scrubbers for live design iteration
Delivered complete pipeline from design to production integration in under 3 months',
 '70–80% faster level production',
 'Unity, C#, Editor Scripting, Procedural Algorithms, Backend Integration, Workflow Automation',
 'Jan 2026 – May 2026',
 'Senior Software Engineer (contract) at UserWise',
 '', '', '',
 'Level production was an engineering bottleneck: hand-crafting fruit spawn trajectories, bomb frequency curves, velocity variance, and difficulty pacing required 5 to 15 days of manual iteration per level.
Difficulty spikes frequently compromised retention, and testing new game modes required repetitive code modifications.',
 'Developed custom Unity Editor tooling in C# with algorithmic curve generation that automatically maps difficulty targets into balanced spawn rhythms, slice trajectories, and obstacle combinations.
Built an interactive visual timeline and inspector interface allowing designers to preview, scrub, and customize procedural segments in real time.
Integrated automated validation rules that detect unsolvable fruit clusters and timing deadlocks before levels reach QA, exporting clean validated JSON configs to the backend.',
 'Reduced end-to-end level creation turnaround by 70–80% (from 5–15 days down to 1–5 days per level).
Fully deployed the entire pipeline from design concept to production in 3 months, completely removing engineering support bottlenecks from the content team.',
 TRUE, 3, TRUE),

('winnder-real-money-gaming-platform',
 'WINNDER — Real-Money Competitive Gaming Platform',
 'Unity / Games',
 'Real-money competitive skill gaming platform built in Unity and C#, featuring deterministic gameplay, Firebase synchronization, and ASP.NET Core APIs.',
 'Production Unity mobile titles and client-server backend infrastructure for the WINNDER / Gamersfy competitive gaming platform, powering cash-prize skill matches with real-time matchmaking, secure scoring, and anti-cheat validation.',
 'Engineered server-authoritative client gameplay and input validation in Unity and C#
Integrated Firebase Auth, Realtime Database, and Cloud Functions for instant skill-based matchmaking
Implemented anti-tamper telemetry and replay score verification for financial dispute prevention
Maintained rock-solid 60 FPS performance across iOS and diverse Android device tiers',
 'Real-time competitive mobile multiplayer',
 'Unity, C#, ASP.NET Core, Firebase, WebSockets, iOS, Android, REST APIs',
 '2023 – 2026',
 '',
 'https://winnder.com', '', '',
 'Real-money competitive gaming requires absolute determinism and zero tolerance for client-side score tampering or connection exploits.
Mobile cellular networks introduce frequent latency spikes and dropped packets that cannot be allowed to desynchronize match states or cause unfair disqualifications.',
 'Engineered deterministic Unity gameplay in C# with server-authoritative score auditing over ASP.NET Core REST APIs and WebSockets.
Implemented real-time matchmaking and match room state synchronization using Firebase Realtime Database and Cloud Functions.
Added client input telemetry replay heuristics to detect memory manipulation, speed hacks, and impossible action sequences before awarding prize payouts.
Optimized garbage collection and shader warm-up to sustain 60 FPS across mid-range Android devices and iPhones.',
 'Maintained a reliable, cheat-free competitive platform with zero unresolved match disputes from technical desyncs.
Delivered smooth 60 FPS gameplay on iOS and Android under live tournament concurrency, receiving commendations from executive leadership for delivery reliability.',
 TRUE, 4, TRUE),

('ar-vr-metaverse-experiences',
 'AR / VR & Metaverse Interactive Experiences',
 'Unity / XR',
 'Virtual reality simulators, metaverse worlds, and automotive digital twins built in Unity with OpenXR, physical hardware controllers, and SignalR multiplayer.',
 'A suite of production XR applications and metaverse environments, including physical VR bicycle and automotive driving simulators, interactive digital twins, and hand-gesture interaction experiences for enterprise and entertainment.',
 'Built custom vehicle kinematics and input drivers for physical VR bike and car simulators
Implemented multiplayer avatar synchronization, dead reckoning, and spatial audio via SignalR
Optimized rendering pipelines for consistent 90 FPS on standalone Meta Quest hardware',
 '90 FPS sub-20ms latency VR simulation',
 'Unity, C#, OpenXR, Meta Quest SDK, SignalR, Physics Simulation, Spatial Audio',
 '2022 – 2026',
 '', '', '', '',
 'Achieving rock-solid 90+ FPS and under 20ms motion-to-photon latency on standalone VR headsets (Meta Quest) to prevent simulator sickness, while simultaneously interfacing with external physical simulator hardware and syncing multiplayer physics.',
 'Engineered custom vehicle kinematics and physics drivers in Unity C# interfacing with physical ergometers and steering inputs.
Applied aggressive XR graphics optimizations: Single-Pass Instanced rendering, fixed foveated rendering, and GPU instancing.
Implemented low-latency multiplayer avatar synchronization and spatial audio over SignalR and WebSockets.',
 'Delivered sickness-free, immersive VR experiences operating at a steady 90 FPS on standalone hardware.
Successfully deployed commercial installations integrating physical simulator equipment with real-time virtual environments.',
 FALSE, 5, TRUE),

('ads-package-for-unity',
 'Waterfall Ads Mediation Package for Unity',
 'Unity / Packages',
 'Modular, thread-safe Unity monetization SDK supporting multi-network waterfall mediation with Firebase Remote Config and analytics.',
 'A lightweight, decoupled Unity monetization package that abstracts multiple ad networks (Google AdMob, Unity Ads, AppLovin) into a unified, event-driven C# facade with dynamic cloud mediation rules.',
 'Built unified waterfall mediation abstraction over Google AdMob and major ad networks
Enabled dynamic mediation waterfall tuning via Firebase Remote Config without app updates
Engineered failover chains and thread isolation preventing game loop frame drops',
 'Zero-overhead multi-network ad mediation',
 'Unity, C#, Google AdMob, Firebase Remote Config, Native Android/iOS Bridges',
 'Sep 2023 – Present',
 '', '', '', '',
 'Monetization SDKs frequently introduce main-thread stutters, fail to handle orientation transitions safely, and require rebuilding and re-submitting games to app stores just to re-order ad networks or change impression timeouts.',
 'Architected a thread-safe C# mediation layer with non-blocking async callbacks that isolates third-party native SDK calls from the Unity game loop.
Integrated Firebase Remote Config to dynamically update waterfall network priorities, fallback timeouts, and rewarded video cooldowns on the fly.
Added automated failover chains ensuring an ad always loads or fails gracefully without interrupting gameplay.',
 'Adopted across live commercial mobile games, increasing ad fill rates by 25% and maximizing eCPMs without requiring client app updates.
Cut new project ad integration time from days to minutes.',
 FALSE, 6, TRUE),

('observable-properties-system-for-unity',
 'Thread-Safe Observable Properties System for Unity',
 'Unity / Packages',
 'High-performance, GC-friendly reactive state management and property binding library for Unity C# architectures.',
 'An open-source reactive data-binding system for Unity that eliminates expensive polling in Update() loops, cleanly decouples UI from gameplay state, and prevents memory leaks through lifecycle-aware subscriptions.',
 'Zero-allocation event dispatch for value types preventing garbage collection spikes
Automated unsubscription tied to Unity GameObject destruction preventing memory leaks
Custom Unity Inspector drawers for live runtime inspection and state debugging',
 'Zero-allocation reactive state management',
 'Unity, C#, Reactive Programming, Memory Optimization, Custom Inspectors',
 '', '', '',
 'https://github.com/ShoaibRiaz007/Observable-Properties-System-for-Unity', '',
 'Game development architectures often degrade into tightly coupled systems where UI scripts poll gameplay state every frame inside Update(), wasting CPU cycles and causing fragile dependencies.
Native C# events are prone to memory leaks when event listeners are not manually detached upon GameObject destruction.',
 'Engineered a generic ObservableProperty<T> system with automated subscription management tied directly to Unity MonoBehaviour lifecycles.
Implemented zero-allocation value comparisons and event dispatching for primitive and struct types to avoid garbage collection spikes.
Developed custom Unity Editor PropertyDrawers for real-time value inspection and live debugging in the inspector.',
 'Open-sourced on GitHub with comprehensive test coverage.
Eliminated UI polling overhead across game components, improving frame budget predictability and architectural modularity.',
 FALSE, 7, TRUE),

('unity-editor-subclass-selector',
 'Subclass Selector Attribute for Unity Inspector',
 'Unity / Packages',
 'Unity editor extension enabling intuitive polymorphic serialization and concrete subclass selection directly within the inspector.',
 'A developer productivity package extending Unity''s [SerializeReference] attribute with an elegant, searchable dropdown interface to instantiate, inspect, and swap concrete subclasses directly inside the Unity Inspector.',
 'Dynamic reflection scanning with cached type resolution for zero inspector lag
Searchable dropdown UI supporting deep polymorphic inheritance hierarchies
Full Unity Undo/Redo and Prefab serialization compatibility',
 'Polymorphic serialization in Unity Editor',
 'Unity, C#, Editor Scripting, Reflection, Polymorphic Serialization',
 '', '', '', '', '',
 'While Unity supports polymorphic serialization with [SerializeReference], the native inspector offers no UI to instantiate or switch concrete implementations without writing cumbersome custom editor scripts for every polymorphic type.',
 'Built a generic PropertyDrawer and type-scanning utility that dynamically identifies all assignable subclasses in the project using cached reflection.
Created a searchable, categorized dropdown popup allowing developers to instantiate instances with default or cloned state.
Fully integrated with Unity''s Undo, Redo, and Prefab override serialization pipelines.',
 'Dramatically accelerated game configuration workflows for state machines, ability systems, and AI behavior trees, saving game engineers dozens of hours of custom editor boilerplate.',
 FALSE, 8, TRUE)

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
    "Challenge" = EXCLUDED."Challenge",
    "Solution" = EXCLUDED."Solution",
    "Outcome" = EXCLUDED."Outcome",
    "IsFeatured" = EXCLUDED."IsFeatured",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsPublished" = TRUE;

COMMIT;
