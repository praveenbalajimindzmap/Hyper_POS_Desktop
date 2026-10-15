namespace HyperPOS.Desktop.Models;

public sealed class SaleSummary
{
    public long Id { get; init; }
    public string ReceiptNumber { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string CustomerName { get; init; } = "Walking Customer";
    public string PaymentMethod { get; init; } = "";
    public decimal Total { get; init; }
    public int ItemCount { get; init; }
}
