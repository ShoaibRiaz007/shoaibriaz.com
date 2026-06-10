namespace Portfolio.Domain.Entities;

/// <summary>Admin account used to log in and edit site content. Password is stored as a salted hash.</summary>
public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Rotated on password change; embedded in the auth cookie and re-validated per request,
    /// so changing the password invalidates every outstanding session.</summary>
    public string SecurityStamp { get; set; } = string.Empty;
}
