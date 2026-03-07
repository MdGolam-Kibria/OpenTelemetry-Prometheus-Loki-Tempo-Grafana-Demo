using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OtelDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private readonly OtelDemoMetrics _metrics;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(OtelDemoMetrics metrics, ILogger<WeatherController> logger)
    {
        _metrics = metrics;
        _logger  = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var sw = Stopwatch.StartNew();

        using var activity = OtelDemoMetrics.ActivitySource.StartActivity(
            "WeatherController.Get", ActivityKind.Server);

        _logger.LogInformation("Weather forecast requested from {ClientIp}",
            HttpContext.Connection.RemoteIpAddress);

        var forecast = Enumerable.Range(1, 5).Select(index => new
        {
            Date          = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC  = Random.Shared.Next(-20, 55),
            Summary       = Summaries[Random.Shared.Next(Summaries.Length)],
        }).ToArray();

        activity?.SetTag("forecast.days", forecast.Length);
        activity?.SetTag("forecast.min_temp", forecast.Min(f => f.TemperatureC));
        activity?.SetTag("forecast.max_temp", forecast.Max(f => f.TemperatureC));

        _metrics.HttpRequestsTotal.Add(1, new TagList
        {
            { "endpoint",    "/api/weather" },
            { "method",      "GET" },
            { "status_code", 200 },
        });

        _metrics.RequestDurationMs.Record(sw.ElapsedMilliseconds, new TagList
        {
            { "endpoint", "/api/weather" },
            { "method",   "GET" },
        });

        _logger.LogInformation("Weather forecast generated — {Count} days", forecast.Length);

        return Ok(forecast);
    }
}