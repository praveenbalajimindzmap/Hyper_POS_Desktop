namespace HyperPOS.Desktop.Models;

public sealed class ShiftSettlement
{
    public long ShiftId { get; init; }
    public DateTime BusinessDate { get; init; }
    public DateTime OpenedAt { get; init; }
    public decimal OpeningCash { get; init; }
    public int OrderCount { get; init; }
    public int ItemCount { get; init; }
    public decimal TotalSales { get; init; }
    public decimal CashSales { get; init; }
    public IReadOnlyList<PaymentMethodSummary> Payments { get; init; } = [];
    public decimal ExpectedCash => OpeningCash + CashSales;
}
