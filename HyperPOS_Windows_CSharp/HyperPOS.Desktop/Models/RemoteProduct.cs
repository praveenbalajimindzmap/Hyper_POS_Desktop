namespace HyperPOS.Desktop.Models;

public sealed class RemoteProduct
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Sku { get; init; } = "";
    public string Barcode { get; init; } = "";
    public decimal Price { get; init; }
    public int Stock { get; init; }
}
