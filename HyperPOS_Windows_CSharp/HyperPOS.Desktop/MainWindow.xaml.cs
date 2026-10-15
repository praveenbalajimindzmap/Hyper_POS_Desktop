using System.Collections.ObjectModel;
using HyperPOS.Desktop.Data;
using HyperPOS.Desktop.Models;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace HyperPOS.Desktop;

public partial class MainWindow : Window
{
    private readonly PosDatabase database = new();
    private readonly SyncConfigurationStore syncConfigurationStore = new();
    private readonly ProductSyncService productSyncService = new();
    private readonly ProductCsvImporter productCsvImporter = new();
    private readonly ObservableCollection<CartItem> cart = [];
    private string selectedCategory = "All";
    private string pendingCustomerName = "Walking Customer";
    private DateTime selectedReceiptDate = DateTime.Today;
    private int receiptWeekOffset;
    private PosShift? activeShift;
    private bool isSyncing;
    private readonly string cashierName;
    private readonly string cashierEmail;
    private Customer? selectedCustomer;
    private decimal extraDiscountRate = 0m;
    private string extraDiscountType = "percent";
    private Coupon? activeCoupon;
    private decimal currentPayableTotal;
    private LocalScannerGateway? scannerGateway;
    private CustomerDisplayWindow? customerDisplayWindow;
    private decimal currentGstRate = 5m;
    private bool isDarkMode;
    private RefundOrderSummary? currentRefundSummary;

    // Inbuilt Desk Calculator State
    private string calcDisplay = "0";
    private decimal calcFirstOperand = 0m;
    private string? calcPendingOp;
    private bool calcIsNewEntry = true;

    private readonly List<Coupon> availableCoupons = new()
    {
        new Coupon { Code = "SARAA10", Title = "10% Storewide Off", DiscountType = "percent", Discount = 10, MinPurchase = 500 },
        new Coupon { Code = "SARAA20", Title = "20% Festive Bonanza", DiscountType = "percent", Discount = 20, MinPurchase = 1000 },
        new Coupon { Code = "WELCOME50", Title = "₹50 Welcome Voucher", DiscountType = "amount", Discount = 50, MinPurchase = 300 },
        new Coupon { Code = "FESTIVE100", Title = "₹100 Flat Festival Discount", DiscountType = "amount", Discount = 100, MinPurchase = 800 },
        new Coupon { Code = "SUPERDEAL", Title = "15% Special Deal", DiscountType = "percent", Discount = 15, MinPurchase = 600 }
    };

    public MainWindow() : this("Praveen Balaji", "praveenbalajimindzmap@gmail.com")
    {
    }

