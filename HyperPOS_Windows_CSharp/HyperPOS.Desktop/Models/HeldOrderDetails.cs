namespace HyperPOS.Desktop.Models;

public sealed class HeldOrderDetails
{
    public string CustomerName { get; init; } = "Walking Customer";
    public IReadOnlyList<CartItem> Items { get; init; } = [];
}
