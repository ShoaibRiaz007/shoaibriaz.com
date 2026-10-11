namespace Portfolio.Application.Telemetry;

/// <summary>Column sizes for telemetry rows: the facade truncates to these and AppDbContext maps them, so an
/// oversized beacon field is trimmed rather than failing the insert.</summary>
public static class TelemetryLimits
{
    public const int Path = 300;
    public const int ReferrerHost = 200;
    public const int Referrer = 500;
    public const int Source = TrafficSources.MaxSourceLength;
    public const int Utm = 100;
    public const int Country = 2;
    public const int CountryName = 100;
    public const int TimeZone = 64;
    public const int Language = 16;
    public const int Device = 16;
    public const int Browser = 40;
    public const int Os = 40;
    public const int VisitorHash = 64;

    public const int Username = 100;
    public const int FailureReason = 100;
    public const int IpAddress = 64;
    public const int UserAgent = 300;

    /// <summary>Largest beacon body the endpoint accepts, in bytes.</summary>
    public const int MaxBeaconBytes = 2048;
}
