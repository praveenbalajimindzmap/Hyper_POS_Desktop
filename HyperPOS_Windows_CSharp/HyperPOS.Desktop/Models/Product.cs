using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyperPOS.Desktop.Models;

public sealed class Product : INotifyPropertyChanged
{
    private int cartQuantity;

    public long Id { get; init; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal Discount { get; set; } = 100;
    public string DiscountType { get; set; } = "flat";
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }

    public decimal DiscountAmount => DiscountType == "percent"
        ? Math.Round((OriginalPrice * Discount) / 100m, 2)
        : Discount;

    public int CartQuantity
    {
        get => cartQuantity;
        set
        {
            if (cartQuantity != value)
            {
                cartQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInCart));
            }
        }
    }

    public bool IsInCart => CartQuantity > 0;

    public string DealBadge => "Deal Of The Day";
    public bool HasDealBadge => true;
    public string StockBadgeColor => Stock <= 0 ? "#DC2626" : "#16A34A";
    public string SkuCategoryLine => string.IsNullOrWhiteSpace(Category) || Category == "All" 
        ? Sku 
        : $"{Sku} • {Category}";
    public bool HasDiscount => OriginalPrice > Price && DiscountAmount > 0;
    public string DiscountBadge => DiscountType == "percent" ? $"{Discount:0}% Off" : $"₹{Discount:0} Off";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
