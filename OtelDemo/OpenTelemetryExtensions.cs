using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OtelDemo;

public static class OpenTelemetryExtensions
{
    public static WebApplicationBuilder AddOpenTelemetryObservability(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;

        // ── Shared resource: identifies this service in Grafana ──────────────
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName:       config["OpenTelemetry:ServiceName"]    ?? "OtelDemo",
                serviceVersion:    config["OpenTelemetry:ServiceVersion"] ?? "1.0.0",
                serviceInstanceId: Environment.MachineName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = config["OpenTelemetry:Environment"] ?? "development",
                ["host.name"]              = Environment.MachineName,
                ["dotnet.version"]         = Environment.Version.ToString(),
            });

        var otlpEndpoint = config["OpenTelemetry:CollectorEndpoint"] ?? "http://localhost:4317";

        // ── 1. TRACES + METRICS via fluent WithTracing / WithMetrics ─────────
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddSource(OtelDemoMetrics.ActivitySourceName)
                .AddAspNetCoreInstrumentation(opt =>
                {
                    opt.RecordException = true;
                    opt.EnrichWithHttpRequest  = (activity, req) =>
                        activity.SetTag("http.client_ip",
                            req.HttpContext.Connection.RemoteIpAddress?.ToString());
                    opt.EnrichWithHttpResponse = (activity, res) =>
                        activity.SetTag("http.response_content_length", res.ContentLength);
                })
                .AddHttpClientInstrumentation(opt =>
                {
                    opt.RecordException = true;
                    opt.EnrichWithHttpRequestMessage = (activity, req) =>
                        activity.SetTag("http.request.url", req.RequestUri?.ToString());
                })
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(otlpEndpoint);
                    opt.Protocol = OtlpExportProtocol.Grpc;
                })
            )
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(OtelDemoMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(otlpEndpoint);
                    opt.Protocol = OtlpExportProtocol.Grpc;
                })
            );

        // ── 2. LOGS → Loki (via Collector) ───────────────────────────────────
        // Must use builder.Logging.AddOpenTelemetry() — this is the only place
        // OpenTelemetryLoggerOptions (IncludeFormattedMessage etc.) is accessible.
        // .WithLogging() exposes LoggerProviderBuilder, which does NOT have these properties.
        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeFormattedMessage = true;   // send rendered message to Loki
            logging.IncludeScopes           = true;   // include ILogger scopes as attributes
            logging.ParseStateValues        = true;   // structured key/value pairs
            logging.AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri(otlpEndpoint);
                opt.Protocol = OtlpExportProtocol.Grpc;
            });
            logging.AddConsoleExporter();             // also log to stdout locally
        });

        return builder;
    }
}