using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Net.Http.Headers;
using Portfolio.Application.Models;
using Portfolio.Application.Telemetry;

namespace Portfolio.Telemetry;

/// <summary>JSON body of POST /api/t, as sent by wwwroot/js/telemetry.js and the 3D island.</summary>
public class BeaconPayload
{
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("referrer")] public string? Referrer { get; set; }
    [JsonPropertyName("tz")] public string? TimeZone { get; set; }
    [JsonPropertyName("lang")] public string? Language { get; set; }
    [JsonPropertyName("sw")] public int? ScreenWidth { get; set; }
    [JsonPropertyName("utm")] public BeaconUtm? Utm { get; set; }

    public BeaconInput ToInput()
        => new(Path, Referrer, TimeZone, Language, ScreenWidth, Utm?.Source, Utm?.Medium, Utm?.Campaign);
}

public class BeaconUtm
{
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("medium")] public string? Medium { get; set; }
    [JsonPropertyName("campaign")] public string? Campaign { get; set; }
}

/// <summary>Request-shape rules for the beacon endpoint, kept free of HttpContext so they are unit-testable.</summary>
public static class BeaconRules
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 4 };

    public static bool IsJsonContentType(string? contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out var media)
           && media.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase);

    /// <summary>Same-origin when the browser says so. Either header may be absent (older browsers, some beacon
    /// paths), in which case it doesn't count against the request; when present it must match.</summary>
    public static bool IsSameOrigin(string? origin, string? secFetchSite, string requestHost)
    {
        if (!string.IsNullOrEmpty(secFetchSite) && secFetchSite is not ("same-origin" or "none")) return false;
        if (string.IsNullOrEmpty(origin)) return true;
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
               && uri.Authority.Equals(requestHost, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads at most <see cref="TelemetryLimits.MaxBeaconBytes"/>; null when the body is larger.</summary>
    public static async Task<byte[]?> ReadBoundedAsync(Stream body, CancellationToken ct)
    {
        var buffer = new byte[TelemetryLimits.MaxBeaconBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await body.ReadAsync(buffer.AsMemory(total), ct)) > 0) total += read;
        return total > TelemetryLimits.MaxBeaconBytes ? null : buffer[..total];
    }

    /// <summary>Null for malformed JSON or a non-object body.</summary>
    public static BeaconPayload? Parse(ReadOnlySpan<byte> json)
    {
        try { return JsonSerializer.Deserialize<BeaconPayload>(json, Json); }
        catch (JsonException) { return null; }
    }
}
