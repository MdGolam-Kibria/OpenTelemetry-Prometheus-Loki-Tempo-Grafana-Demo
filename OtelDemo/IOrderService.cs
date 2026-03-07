using System.Diagnostics;

namespace OtelDemo.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken ct = default);
    Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private readonly List<Order>     _orders = [];
    private readonly OtelDemoMetrics _metrics;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OtelDemoMetrics metrics, ILogger<OrderService> logger)
    {
        _metrics = metrics;
        _logger  = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        // ── Start a custom child span ────────────────────────────────────────
        using var activity = OtelDemoMetrics.ActivitySource.StartActivity(
            "OrderService.CreateOrder",
            ActivityKind.Internal);

        activity?.SetTag("order.customer",  request.CustomerName);
        activity?.SetTag("order.product",   request.Product);
        activity?.SetTag("order.quantity",  request.Quantity);
        activity?.SetTag("order.unitPrice", request.UnitPrice);

        // Simulate async DB write
        await Task.Delay(Random.Shared.Next(20, 80), ct);

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerName,
            request.Product,
            request.Quantity,
            request.UnitPrice);

        _orders.Add(order);

        // ── Record metrics ───────────────────────────────────────────────────
        _metrics.OrdersCreatedTotal.Add(1, new TagList
        {
            { "product",  order.Product },
            { "customer", order.CustomerName },
        });

        _metrics.OrderValueHistogram.Record(order.TotalValue, new TagList
        {
            { "product", order.Product },
        });

        activity?.SetTag("order.id",    order.Id.ToString());
        activity?.SetTag("order.total", order.TotalValue);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogInformation(
            "Order {OrderId} created for {Customer} — {Product} x{Qty} = ${Total:F2}",
            order.Id, order.CustomerName, order.Product, order.Quantity, order.TotalValue);

        return order;
    }

    public async Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken ct = default)
    {
        using var activity = OtelDemoMetrics.ActivitySource.StartActivity(
            "OrderService.GetOrders", ActivityKind.Internal);

        await Task.Delay(Random.Shared.Next(5, 25), ct);
        activity?.SetTag("orders.count", _orders.Count);

        return _orders.AsReadOnly();
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var activity = OtelDemoMetrics.ActivitySource.StartActivity(
            "OrderService.GetOrderById", ActivityKind.Internal);

        activity?.SetTag("order.id", id.ToString());
        await Task.Delay(Random.Shared.Next(5, 20), ct);

        var order = _orders.FirstOrDefault(o => o.Id == id);
        activity?.SetTag("order.found", order is not null);

        if (order is null)
            _logger.LogWarning("Order {OrderId} not found", id);

        return order;
    }
}