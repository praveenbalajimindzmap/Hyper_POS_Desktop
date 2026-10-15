using System.Globalization;
using System.Windows.Documents;
using HyperPOS.Desktop.Models;

namespace HyperPOS.Desktop;

public partial class ReceiptWindow : Window
{
    private readonly ReceiptDetails receipt;
    private readonly FlowDocument document;
    private readonly CultureInfo currencyCulture = CultureInfo.GetCultureInfo("en-IN");

    public ReceiptWindow(ReceiptDetails receipt)
    {
        this.receipt = receipt;
        InitializeComponent();
        ReceiptSubTitle.Text = $"{receipt.Sale.ReceiptNumber} · {receipt.Sale.CreatedAt:g}";
        document = CreateReceiptDocument();
        ReceiptPreview.Document = document;
    }

    private FlowDocument CreateReceiptDocument()
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(16),
            PageWidth = 300,
            PageHeight = 900,
            ColumnWidth = 280,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Black
        };
        document.Blocks.Add(Centered("JomJom POS", 19, FontWeights.Bold));
        document.Blocks.Add(Centered("LOCAL POS RECEIPT", 10, FontWeights.SemiBold));
        document.Blocks.Add(Centered("Thank you for shopping with us", 10, FontWeights.Normal));
        document.Blocks.Add(Separator());
        document.Blocks.Add(KeyValue("Receipt", receipt.Sale.ReceiptNumber));
        document.Blocks.Add(KeyValue("Date", receipt.Sale.CreatedAt.ToString("dd MMM yyyy, hh:mm tt")));
        document.Blocks.Add(KeyValue("Cashier", "Local operator"));
        document.Blocks.Add(KeyValue("Customer", receipt.Sale.CustomerName));
        document.Blocks.Add(KeyValue("Payment", receipt.Sale.PaymentMethod));
        document.Blocks.Add(Separator());
        document.Blocks.Add(BuildItemsTable());
        document.Blocks.Add(Separator());
        document.Blocks.Add(KeyValue("Items", receipt.Sale.ItemCount.ToString(CultureInfo.InvariantCulture)));
        document.Blocks.Add(KeyValue("Subtotal", FormatMoney(receipt.Sale.Total)));
        document.Blocks.Add(KeyValue("Tax", "₹0.00"));
        document.Blocks.Add(KeyValue("Total", FormatMoney(receipt.Sale.Total), 14, FontWeights.Bold));
        document.Blocks.Add(KeyValue("Amount paid", FormatMoney(receipt.AmountPaid)));
        document.Blocks.Add(KeyValue("Change", FormatMoney(Math.Max(0, receipt.AmountPaid - receipt.Sale.Total))));
        document.Blocks.Add(Separator());
        document.Blocks.Add(Centered("Thank you — please visit again", 10, FontWeights.SemiBold));
        return document;
    }

    private Table BuildItemsTable()
    {
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(0.45, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(0.65, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });
        var group = new TableRowGroup();
        var header = new TableRow();
        header.Cells.Add(Cell("Product", TextAlignment.Left, FontWeights.Bold));
        header.Cells.Add(Cell("Qty", TextAlignment.Right, FontWeights.Bold));
        header.Cells.Add(Cell("Price", TextAlignment.Right, FontWeights.Bold));
        header.Cells.Add(Cell("Total", TextAlignment.Right, FontWeights.Bold));
        group.Rows.Add(header);
        foreach (var item in receipt.Items)
        {
            var row = new TableRow();
            row.Cells.Add(Cell(item.ProductName, TextAlignment.Left, FontWeights.Normal));
            row.Cells.Add(Cell(item.Quantity.ToString(CultureInfo.InvariantCulture), TextAlignment.Right, FontWeights.Normal));
            row.Cells.Add(Cell(FormatMoney(item.UnitPrice), TextAlignment.Right, FontWeights.Normal));
            row.Cells.Add(Cell(FormatMoney(item.LineTotal), TextAlignment.Right, FontWeights.Normal));
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        return table;
    }

    private static TableCell Cell(string text, TextAlignment alignment, FontWeight weight) =>
        new(new Paragraph(new Run(text))
        {
            Margin = new Thickness(0, 3, 0, 3),
            TextAlignment = alignment,
            FontWeight = weight,
            FontSize = 9
        })
        {
            Padding = new Thickness(1, 2, 1, 2),
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 0.5)
        };

    private static Paragraph Centered(string text, double size, FontWeight weight) =>
        new(new Run(text))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = size,
            FontWeight = weight,
            Margin = new Thickness(0, 2, 0, 2)
        };

    private Paragraph KeyValue(string label, string value, double size = 10, FontWeight? weight = null)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2), FontSize = size };
        paragraph.Inlines.Add(new Run($"{label}: ") { FontWeight = FontWeights.SemiBold });
        paragraph.Inlines.Add(new Run(value) { FontWeight = weight ?? FontWeights.Normal });
        return paragraph;
    }

    private static Paragraph Separator() =>
        new(new Run("------------------------------------------------"))
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4),
            FontSize = 9
        };

    private string FormatMoney(decimal amount) => amount.ToString("C2", currencyCulture);

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
            return;

        try
        {
            printDialog.PrintDocument(
                ((IDocumentPaginatorSource)document).DocumentPaginator,
                $"Receipt {receipt.Sale.ReceiptNumber}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Printing.PrintSystemException)
        {
            MessageBox.Show($"The receipt could not be printed.\n\n{ex.Message}",
                "Print failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
