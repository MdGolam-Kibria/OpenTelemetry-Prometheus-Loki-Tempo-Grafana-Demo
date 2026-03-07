namespace OtelDemo;

public record Order(
    Guid   Id,
    string CustomerName,
    string Product,
    int    Quantity,
    double UnitPrice)
{
    public double TotalValue => Quantity * UnitPrice;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record CreateOrderRequest(
    string CustomerName,
    string Product,
    int    Quantity,
    double UnitPrice);