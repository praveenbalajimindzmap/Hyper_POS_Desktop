namespace HyperPOS.Desktop.Models;

public sealed class HeldOrderSummary
{
    public long Id { get; init; }
    public string CustomerName { get; init; } = "";
    public DateTime HeldAt { get; init; }
    public int ItemCount { get; init; }
    public decimal Total { get; init; }
}
