namespace Portfolio.Application.Models;

/// <summary>Reorderable / publishable content lists, used by the admin's generic reorder and publish endpoints.</summary>
public enum ContentKind
{
    Projects,
    Experience,
    Services,
    Skills,
    Testimonials,
    Education,
    Certifications,
}
