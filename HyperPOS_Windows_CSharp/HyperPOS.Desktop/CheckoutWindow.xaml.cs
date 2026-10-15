using System.Globalization;

namespace HyperPOS.Desktop;

public partial class CheckoutWindow : Window
{
    private readonly decimal total;
    private readonly CultureInfo currencyCulture = new("en-IN");

    public string PaymentMethod { get; private set; } = "Cash";
    public string CustomerName => CustomerNameBox.Text.Trim();
    public decimal AmountPaid { get; private set; }

    public CheckoutWindow(decimal total, string customerName = "Walking Customer")
    {
        this.total = total;
        InitializeComponent();
        CustomerNameBox.Text = customerName;
        TotalText.Text = total.ToString("C2", currencyCulture);
        AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        AmountBox.SelectAll();
    }

    private void PaymentMethodBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AmountBox is null || PaymentMethodBox.SelectedItem is not ComboBoxItem item)
            return;

        PaymentMethod = item.Content?.ToString() ?? "Cash";
        var isCash = PaymentMethod == "Cash";
        AmountLabel.Text = isCash ? "CASH RECEIVED" : "AMOUNT PAID";
        AmountBox.IsReadOnly = !isCash;
        AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        UpdateChange();
    }

    private void AmountBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateChange();

    private void UpdateChange()
    {
        if (AmountBox is null || ChangeText is null || ValidationText is null)
            return;

        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount < total)
        {
            ChangeText.Text = total.ToString("C2", currencyCulture);
            ValidationText.Text = "Amount must be a valid number equal to or greater than the amount due.";
            ConfirmButton.IsEnabled = false;
            return;
        }

        ChangeText.Text = (amount - total).ToString("C2", currencyCulture);
        ValidationText.Text = "";
        ConfirmButton.IsEnabled = true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount < total)
        {
            UpdateChange();
            return;
        }

        AmountPaid = amount;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        var key = e.SystemKey != System.Windows.Input.Key.None ? e.SystemKey : e.Key;
        var alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;

        if (alt && key == System.Windows.Input.Key.S)
        {
            if (ConfirmButton.IsEnabled)
            {
                Confirm_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
        else if ((alt && key == System.Windows.Input.Key.Z) || key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
