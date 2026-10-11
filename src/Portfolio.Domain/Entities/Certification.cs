using Portfolio.Domain.Abstractions;

namespace Portfolio.Domain.Entities;

/// <summary>A license or certification, optionally linking to its public verification page.</summary>
public class Certification : IOrderedContent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateOnly? IssuedOn { get; set; }
    public string CredentialId { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
