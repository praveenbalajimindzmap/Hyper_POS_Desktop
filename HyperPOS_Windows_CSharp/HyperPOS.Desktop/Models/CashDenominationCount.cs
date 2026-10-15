using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyperPOS.Desktop.Models;

public sealed class CashDenominationCount : INotifyPropertyChanged
{
    private int quantity;

    public decimal Denomination { get; init; }
    public string Label { get; init; } = "";
    public int Quantity
    {
        get => quantity;
        set
        {
            if (quantity == value)
                return;

            quantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Total));
        }
    }

    public decimal Total => Denomination * Quantity;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
