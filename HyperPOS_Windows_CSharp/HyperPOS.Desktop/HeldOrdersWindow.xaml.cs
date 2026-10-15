using System.Collections.ObjectModel;
using HyperPOS.Desktop.Data;
using HyperPOS.Desktop.Models;
using Microsoft.Data.Sqlite;

namespace HyperPOS.Desktop;

public partial class HeldOrdersWindow : Window
{
    private readonly PosDatabase database;
    private readonly IReadOnlyCollection<CartItem> currentCart;
    private readonly ObservableCollection<HeldOrderSummary> orders = [];
    private readonly string cashierName;
    private readonly string cashierEmail;

    public IReadOnlyList<CartItem>? ResumedItems { get; private set; }
    public string ResumedCustomerName { get; private set; } = "Walking Customer";
    public bool HeldCurrentCart { get; private set; }

    public HeldOrdersWindow(
        PosDatabase database,
        IReadOnlyCollection<CartItem> currentCart,
        string customerName = "Walking Customer",
        string cashierName = "Praveen Balaji",
        string cashierEmail = "praveenbalajimindzmap@gmail.com")
    {
        this.database = database;
        this.currentCart = currentCart;
        this.cashierName = cashierName;
        this.cashierEmail = cashierEmail;
        InitializeComponent();
        OrdersGrid.ItemsSource = orders;
        CustomerNameBox.Text = customerName;
        HoldCurrentButton.IsEnabled = currentCart.Count > 0;
        RefreshOrders();
    }

    private void RefreshOrders()
    {
        orders.Clear();
        var search = SearchBox.Text.Trim();
        foreach (var order in database.GetHeldOrders(cashierEmail).Where(order =>
                     search.Length == 0 ||
                     order.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                     order.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)
                         .Contains(search, StringComparison.OrdinalIgnoreCase)))
            orders.Add(order);
        CountText.Text = $"{orders.Count} held order{(orders.Count == 1 ? "" : "s")} shown";
    }

    private void HoldCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (currentCart.Count == 0)
            return;

        try
        {
            database.SaveHeldOrder(CustomerNameBox.Text, currentCart, cashierName, cashierEmail);
            HeldCurrentCart = true;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqliteException)
        {
            MessageBox.Show($"The cart could not be held.\n\n{ex.Message}",
                "Hold order failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not HeldOrderSummary selected)
        {
            MessageBox.Show("Select a held order to resume.", "No order selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (currentCart.Count > 0 && MessageBox.Show(
                "Resuming this order replaces the current cart. Continue?",
                "Replace current cart", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            var resumedOrder = database.ResumeHeldOrder(selected.Id);
            ResumedItems = resumedOrder.Items;
            ResumedCustomerName = resumedOrder.CustomerName;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqliteException)
        {
            MessageBox.Show($"The held order could not be resumed.\n\n{ex.Message}",
                "Resume order failed", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshOrders();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not HeldOrderSummary selected)
        {
            MessageBox.Show("Select a held order to delete.", "No order selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete the held order for {selected.CustomerName}? This cannot be undone.",
            "Delete held order", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            database.DeleteHeldOrder(selected.Id);
            RefreshOrders();
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqliteException)
        {
            MessageBox.Show($"The held order could not be deleted.\n\n{ex.Message}",
                "Delete order failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OrdersGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        Resume_Click(sender, e);

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshOrders();

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
