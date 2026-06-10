using Portfolio.Domain.Entities;

namespace Portfolio.Models;

// Aggregates everything the home/admin views render. Populated by controllers from the facades.
public class HomeViewModel
{
    public Bio Bio { get; set; } = new();
    public List<Service> Services { get; set; } = new();
    public List<Experience> Experiences { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public List<Skill> Skills { get; set; } = new();
    public List<Testimonial> Testimonials { get; set; } = new();
    public int TotalProjects { get; set; }
}
