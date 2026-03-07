using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using OtelDemo.Services;

namespace OtelDemo;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService            _orderService;
    private readonly OtelDemoMetrics          _metrics;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        OtelDemoMetrics metrics,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _metrics      = metrics;
        _logger       = logger;
    }

    // ── Shared helper: gets current TraceId and injects it into response ──────
    private string? InjectTraceId()
    {
        var traceId = Activity.Current?.TraceId.ToString();
        if (traceId is not null)
            Response.Headers["X-Trace-Id"] = traceId;
        return traceId;
    }

    // GET /api/orders
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        using var activity = OtelDemoMetrics.ActivitySource.StartActivity(
            "OrdersController.GetAll", ActivityKind.Server);

        _logger.LogInformation("Fetching all orders");

        var orders = await _orderService.GetOrdersAsync(ct);
        var traceId = InjectTraceId();

        _metrics.HttpRequestsTotal.Add(1, new TagList
        {
            { "endpoint",    "/api/orders" },
            { "method",      "GET" },
            { "status_code", 200 },
        });

        _metrics.RequestDurationMs.Record(sw.ElapsedMilliseconds, new TagList
        {
            { "endpoint", "/api/orders" },
            { "method",   "GET" },
        });

        activity?.SetTag("orders.returned", orders.Count);

        return Ok(new
        {
            traceId,
            count  = orders.Count,
            data   = orders,
        });
    }

    // GET /api/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        using var activity = OtelDemoMetrics.ActivitySource.StartActivity(
            "OrdersController.GetById", ActivityKind.Server);
        activity?.SetTag("order.id", id.ToString());

        var order = await _orderService.GetOrderByIdAsync(id, ct);
        var traceId = InjectTraceId();

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found — returning 404", id);

            _metrics.HttpRequestsTotal.Add(1, new TagList
            {
                { "endpoint",    "/api/orders/{id}" },
                { "method",      "GET" },
                { "status_code", 404 },
            });

            return NotFound(new
            {
                traceId,
                message = $"Order {id} not found",
            });
        }

        _metrics.HttpRequestsTotal.Add(1, new TagList
        {
            { "endpoint",    "/api/orders/{id}" },
            { "method",      "GET" },
            { "status_code", 200 },
        });

        _metrics.RequestDurationMs.Record(sw.ElapsedMilliseconds, new TagList
        {
            { "endpoint", "/api/orders/{id}" },
            { "method",   "GET" },
        });

        return Ok(new
        {
            traceId,
            data = order,
        });
    }

    // POST /api/orders
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        using var activity = OtelDemoMetrics.ActivitySource.StartActivity(
            "OrdersController.Create", ActivityKind.Server);

        var traceId = InjectTraceId();

        if (request.Quantity <= 0 || request.UnitPrice <= 0)
        {
            _metrics.ErrorsTotal.Add(1, new TagList { { "source", "validation" } });

            _logger.LogWarning(
                "Invalid order request — Customer: {Customer}, Qty: {Qty}, Price: {Price}",
                request.CustomerName, request.Quantity, request.UnitPrice);

            activity?.SetStatus(ActivityStatusCode.Error, "Validation failed");

            return BadRequest(new
            {
                traceId,
                message = "Quantity and UnitPrice must be > 0",
            });
        }

        try
        {
            var order = await _orderService.CreateOrderAsync(request, ct);

            _metrics.HttpRequestsTotal.Add(1, new TagList
            {
                { "endpoint",    "/api/orders" },
                { "method",      "POST" },
                { "status_code", 201 },
            });

            _metrics.RequestDurationMs.Record(sw.ElapsedMilliseconds, new TagList
            {
                { "endpoint", "/api/orders" },
                { "method",   "POST" },
            });

            _logger.LogInformation(
                "Order {OrderId} created — TraceId: {TraceId}", order.Id, traceId);

            return CreatedAtAction(nameof(GetById), new { id = order.Id }, new
            {
                traceId,
                data = order,
            });
        }
        catch (Exception ex)
        {
            _metrics.ErrorsTotal.Add(1, new TagList { { "source", "order_creation" } });

            _logger.LogError(ex,
                "Failed to create order for {Customer} — TraceId: {TraceId}",
                request.CustomerName, traceId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            return StatusCode(500, new
            {
                traceId,
                message = "Failed to create order",
            });
        }
    }
}