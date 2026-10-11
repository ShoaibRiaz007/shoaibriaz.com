namespace Portfolio.Domain.Entities;

/// <summary>One admin sign-in attempt, kept as security data for the Activity page. The password is never recorded.</summary>
public class LoginAttempt
{
    public int Id { get; set; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>The admin account the attempt was for (matched ignoring case and surrounding spaces); empty when the
    /// typed name was no account or none was sent. The typed text itself is never stored.</summary>
    public string Username { get; set; } = string.Empty;
    public bool Succeeded { get; set; }

    /// <summary>"invalid credentials" | "locked" | "rate limited"; null on success.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Client IP as seen through the platform proxy (unlike page views, login attempts keep it: they are
    /// security data, not visitor analytics).</summary>
    public string? IpAddress { get; set; }

    /// <summary>ISO 3166-1 alpha-2 from the Accept-Language region, when it has one.</summary>
    public string? Country { get; set; }
    public string? UserAgent { get; set; }
}
