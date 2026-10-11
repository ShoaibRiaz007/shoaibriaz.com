using Portfolio.Application.Abstractions;

namespace Portfolio.Hosting;

/// <summary>Once a day, deletes page views and login attempts older than <c>Telemetry:RetentionDays</c> (default 180).</summary>
public class TelemetryRetentionService : BackgroundService
{
    public const int DefaultRetentionDays = 180;

    private readonly IServiceProvider _services;
    private readonly TimeProvider _clock;
    private readonly ILogger<TelemetryRetentionService> _logger;
    private readonly int _retentionDays;

    public TelemetryRetentionService(IServiceProvider services, IConfiguration config, TimeProvider clock, ILogger<TelemetryRetentionService> logger)
    {
        _services = services; _clock = clock; _logger = logger;
        _retentionDays = RetentionDays(config);
    }

    /// <summary>The configured retention period in days (at least 1); the privacy page states it.</summary>
    public static int RetentionDays(IConfiguration config) => Math.Max(1, config.GetValue("Telemetry:RetentionDays", DefaultRetentionDays));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), _clock);
        do
        {
            // A failed sweep is retried tomorrow; it must not take the site down (an unhandled exception in a
            // BackgroundService stops the host).
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Telemetry retention sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Deletes everything older than the retention period; returns the number of rows removed.</summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ITelemetryStore>();
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-_retentionDays);
        var removed = await store.DeleteOlderThanAsync(cutoff, ct);
        if (removed > 0) _logger.LogInformation("Telemetry retention removed {Count} rows older than {Days} days.", removed, _retentionDays);
        return removed;
    }
}
