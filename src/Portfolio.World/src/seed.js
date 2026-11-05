// High-fidelity fallback dataset for Portfolio.World when running offline or without active API connection.
// Matches the exact shape of DTOs returned by /api/site, /api/experience, and /api/projects.

export const FALLBACK_SITE = {
    profile: {
        fullName: "Shoaib Riaz",
        headline: "Team Lead · C# / .NET · AI/LLM · Office Add-ins & Unity",
        tagline: "I build AI-powered .NET software, from backend APIs to desktop add-ins and real-time Unity clients, and lead the team that ships it.",
        about: [
            "Senior Software Engineer and Team Lead with 7+ years across C#, .NET and Unity.",
            "I started in game development and grew into full-stack .NET work, so I'm comfortable on both sides of the wire: the backend serving the data and the real-time client consuming it.",
            "Multiplayer games talking to ASP.NET Core APIs, AR/VR apps syncing through Firebase, Unity clients driven by backend features.",
            "Today I lead the Lahore engineering team at Lucen Software, building AI-powered Microsoft Office add-ins: .NET 9 desktop apps with React frontends, integrating the Claude API for AI-driven diagram generation inside PowerPoint."
        ],
        location: "Lahore, Punjab, Pakistan",
        yearsExperience: "7+",
        email: "riazshoaib17@gmail.com",
        linkedIn: "https://www.linkedin.com/in/mshoaibriaz",
        gitHub: "https://github.com/ShoaibRiaz007",
        website: "https://shoaibriaz.com",
        resumeUrl: "/cv",
        profileImageUrl: "/images/profile.jpg"
    },
    experience: [
        {
            company: "Lucen Software",
            period: "Jun 2026 – Present",
            roles: [
                {
                    role: "Team Lead",
                    employmentType: "Full-time",
                    location: "Lahore, Pakistan · On-site",
                    period: "Jul 2026 – Present",
                    highlights: [
                        "Lead the Lahore team building the next-generation Office Timeline suite: AI-powered PowerPoint and Excel add-ins coordinated by a shared desktop launcher.",
                        "Own sprint priorities and coordinate releases with the wider engineering organization.",
                        "Run code reviews and technical design discussions, enforcing strict architectural boundaries: presentation-only UI, launcher as coordinator, independent products, strictly shared libraries.",
                        "Consolidated team coding standards into shared CLAUDE.md conventions, one source of truth for engineers and AI agents across the monorepo.",
                        "Introduced a structured bug-investigation workflow (root cause proven before any code change, confidence gate, minimal-fix rule) that turns Jira tickets into agent-ready task briefs.",
                        "Remain hands-on in C#/.NET, WPF and React/TypeScript on AI diagram generation and the React ↔ .NET bridge layer."
                    ]
                },
                {
                    role: "Backend Engineer",
                    employmentType: "Full-time",
                    location: "Lahore, Pakistan · Hybrid",
                    period: "Jun 2026 – Jul 2026",
                    highlights: [
                        "Built LLM-driven diagram and flowchart generation inside PowerPoint using Claude structured outputs, clarifying-question flows and conversational refinement.",
                        "Built evaluation harnesses that measure AI generation quality and gate changes before release.",
                        "Prototyped MCP integration exposing add-in actions to AI assistants.",
                        "Designed JSON-RPC 2.0 over named-pipe IPC between the WinUI 3 launcher and the .NET 9 add-ins.",
                        "Owned launcher install, auto-update and licensing flows, integrated with FastAPI/PostgreSQL entitlement services."
                    ]
                }
            ]
        },
        {
            company: "UserWise",
            period: "Jan 2026 – May 2026",
            roles: [
                {
                    role: "Senior Software Engineer",
                    employmentType: "Contract",
                    location: "Lahore, Pakistan · Hybrid",
                    period: "Jan 2026 – May 2026",
                    highlights: [
                        "Built an AI-assisted level creation pipeline for Fruit Ninja Adventure, letting designers create and polish levels with minimal engineering support.",
                        "Cut level production time 70–80% (5–15 days → 1–5 days).",
                        "Took the system from design to production in 3 months (C#, Unity, backend integration, workflow automation)."
                    ]
                }
            ]
        },
        {
            company: "InvoZone",
            period: "Jul 2022 – May 2026",
            roles: [
                {
                    role: "Senior Game Engineer",
                    employmentType: "Full-time",
                    location: "Remote",
                    period: "Jun 2025 – May 2026",
                    highlights: [
                        "Client-facing technical lead for gamification and game projects, turning business goals into shipped interactive products.",
                        "Led discovery meetings to define goals, KPIs and scope, and translated them into game mechanics and technical plans.",
                        "Gamified business processes (training, onboarding, engagement) with progression, rewards, challenges and leaderboards.",
                        "Built the Unity clients and backend integrations behind these experiences (C#, ASP.NET Core APIs, Firebase, analytics)."
                    ]
                },
                {
                    role: "Software Engineer",
                    employmentType: "Full-time",
                    location: "Lahore, Pakistan · Hybrid",
                    period: "Aug 2023 – Jun 2025",
                    highlights: [
                        "Developed ASP.NET Core backend services, REST APIs and web application features for client projects.",
                        "Worked on SQL Server, Entity Framework Core, authentication, API design and system integrations.",
                        "Improved backend performance, maintainability and reliability using clean code practices."
                    ]
                },
                {
                    role: "Game Developer",
                    employmentType: "Full-time",
                    location: "Lahore, Pakistan · Hybrid",
                    period: "Jul 2022 – Aug 2023",
                    highlights: [
                        "Developed Unity-based gameplay systems, AR/VR experiences and real-time interactive applications.",
                        "Integrated Unity clients with backend APIs, Firebase, analytics, ads and third-party services."
                    ]
                }
            ]
        },
        {
            company: "OZI Technology",
            period: "Jan 2022 – Jul 2022",
            roles: [
                {
                    role: "Game Developer",
                    employmentType: "Full-time",
                    location: "Lahore, Pakistan · On-site",
                    period: "Jan 2022 – Jul 2022",
                    highlights: [
                        "Shipped live Unity game updates with Firebase Auth, Realtime Database, Analytics and AdMob monetization."
                    ]
                }
            ]
        },
        {
            company: "Survival Tech Valley",
            period: "Mar 2020 – Dec 2021",
            roles: [
                {
                    role: "Game Developer",
                    employmentType: "Full-time",
                    location: "Lahore, Pakistan · On-site",
                    period: "Mar 2020 – Dec 2021",
                    highlights: [
                        "Built gameplay features and maintained legacy Unity/C# codebases through iterative releases."
                    ]
                }
            ]
        }
    ],
    projects: [
        {
            slug: "rage-war",
            title: "Rage War",
            summary: "City-building, hero battles, and real-time strategy combined in a fast, action-packed RPG built with Unity.",
            category: "Strategy RPG",
            techStack: ["Unity", "C#", "Backend APIs", "Multiplayer"],
            timeline: "2024",
            imageUrl: "/images/projects/rage-war.jpg",
            projectUrl: "https://invogames.com/our-work",
            gitHubUrl: null,
            metric: "Live Ops & PvP",
            isFeatured: true
        },
        {
            slug: "atomic-golf",
            title: "Atomic Golf",
            summary: "Real-time gaming experience with zero downtime, powered by Unity and Node.js microservices.",
            category: "Sports Game",
            techStack: ["Unity", "Node.js", "REST APIs", "WebSocket"],
            timeline: "2024 – 2025",
            imageUrl: "/images/projects/atomic-golf.jpg",
            projectUrl: "https://invogames.com/our-work",
            gitHubUrl: null,
            metric: "99.99% Uptime",
            isFeatured: true
        },
        {
            slug: "fps-survival-game",
            title: "Multiplayer Survival FPS",
            summary: "A fast-paced multiplayer FPS with low-latency matchmaking and session handling, built on Unreal Engine and AWS EC2.",
            category: "FPS / Multiplayer",
            techStack: ["Unreal Engine", "AWS EC2", "C++", "Multiplayer"],
            timeline: "2024",
            imageUrl: "/images/projects/fps-survival.jpg",
            projectUrl: "https://invogames.com/our-work",
            gitHubUrl: null,
            metric: "<45ms Matchmaking",
            isFeatured: true
        },
        {
            slug: "vr-music-app",
            title: "VR Music Learning App",
            summary: "Learn instruments in VR with hand gestures, real-time feedback, and guided lessons on Apple Vision Pro.",
            category: "AR/VR",
            techStack: ["Unity", "Apple Vision Pro", "XR Interaction Toolkit", "LMS"],
            timeline: "2025",
            imageUrl: "/images/projects/vr-music.jpg",
            projectUrl: "https://invogames.com/our-work",
            gitHubUrl: null,
            metric: "VisionOS Gestures",
            isFeatured: true
        },
        {
            slug: "tire-burners",
            title: "Tire Burners",
            summary: "Custom track building, multiplayer racing, NOS stunts, and a full progression system in one high-speed PC racing game.",
            category: "Racing",
            techStack: ["Unity", "C#", "Multiplayer", "Physics"],
            timeline: "2023 – 2024",
            imageUrl: "/images/projects/tire-burners.jpg",
            projectUrl: "https://invogames.com/our-work",
            gitHubUrl: null,
            metric: "60 FPS Physics",
            isFeatured: true
        }
    ]
};
