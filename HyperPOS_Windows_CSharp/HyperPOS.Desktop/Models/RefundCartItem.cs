using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyperPOS.Desktop.Models;

public sealed class RefundCartItem : INotifyPropertyChanged
{
    private int quantity = 1;
    public required Product Product { get; init; }
    public required string ReceiptNumber { get; init; }
    public decimal UnitPrice { get; init; }

    public int Quantity
    {
        get => quantity;
        set
        {
            if (quantity == value) return;
            quantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RefundTotal));
        }
    }

    public decimal RefundTotal => UnitPrice * Quantity;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
