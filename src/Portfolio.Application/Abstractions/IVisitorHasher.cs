namespace Portfolio.Application.Abstractions;

/// <summary>Turns (client IP, User-Agent) into an opaque visitor id that is stable for one UTC day only.
/// Implementations must never persist the salt or the inputs, so the IP cannot be recovered from stored rows.</summary>
public interface IVisitorHasher
{
    string Hash(string? clientIp, string? userAgent);
}
