namespace HyperPOS.Desktop.Models;

public sealed class SaleSummary
{
    public long Id { get; init; }
    public string ReceiptNumber { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string CustomerName { get; init; } = "Walking Customer";
    public string CustomerPhone { get; init; } = "No phone number";
    public string PaymentMethod { get; init; } = "";
    public decimal Total { get; init; }
    public int ItemCount { get; init; }
    public string Status { get; init; } = "Delivered";
    public decimal RefundedAmount { get; init; }

    public string StatusBackground => Status switch
    {
        "Refunded" => "#FEE2E2",
        "Partially Refunded" => "#FEF3C7",
        _ => "#DCFCE7"
    };

    public string StatusForeground => Status switch
    {
        "Refunded" => "#DC2626",
        "Partially Refunded" => "#D97706",
        _ => "#15803D"
    };

    public bool IsRefundable => Status != "Refunded";
}
