using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OtelDemo;

/// <summary>
/// Central place for all custom Meters, Counters, Histograms, and ActivitySource.
/// Register the names in OpenTelemetryExtensions so they are exported.
/// </summary>
public sealed class OtelDemoMetrics : IDisposable
{
    // ── Names (must match what's registered in AddMeter / AddSource) ─────────
    public const string MeterName          = "OtelDemo.Metrics";
    public const string ActivitySourceName = "OtelDemo.Activities";

    // ── ActivitySource for manual tracing ───────────────────────────────────
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    // ── Meter ────────────────────────────────────────────────────────────────
    private readonly Meter _meter;

    // Counters
    public readonly Counter<long>     HttpRequestsTotal;
    public readonly Counter<long>     OrdersCreatedTotal;
    public readonly Counter<long>     ErrorsTotal;

    // Histograms
    public readonly Histogram<double> RequestDurationMs;
    public readonly Histogram<double> OrderValueHistogram;

    // Gauges (ObservableGauge — polled on each collection)
    public readonly ObservableGauge<int> ActiveUsersGauge;

    private int _activeUsers;

    public OtelDemoMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        HttpRequestsTotal = _meter.CreateCounter<long>(
            "http_requests_total",
            unit: "{requests}",
            description: "Total number of HTTP requests received");

        OrdersCreatedTotal = _meter.CreateCounter<long>(
            "orders_created_total",
            unit: "{orders}",
            description: "Total number of orders created");

        ErrorsTotal = _meter.CreateCounter<long>(
            "errors_total",
            unit: "{errors}",
            description: "Total number of application errors");

        RequestDurationMs = _meter.CreateHistogram<double>(
            "request_duration_ms",
            unit: "ms",
            description: "Duration of HTTP requests in milliseconds");

        OrderValueHistogram = _meter.CreateHistogram<double>(
            "order_value_usd",
            unit: "USD",
            description: "Value distribution of orders");

        ActiveUsersGauge = _meter.CreateObservableGauge<int>(
            "active_users",
            () => _activeUsers,
            unit: "{users}",
            description: "Current number of active users (simulated)");
    }

    public void SetActiveUsers(int count) => _activeUsers = count;

    public void Dispose() => _meter.Dispose();
}