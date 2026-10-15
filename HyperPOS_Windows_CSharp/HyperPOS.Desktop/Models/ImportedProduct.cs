namespace HyperPOS.Desktop.Models;

public sealed class ImportedProduct
{
    public required string Name { get; init; }
    public string Category { get; init; } = "Uncategorized";
    public required string Sku { get; init; }
    public string Barcode { get; init; } = "";
    public decimal Price { get; init; }
    public int Stock { get; init; }
}

public sealed class ProductImportResult
{
    public int Added { get; init; }
    public int Updated { get; init; }
}