    public MainWindow(string cashierName, string cashierEmail = "praveenbalajimindzmap@gmail.com")
    {
        this.cashierName = string.IsNullOrWhiteSpace(cashierName) ? "Praveen Balaji" : cashierName;
        this.cashierEmail = string.IsNullOrWhiteSpace(cashierEmail) ? "praveenbalajimindzmap@gmail.com" : cashierEmail;
        InitializeComponent();
        if (CashierNameText != null)
        {
            CashierNameText.Text = this.cashierName;
        }
        if (CashierRoleLabel != null)
        {
            if (this.cashierEmail.Equals("praveenbalajimindzmap@gmail.com", StringComparison.OrdinalIgnoreCase) ||
                this.cashierName.Contains("Super Admin", StringComparison.OrdinalIgnoreCase))
            {
                CashierRoleLabel.Text = "Super Admin: ";
            }
            else
            {
                CashierRoleLabel.Text = "Cashier: ";
            }
        }
        CurrentDateText.Text = "🕒 " + DateTime.Now.ToString("dd MMM, yyyy - hh:mm tt");
        SyncStatusText.Text = syncConfigurationStore.IsConfigured
            ? "Offline · cache available"
            : "Offline · local catalog";
        CartItems.ItemsSource = cart;
        SaraaSqlImporter.ImportSaraaProducts(database);
        RefreshCatalog();
        RefreshProducts();
        activeShift = database.GetActiveShift(this.cashierEmail);
        RefreshShiftStatus();
        RefreshSales();
        UpdateCartSummary();
        LoadRecentCustomers();

        // Start Local Wi-Fi Scanner Gateway for Mobile/Flutter App scans
        try
        {
            scannerGateway = new LocalScannerGateway(8085);
            scannerGateway.OnBarcodeReceived += (barcode) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try { System.Media.SystemSounds.Beep.Play(); } catch { }
                    ScanProductByCode(barcode);
                });
            };
            scannerGateway.Start();
        }
        catch { }
    }

    public void ScanProductByCode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        ScanBox.Text = barcode.Trim();
        ScanProduct();
    }

    private void RefreshCatalog()
    {
        var products = database.GetProducts(SearchBox.Text, selectedCategory);
        var cartLookup = cart.ToDictionary(c => c.Product.Id, c => c.Quantity);
        foreach (var prod in products)
        {
            prod.CartQuantity = cartLookup.TryGetValue(prod.Id, out var qty) ? qty : 0;
        }
        ProductCatalog.ItemsSource = products;
        var allProducts = database.GetProducts(SearchBox.Text);
        ProductCountText.Text = $"Showing 1 – {products.Count} of {allProducts.Count} Products";
        var categories = database.GetCategories();
        CategoryButtons.Children.Clear();
        foreach (var category in categories)
        {
            var count = category == "All" 
                ? allProducts.Count 
                : allProducts.Count(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
            var displayLabel = category == "All" ? $"All categories ({count})" : $"{category} ({count})";

            var isSelected = category == selectedCategory;
            var button = new Button
            {
                Content = displayLabel,
                Tag = category,
                Style = (Style)FindResource("CategoryButton"),
                Height = 34,
                MinHeight = 34,
                Padding = new Thickness(14, 0, 14, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(238, 242, 255))
                    : Brushes.White,
                Foreground = isSelected
                    ? new SolidColorBrush(Color.FromRgb(59, 75, 216))
                    : new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(59, 75, 216))
                    : new SolidColorBrush(Color.FromRgb(229, 231, 235))
            };
            button.Click += Category_Click;
            CategoryButtons.Children.Add(button);
        }
    }

    private void RefreshProducts() => ProductsGrid.ItemsSource = database.GetProducts();
    private void RefreshSales()
    {
        ReceiptDateButtons.Children.Clear();
        var weekEnd = DateTime.Today.AddDays(-7 * receiptWeekOffset);
        for (var offset = 6; offset >= 0; offset--)
        {
            var date = weekEnd.AddDays(-offset);
            var daySales = database.GetSales(date, cashierEmail);
            bool isSelected = date.Date == selectedReceiptDate.Date;
            var button = new Button
            {
                Tag = date,
                Width = 122,
                Height = 86,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(8, 6, 8, 6),
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(238, 242, 255))
                    : Brushes.White,
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(67, 56, 202))
                    : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = (Style)FindResource("FlatButton"),
                Content = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = date.ToString("ddd"),
                            FontSize = 11,
                            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = isSelected
                                ? new SolidColorBrush(Color.FromRgb(67, 56, 202))
                                : new SolidColorBrush(Color.FromRgb(71, 85, 105))
                        },
                        new TextBlock
                        {
                            Text = date.ToString("dd MMM"),
                            FontSize = 11,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 2, 0, 0),
                            Foreground = isSelected
                                ? new SolidColorBrush(Color.FromRgb(67, 56, 202))
                                : new SolidColorBrush(Color.FromRgb(100, 116, 139))
                        },
                        new TextBlock
                        {
                            Text = FormatMoney(daySales.Sum(sale => sale.Total)),
                            FontSize = 13.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = isSelected
                                ? new SolidColorBrush(Color.FromRgb(59, 75, 216))
                                : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 3, 0, 0)
                        },
                        new TextBlock
                        {
                            Text = $"{daySales.Count} Orders",
                            FontSize = 11,
                            Foreground = isSelected
                                ? new SolidColorBrush(Color.FromRgb(67, 56, 202))
                                : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 2, 0, 0)
                        }
                    }
                }
            };
            button.Click += ReceiptDate_Click;
            ReceiptDateButtons.Children.Add(button);
        }

        var sales = database.GetSales(selectedReceiptDate, cashierEmail);
        SalesGrid.ItemsSource = sales;
        SelectedReceiptDateText.Text = $"Orders for Business Day {selectedReceiptDate:dd MMM yyyy} ({selectedReceiptDate:dddd})";
        ReceiptOrderCountText.Text = $"Total orders: {sales.Count}";
        ReceiptDayTotalText.Text = $"Total Sales: {FormatMoney(sales.Sum(sale => sale.Total))}";
        ReceiptShowingText.Text = $"Showing 1 - {sales.Count} of {sales.Count} Receipts";
        NextReceiptWeekButton.IsEnabled = receiptWeekOffset > 0;
        if (ReceiptsCountText != null)
        {
            ReceiptsCountText.Text = database.GetSales(DateTime.Today, cashierEmail).Count.ToString();
        }
    }

    private void RefreshShiftStatus()
    {
        activeShift = database.GetActiveShift(cashierEmail);
        if (OpeningCashAmountText != null)
        {
            OpeningCashAmountText.Text = activeShift is null
                ? "S$ 100.00"
                : FormatMoney(activeShift.OpeningCash);
        }
        DayEndButton.IsEnabled = activeShift is not null;
        var holdCount = database.GetHeldOrders(cashierEmail).Count;
        if (HoldCountText != null)
        {
            HoldCountText.Text = holdCount.ToString();
        }
        if (ReceiptsCountText != null)
        {
            ReceiptsCountText.Text = database.GetSales(DateTime.Today, cashierEmail).Count.ToString();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshCatalog();

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
            return;

        var scannedCode = SearchBox.Text.Trim();
        var product = database.GetProducts().FirstOrDefault(item =>
            string.Equals(item.Barcode, scannedCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Sku, scannedCode, StringComparison.OrdinalIgnoreCase));
        if (product is not null)
        {
            AddProductToCart(product);
            SearchBox.Clear();
        }
        e.Handled = true;
    }

    private void ScanBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
            return;

        ScanProduct();
        e.Handled = true;
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e) => ScanProduct();

    private void ScanProduct()
    {
        var scannedCode = ScanBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(scannedCode))
            return;

        var product = database.GetProducts().FirstOrDefault(item =>
            string.Equals(item.Barcode, scannedCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Sku, scannedCode, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            MessageBox.Show($"No product was found for barcode or SKU '{scannedCode}'.",
                "Product not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AddProductToCart(product);
        LastScannedText.Text = $"Last scanned: {product.Name}";
        ScanBox.Clear();
    }

    private void FocusScanner_Click(object sender, RoutedEventArgs e) => ScanBox.Focus();

    private async void SyncProducts_Click(object sender, RoutedEventArgs e)
    {
        SyncConfiguration? configuration;
        try
        {
            configuration = syncConfigurationStore.Load();
        }
        catch (Exception ex) when (ex is IOException or JsonException or CryptographicException or FormatException)
        {
            MessageBox.Show($"Saved product-sync settings could not be opened.\n\n{ex.Message}",
                "Product sync settings", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (configuration is null)
        {
            await OpenProductSyncSettingsAsync();
            return;
        }

        await SyncCatalogAsync(configuration);
    }

    private async void SetupProductSync_Click(object sender, RoutedEventArgs e) =>
        await OpenProductSyncSettingsAsync();

    private async Task OpenProductSyncSettingsAsync()
    {
        var settings = new ProductSyncSettingsWindow { Owner = this };
        if (settings.ShowDialog() != true || settings.Configuration is null)
            return;

        await SyncCatalogAsync(settings.Configuration);
    }

    private async Task SyncCatalogAsync(SyncConfiguration configuration)
    {
        if (isSyncing)
            return;

        isSyncing = true;
        SyncButton.IsEnabled = false;
        try
        {
            syncConfigurationStore.Save(configuration.BaseUrl, configuration.ApiToken);
            SyncStatusText.Text = "Syncing catalog…";
            var count = await productSyncService.SyncAsync(configuration, database);
            SyncStatusText.Text = $"Synced {count} products · {DateTime.Now:t}";
            RefreshCatalog();
            RefreshProducts();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException or IOException or CryptographicException or JsonException or FormatException)
        {
            SyncStatusText.Text = "Sync failed · local cache kept";
            MessageBox.Show(
                $"The product catalog could not be synchronized. Your last downloaded catalog has not been replaced.\n\n{ex.Message}",
                "Product sync failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SyncButton.IsEnabled = true;
            isSyncing = false;
        }
    }

    private void CategoryScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            if (e.Delta < 0)
                sv.LineRight();
            else
                sv.LineLeft();
            e.Handled = true;
        }
    }

    private void CategoryScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        CategoryChipsScrollViewer?.LineLeft();
        CategoryChipsScrollViewer?.LineLeft();
    }

    private void CategoryScrollRight_Click(object sender, RoutedEventArgs e)
    {
        CategoryChipsScrollViewer?.LineRight();
        CategoryChipsScrollViewer?.LineRight();
    }

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        selectedCategory = (string)((Button)sender).Tag;
        RefreshCatalog();
    }

    private void ProductCard_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not Product product)
            return;

        AddProductToCart(product);
    }

    private void AddProductToCart(Product product)
    {
        if (product.Stock <= 0)
        {
            MessageBox.Show($"{product.Name} is out of stock.", "Stock unavailable",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var line = cart.FirstOrDefault(item => item.Product.Id == product.Id);
        if (line is null)
            cart.Add(new CartItem { Product = product });
        else if (line.Quantity < product.Stock)
            line.Quantity++;
        else
        {
            MessageBox.Show($"Only {product.Stock} units of {product.Name} are in stock.",
                "Stock limit", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        UpdateCartSummary();
    }

    private void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
    {
        var item = (CartItem)((Button)sender).Tag;
        if (item.Quantity >= item.Product.Stock)
        {
            MessageBox.Show($"Only {item.Product.Stock} units of {item.Product.Name} are in stock.",
                "Stock limit", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        item.Quantity++;
        UpdateCartSummary();
    }

    private void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
    {
        var item = (CartItem)((Button)sender).Tag;
        if (item.Quantity == 1)
            cart.Remove(item);
        else
            item.Quantity--;
        UpdateCartSummary();
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        cart.Remove((CartItem)((Button)sender).Tag);
        UpdateCartSummary();
    }

    private void ClearCart_Click(object sender, RoutedEventArgs e)
    {
        cart.Clear();
        UpdateCartSummary();
    }

    private void UpdateCartSummary()
    {
        if (cart.Count == 0)
        {
            pendingCustomerName = selectedCustomer?.Name ?? "Walking Customer";
            extraDiscountRate = 0m;
            activeCoupon = null;
        }
        var count = cart.Sum(item => item.Quantity);
        var grossTotal = cart.Sum(item => item.GrossTotal);
        var productDiscountTotal = cart.Sum(item => item.DiscountTotal);
        var cartSubtotal = cart.Sum(item => item.LineTotal);

        // Calculate extra discount
        decimal extraDiscount = 0m;
        if (extraDiscountType == "percent")
        {
            extraDiscount = Math.Round(cartSubtotal * (extraDiscountRate / 100m), 2);
        }
        else
        {
            extraDiscount = Math.Min(cartSubtotal, extraDiscountRate);
        }

        // Calculate coupon discount
        decimal couponDiscount = 0m;
        if (activeCoupon != null)
        {
            var eligibleSubtotal = Math.Max(0, cartSubtotal - extraDiscount);
            if (eligibleSubtotal >= activeCoupon.MinPurchase)
            {
                if (activeCoupon.DiscountType == "percent")
                    couponDiscount = Math.Round(eligibleSubtotal * (activeCoupon.Discount / 100m), 2);
                else
                    couponDiscount = Math.Min(eligibleSubtotal, activeCoupon.Discount);
            }
        }

        var totalDiscount = productDiscountTotal + extraDiscount + couponDiscount;
        var taxableAmount = Math.Max(0, grossTotal - totalDiscount);
        var gstAmount = Math.Round(taxableAmount * (currentGstRate / 100m), 2);
        var cgst = Math.Round(gstAmount / 2m, 2);
        var sgst = gstAmount - cgst;
        currentPayableTotal = taxableAmount + gstAmount;

        if (EmptyCartIllustration != null)
        {
            EmptyCartIllustration.Visibility = cart.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        if (CartItemsScrollViewer != null)
        {
            CartItemsScrollViewer.Visibility = cart.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        var culture = new System.Globalization.CultureInfo("en-IN");
        CartCountText.Text = $"{cart.Count} Items ({count} Units)";
        SubtotalText.Text = grossTotal.ToString("C2", culture);
        DiscountText.Text = totalDiscount > 0 ? $"- {totalDiscount.ToString("C2", culture)}" : "- ₹0.00";
        TotalText.Text = currentPayableTotal.ToString("C2", culture);
        GstText.Text = $"₹{gstAmount:N2}";
        CheckoutButton.IsEnabled = cart.Count > 0;
        CheckoutButton.Opacity = cart.Count > 0 ? 1 : 0.55;

        // Mirror to Customer-Facing Display if active
        if (customerDisplayWindow != null && customerDisplayWindow.IsLoaded)
        {
            customerDisplayWindow.UpdateCart(cart, grossTotal, totalDiscount, currentPayableTotal, gstAmount);
        }

        // Sync product catalog overlay quantities
        if (ProductCatalog?.ItemsSource is IEnumerable<Product> catalogProducts)
        {
            var cartLookup = cart.ToDictionary(c => c.Product.Id, c => c.Quantity);
            foreach (var prod in catalogProducts)
            {
                prod.CartQuantity = cartLookup.TryGetValue(prod.Id, out var qty) ? qty : 0;
            }
        }
    }

    private void Checkout_Click(object sender, RoutedEventArgs e)
    {
        if (cart.Count == 0)
            return;
        if (!EnsureOpenShift())
            return;

        var total = currentPayableTotal > 0 ? currentPayableTotal : cart.Sum(item => item.LineTotal);
        var checkout = new CheckoutWindow(total, pendingCustomerName, selectedCustomer?.Phone ?? "") { Owner = this };
        if (checkout.ShowDialog() != true)
            return;

        string receiptNumber;
        try
        {
            receiptNumber = database.SaveSale(
                cart, checkout.PaymentMethod, checkout.AmountPaid, checkout.CustomerName, activeShift!.Id, cashierName, cashierEmail);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show($"The sale could not be saved. No stock or sale changes were kept.\n\n{ex.Message}",
                "Checkout failed", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshCatalog();
            RefreshProducts();
            return;
        }

        checkout.LaunchWhatsAppReceipt(receiptNumber, total);

        cart.Clear();
        selectedCustomer = null;
        pendingCustomerName = "Walking Customer";
        extraDiscountRate = 0m;
        activeCoupon = null;
        if (CurrentCustomerText != null)
        {
            CurrentCustomerText.Text = "Walking Customer";
        }
        RefreshCatalog();
        RefreshProducts();
        RefreshSales();
        UpdateCartSummary();
        try
        {
            var sale = database.GetSales(DateTime.Today, cashierEmail).FirstOrDefault(item => item.ReceiptNumber == receiptNumber)
                       ?? database.GetSales().FirstOrDefault(item => item.ReceiptNumber == receiptNumber);
            var receipt = sale is null ? null : database.GetReceipt(sale.Id);
            if (receipt is null)
                throw new InvalidOperationException("The completed sale was saved, but its receipt details could not be loaded.");
            new ReceiptWindow(receipt) { Owner = this }.ShowDialog();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(
                $"The sale was completed and saved as {receiptNumber}, but its receipt could not be opened.\n\n{ex.Message}",
                "Receipt unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddProduct_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ProductEditorWindow { Owner = this };
        if (editor.ShowDialog() != true)
            return;

        try
        {
            database.AddProduct(editor.ProductName, editor.Category, editor.Sku,
                editor.Barcode, editor.Price, editor.Stock);
            RefreshCatalog();
            RefreshProducts();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            MessageBox.Show($"The product could not be saved. Check that its SKU is unique.\n\n{ex.Message}",
                "Product save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportProductsCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import products from CSV or Excel",
            Filter = "Product files (*.csv;*.xlsx)|*.csv;*.xlsx|CSV files (*.csv)|*.csv|Excel workbooks (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var result = productCsvImporter.Import(dialog.FileName, database);
            selectedCategory = "All";
            RefreshCatalog();
            RefreshProducts();
            MessageBox.Show(
                $"Product import completed.\n\nAdded: {result.Added}\nUpdated by SKU: {result.Updated}\n\nProducts not listed in the report were left unchanged. Reports without a SKU use a generated SKU based on product name.",
                "Product import complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException or ArgumentException or System.Xml.XmlException)
        {
            MessageBox.Show(
                $"No products were imported. Check the CSV or Excel report and try again.\n\n{ex.Message}",
                "Product import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowPos_Click(object sender, RoutedEventArgs e)
    {
        SetPage("Point of sale", "Browse your catalog and build a sale.", PosPanel);
        CartPanel.Visibility = Visibility.Visible;
        CartColumn.Width = new GridLength(380);
        SetNavigation(PosNav);
        RefreshCatalog();
    }

    private void ShowProducts_Click(object sender, RoutedEventArgs e)
    {
        SetPage("Products", "Manage your local product catalog and stock.", ProductsPanel);
        CartPanel.Visibility = Visibility.Collapsed;
        CartColumn.Width = new GridLength(0);
        SetNavigation(ProductsNav);
        RefreshProducts();
    }

    private void ShowSales_Click(object sender, RoutedEventArgs e)
    {
        ReceiptsModal.Visibility = Visibility.Visible;
        receiptWeekOffset = 0;
        selectedReceiptDate = DateTime.Today;
        RefreshSales();
    }

    private void CloseReceiptsModal_Click(object sender, RoutedEventArgs e)
    {
        ReceiptsModal.Visibility = Visibility.Collapsed;
    }

    private void OpeningCash_Click(object sender, RoutedEventArgs e)
    {
        RefreshShiftStatus();
        if (activeShift is not null)
        {
            new OpeningCashWindow(activeShift) { Owner = this }.ShowDialog();
            return;
        }

        var dialog = new OpeningCashWindow { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            database.OpenShift(dialog.OpeningCash, cashierName, cashierEmail);
            RefreshShiftStatus();
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show($"The shift could not be opened.\n\n{ex.Message}",
                "Opening cash failed", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshShiftStatus();
        }
    }

    private bool EnsureOpenShift()
    {
        RefreshShiftStatus();
        if (activeShift is not null)
            return true;

        var dialog = new OpeningCashWindow { Owner = this };
        if (dialog.ShowDialog() != true)
            return false;

        try
        {
            database.OpenShift(dialog.OpeningCash, cashierName, cashierEmail);
            RefreshShiftStatus();
            return activeShift is not null;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show($"The shift could not be opened. The sale was not started.\n\n{ex.Message}",
                "Opening cash failed", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshShiftStatus();
            return false;
        }
    }

    private void HoldOrders_Click(object sender, RoutedEventArgs e)
    {
        OpenHoldOrderListModal();
    }

    private void OpenHoldOrderListModal()
    {
        HoldOrderListModal.Visibility = Visibility.Visible;
        if (cart.Count > 0)
        {
            HoldCurrentCartBar.Visibility = Visibility.Visible;
            HoldCustomerNameBox.Text = pendingCustomerName;
            HoldCartItemsPreviewText.Text = $"({cart.Sum(i => i.Quantity)} items • {FormatMoney(cart.Sum(i => i.LineTotal))})";
        }
        else
        {
            HoldCurrentCartBar.Visibility = Visibility.Collapsed;
        }

        if (HoldOrderSearchBox != null)
        {
            HoldOrderSearchBox.Text = string.Empty;
        }
        RefreshHoldOrdersList();
    }

    private void CloseHoldOrderList_Click(object sender, RoutedEventArgs e)
    {
        HoldOrderListModal.Visibility = Visibility.Collapsed;
    }

    private void RefreshHoldOrdersList()
    {
        try
        {
            var search = HoldOrderSearchBox?.Text?.Trim() ?? "";
            var allHeld = database.GetHeldOrders(cashierEmail);
            var held = allHeld;
            if (!string.IsNullOrWhiteSpace(search))
            {
                held = allHeld.Where(o =>
                    o.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    o.Id.ToString().Contains(search) ||
                    o.Total.ToString().Contains(search)).ToList();
            }

            if (HoldCountText != null)
            {
                HoldCountText.Text = allHeld.Count.ToString();
            }

            if (held.Count == 0)
            {
                HoldOrderEmptyState.Visibility = Visibility.Visible;
                HoldOrdersGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                HoldOrderEmptyState.Visibility = Visibility.Collapsed;
                HoldOrdersGrid.Visibility = Visibility.Visible;
                HoldOrdersGrid.ItemsSource = held;
            }

            if (HoldOrdersCountText != null)
            {
                HoldOrdersCountText.Text = $"Showing {held.Count} Hold Order{(held.Count == 1 ? "" : "s")}";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading held orders: {ex.Message}");
        }
    }

    private void HoldOrderSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshHoldOrdersList();
    }

    private void HoldCurrentCart_Click(object sender, RoutedEventArgs e)
    {
        if (cart.Count == 0)
        {
            MessageBox.Show("There are no items in the cart to hold.", "Cart Empty", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var custName = HoldCustomerNameBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(custName)) custName = "Walking Customer";
            database.SaveHeldOrder(custName, cart, cashierName, cashierEmail);
            cart.Clear();
            selectedCustomer = null;
            pendingCustomerName = "Walking Customer";
            if (CurrentCustomerText != null) CurrentCustomerText.Text = "Walking Customer";
            UpdateCartSummary();
            RefreshHoldOrdersList();
            HoldCurrentCartBar.Visibility = Visibility.Collapsed;
            MessageBox.Show("Cart held successfully!", "Order Held", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to hold cart: {ex.Message}", "Hold Order Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResumeHoldOrderRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HeldOrderSummary selected)
            return;

        if (cart.Count > 0)
        {
            var confirm = MessageBox.Show(
                "Resuming this order will replace your current cart items. Do you want to continue?",
                "Replace Current Cart", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }

        try
        {
            var resumed = database.ResumeHeldOrder(selected.Id);
            cart.Clear();
            foreach (var item in resumed.Items)
            {
                cart.Add(item);
            }
            pendingCustomerName = resumed.CustomerName;
            if (CurrentCustomerText != null) CurrentCustomerText.Text = pendingCustomerName;
            UpdateCartSummary();
            HoldOrderListModal.Visibility = Visibility.Collapsed;
            RefreshHoldOrdersList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not resume held order: {ex.Message}", "Resume Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteHoldOrderRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HeldOrderSummary selected)
            return;

        var confirm = MessageBox.Show(
            $"Delete the held order for {selected.CustomerName} ({FormatMoney(selected.Total)})?",
            "Delete Held Order", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            database.DeleteHeldOrder(selected.Id);
            RefreshHoldOrdersList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not delete held order: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ModalBackdrop_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender && sender is Grid grid)
        {
            grid.Visibility = Visibility.Collapsed;
        }
    }

    private void DayEndButton_Click(object sender, RoutedEventArgs e)
    {
        if (DayEndButton.ContextMenu != null)
        {
            DayEndButton.ContextMenu.PlacementTarget = DayEndButton;
            DayEndButton.ContextMenu.IsOpen = true;
        }
    }

    private void ShiftSettlement_Click(object sender, RoutedEventArgs e)
    {
        OpeningCash_Click(sender, e);
    }

    private ShiftSettlement? currentShiftSettlement;
    private readonly ObservableCollection<CashDenominationCount> dayEndDenominations =
    [
        new() { Denomination = 2000, Label = "₹2,000.00" },
        new() { Denomination = 500,  Label = "₹500.00" },
        new() { Denomination = 200,  Label = "₹200.00" },
        new() { Denomination = 100,  Label = "₹100.00" },
        new() { Denomination = 50,   Label = "₹50.00" },
        new() { Denomination = 20,   Label = "₹20.00" },
        new() { Denomination = 10,   Label = "₹10.00" },
        new() { Denomination = 5,    Label = "Coins (₹5.00)" }
    ];

    private void DayEnd_Click(object sender, RoutedEventArgs e)
    {
        RefreshShiftStatus();
        if (activeShift is null)
        {
            MessageBox.Show("Open a cash shift before running day-end settlement.",
                "No open shift", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            currentShiftSettlement = database.GetShiftSettlement(activeShift.Id);
            if (currentShiftSettlement is null)
            {
                RefreshShiftStatus();
                MessageBox.Show("The active shift could not be found. Refresh and try again.",
                    "Settlement unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DayEndBusinessDateText.Text = currentShiftSettlement.BusinessDate.ToString("dd MMM yyyy - hh:mm tt");
            DayEndDayOfWeekText.Text = currentShiftSettlement.BusinessDate.ToString("dddd");
            DayEndHoursText.Text = $"{currentShiftSettlement.OpenedAt:hh:mm tt} - {DateTime.Now:hh:mm tt}";
            var duration = DateTime.Now - currentShiftSettlement.OpenedAt;
            DayEndDurationText.Text = $"{(int)duration.TotalHours}h {duration.Minutes}m";
            DayEndTotalOrdersText.Text = $"{currentShiftSettlement.OrderCount} ({currentShiftSettlement.ItemCount} Items)";
            DayEndCashierText.Text = cashierName;

            DayEndGrossSalesText.Text = FormatMoney(currentShiftSettlement.TotalSales);
            DayEndProductDiscountText.Text = "- ₹0.00";
            DayEndNetSalesText.Text = FormatMoney(currentShiftSettlement.TotalSales);

            DayEndPaymentsGrid.ItemsSource = currentShiftSettlement.Payments;
            DayEndExpectedCashText.Text = FormatMoney(currentShiftSettlement.ExpectedCash);
            DayEndOpeningCashText.Text = FormatMoney(currentShiftSettlement.OpeningCash);
            DayEndTodaySalesText.Text = FormatMoney(currentShiftSettlement.TotalSales);
            DayEndSummarySalesText.Text = FormatMoney(currentShiftSettlement.TotalSales);
            DayEndHighlightSalesText.Text = FormatMoney(currentShiftSettlement.TotalSales);

            DayEndDenominationsList.ItemsSource = dayEndDenominations;
            foreach (var d in dayEndDenominations)
            {
                d.Quantity = 0;
                d.PropertyChanged -= DayEndDenomination_PropertyChanged;
                d.PropertyChanged += DayEndDenomination_PropertyChanged;
            }
            UpdateDayEndCashSummary();

            DayEndSettlementModal.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load settlement details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DayEndDenomination_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CashDenominationCount.Quantity) or nameof(CashDenominationCount.Total))
        {
            UpdateDayEndCashSummary();
        }
    }

    private void UpdateDayEndCashSummary()
    {
        if (currentShiftSettlement is null) return;
        var counted = dayEndDenominations.Sum(d => d.Total);
        var difference = counted - currentShiftSettlement.ExpectedCash;

        DayEndTotalCashCountedText.Text = FormatMoney(counted);
        DayEndReconciliationCountedText.Text = FormatMoney(counted);
        DayEndDifferenceText.Text = FormatMoney(Math.Abs(difference));
        DayEndDifferenceText.Foreground = difference == 0
            ? new SolidColorBrush(Color.FromRgb(30, 142, 76))
            : new SolidColorBrush(Color.FromRgb(220, 38, 38));
    }

    private void CloseDayEndModal_Click(object sender, RoutedEventArgs e)
    {
        DayEndSettlementModal.Visibility = Visibility.Collapsed;
    }

    private void ConfirmDayEndSettlement_Click(object sender, RoutedEventArgs e)
    {
        if (currentShiftSettlement is null) return;
        var counted = dayEndDenominations.Sum(d => d.Total);
        var reason = (DayEndDifferenceReasonCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (reason == "Select ▾") reason = "";
        var notes = DayEndNotesBox.Text.Trim();
        if (notes == "Enter the notes...") notes = "";

        try
        {
            database.CloseShift(currentShiftSettlement, counted, reason, notes, dayEndDenominations);
            DayEndSettlementModal.Visibility = Visibility.Collapsed;
            RefreshShiftStatus();
            MessageBox.Show(
                $"Shift closed successfully!\n\nOrders: {currentShiftSettlement.OrderCount}\nTotal Sales: {FormatMoney(currentShiftSettlement.TotalSales)}\nCounted Cash: {FormatMoney(counted)}",
                "Settlement Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The shift could not be closed.\n\n{ex.Message}", "Settlement Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReceiptDate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateTime date })
            return;
        selectedReceiptDate = date.Date;
        RefreshSales();
    }

    private void PreviousReceiptWeek_Click(object sender, RoutedEventArgs e)
    {
        receiptWeekOffset++;
        selectedReceiptDate = DateTime.Today.AddDays(-7 * receiptWeekOffset);
        RefreshSales();
    }

    private void NextReceiptWeek_Click(object sender, RoutedEventArgs e)
    {
        if (receiptWeekOffset == 0)
            return;
        receiptWeekOffset--;
        selectedReceiptDate = DateTime.Today.AddDays(-7 * receiptWeekOffset);
        RefreshSales();
    }

    private void ViewReceipt_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SaleSummary sale)
            return;

        OpenReceipt(sale);
    }

    private void ReceiptActionMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void ReceiptPrintMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is SaleSummary sale)
        {
            OpenReceipt(sale);
        }
    }

    private void ReceiptRefundMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is SaleSummary sale)
        {
            OpenRefundOrder(sale.Id);
        }
    }

    private void OpenReceipt(SaleSummary sale)
    {
        try
        {
            var receipt = database.GetReceipt(sale.Id);
            if (receipt is null)
            {
                MessageBox.Show("This receipt could not be found.", "Receipt unavailable",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshSales();
                return;
            }
            new ReceiptWindow(receipt) { Owner = this }.ShowDialog();
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            MessageBox.Show($"The receipt could not be opened.\n\n{ex.Message}",
                "Receipt unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenRefundOrder(long saleId)
    {
        try
        {
            var summary = database.GetRefundOrderSummary(saleId);
            if (summary == null)
            {
                MessageBox.Show("Order could not be loaded for refund.", "Refund Unavailable",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (summary.RemainingRefundableAmount <= 0)
            {
                MessageBox.Show("This order has already been fully refunded.", "Fully Refunded",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            currentRefundSummary = summary;
            RefundOrderNumberText.Text = $"#{summary.ReceiptNumber}";
            RefundOrderItemCountText.Text = $"{summary.TotalItemCount} Items";
            RefundCustomerNameText.Text = summary.CustomerName;
            RefundCustomerPhoneText.Text = summary.CustomerPhone;
            RefundOrderDateText.Text = summary.CreatedAt.ToString("dd MMM yyyy");
            RefundOrderTimeText.Text = summary.CreatedAt.ToString("hh:mm tt");
            RefundPaymentMethodText.Text = summary.PaymentMethod;
            RefundOrderTotalText.Text = $"₹{summary.TotalAmount:N2}";
            RefundAsideMethodLabel.Text = $"Refund Amount ({summary.PaymentMethod})";

            // Hook up change events
            foreach (var item in summary.Items)
            {
                item.RefundQtyChanged += (s, ev) => UpdateRefundCalculations();
            }

            RefundItemsList.ItemsSource = summary.Items;
            RefundShowingText.Text = $"Showing 1 - {summary.Items.Count} of {summary.Items.Count} Order Items";

            RefundReasonComboBox.SelectedIndex = 0;
            RefundNotesTextBox.Text = "";
            RefundNotesCounterText.Text = "0/500";

            UpdateRefundCalculations();
            RefundOrderModal.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load refund details: {ex.Message}", "Refund Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateRefundCalculations()
    {
        if (currentRefundSummary == null) return;

        decimal totalRefund = currentRefundSummary.Items.Sum(i => i.RefundAmount);
        RefundTotalAmountText.Text = $"₹{totalRefund:N2}";
        decimal remaining = Math.Max(0, currentRefundSummary.RemainingRefundableAmount - totalRefund);
        RefundRemainingRefundableText.Text = $"Remaining refundable amount: ₹{remaining:N2}";

        ProcessRefundButton.IsEnabled = totalRefund > 0;
        ProcessRefundButton.Opacity = totalRefund > 0 ? 1.0 : 0.6;
    }

    private void RefundQtyMinus_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is RefundLineItem item)
        {
            if (item.RefundQty > 0)
            {
                item.RefundQty--;
            }
        }
    }

    private void RefundQtyPlus_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is RefundLineItem item)
        {
            if (item.RefundQty < item.RemainingQty)
            {
                item.RefundQty++;
            }
        }
    }

    private void RefundNotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (RefundNotesCounterText != null && RefundNotesTextBox != null)
        {
            RefundNotesCounterText.Text = $"{RefundNotesTextBox.Text.Length}/500";
        }
    }

    private void CloseRefundOrderModal_Click(object sender, RoutedEventArgs e)
    {
        RefundOrderModal.Visibility = Visibility.Collapsed;
        currentRefundSummary = null;
    }

    private void ProcessRefund_Click(object sender, RoutedEventArgs e)
    {
        if (currentRefundSummary == null) return;

        var selectedItems = currentRefundSummary.Items.Where(i => i.RefundQty > 0).ToList();
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Please select at least one item and quantity to refund.", "No Items Selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reason = (RefundReasonComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(reason) || reason == "Select a Reason")
        {
            MessageBox.Show("Please select a valid reason for the refund.", "Reason Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            RefundReasonComboBox.Focus();
            return;
        }

        var totalRefund = selectedItems.Sum(i => i.RefundAmount);
        var result = MessageBox.Show(
            $"Are you sure you want to process a refund of ₹{totalRefund:N2} for Order #{currentRefundSummary.ReceiptNumber}?\n\nThis will restore the product stock and cannot be undone.",
            "Confirm Refund",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var notes = RefundNotesTextBox.Text.Trim();
        var (success, message, refundedAmount) = database.ProcessRefund(
            currentRefundSummary.OrderId,
            currentRefundSummary.Items,
            reason,
            notes,
            cashierName);

        if (!success)
        {
            MessageBox.Show($"Refund could not be processed:\n{message}", "Refund Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            $"Refund of ₹{refundedAmount:N2} processed successfully!\nProduct stock has been restored to the catalog.",
            "Refund Completed",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        RefundOrderModal.Visibility = Visibility.Collapsed;
        currentRefundSummary = null;

        RefreshCatalog();
        RefreshProducts();
        RefreshSales();
    }

    private void SetPage(string title, string subtitle, UIElement activePanel)
    {
        PosPanel.Visibility = Visibility.Collapsed;
        ProductsPanel.Visibility = Visibility.Collapsed;
        SalesPanel.Visibility = Visibility.Collapsed;
        activePanel.Visibility = Visibility.Visible;
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
    }

    private void SetNavigation(Button active)
    {
        foreach (var button in new[] { PosNav, ProductsNav, SalesNav })
        {
            button.Background = button == active
                ? new SolidColorBrush(Color.FromRgb(230, 247, 235))
                : Brushes.White;
            button.BorderBrush = button == active
                ? new SolidColorBrush(Color.FromRgb(189, 231, 205))
                : new SolidColorBrush(Color.FromRgb(229, 230, 232));
            button.Foreground = button == active
                ? new SolidColorBrush(Color.FromRgb(30, 142, 76))
                : new SolidColorBrush(Color.FromRgb(65, 67, 74));
        }
    }

    private static string FormatMoney(decimal amount) =>
        amount.ToString("C2", new System.Globalization.CultureInfo("en-IN"));

    #region Product Card Stepper Overlay Handlers
    private void CardIncreaseQuantity_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Product? product = (sender as FrameworkElement)?.Tag as Product
            ?? (sender as FrameworkElement)?.DataContext as Product;
        if (product is null) return;

        AddProductToCart(product);
    }

    private void CardDecreaseQuantity_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Product? product = (sender as FrameworkElement)?.Tag as Product
            ?? (sender as FrameworkElement)?.DataContext as Product;
        if (product is null) return;

        var line = cart.FirstOrDefault(item => item.Product.Id == product.Id);
        if (line is not null)
        {
            if (line.Quantity <= 1)
                cart.Remove(line);
            else
                line.Quantity--;
            UpdateCartSummary();
        }
    }
    #endregion

    #region Customer Management Handlers
    private void OpenAddCustomer_Click(object sender, RoutedEventArgs e)
    {
        NewCustomerNameBox.Clear();
        NewCustomerPhoneBox.Clear();
        NewCustomerEmailBox.Clear();
        NewCustomerAddressBox.Clear();
        LoadRecentCustomers();
        AddCustomerModal.Visibility = Visibility.Visible;
        NewCustomerNameBox.Focus();
    }

    private void CloseAddCustomer_Click(object sender, RoutedEventArgs e)
    {
        AddCustomerModal.Visibility = Visibility.Collapsed;
    }

    private void SubmitNewCustomer_Click(object sender, RoutedEventArgs e)
    {
        var name = NewCustomerNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter the customer's full name.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NewCustomerNameBox.Focus();
            return;
        }

        var phone = NewCustomerPhoneBox.Text.Trim();
        var email = NewCustomerEmailBox.Text.Trim();
        var address = NewCustomerAddressBox.Text.Trim();

        try
        {
            var customer = database.AddCustomer(name, phone, email, address);
            SelectCustomer(customer);
            AddCustomerModal.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save customer: {ex.Message}", "Customer Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectCustomer(Customer customer)
    {
        selectedCustomer = customer;
        pendingCustomerName = customer.Name;
        if (CurrentCustomerText != null)
        {
            CurrentCustomerText.Text = !string.IsNullOrWhiteSpace(customer.Phone)
                ? $"{customer.Name} ({customer.Phone})"
                : customer.Name;
        }
    }

    private void LoadRecentCustomers()
    {
        if (RecentCustomersPanel == null) return;
        RecentCustomersPanel.Children.Clear();

        var customers = database.GetCustomers();
        foreach (var customer in customers.Take(8))
        {
            var chip = new Button
            {
                Content = $"{customer.Name}{(string.IsNullOrWhiteSpace(customer.Phone) ? "" : " • " + customer.Phone)}",
                Tag = customer,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = (Style)FindResource("FlatButton")
            };
            chip.Click += (s, e) =>
            {
                if (s is Button btn && btn.Tag is Customer cust)
                {
                    SelectCustomer(cust);
                    AddCustomerModal.Visibility = Visibility.Collapsed;
                }
            };
            RecentCustomersPanel.Children.Add(chip);
        }
    }

    private void CustomerSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var phoneQuery = NewCustomerPhoneBox?.Text?.Trim() ?? "";
        var nameQuery = NewCustomerNameBox?.Text?.Trim() ?? "";
        var query = !string.IsNullOrWhiteSpace(phoneQuery) ? phoneQuery : nameQuery;

        if (RecentCustomersPanel == null) return;
        RecentCustomersPanel.Children.Clear();
        var matched = database.GetCustomers(string.IsNullOrWhiteSpace(query) ? null : query);

        foreach (var customer in matched.Take(10))
        {
            var chip = new Button
            {
                Content = $"{customer.Name}{(string.IsNullOrWhiteSpace(customer.Phone) ? "" : " • " + customer.Phone)}",
                Tag = customer,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = (Style)FindResource("FlatButton")
            };
            chip.Click += (s, ev) =>
            {
                if (s is Button btn && btn.Tag is Customer cust)
                {
                    SelectCustomer(cust);
                    AddCustomerModal.Visibility = Visibility.Collapsed;
                }
            };
            RecentCustomersPanel.Children.Add(chip);
        }
    }
    #endregion

    #region Tax Management (GST Slabs)
    private void GstSlab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag && decimal.TryParse(tag, out var rate))
        {
            currentGstRate = rate;
            UpdateCartSummary();
        }
    }
    #endregion

    #region Customer-Facing Display (CFD)
    private void ToggleCustomerDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (customerDisplayWindow == null || !customerDisplayWindow.IsLoaded)
        {
            customerDisplayWindow = new CustomerDisplayWindow();
            if (SystemParameters.VirtualScreenWidth > SystemParameters.PrimaryScreenWidth)
            {
                customerDisplayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                customerDisplayWindow.Left = SystemParameters.PrimaryScreenWidth + 20;
                customerDisplayWindow.Top = 40;
                customerDisplayWindow.WindowState = WindowState.Maximized;
            }
            customerDisplayWindow.Show();
            UpdateCartSummary();
        }
        else
        {
            customerDisplayWindow.Close();
            customerDisplayWindow = null;
        }
    }
    #endregion

    #region Desk Calculator Handlers
    private void ToggleCalculator_Click(object sender, RoutedEventArgs e)
    {
        CalculatorModal.Visibility = CalculatorModal.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CloseCalculator_Click(object sender, RoutedEventArgs e)
    {
        CalculatorModal.Visibility = Visibility.Collapsed;
    }

    private void CalcBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var text = btn.Content.ToString() ?? "";

        if (text is "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9")
        {
            if (calcIsNewEntry || calcDisplay == "0")
            {
                calcDisplay = text;
                calcIsNewEntry = false;
            }
            else
            {
                calcDisplay += text;
            }
            CalcDisplayText.Text = calcDisplay;
        }
        else if (text == ".")
        {
            if (calcIsNewEntry)
            {
                calcDisplay = "0.";
                calcIsNewEntry = false;
            }
            else if (!calcDisplay.Contains('.'))
            {
                calcDisplay += ".";
            }
            CalcDisplayText.Text = calcDisplay;
        }
        else if (text == "C")
        {
            calcDisplay = "0";
            calcFirstOperand = 0m;
            calcPendingOp = null;
            calcIsNewEntry = true;
            CalcDisplayText.Text = "0";
            CalcHistoryText.Text = "";
        }
        else if (text == "CE")
        {
            calcDisplay = "0";
            calcIsNewEntry = true;
            CalcDisplayText.Text = "0";
        }
        else if (text == "⌫")
        {
            if (!calcIsNewEntry && calcDisplay.Length > 1)
            {
                calcDisplay = calcDisplay.Substring(0, calcDisplay.Length - 1);
            }
            else
            {
                calcDisplay = "0";
                calcIsNewEntry = true;
            }
            CalcDisplayText.Text = calcDisplay;
        }
        else if (text is "+" or "-" or "×" or "÷")
        {
            decimal.TryParse(calcDisplay, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var currentVal);
            calcFirstOperand = currentVal;
            calcPendingOp = text;
            calcIsNewEntry = true;
            CalcHistoryText.Text = $"{calcFirstOperand} {calcPendingOp}";
        }
        else if (text == "%")
        {
            if (decimal.TryParse(calcDisplay, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
            {
                val /= 100m;
                calcDisplay = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
                CalcDisplayText.Text = calcDisplay;
            }
        }
        else if (text == "=")
        {
            if (calcPendingOp != null && decimal.TryParse(calcDisplay, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var secondVal))
            {
                decimal result = calcPendingOp switch
                {
                    "+" => calcFirstOperand + secondVal,
                    "-" => calcFirstOperand - secondVal,
                    "×" => calcFirstOperand * secondVal,
                    "÷" => secondVal != 0 ? calcFirstOperand / secondVal : 0m,
                    _ => secondVal
                };
                CalcHistoryText.Text = $"{calcFirstOperand} {calcPendingOp} {secondVal} =";
                calcDisplay = result.ToString(System.Globalization.CultureInfo.InvariantCulture);
                CalcDisplayText.Text = calcDisplay;
                calcPendingOp = null;
                calcIsNewEntry = true;
            }
        }
    }

    private void CopyCalcValue_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(calcDisplay); } catch { }
        ScanBox.Text = calcDisplay;
        CalculatorModal.Visibility = Visibility.Collapsed;
        ScanBox.Focus();
        ScanBox.SelectAll();
    }
    #endregion

    #region Theme Toggle (Light / Dark Mode)
    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        isDarkMode = !isDarkMode;
        ApplyTheme(isDarkMode);
    }

    private void ApplyTheme(bool dark)
    {
        if (dark)
        {
            Background = new SolidColorBrush(Color.FromRgb(18, 24, 38)); // Dark navy / slate
            ThemeToggleButton.Content = "☀️";
            ThemeToggleButton.ToolTip = "Switch to Light Mode";
        }
        else
        {
            Background = new SolidColorBrush(Color.FromRgb(239, 239, 239)); // Light gray
            ThemeToggleButton.Content = "🌙";
            ThemeToggleButton.ToolTip = "Switch to Dark Mode";
        }
    }
    #endregion

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        try
        {
            scannerGateway?.Stop();
            scannerGateway?.Dispose();
        }
        catch { }

        try
        {
            customerDisplayWindow?.Close();
        }
        catch { }
    }

    #region Shortcut Keys Modal Handlers
    private void OpenShortCutKeys_Click(object sender, RoutedEventArgs e)
    {
        ShortCutKeysModal.Visibility = Visibility.Visible;
    }

    private void CloseShortCutKeys_Click(object sender, RoutedEventArgs e)
    {
        ShortCutKeysModal.Visibility = Visibility.Collapsed;
    }

    private void ToggleShortCutKeys()
    {
        ShortCutKeysModal.Visibility = ShortCutKeysModal.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Coupon_Click(object sender, RoutedEventArgs e) => OpenCoupon_Click(sender, e);
    private void Discount_Click(object sender, RoutedEventArgs e) => OpenExtraDiscount_Click(sender, e);

    private void PromptExtraDiscount() => OpenExtraDiscount_Click(this, new RoutedEventArgs());
    private void PromptCouponDiscount() => OpenCoupon_Click(this, new RoutedEventArgs());

    #region Add Product (Quick Add) Modal Handlers
    public void OpenAddProduct_Click(object sender, RoutedEventArgs e)
    {
        PopulateAddProductForm();
        AddProductModal.Visibility = Visibility.Visible;
        NewProductNameBox.Focus();
    }

    public void CloseAddProduct_Click(object sender, RoutedEventArgs e)
    {
        AddProductModal.Visibility = Visibility.Collapsed;
    }

    private void PopulateAddProductForm()
    {
        // Populate Categories
        var categories = database.GetCategories().Where(c => c != "All").ToList();
        if (categories.Count == 0) categories.Add("Sarees");
        NewProductCategoryCombo.ItemsSource = categories;
        if (categories.Count > 0 && NewProductCategoryCombo.SelectedIndex < 0)
            NewProductCategoryCombo.SelectedIndex = 0;

        // Sub categories
        var subCats = new List<string> { "Party Wear Sarees", "Silk Sarees", "Casual Cotton", "Festival Special", "Printed Georgette" };
        NewProductSubCategoryCombo.ItemsSource = subCats;
        if (NewProductSubCategoryCombo.SelectedIndex < 0) NewProductSubCategoryCombo.SelectedIndex = 0;

        // Brands
        var brands = new List<string> { "Saraa Boutique", "Saraa Exclusive", "Herbindra", "Heritage Weaves" };
        NewProductBrandCombo.ItemsSource = brands;
        if (NewProductBrandCombo.SelectedIndex < 0) NewProductBrandCombo.SelectedIndex = 0;

        // Units
        var units = new List<string> { "pcs", "set", "meter", "box", "pair" };
        NewProductUnitCombo.ItemsSource = units;
        if (NewProductUnitCombo.SelectedIndex < 0) NewProductUnitCombo.SelectedIndex = 0;

        if (string.IsNullOrWhiteSpace(NewProductBarcodeBox.Text))
            GenerateBarcode_Click(this, new RoutedEventArgs());
        if (string.IsNullOrWhiteSpace(NewProductSkuBox.Text))
            GenerateSku_Click(this, new RoutedEventArgs());
    }

    private void GenerateBarcode_Click(object sender, RoutedEventArgs e)
    {
        var random = new Random();
        NewProductBarcodeBox.Text = $"890{random.Next(100000000, 999999999)}";
    }

    private void GenerateSku_Click(object sender, RoutedEventArgs e)
    {
        var maxId = database.GetProducts().Count + 1;
        NewProductSkuBox.Text = $"SB{maxId:D4}";
    }

    private void SaveNewProduct_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProductNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter product name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NewProductNameBox.Focus();
            return;
        }

        var sku = NewProductSkuBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sku))
        {
            MessageBox.Show("Please enter product SKU.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NewProductSkuBox.Focus();
            return;
        }

        var barcode = NewProductBarcodeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
            barcode = sku;

        var category = NewProductCategoryCombo.Text?.Trim();
        if (string.IsNullOrWhiteSpace(category))
            category = "Sarees";

        if (!decimal.TryParse(NewProductPriceBox.Text.Trim(), out var price) || price <= 0)
        {
            MessageBox.Show("Please enter a valid selling price.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NewProductPriceBox.Focus();
            return;
        }

        int.TryParse(NewProductStockBox.Text.Trim(), out var stock);
        if (stock <= 0) stock = 10;

        try
        {
            decimal originalPrice = price + 100m;
            decimal discount = 100m;
            string discountType = "flat";
            database.AddProduct(name, category, sku, barcode, price, stock, null, originalPrice, discount, discountType);
            RefreshCatalog();
            RefreshProducts();
            AddProductModal.Visibility = Visibility.Collapsed;
            MessageBox.Show($"Product '{name}' saved successfully and added to catalog!", "Product Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save product: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    #endregion

    #region Extra Discount Modal Handlers
    public void OpenExtraDiscount_Click(object sender, RoutedEventArgs e)
    {
        UpdateExtraDiscountPreview();
        ExtraDiscountModal.Visibility = Visibility.Visible;
        DiscountValueBox.Focus();
        DiscountValueBox.SelectAll();
    }

    public void CloseExtraDiscount_Click(object sender, RoutedEventArgs e)
    {
        ExtraDiscountModal.Visibility = Visibility.Collapsed;
    }

    private void DiscountTabPercent_Click(object sender, RoutedEventArgs e)
    {
        extraDiscountType = "percent";
        DiscountTabPercent.Background = new SolidColorBrush(Color.FromRgb(47, 63, 184));
        DiscountTabPercent.Foreground = Brushes.White;
        DiscountTabPercent.BorderBrush = new SolidColorBrush(Color.FromRgb(47, 63, 184));

        DiscountTabAmount.Background = Brushes.White;
        DiscountTabAmount.Foreground = new SolidColorBrush(Color.FromRgb(47, 63, 184));
        DiscountTabAmount.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));

        DiscountInputLabel.Text = "Discount Percentage";
        QuickDiscountLabel.Text = "Exact Percentage";
        DiscountPrefixBorder.Visibility = Visibility.Collapsed;
        DiscountSuffixBorder.Visibility = Visibility.Visible;

        QuickDiscBtn1.Content = "5%"; QuickDiscBtn1.Tag = "5";
        QuickDiscBtn2.Content = "10%"; QuickDiscBtn2.Tag = "10";
        QuickDiscBtn3.Content = "15%"; QuickDiscBtn3.Tag = "15";
        QuickDiscBtn4.Content = "20%"; QuickDiscBtn4.Tag = "20";

        DiscountValueBox.Text = "10";
        UpdateExtraDiscountPreview();
    }

    private void DiscountTabAmount_Click(object sender, RoutedEventArgs e)
    {
        extraDiscountType = "amount";
        DiscountTabAmount.Background = new SolidColorBrush(Color.FromRgb(47, 63, 184));
        DiscountTabAmount.Foreground = Brushes.White;
        DiscountTabAmount.BorderBrush = new SolidColorBrush(Color.FromRgb(47, 63, 184));

        DiscountTabPercent.Background = Brushes.White;
        DiscountTabPercent.Foreground = new SolidColorBrush(Color.FromRgb(47, 63, 184));
        DiscountTabPercent.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));

        DiscountInputLabel.Text = "Discount Amount";
        QuickDiscountLabel.Text = "Exact Amount";
        DiscountPrefixBorder.Visibility = Visibility.Visible;
        DiscountSuffixBorder.Visibility = Visibility.Collapsed;

        QuickDiscBtn1.Content = "₹50"; QuickDiscBtn1.Tag = "50";
        QuickDiscBtn2.Content = "₹100"; QuickDiscBtn2.Tag = "100";
        QuickDiscBtn3.Content = "₹200"; QuickDiscBtn3.Tag = "200";
        QuickDiscBtn4.Content = "₹500"; QuickDiscBtn4.Tag = "500";

        DiscountValueBox.Text = "100";
        UpdateExtraDiscountPreview();
    }

    private void QuickDiscount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            if (tag != "custom")
            {
                DiscountValueBox.Text = tag;
            }
            HighlightQuickPill(btn);
            UpdateExtraDiscountPreview();
        }
    }

    private void HighlightQuickPill(Button selectedBtn)
    {
        var pills = new[] { QuickDiscBtn1, QuickDiscBtn2, QuickDiscBtn3, QuickDiscBtn4, QuickDiscBtn5 };
        foreach (var p in pills)
        {
            if (p == null) continue;
            if (p == selectedBtn)
            {
                p.Background = new SolidColorBrush(Color.FromRgb(47, 63, 184));
                p.Foreground = Brushes.White;
                p.BorderBrush = new SolidColorBrush(Color.FromRgb(47, 63, 184));
            }
            else
            {
                p.Background = Brushes.White;
                p.Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81));
                p.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));
            }
        }
    }

    private void DiscountValueBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateExtraDiscountPreview();
    }

    private void UpdateExtraDiscountPreview()
    {
        if (PreviewSubtotalText == null || PreviewDiscountText == null || PreviewFinalText == null)
            return;

        var subtotal = cart.Sum(i => i.LineTotal);
        var culture = new System.Globalization.CultureInfo("en-IN");
        decimal.TryParse(DiscountValueBox?.Text?.Trim(), out var val);

        decimal discount = 0m;
        if (extraDiscountType == "percent")
        {
            discount = Math.Round(subtotal * (val / 100m), 2);
            PreviewDiscountLabel.Text = $"Discount ({val}%)";
        }
        else
        {
            discount = Math.Min(subtotal, val);
            PreviewDiscountLabel.Text = "Discount (Flat)";
        }

        var finalAmount = Math.Max(0, subtotal - discount);
        PreviewSubtotalText.Text = subtotal.ToString("C2", culture);
        PreviewDiscountText.Text = $"- {discount.ToString("C2", culture)}";
        PreviewFinalText.Text = finalAmount.ToString("C2", culture);
    }

    private void ApplyExtraDiscount_Click(object sender, RoutedEventArgs e)
    {
        decimal.TryParse(DiscountValueBox.Text.Trim(), out extraDiscountRate);
        UpdateCartSummary();
        ExtraDiscountModal.Visibility = Visibility.Collapsed;
    }
    #endregion

    #region Coupon Modal Handlers
    public void OpenCoupon_Click(object sender, RoutedEventArgs e)
    {
        PopulateCouponsList();
        CouponModal.Visibility = Visibility.Visible;
        CouponCodeInputBox.Focus();
    }

    public void CloseCoupon_Click(object sender, RoutedEventArgs e)
    {
        CouponModal.Visibility = Visibility.Collapsed;
    }

    private void PopulateCouponsList()
    {
        if (CouponsListPanel == null) return;
        CouponsListPanel.Children.Clear();
        ExistingCouponsHeader.Text = $"Existing Coupon Codes ({availableCoupons.Count})";

        foreach (var coupon in availableCoupons)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(241, 242, 246)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 8, 0, 8)
            };
            var dock = new DockPanel();

            // Code capsule
            var codeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(238, 241, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 230, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Width = 100,
                Margin = new Thickness(0, 0, 10, 0)
            };
            codeBorder.Child = new TextBlock
            {
                Text = coupon.Code,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            dock.Children.Add(codeBorder);

            // Apply button
            var applyBtn = new Button
            {
                Content = activeCoupon?.Code == coupon.Code ? "Applied ✓" : "Apply",
                Tag = coupon,
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                Background = activeCoupon?.Code == coupon.Code
                    ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
                    : new SolidColorBrush(Color.FromRgb(47, 63, 184)),
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Style = (Style)FindResource("FlatButton"),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            DockPanel.SetDock(applyBtn, Dock.Right);
            applyBtn.Click += (s, ev) =>
            {
                if (s is Button b && b.Tag is Coupon c)
                {
                    ApplyCoupon(c);
                }
            };
            dock.Children.Add(applyBtn);

            // Middle info
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = coupon.Title,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55))
            });
            var discountStr = coupon.DiscountType == "percent" ? $"{coupon.Discount}% Off" : $"₹{coupon.Discount} Off";
            stack.Children.Add(new TextBlock
            {
                Text = $"{discountStr} • Min purchase ₹{coupon.MinPurchase:N0}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
            });
            dock.Children.Add(stack);

            border.Child = dock;
            CouponsListPanel.Children.Add(border);
        }
    }

    private void ApplyCouponCode_Click(object sender, RoutedEventArgs e)
    {
        var code = CouponCodeInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show("Please enter a coupon code.", "Coupon", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var found = availableCoupons.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        if (found == null)
        {
            // Allow ad-hoc coupon code
            found = new Coupon { Code = code.ToUpperInvariant(), Title = $"Promo: {code}", DiscountType = "percent", Discount = 10, MinPurchase = 0 };
        }

        ApplyCoupon(found);
    }

    private void ApplyCoupon(Coupon coupon)
    {
        var subtotal = cart.Sum(i => i.LineTotal);
        if (subtotal < coupon.MinPurchase)
        {
            MessageBox.Show($"This coupon requires a minimum cart purchase of ₹{coupon.MinPurchase:N2}.\nCurrent subtotal: ₹{subtotal:N2}.", "Coupon Condition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        activeCoupon = coupon;
        UpdateCartSummary();
        CouponModal.Visibility = Visibility.Collapsed;
        MessageBox.Show($"Coupon '{coupon.Code}' applied successfully!", "Coupon Applied", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    #region Screen Window Controls (Minimize, Expandable, Close)
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (MaximizeIconText != null) MaximizeIconText.Text = "🗖";
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (MaximizeIconText != null) MaximizeIconText.Text = "🗗";
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (MaximizeIconText != null)
        {
            MaximizeIconText.Text = WindowState == WindowState.Maximized ? "🗗" : "🗖";
        }
    }
    #endregion
    #endregion

    #region Keyboard Shortcuts PreviewKeyDown
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.SystemKey != System.Windows.Input.Key.None ? e.SystemKey : e.Key;
        var alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;

        // Escape closes any open modal
        if (key == System.Windows.Input.Key.Escape)
        {
            if (CalculatorModal.Visibility == Visibility.Visible)
            {
                CalculatorModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (AddProductModal.Visibility == Visibility.Visible)
            {
                AddProductModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (ExtraDiscountModal.Visibility == Visibility.Visible)
            {
                ExtraDiscountModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (CouponModal.Visibility == Visibility.Visible)
            {
                CouponModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (ShortCutKeysModal.Visibility == Visibility.Visible)
            {
                ShortCutKeysModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (HoldOrderListModal.Visibility == Visibility.Visible)
            {
                HoldOrderListModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (ReceiptsModal.Visibility == Visibility.Visible)
            {
                ReceiptsModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (RefundOrderModal.Visibility == Visibility.Visible)
            {
                RefundOrderModal.Visibility = Visibility.Collapsed;
                currentRefundSummary = null;
                e.Handled = true;
                return;
            }
            if (DayEndSettlementModal.Visibility == Visibility.Visible)
            {
                DayEndSettlementModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (AddCustomerModal.Visibility == Visibility.Visible)
            {
                AddCustomerModal.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (SalesPanel.Visibility == Visibility.Visible || ProductsPanel.Visibility == Visibility.Visible)
            {
                ShowPos_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }

        if (alt)
        {
            switch (key)
            {
                // Alt + O: To click order / pay & tender
                case System.Windows.Input.Key.O:
                    if (cart.Count > 0)
                    {
                        Checkout_Click(this, new RoutedEventArgs());
                    }
                    e.Handled = true;
                    break;

                // Alt + S: To click payment submit
                case System.Windows.Input.Key.S:
                    if (cart.Count > 0)
                    {
                        Checkout_Click(this, new RoutedEventArgs());
                    }
                    e.Handled = true;
                    break;

                // Alt + Z: To close payment submit / modal
                case System.Windows.Input.Key.Z:
                    if (RefundOrderModal.Visibility == Visibility.Visible)
                    {
                        RefundOrderModal.Visibility = Visibility.Collapsed;
                        currentRefundSummary = null;
                    }
                    if (HoldOrderListModal.Visibility == Visibility.Visible)
                        HoldOrderListModal.Visibility = Visibility.Collapsed;
                    if (ReceiptsModal.Visibility == Visibility.Visible)
                        ReceiptsModal.Visibility = Visibility.Collapsed;
                    if (DayEndSettlementModal.Visibility == Visibility.Visible)
                        DayEndSettlementModal.Visibility = Visibility.Collapsed;
                    if (CalculatorModal.Visibility == Visibility.Visible)
                        CalculatorModal.Visibility = Visibility.Collapsed;
                    if (AddProductModal.Visibility == Visibility.Visible)
                        AddProductModal.Visibility = Visibility.Collapsed;
                    if (ExtraDiscountModal.Visibility == Visibility.Visible)
                        ExtraDiscountModal.Visibility = Visibility.Collapsed;
                    if (CouponModal.Visibility == Visibility.Visible)
                        CouponModal.Visibility = Visibility.Collapsed;
                    if (ShortCutKeysModal.Visibility == Visibility.Visible)
                        ShortCutKeysModal.Visibility = Visibility.Collapsed;
                    if (AddCustomerModal.Visibility == Visibility.Visible)
                        AddCustomerModal.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    break;

                // Alt + C: Toggle POS Calculator
                case System.Windows.Input.Key.C:
                    ToggleCalculator_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + M: Toggle Customer-Facing Display (CFD)
                case System.Windows.Input.Key.M:
                    ToggleCustomerDisplay_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + T: Toggle Light / Dark Theme
                case System.Windows.Input.Key.T:
                    ToggleTheme_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + A: To click add new customer
                case System.Windows.Input.Key.A:
                    OpenAddCustomer_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + N: To submit add new customer form
                case System.Windows.Input.Key.N:
                    if (AddCustomerModal.Visibility == Visibility.Visible)
                        SubmitNewCustomer_Click(this, new RoutedEventArgs());
                    else
                        OpenAddCustomer_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + K: To click short cut keys
                case System.Windows.Input.Key.K:
                    ToggleShortCutKeys();
                    e.Handled = true;
                    break;

                // Alt + P: To print invoice / view sales receipts
                case System.Windows.Input.Key.P:
                    ShowSales_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + B: To cancel invoice / return to POS billing
                case System.Windows.Input.Key.B:
                    ShowPos_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + Q: To focus search input
                case System.Windows.Input.Key.Q:
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    e.Handled = true;
                    break;

                // Alt + E: To click extra discount
                case System.Windows.Input.Key.E:
                    OpenExtraDiscount_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + D: To click coupon discount
                case System.Windows.Input.Key.D:
                    OpenCoupon_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + X: To click clear cart
                case System.Windows.Input.Key.X:
                    ClearCart_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // Alt + R: To click new order
                case System.Windows.Input.Key.R:
                    ClearCart_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (key)
            {
                // F1: Focus barcode scanner
                case System.Windows.Input.Key.F1:
                    ScanBox.Focus();
                    ScanBox.SelectAll();
                    e.Handled = true;
                    break;

                // F2: Add product
                case System.Windows.Input.Key.F2:
                    OpenAddProduct_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // F4: Hold order
                case System.Windows.Input.Key.F4:
                    HoldOrders_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                // F12: Pay / Tender
                case System.Windows.Input.Key.F12:
                    if (cart.Count > 0)
                    {
                        Checkout_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
    #endregion
}

