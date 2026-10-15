using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyperPOS.Desktop.Models;

public sealed class RefundLineItem : INotifyPropertyChanged
{
    public long OrderItemId { get; init; }
    public long ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public string Sku { get; init; } = "";
    public string? ImageUrl { get; init; }
    public decimal UnitPrice { get; init; }
    public int OriginalQty { get; init; }
    public int RefundedQty { get; init; }
    public int RemainingQty => Math.Max(0, OriginalQty - RefundedQty);

    private int _refundQty;
    public int RefundQty
    {
        get => _refundQty;
        set
        {
            var clamped = Math.Clamp(value, 0, RemainingQty);
            if (_refundQty != clamped)
            {
                _refundQty = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsChecked));
                OnPropertyChanged(nameof(RefundAmount));
                OnPropertyChanged(nameof(RefundAmountFormatted));
                RefundQtyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsChecked
    {
        get => _refundQty > 0;
        set
        {
            if (value && _refundQty == 0)
            {
                RefundQty = RemainingQty > 0 ? RemainingQty : 1;
            }
            else if (!value && _refundQty > 0)
            {
                RefundQty = 0;
            }
            OnPropertyChanged();
        }
    }

    public decimal RefundAmount => UnitPrice * _refundQty;
    public string RefundAmountFormatted => RefundAmount > 0 ? $"₹{RefundAmount:N2}" : "—";
    public bool IsRefundable => RemainingQty > 0;
    public string SkuPriceMeta => $"SKU: {Sku} • Price: ₹{UnitPrice:N2}";

    public event EventHandler? RefundQtyChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RefundOrderSummary
{
    public long OrderId { get; init; }
    public string ReceiptNumber { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string CustomerName { get; init; } = "Walking Customer";
    public string CustomerPhone { get; init; } = "No phone number";
    public string PaymentMethod { get; init; } = "Cash";
    public decimal TotalAmount { get; init; }
    public decimal AlreadyRefundedAmount { get; init; }
    public decimal RemainingRefundableAmount => Math.Max(0, TotalAmount - AlreadyRefundedAmount);
    public int TotalItemCount { get; init; }
    public List<RefundLineItem> Items { get; init; } = new();
}
