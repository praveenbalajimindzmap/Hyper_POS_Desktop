using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HyperPOS.Desktop.Data;

namespace HyperPOS.Desktop;

public partial class CheckoutWindow : Window
{
    private readonly decimal total;
    private readonly CultureInfo currencyCulture = new("en-IN");

    public string PaymentMethod { get; private set; } = "Cash";
    public string CustomerName => CustomerNameBox.Text.Trim();
    public string CustomerPhone => CustomerPhoneBox.Text.Trim();
    public decimal AmountPaid { get; private set; }
    public bool ShouldSendWhatsApp => SendWhatsAppCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(CustomerPhone);

    public CheckoutWindow(decimal total, string customerName = "Walking Customer", string customerPhone = "")
    {
        this.total = total;
        InitializeComponent();
        CustomerNameBox.Text = customerName;
        CustomerDisplayHeader.Text = customerName;
        CustomerPhoneBox.Text = customerPhone;
        TotalText.Text = total.ToString("C2", currencyCulture);
        AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        AmountBox.SelectAll();

        // Pre-generate dynamic UPI QR in background
        GenerateUpiQr();
    }

    private void GenerateUpiQr()
    {
        try
        {
            var invoiceNo = $"SB-{DateTime.Now:yyyyMMddHHmmss}";
            var payload = UpiQrService.BuildUpiPayload("9974692496@okbizaxis", "Saraa Boutique", total, invoiceNo);
            UpiQrImage.Source = UpiQrService.GenerateQrBitmap(payload, 8);
        }
        catch
        {
            // Silently ignore QR rendering error
        }
    }

    private void PaymentMethod_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || AmountBox == null) return;

        if (rb == PayCashRadio)
        {
            PaymentMethod = "Cash";
            UpiQrContainer.Visibility = Visibility.Collapsed;
            SplitPayContainer.Visibility = Visibility.Collapsed;
            CashTenderSection.Visibility = Visibility.Visible;
            AmountLabel.Text = "CASH RECEIVED";
            AmountBox.IsReadOnly = false;
        }
        else if (rb == PayUpiRadio)
        {
            PaymentMethod = "UPI";
            UpiQrContainer.Visibility = Visibility.Visible;
            SplitPayContainer.Visibility = Visibility.Collapsed;
            CashTenderSection.Visibility = Visibility.Collapsed;
            AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        }
        else if (rb == PayCardRadio)
        {
            PaymentMethod = "Card";
            UpiQrContainer.Visibility = Visibility.Collapsed;
            SplitPayContainer.Visibility = Visibility.Collapsed;
            CashTenderSection.Visibility = Visibility.Collapsed;
            AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        }
        else if (rb == PayCreditRadio)
        {
            PaymentMethod = "Store Credit";
            UpiQrContainer.Visibility = Visibility.Collapsed;
            SplitPayContainer.Visibility = Visibility.Collapsed;
            CashTenderSection.Visibility = Visibility.Collapsed;
            AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        }
        else if (rb == PayChequeRadio)
        {
            PaymentMethod = "Cheque";
            UpiQrContainer.Visibility = Visibility.Collapsed;
            SplitPayContainer.Visibility = Visibility.Collapsed;
            CashTenderSection.Visibility = Visibility.Collapsed;
            AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        }
        else if (rb == PaySplitRadio)
        {
            PaymentMethod = "Split Payment";
            UpiQrContainer.Visibility = Visibility.Collapsed;
            SplitPayContainer.Visibility = Visibility.Visible;
            CashTenderSection.Visibility = Visibility.Collapsed;
            SplitCashBox.Text = (total / 2m).ToString("0.00", CultureInfo.InvariantCulture);
            SplitUpiBox.Text = (total - (total / 2m)).ToString("0.00", CultureInfo.InvariantCulture);
        }

        UpdateChange();
    }

    private void QuickCashExact_Click(object sender, RoutedEventArgs e)
    {
        AmountBox.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
        AmountBox.Focus();
        AmountBox.SelectAll();
    }

    private void QuickCashAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && decimal.TryParse(tag, out var addVal))
        {
            if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var current))
            {
                current = 0m;
            }
            var nextAmount = current + addVal;
            AmountBox.Text = nextAmount.ToString("0.00", CultureInfo.InvariantCulture);
            AmountBox.Focus();
            AmountBox.SelectAll();
        }
    }

    private void SplitBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SplitCashBox == null || SplitUpiBox == null || ValidationText == null || ConfirmButton == null) return;

        decimal.TryParse(SplitCashBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var cash);
        decimal.TryParse(SplitUpiBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var upi);

        var splitSum = cash + upi;
        if (Math.Abs(splitSum - total) > 0.01m)
        {
            ValidationText.Text = $"Split sum (₹{splitSum:N2}) must equal total payable (₹{total:N2}). Difference: ₹{(total - splitSum):N2}";
            ConfirmButton.IsEnabled = false;
        }
        else
        {
            ValidationText.Text = "";
            ConfirmButton.IsEnabled = true;
        }
    }

    private void AmountBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateChange();

    private void UpdateChange()
    {
        if (AmountBox is null || ChangeText is null || ValidationText is null || ConfirmButton is null)
            return;

        if (PaymentMethod != "Cash")
        {
            ChangeText.Text = "₹0.00";
            ValidationText.Text = "";
            ConfirmButton.IsEnabled = true;
            return;
        }

        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount < total)
        {
            ChangeText.Text = "₹0.00";
            ValidationText.Text = "Cash received must be equal to or greater than total amount due.";
            ConfirmButton.IsEnabled = false;
            return;
        }

        ChangeText.Text = (amount - total).ToString("C2", currencyCulture);
        ValidationText.Text = "";
        ConfirmButton.IsEnabled = true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (PaymentMethod == "Cash")
        {
            if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
                amount < total)
            {
                UpdateChange();
                return;
            }
            AmountPaid = amount;
        }
        else if (PaymentMethod == "Split Payment")
        {
            decimal.TryParse(SplitCashBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var cash);
            decimal.TryParse(SplitUpiBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var upi);
            if (Math.Abs((cash + upi) - total) > 0.01m)
            {
                return;
            }
            AmountPaid = total;
        }
        else
        {
            AmountPaid = total;
        }

        DialogResult = true;
    }

    public void LaunchWhatsAppReceipt(string receiptNumber, decimal grandTotal)
    {
        if (!ShouldSendWhatsApp) return;

        try
        {
            var rawPhone = new string(CustomerPhone.Where(char.IsDigit).ToArray());
            if (rawPhone.Length == 10) rawPhone = "91" + rawPhone;

            var message = $"🌸 *Saraa Boutique E-Receipt* 🌸%0A" +
                          $"Thank you, *{CustomerName}*!%0A" +
                          $"Invoice: *{receiptNumber}*%0A" +
                          $"Paid via: *{PaymentMethod}*%0A" +
                          $"Amount: *₹{grandTotal:N2}*%0A" +
                          $"Date: {DateTime.Now:dd MMM yyyy, hh:mm tt}%0A%0A" +
                          $"Exchange policy: 7 Days with tag intact.%0A" +
                          $"Support: +91 99746 92496. Visit again soon! ✨";

            var url = $"https://wa.me/{rawPhone}?text={message}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently ignore if browser launching has restrictions
        }
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
