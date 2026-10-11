using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;

namespace Portfolio.Models;

// Aggregates everything the home/admin views render. Populated by controllers from the facades.
public class HomeViewModel : IPublicPage
{
    public Bio Bio { get; set; } = new();
    public List<Service> Services { get; set; } = new();
    /// <summary>Experiences grouped by company (promotions), in display order. Public home page only.</summary>
    public IReadOnlyList<CompanyGroup> ExperienceGroups { get; set; } = Array.Empty<CompanyGroup>();
    public List<Project> Projects { get; set; } = new();
    public List<Skill> Skills { get; set; } = new();
    public List<Testimonial> Testimonials { get; set; } = new();
    public List<Education> Educations { get; set; } = new();
    public List<Certification> Certifications { get; set; } = new();
    public int TotalProjects { get; set; }
    public PageMeta Meta { get; set; } = new("", "", "/");
    public PersonFacts? Person { get; set; }
}
