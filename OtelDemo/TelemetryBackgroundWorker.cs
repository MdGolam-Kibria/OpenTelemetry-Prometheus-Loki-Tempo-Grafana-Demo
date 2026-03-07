using System.Diagnostics;

namespace OtelDemo.Services;

/// <summary>
/// This class just for test to make sure matrics available in graphana.
/// Runs in the background to simulate real workload:
/// — updates active-user gauge
/// — records periodic request durations
/// — occasionally logs warnings / errors so Loki has interesting data
/// </summary>
public class TelemetryBackgroundWorker : BackgroundService
{
    private readonly OtelDemoMetrics          _metrics;
    private readonly ILogger<TelemetryBackgroundWorker> _logger;

    public TelemetryBackgroundWorker(
        OtelDemoMetrics metrics,
        ILogger<TelemetryBackgroundWorker> logger)
    {
        _metrics = metrics;
        _logger  = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelemetryBackgroundWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SimulateWorkloadAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _metrics.ErrorsTotal.Add(1, new TagList { { "source", "background_worker" } });
                _logger.LogError(ex, "Unexpected error in TelemetryBackgroundWorker");
            }
        }

        _logger.LogInformation("TelemetryBackgroundWorker stopped");
    }

    private async Task SimulateWorkloadAsync(CancellationToken ct)
    {
        // ── Update active users gauge ────────────────────────────────────────
        var activeUsers = Random.Shared.Next(10, 200);
        _metrics.SetActiveUsers(activeUsers);

        // ── Simulate batch of background requests ────────────────────────────
        var batchSize = Random.Shared.Next(3, 10);

        using var batchActivity = OtelDemoMetrics.ActivitySource.StartActivity(
            "BackgroundWorker.SimulatedBatch", ActivityKind.Internal);
        batchActivity?.SetTag("batch.size", batchSize);

        for (int i = 0; i < batchSize; i++)
        {
            using var itemActivity = OtelDemoMetrics.ActivitySource.StartActivity(
                "BackgroundWorker.SimulatedRequest", ActivityKind.Internal);

            var endpoint   = GetRandomEndpoint();
            var statusCode = GetRandomStatusCode();
            var durationMs = GetRandomDuration(endpoint);

            itemActivity?.SetTag("http.method",      "GET");
            itemActivity?.SetTag("http.route",        endpoint);
            itemActivity?.SetTag("http.status_code",  statusCode);

            await Task.Delay(durationMs, ct);

            _metrics.HttpRequestsTotal.Add(1, new TagList
            {
                { "endpoint",    endpoint },
                { "status_code", statusCode },
                { "method",      "GET" },
            });

            _metrics.RequestDurationMs.Record(durationMs, new TagList
            {
                { "endpoint",    endpoint },
                { "status_code", statusCode },
            });

            // ── Log at different levels based on status ───────────────────────
            if (statusCode >= 500)
            {
                _metrics.ErrorsTotal.Add(1, new TagList { { "source", endpoint } });
                itemActivity?.SetStatus(ActivityStatusCode.Error, $"HTTP {statusCode}");

                _logger.LogError(
                    "Simulated server error on {Endpoint} — Status {StatusCode}, Duration {DurationMs}ms",
                    endpoint, statusCode, durationMs);
            }
            else if (statusCode >= 400)
            {
                _logger.LogWarning(
                    "Simulated client error on {Endpoint} — Status {StatusCode}, Duration {DurationMs}ms",
                    endpoint, statusCode, durationMs);
            }
            else if (durationMs > 300)
            {
                _logger.LogWarning(
                    "Slow response on {Endpoint} — {DurationMs}ms (threshold: 300ms)",
                    endpoint, durationMs);
            }
            else
            {
                _logger.LogDebug(
                    "Request to {Endpoint} completed — Status {StatusCode}, Duration {DurationMs}ms",
                    endpoint, statusCode, durationMs);
            }
        }

        _logger.LogInformation(
            "Background batch complete — {BatchSize} simulated requests, {ActiveUsers} active users",
            batchSize, activeUsers);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetRandomEndpoint() =>
        Random.Shared.Next(5) switch
        {
            0 => "/api/orders",
            1 => "/api/orders/{id}",
            2 => "/api/weather",
            3 => "/api/health",
            _ => "/api/products",
        };

    private static int GetRandomStatusCode()
    {
        var n = Random.Shared.Next(100);
        return n switch
        {
            < 75 => 200,    // 75% success
            < 85 => 201,    //  10% created
            < 90 => 400,    //   5% bad request
            < 95 => 404,    //   5% not found
            < 98 => 500,    //   3% server error
            _    => 503,    //   2% service unavailable
        };
    }

    private static int GetRandomDuration(string endpoint) =>
        endpoint switch
        {
            "/api/orders"     => Random.Shared.Next(50, 400),
            "/api/orders/{id}"=> Random.Shared.Next(20, 150),
            "/api/weather"    => Random.Shared.Next(10,  80),
            _                 => Random.Shared.Next(30, 250),
        };
}