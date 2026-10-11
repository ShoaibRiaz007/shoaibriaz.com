using System.Globalization;
using Portfolio.Application.Models;
using Portfolio.Domain.Abstractions;

namespace Portfolio.Models;

/// <summary>Static description of one admin content section: what it's called and which actions edit it.</summary>
public record AdminSection(ContentKind Kind, string Title, string Singular, string Icon, string EditAction, string DeleteAction)
{
    /// <summary>Lowercase kind, used in /Admin/List/{slug} and as the list's element id.</summary>
    public string Slug => Kind.ToString().ToLowerInvariant();
}

/// <summary>Live/hidden counts for one section (dashboard cards and sidebar badges).</summary>
public record AdminSectionStat(AdminSection Section, int Live, int Hidden)
{
    public int Total => Live + Hidden;
}

/// <summary>One profile-completeness check. <see cref="FixAction"/>/<see cref="FixRoute"/>/<see cref="FixFragment"/>
/// point at the page that fixes it.</summary>
public record ChecklistItem(string Label, string Detail, bool Done, string FixAction,
    IDictionary<string, string>? FixRoute = null, string? FixFragment = null);

public class AdminDashboardViewModel
{
    public required Domain.Entities.Bio Bio { get; init; }
    public required IReadOnlyList<AdminSectionStat> Stats { get; init; }
    public required IReadOnlyList<ChecklistItem> Checklist { get; init; }
    public int CompletedChecks => Checklist.Count(c => c.Done);
}

/// <summary>Sidebar model: the section counts plus which entry is current.</summary>
public record AdminNavViewModel(IReadOnlyList<AdminSectionStat> Stats, string? CurrentSlug, string? CurrentAction);

/// <summary>Builds the admin's dashboard, list and sidebar models from one <see cref="AdminDashboard"/> snapshot.</summary>
public static class AdminOverview
{
    /// <summary>Home page highlights need at least this many live featured projects, otherwise it falls back
    /// to the first projects in list order (PublicContentFacade.GetHomeAsync).</summary>
    public const int MinFeaturedProjects = Application.Facades.PublicContentFacade.MinFeaturedProjects;
    public const int HomeFeaturedProjects = Application.Facades.PublicContentFacade.HomeFeaturedProjects;

    public static readonly IReadOnlyList<AdminSection> Sections = new[]
    {
        new AdminSection(ContentKind.Projects, "Projects", "project", "folder", "EditProject", "DeleteProject"),
        new AdminSection(ContentKind.Experience, "Experience", "role", "briefcase", "EditExperience", "DeleteExperience"),
        new AdminSection(ContentKind.Education, "Education", "education", "graduation", "EditEducation", "DeleteEducation"),
        new AdminSection(ContentKind.Certifications, "Certifications", "certification", "award", "EditCertification", "DeleteCertification"),
        new AdminSection(ContentKind.Skills, "Skills", "skill", "wrench", "EditSkill", "DeleteSkill"),
        new AdminSection(ContentKind.Services, "Services", "service", "layers", "EditService", "DeleteService"),
        new AdminSection(ContentKind.Testimonials, "Testimonials", "testimonial", "quote", "EditTestimonial", "DeleteTestimonial"),
    };

    public static AdminSection Section(ContentKind kind) => Sections.First(s => s.Kind == kind);

    public static IReadOnlyList<AdminSectionStat> Stats(AdminDashboard d) => Sections
        .Select(s =>
        {
            var rows = Rows(d, s.Kind);
            var live = rows.Count(r => r.IsPublished);
            return new AdminSectionStat(s, live, rows.Count - live);
        })
        .ToList();

    /// <summary>The same cards from count-only queries (IAdminContentFacade.GetContentCountsAsync).</summary>
    public static IReadOnlyList<AdminSectionStat> Stats(IReadOnlyList<ContentCount> counts) => Sections
        .Select(s => counts.FirstOrDefault(c => c.Kind == s.Kind) is { } c
            ? new AdminSectionStat(s, c.Live, c.Hidden)
            : new AdminSectionStat(s, 0, 0))
        .ToList();

