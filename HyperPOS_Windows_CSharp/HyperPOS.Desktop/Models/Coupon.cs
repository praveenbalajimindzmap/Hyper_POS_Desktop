namespace HyperPOS.Desktop.Models;

public sealed class Coupon
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "percent"; // "percent" or "amount"
    public decimal Discount { get; set; }
    public decimal MinPurchase { get; set; }
    public DateTime? ExpireDate { get; set; }
}
