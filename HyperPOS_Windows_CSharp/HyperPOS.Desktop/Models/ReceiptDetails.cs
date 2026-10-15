namespace HyperPOS.Desktop.Models;

public sealed class ReceiptDetails
{
    public required SaleSummary Sale { get; init; }
    public required IReadOnlyList<ReceiptLine> Items { get; init; }
    public decimal AmountPaid { get; init; }
}

public sealed class ReceiptLine
{
    public string ProductName { get; init; } = "";
    public string Sku { get; init; } = "";
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal => UnitPrice * Quantity;
}
