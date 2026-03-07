using OtelDemo;
using OtelDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// ── OpenTelemetry (Traces + Metrics + Logs → OTLP → Collector) ──────────────
builder.AddOpenTelemetryObservability();

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<OtelDemoMetrics>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddHostedService<TelemetryBackgroundWorker>();

// ── ASP.NET ───────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "OtelDemo API",
        Version     = "v1",
        Description = "Demo app wired to OpenTelemetry → Loki / Tempo / Prometheus via Grafana"
    });
});

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OtelDemo API v1");
        c.RoutePrefix = string.Empty; // Swagger at root "/"
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// ── Minimal API: quick diagnostic endpoint ────────────────────────────────────
app.MapGet("/api/diagnostics", (OtelDemoMetrics metrics) => new
{
    service     = "OtelDemo",
    version     = "1.0.0",
    environment = app.Environment.EnvironmentName,
    utcNow      = DateTime.UtcNow,
    collector   = app.Configuration["OpenTelemetry:CollectorEndpoint"],
    message     = "Traces → Tempo | Metrics → Prometheus | Logs → Loki  (all via OTel Collector)",
});

app.Logger.LogInformation(
    "OtelDemo started — Swagger at http://localhost:{Port}",
    app.Configuration["ASPNETCORE_HTTP_PORT"] ?? "5000");

app.Run();