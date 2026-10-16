using System.Globalization;
using HyperPOS.Desktop.Models;

namespace HyperPOS.Desktop;

public partial class OpeningCashWindow : Window
{
    public decimal OpeningCash { get; private set; }

    public OpeningCashWindow()
    {
        InitializeComponent();
        AmountBox.Focus();
    }

    public OpeningCashWindow(PosShift savedShift)
    {
        InitializeComponent();
        SubtitleText.Text = "Enter the starting cash amount for this shift";
        InputPanel.Visibility = Visibility.Collapsed;
        SavedPanel.Visibility = Visibility.Visible;
        SavedAmountText.Text = savedShift.OpeningCash.ToString("C2", CultureInfo.GetCultureInfo("en-IN"));
        SavedBusinessDateText.Text = $"Current Business Date - {savedShift.BusinessDate:dd MMM yyyy} (Opening Cash Entered)";
        ValidationText.Visibility = Visibility.Collapsed;
        SaveButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Visible;
    }

    private void QuickAmount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string amount })
            AmountBox.Text = amount;
    }

    private void AmountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var valid = decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture,
            out var amount) && amount is > 0 and <= 999999.99m;
        ValidationText.Text = valid ? "" : "Enter an amount between ₹0.01 and ₹999,999.99.";
        if (SaveButton is not null)
            SaveButton.IsEnabled = valid;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture,
                out var amount) || amount is <= 0 or > 999999.99m)
        {
            ValidationText.Text = "Enter an amount between ₹0.01 and ₹999,999.99.";
            SaveButton.IsEnabled = false;
            return;
        }

        OpeningCash = amount;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
