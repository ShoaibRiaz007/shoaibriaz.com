namespace Portfolio.Application.Abstractions;

/// <summary>Outcome of a successful credential check. The security stamp is embedded in the auth
/// cookie and re-validated per request so password changes invalidate outstanding sessions.</summary>
public record AuthResult(string Username, string SecurityStamp);

/// <summary>Validates admin credentials for sign-in.</summary>
public interface IAuthService
{
    /// <summary>Returns the authenticated user, or null on bad credentials / locked-out account.</summary>
    Task<AuthResult?> ValidateAsync(string username, string password, CancellationToken ct = default);

    /// <summary>True when the cookie's security stamp still matches the stored one for this user.</summary>
    Task<bool> ValidateSecurityStampAsync(string username, string securityStamp, CancellationToken ct = default);
}
