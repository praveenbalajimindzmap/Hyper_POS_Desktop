using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using HyperPOS.Desktop.Models;

namespace HyperPOS.Desktop;

public partial class DayEndSettlementWindow : Window
{
    private readonly ShiftSettlement settlement;
    private readonly ObservableCollection<CashDenominationCount> denominations =
    [
        new() { Denomination = 2000, Label = "₹2,000.00" },
        new() { Denomination = 500, Label = "₹500.00" },
        new() { Denomination = 200, Label = "₹200.00" },
        new() { Denomination = 100, Label = "₹100.00" },
        new() { Denomination = 50, Label = "₹50.00" },
        new() { Denomination = 20, Label = "₹20.00" },
        new() { Denomination = 10, Label = "₹10.00" },
        new() { Denomination = 5, Label = "Coins (₹5.00)" }
    ];

    public decimal CountedCash => denominations.Sum(item => item.Total);
    public IReadOnlyCollection<CashDenominationCount> CashCounts => denominations;
    public string DifferenceReason { get; private set; } = "";
    public string Notes => NotesBox.Text.Trim();

    public DayEndSettlementWindow(ShiftSettlement settlement)
    {
        this.settlement = settlement;
        InitializeComponent();
        BusinessDateText.Text = settlement.BusinessDate.ToString("dd MMM yyyy · dddd");
        OpenedAtText.Text = settlement.OpenedAt.ToString("hh:mm tt");
        OrderCountText.Text = $"{settlement.OrderCount} ({settlement.ItemCount} items)";
        TotalSalesText.Text = FormatMoney(settlement.TotalSales);
        ItemsSoldText.Text = settlement.ItemCount.ToString(CultureInfo.InvariantCulture);
        NetSalesText.Text = FormatMoney(settlement.TotalSales);
        ExpectedCashText.Text = FormatMoney(settlement.ExpectedCash);
        PaymentsGrid.ItemsSource = settlement.Payments;
        DenominationsList.ItemsSource = denominations;
        foreach (var denomination in denominations)
            denomination.PropertyChanged += Denomination_PropertyChanged;
        UpdateCashSummary();
    }

    private void Denomination_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CashDenominationCount.Quantity) or nameof(CashDenominationCount.Total))
            UpdateCashSummary();
    }

    private void UpdateCashSummary()
    {
        var counted = CountedCash;
        var difference = counted - settlement.ExpectedCash;
        CountedCashText.Text = FormatMoney(counted);
        ReconciliationCountText.Text = FormatMoney(counted);
        DifferenceText.Text = $"{(difference < 0 ? "Short " : difference > 0 ? "Over " : "")}{FormatMoney(Math.Abs(difference))}";
        DifferenceText.Foreground = difference == 0
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 142, 76))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 72));
    }

    private void CloseShift_Click(object sender, RoutedEventArgs e)
    {
        if (denominations.Any(item => item.Quantity < 0))
        {
            ValidationText.Text = "Cash denomination quantities cannot be negative.";
            return;
        }

        var difference = CountedCash - settlement.ExpectedCash;
        DifferenceReason = DifferenceReasonBox.SelectedIndex > 0
            ? ((ComboBoxItem)DifferenceReasonBox.SelectedItem).Content?.ToString() ?? ""
            : "";
        if (Math.Abs(difference) >= 0.01m && DifferenceReason.Length == 0)
        {
            ValidationText.Text = "Select a reason for the cash difference before closing.";
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string FormatMoney(decimal amount) =>
        amount.ToString("C2", CultureInfo.GetCultureInfo("en-IN"));
}
