namespace HyperPOS.Desktop.Models;

public sealed class PaymentMethodSummary
{
    public string PaymentMethod { get; init; } = "";
    public int OrderCount { get; init; }
    public decimal Amount { get; init; }
}
