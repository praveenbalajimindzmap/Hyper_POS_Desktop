using System.Globalization;

namespace HyperPOS.Desktop;

public partial class ProductEditorWindow : Window
{
    public string ProductName => NameBox.Text.Trim();
    public string Category => CategoryBox.Text.Trim();
    public string Sku => SkuBox.Text.Trim();
    public string Barcode => BarcodeBox.Text.Trim();
    public decimal Price { get; private set; }
    public int Stock { get; private set; }

    public ProductEditorWindow()
    {
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProductName) ||
            string.IsNullOrWhiteSpace(Category) ||
            string.IsNullOrWhiteSpace(Sku))
        {
            ValidationText.Text = "Product name, category, and SKU are required.";
            return;
        }

        if (!decimal.TryParse(PriceBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ||
            price < 0)
        {
            ValidationText.Text = "Enter a valid non-negative price (for example, 1299.00).";
            return;
        }

        if (!int.TryParse(StockBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock) ||
            stock < 0)
        {
            ValidationText.Text = "Enter a valid non-negative stock quantity.";
            return;
        }

        Price = price;
        Stock = stock;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
