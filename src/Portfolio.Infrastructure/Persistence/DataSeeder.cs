using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Abstractions;
using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;

namespace Portfolio.Infrastructure.Persistence;

/// <summary>Admin bootstrap credentials supplied by configuration (no hardcoded fallback for the password).</summary>
public record AdminSeedOptions(string Username, string? Password);

/// <summary>Applies migrations and seeds initial content + the admin account. Idempotent.</summary>
public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly AdminSeedOptions _admin;

    public DataSeeder(AppDbContext db, IPasswordHasher hasher, AdminSeedOptions admin)
    {
        _db = db; _hasher = hasher; _admin = admin;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);

        // ---- Admin account ----
        if (!await _db.AdminUsers.AnyAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(_admin.Password))
                throw new InvalidOperationException(
                    "No admin account exists and no admin password is configured. " +
                    "Set 'Admin:Password' (user-secrets) or the 'Admin__Password' environment variable before first run.");

            _db.AdminUsers.Add(new AdminUser
            {
                Username = string.IsNullOrWhiteSpace(_admin.Username) ? "admin" : _admin.Username,
                PasswordHash = _hasher.Hash(_admin.Password),
                SecurityStamp = Guid.NewGuid().ToString("N")
            });
        }

        // Backfill stamps for accounts created before security stamps existed.
        foreach (var u in await _db.AdminUsers.Where(u => u.SecurityStamp == "").ToListAsync(ct))
            u.SecurityStamp = Guid.NewGuid().ToString("N");

        if (!await _db.Bios.AnyAsync(ct))
        {
            _db.Bios.Add(new Bio
            {
                FullName = "Shoaib Riaz",
                Headline = "Senior .NET Developer | ASP.NET Core & Unity Systems",
                Tagline = "I build backend APIs, real-time systems, and production-ready Unity experiences — bridging server engineering with interactive, real-time clients.",
                About =
                    "Senior Software Engineer with 6+ years of experience in C#, ASP.NET Core, and Unity-based interactive systems. " +
                    "I specialize in building backend APIs, real-time systems, and production-ready Unity integrations for games, AR/VR applications, and web platforms. " +
                    "My work includes ASP.NET Core Web APIs, SQL Server, Entity Framework Core, SignalR/WebSockets, authentication flows, Firebase integrations, and scalable client-server architecture.\n\n" +
                    "Alongside backend development, I have strong hands-on experience with Unity gameplay systems, multiplayer features, performance optimization, and live production support. " +
                    "This combination helps me bridge the gap between backend engineering and real-time interactive client experiences.\n\n" +
                    "I have worked on products involving multiplayer systems, AR/VR experiences, metaverse applications, backend-driven gameplay features, and internal tools that improved production workflows. " +
                    "One of my recent contributions reduced level production time by 70–80% through an AI-assisted Unity pipeline.",
                Location = "Lahore, Punjab, Pakistan",
                YearsExperience = "6+",
                Email = "riazshoaib17@gmail.com",
                LinkedIn = "https://www.linkedin.com/in/shoaib-riaz-game-dev",
                GitHub = "https://github.com/ShoaibRiaz007",
            });
        }

        if (!await _db.Services.AnyAsync(ct))
        {
            _db.Services.AddRange(
                new Service { Title = "Backend & API Development", Icon = "🧩", SortOrder = 1,
                    Description = "Robust ASP.NET Core Web APIs and REST services with clean architecture, authentication flows, and scalable client-server design." },
                new Service { Title = "Unity Game Development", Icon = "🎮", SortOrder = 2,
                    Description = "Production-ready Unity gameplay systems, mechanics, and live-ops support — built in C# with performance and stability in mind." },
                new Service { Title = "AR / VR & Metaverse", Icon = "🕶️", SortOrder = 3,
                    Description = "Immersive AR/VR experiences and metaverse applications, from prototyping through production deployment." },
                new Service { Title = "Real-Time Systems", Icon = "⚡", SortOrder = 4,
                    Description = "Multiplayer and real-time features powered by SignalR / WebSockets for low-latency, synchronized experiences." },
                new Service { Title = "Database & EF Core", Icon = "🗄️", SortOrder = 5,
                    Description = "Data modeling, SQL Server / PostgreSQL, and Entity Framework Core with a focus on performance and maintainability." },
                new Service { Title = "Backend Integration", Icon = "🔌", SortOrder = 6,
                    Description = "Connecting Unity & web clients with backend APIs, Firebase, analytics, ads, and third-party services." }
            );
        }

        if (!await _db.Experiences.AnyAsync(ct))
        {
            _db.Experiences.AddRange(
                new Experience { Role = "Software Engineer", Company = "Lucen Software", EmploymentType = "Full-time",
                    Location = "Lahore, Punjab, Pakistan · Hybrid", Period = "Jun 2026 - Present", SortOrder = 1,
                    Description = "Building backend services and production software across the .NET stack." },
                new Experience { Role = "Senior Software Engineer", Company = "UserWise", EmploymentType = "Full-time",
                    Location = "Lahore, Punjab, Pakistan · On-site", Period = "Mar 2026 - May 2026", SortOrder = 2,
                    Description =
                        "Built an AI-assisted level creation pipeline for Fruit Ninja Adventure, enabling designers to create and polish levels with less engineering support.\n" +
                        "Reduced level production time by 70–80%, improving delivery from 5–15 days to 1–5 days.\n" +
                        "Worked on C#, Unity, backend integration, workflow automation, and production-ready tooling.\n" +
                        "Delivered and integrated the system into production within 3 months." },
                new Experience { Role = "Senior Software Engineer", Company = "InvoZone", EmploymentType = "Full-time",
                    Location = "Hollywood, Florida, United States · Hybrid", Period = "Nov 2023 - May 2026", SortOrder = 3,
                    Description =
                        "Developed ASP.NET Core backend services, REST APIs, and web application features for client projects.\n" +
                        "Worked on SQL Server, Entity Framework Core, authentication, API design, and system integrations.\n" +
                        "Participated in client meetings to gather requirements and convert business needs into development tasks.\n" +
                        "Improved backend performance, maintainability, and reliability using clean code practices.\n" +
                        "Supported project delivery from planning to deployment." },
                new Experience { Role = "Senior Game Engineer", Company = "InvoGames", EmploymentType = "Full-time",
                    Location = "Pembroke Pines, Florida, United States · Hybrid", Period = "Jul 2022 - May 2026", SortOrder = 4,
                    Description =
                        "Developed Unity-based gameplay systems, AR/VR experiences, and real-time interactive applications.\n" +
                        "Integrated Unity clients with backend APIs, Firebase, analytics, ads, and third-party services.\n" +
                        "Worked on multiplayer, metaverse, and production game features across multiple projects.\n" +
                        "Improved performance, stability, and gameplay quality by debugging complex technical issues.\n" +
                        "Collaborated with design, QA, and backend teams to deliver production-ready features." },
                new Experience { Role = "Game Developer", Company = "OZI Technology", EmploymentType = "Full-time",
                    Location = "Lahore District, Punjab, Pakistan · On-site", Period = "Jan 2022 - Jul 2022", SortOrder = 5,
                    Description =
                        "Developed and optimized Unity game features using C#, with focus on gameplay performance, stability, and production readiness.\n" +
                        "Integrated Unity applications with Firebase Auth, Realtime Database, Analytics, Google Ads, and backend APIs.\n" +
                        "Implemented authentication, data syncing, analytics tracking, and monetization workflows for live game products.\n" +
                        "Shipped feature updates, patches, and live improvements while troubleshooting runtime issues in production builds." }
            );
        }

        if (!await _db.Projects.AnyAsync(ct))
        {
            _db.Projects.AddRange(
                new Project { Title = "WINNDER — Real-Money Gaming Platform", Category = "Game", IsFeatured = true, SortOrder = 1,
                    Summary = "Unity games and backend integration for the WINNDER / Gamersfy real-money gaming platform.",
                    Description = "Delivered production Unity games and client-server integrations for WINNDER. Worked closely with the founding team to translate product needs into polished, high-quality games delivered reliably on deadline.",
                    TechStack = "Unity, C#, Backend APIs, Firebase", Timeline = "2023 – 2026", ProjectUrl = "https://winnder.com" },
                new Project { Title = "Fruit Ninja Adventure — AI Level Pipeline", Category = "Tooling", IsFeatured = true, SortOrder = 2,
                    Summary = "AI-assisted level creation pipeline that cut level production time by 70–80%.",
                    Description = "Built an AI-assisted Unity tooling pipeline enabling designers to create and polish levels with far less engineering support — reducing delivery from 5–15 days down to 1–5 days, integrated into production within 3 months.",
                    TechStack = "Unity, C#, Workflow Automation, Tooling", Timeline = "Mar 2026 – May 2026" },
                new Project { Title = "Ads Package for Unity", Category = "Package", IsFeatured = true, SortOrder = 3,
                    Summary = "Unity ad package supporting multiple advertisers in a waterfall method.",
                    Description = "A reusable Unity ad package that supports multiple advertisers using a waterfall mediation method inside Unity, simplifying monetization integration across projects.",
                    TechStack = ".NET Core, Firebase, Unity, C#", Timeline = "Sep 2023 – Present" },
                new Project { Title = "Observable Properties System for Unity", Category = "Package", IsFeatured = false, SortOrder = 4,
                    Summary = "Thread-safe, centralized observable properties / state management for Unity.",
                    Description = "A robust and extensible Observable Properties System for Unity — a centralized, thread-safe notification system for managing observable properties, enabling efficient updates and communication across components.",
                    TechStack = "Unity, C#", Timeline = "",
                    GitHubUrl = "https://github.com/ShoaibRiaz007/Observable-Properties-System-for-Unity" },
                new Project { Title = "Unity Editor Subclass Selector", Category = "Tooling", IsFeatured = false, SortOrder = 5,
                    Summary = "Editor extension for selecting and serializing subclasses in the Unity inspector.",
                    Description = "A Unity Editor tool that lets developers pick concrete subclasses directly from the inspector, improving serialization workflows and reducing boilerplate.",
                    TechStack = "Unity, C#, Editor Tooling", Timeline = "" },
                new Project { Title = "AR / VR & Metaverse Experiences", Category = "AR/VR", IsFeatured = true, SortOrder = 6,
                    Summary = "Immersive AR/VR and metaverse applications with backend-driven gameplay.",
                    Description = "Designed and built AR/VR experiences and metaverse applications at InvoGames, including a VR bike and car controller, integrating real-time interaction with backend services.",
                    TechStack = "Unity, C#, AR/VR, SignalR", Timeline = "2022 – 2026" }
            );
        }

        if (!await _db.Skills.AnyAsync(ct))
        {
            _db.Skills.AddRange(
                new Skill { Name = "C#", Category = "Backend", Level = 95, SortOrder = 1 },
                new Skill { Name = "ASP.NET Core", Category = "Backend", Level = 92, SortOrder = 2 },
                new Skill { Name = "Web API / REST", Category = "Backend", Level = 92, SortOrder = 3 },
                new Skill { Name = "Entity Framework Core", Category = "Backend", Level = 88, SortOrder = 4 },
                new Skill { Name = "SQL Server / PostgreSQL", Category = "Backend", Level = 85, SortOrder = 5 },
                new Skill { Name = "SignalR / WebSockets", Category = "Backend", Level = 85, SortOrder = 6 },
                new Skill { Name = "Unity", Category = "Game Dev", Level = 93, SortOrder = 7 },
                new Skill { Name = "Multiplayer Systems", Category = "Game Dev", Level = 85, SortOrder = 8 },
                new Skill { Name = "AR / VR", Category = "Game Dev", Level = 82, SortOrder = 9 },
                new Skill { Name = "Firebase", Category = "Integration", Level = 85, SortOrder = 10 },
                new Skill { Name = "Clean Architecture", Category = "Practices", Level = 88, SortOrder = 11 },
                new Skill { Name = "Performance Optimization", Category = "Practices", Level = 87, SortOrder = 12 }
            );
        }

        if (!await _db.Testimonials.AnyAsync(ct))
        {
            _db.Testimonials.Add(new Testimonial
            {
                Author = "Abel Chocano Gómez",
                AuthorTitle = "Co-Founder at Gamersfy / Winnder.com",
                SortOrder = 1,
                Quote =
                    "Working with Shoaib on WINNDER projects has been a very positive experience. He has proven to be a highly skilled professional, " +
                    "with a strong ability to understand product needs and translate them accurately into each project. He is very reliable when it comes to " +
                    "meeting deadlines and has a great ability to interpret what the client is looking for and bring it to life effectively in every delivery. " +
                    "Without a doubt, a well-rounded professional who adds value from day one and truly a pleasure to work with."
            });
        }

        await _db.SaveChangesAsync(ct);

        // Replace the old placeholder contact email on databases seeded before it was fixed.
        foreach (var bio in await _db.Bios.Where(b => b.Email == "shoaib.riaz@example.com").ToListAsync(ct))
            bio.Email = "riazshoaib17@gmail.com";

        // ---- Merge in real InvoGames portfolio projects (idempotent) + backfill slugs ----
        var allProjects = await _db.Projects.ToListAsync(ct);
        foreach (var p in allProjects.Where(p => string.IsNullOrWhiteSpace(p.Slug)))
            p.Slug = SlugGenerator.Generate(p.Title);

        var existingSlugs = allProjects.Select(p => p.Slug).ToHashSet();
        var nextOrder = allProjects.Count == 0 ? 0 : allProjects.Max(p => p.SortOrder);
        foreach (var seed in ProjectSeedData.InvoGamesProjects())
        {
            if (existingSlugs.Contains(seed.Slug)) continue;
            seed.SortOrder = ++nextOrder;
            _db.Projects.Add(seed);
            existingSlugs.Add(seed.Slug);
        }

        await _db.SaveChangesAsync(ct);
    }
}
