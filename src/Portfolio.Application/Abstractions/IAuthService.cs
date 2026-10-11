namespace Portfolio.Application.Abstractions;

/// <summary>Outcome of a successful credential check. The security stamp is embedded in the auth
/// cookie and re-validated per request so password changes invalidate outstanding sessions.</summary>
public record AuthResult(string Username, string SecurityStamp);

/// <summary>One sign-in check: the user on success; otherwise whether the lockout refused it (the password was not
/// checked). Taken from the check itself, so the login log's "locked" label matches what actually happened.</summary>
public record SignInCheck(AuthResult? User, bool LockedOut);

/// <summary>Validates admin credentials for sign-in.</summary>
public interface IAuthService
{
    /// <summary>Returns the authenticated user, or null on bad credentials / locked-out account.</summary>
    Task<AuthResult?> ValidateAsync(string username, string password, CancellationToken ct = default);

    /// <summary>True when the cookie's security stamp still matches the stored one for this user.</summary>
    Task<bool> ValidateSecurityStampAsync(string username, string securityStamp, CancellationToken ct = default);

    /// <summary>Like <see cref="ValidateAsync"/>, also saying why a sign-in failed (for the login log).</summary>
    Task<SignInCheck> CheckAsync(string username, string password, CancellationToken ct = default);

    /// <summary>The stored name of the admin account a typed sign-in name refers to, matched the way the lockout
    /// matches it (ignoring letter case and surrounding spaces); null when it is no account. The login log records
    /// this, never the typed text, which is often a password typed into the wrong box.</summary>
    Task<string?> FindAccountNameAsync(string username, CancellationToken ct = default);
}