    public static AdminDashboardViewModel Dashboard(AdminDashboard d) => new()
    {
        Bio = d.Bio,
        Stats = Stats(d),
        Checklist = Checklist(d),
    };

    private static IReadOnlyList<IOrderedContent> Rows(AdminDashboard d, ContentKind kind) => kind switch
    {
        ContentKind.Projects => d.Projects.ToList<IOrderedContent>(),
        ContentKind.Experience => d.Experiences.ToList<IOrderedContent>(),
        ContentKind.Education => d.Educations.ToList<IOrderedContent>(),
        ContentKind.Certifications => d.Certifications.ToList<IOrderedContent>(),
        ContentKind.Skills => d.Skills.ToList<IOrderedContent>(),
        ContentKind.Services => d.Services.ToList<IOrderedContent>(),
        ContentKind.Testimonials => d.Testimonials.ToList<IOrderedContent>(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static IReadOnlyList<ChecklistItem> Checklist(AdminDashboard d)
    {
        static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";
        var bio = d.Bio;
        var liveProjects = d.Projects.Where(p => p.IsPublished).ToList();
        var featured = liveProjects.Count(p => p.IsFeatured);
        var noImage = liveProjects.Where(p => string.IsNullOrWhiteSpace(p.ImageUrl)).ToList();
        var noMetric = liveProjects.Where(p => string.IsNullOrWhiteSpace(p.Metric)).ToList();
        var projectsRoute = new Dictionary<string, string> { ["kind"] = "projects" };
        // A single offender links straight to its edit form; several link to the list.
        ChecklistItem ProjectCheck(string label, IReadOnlyList<Domain.Entities.Project> offenders, string what) =>
            offenders.Count switch
            {
                0 => new(label, "Every live project has one.", true, "List", projectsRoute),
                1 => new(label, $"“{offenders[0].Title}” has no {what}.", false, "EditProject",
                    new Dictionary<string, string> { ["id"] = offenders[0].Id.ToString() }),
                _ => new(label, $"{Plural(offenders.Count, "live project")} without a {what}.", false, "List", projectsRoute),
            };

        return new List<ChecklistItem>
        {
            new("Profile photo", string.IsNullOrWhiteSpace(bio.ProfileImageUrl) ? "Upload a square photo for the hero and CV." : "Uploaded.",
                !string.IsNullOrWhiteSpace(bio.ProfileImageUrl), "EditBio", null, "files"),
            new("CV (PDF)", string.IsNullOrWhiteSpace(bio.ResumeUrl) ? "The site's Download CV buttons have nothing to serve." : "Uploaded.",
                !string.IsNullOrWhiteSpace(bio.ResumeUrl), "EditBio", null, "files"),
            new("Headline & about", string.IsNullOrWhiteSpace(bio.Headline) || string.IsNullOrWhiteSpace(bio.About)
                    ? "Add a headline and an about section." : "Written.",
                !string.IsNullOrWhiteSpace(bio.Headline) && !string.IsNullOrWhiteSpace(bio.About), "EditBio"),
            new("Contact details", string.IsNullOrWhiteSpace(bio.Email) || string.IsNullOrWhiteSpace(bio.LinkedIn)
                    ? "Add an email address and a LinkedIn profile." : "Email and LinkedIn set.",
                !string.IsNullOrWhiteSpace(bio.Email) && !string.IsNullOrWhiteSpace(bio.LinkedIn), "EditBio"),
            new("Featured projects", featured >= MinFeaturedProjects
                    ? featured > HomeFeaturedProjects
                        ? $"{Plural(featured, "live featured project")}; the home page shows the first {HomeFeaturedProjects} in list order."
                        : $"{Plural(featured, "live featured project")} on the home page."
                    : $"{featured} of {MinFeaturedProjects} needed. Below that the home page shows the first projects in list order.",
                featured >= MinFeaturedProjects, "List", projectsRoute),
            ProjectCheck("Project cover images", noImage, "cover image"),
            ProjectCheck("Project headline metrics", noMetric, "headline metric"),
            new("Testimonials", d.Testimonials.Any(t => t.IsPublished) ? "At least one is live." : "None live yet. One good quote goes a long way.",
                d.Testimonials.Any(t => t.IsPublished), "List", new Dictionary<string, string> { ["kind"] = "testimonials" }),
        };
    }

    /// <summary>Range of the views column on the projects list.</summary>
    public const int ProjectViewsDays = 30;

    /// <summary>"42 · 17 visitors", or "0" for a project nobody opened in the range.</summary>
    public static string ViewsCell(IReadOnlyDictionary<string, ProjectViews> views, string slug)
        => views.TryGetValue((slug ?? "").ToLowerInvariant(), out var v)
            ? $"{ActivityViewModel.Number(v.Views)} · {ActivityViewModel.Number(v.Visitors)} visitor{(v.Visitors == 1 ? "" : "s")}"
            : "0";

    public static ContentListViewModel List(AdminDashboard d, ContentKind kind, IReadOnlyDictionary<string, ProjectViews>? projectViews = null)
    {
        var section = Section(kind);
        var (columns, rows) = kind switch
        {
            ContentKind.Projects when projectViews is not null => (new[] { "Category", "Featured", $"Views ({ProjectViewsDays} days)" },
                d.Projects.Select(p => new ContentRow(p.Id, p.Title, p.Metric, p.IsPublished,
                    new[] { p.Category, p.IsFeatured ? "Featured" : "", ViewsCell(projectViews, p.Slug) },
                    SearchedCells: 2)).ToList()),   // all but the trailing views count: numbers would match any digit typed
            ContentKind.Projects => (new[] { "Category", "Featured" },
                d.Projects.Select(p => new ContentRow(p.Id, p.Title, p.Metric, p.IsPublished,
                    new[] { p.Category, p.IsFeatured ? "Featured" : "" })).ToList()),
            ContentKind.Experience => (new[] { "Company", "Period" },
                d.Experiences.Select(e => new ContentRow(e.Id, e.Role, e.EmploymentType, e.IsPublished,
                    new[] { e.Company, e.Period })).ToList()),
            ContentKind.Education => (new[] { "Field", "Years" },
                d.Educations.Select(e => new ContentRow(e.Id, e.Institution, e.Degree, e.IsPublished,
                    new[] { e.FieldOfStudy, e.StartYear is null && e.EndYear is null ? "" : $"{e.StartYear} – {e.EndYear}" })).ToList()),
            ContentKind.Certifications => (new[] { "Issuer", "Issued" },
                d.Certifications.Select(c => new ContentRow(c.Id, c.Name, c.CredentialId, c.IsPublished,
                    new[] { c.Issuer, c.IssuedOn?.ToString("MMM yyyy", CultureInfo.InvariantCulture) ?? "" })).ToList()),
            ContentKind.Skills => (new[] { "Category", "Level" },
                d.Skills.Select(s => new ContentRow(s.Id, s.Name, "", s.IsPublished,
                    new[] { s.Category, $"{s.Level}%" })).ToList()),
            ContentKind.Services => (new[] { "Description" },
                d.Services.Select(s => new ContentRow(s.Id, $"{s.Icon} {s.Title}".Trim(), "", s.IsPublished,
                    new[] { PublicSite.Truncate(s.Description, 80) })).ToList()),
            ContentKind.Testimonials => (new[] { "Quote" },
                d.Testimonials.Select(t => new ContentRow(t.Id, t.Author, t.AuthorTitle, t.IsPublished,
                    new[] { PublicSite.Truncate(t.Quote, 70) })).ToList()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return new ContentListViewModel
        {
            Kind = kind,
            Title = section.Title,
            Icon = section.Icon,
            EditAction = section.EditAction,
            DeleteAction = section.DeleteAction,
            AddLabel = $"Add {section.Singular}",
            Columns = columns,
            Rows = rows,
        };
    }
}

/// <summary>Breadcrumb + title block at the top of an edit form (_EditHeader).</summary>
public record EditPageHeader(string Section, string SectionUrl, string Title, string? Lede = null);

/// <summary>The sticky Save / Cancel bar at the bottom of an edit form (_SaveBar).</summary>
public record SaveBarModel(string CancelUrl, string SaveLabel);
