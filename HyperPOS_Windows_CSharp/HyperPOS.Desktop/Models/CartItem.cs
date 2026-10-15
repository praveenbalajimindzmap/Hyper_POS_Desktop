using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyperPOS.Desktop.Models;

public sealed class CartItem : INotifyPropertyChanged
{
    private int quantity = 1;

    public required Product Product { get; init; }
    public int Quantity
    {
        get => quantity;
        set
        {
            if (quantity == value)
                return;

            quantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(GrossTotal));
            OnPropertyChanged(nameof(DiscountTotal));
        }
    }

    public decimal GrossTotal => (Product.OriginalPrice > Product.Price ? Product.OriginalPrice : Product.Price) * Quantity;
    public decimal DiscountTotal => (Product.OriginalPrice > Product.Price ? (Product.OriginalPrice - Product.Price) : Product.DiscountAmount) * Quantity;
    public decimal LineTotal => Product.Price * Quantity;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
