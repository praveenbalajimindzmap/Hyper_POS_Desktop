using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using HyperPOS.Desktop.Data;
using HyperPOS.Desktop.Models;

namespace HyperPOS.Desktop;

public partial class CustomerDisplayWindow : Window
{
    public CustomerDisplayWindow()
    {
        InitializeComponent();
    }

    public void UpdateCart(IEnumerable<CartItem> items, decimal subtotal, decimal discount, decimal total, decimal tax = 0)
    {
        var itemList = items.ToList();
        CartItemsList.ItemsSource = itemList;

        var totalQty = itemList.Sum(i => i.Quantity);
        ItemCountText.Text = $"{itemList.Count} Items ({totalQty} Units)";

        SubtotalText.Text = $"₹{subtotal:N2}";
        DiscountSavingsText.Text = discount > 0 ? $"- ₹{discount:N2}" : "₹0.00";
        TotalPayableText.Text = $"₹{total:N2}";

        // If there's an active total, refresh the dynamic UPI QR code
        if (total > 0)
        {
            var payload = UpiQrService.BuildUpiPayload("9974692496@okbizaxis", "Saraa Boutique", total, $"SB-{DateTime.Now:yyyyMMddHHmmss}");
            UpiQrImage.Source = UpiQrService.GenerateQrBitmap(payload, 8);
            StatusBadge.Text = "● Ready to Pay";
        }
        else
        {
            UpiQrImage.Source = null;
            StatusBadge.Text = "● Welcome to Saraa Boutique";
        }
    }

    public void SetQrCode(BitmapImage qrImage)
    {
        UpiQrImage.Source = qrImage;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Don't dispose on window close if user closes CFD, allow re-opening from POS
        base.OnClosing(e);
    }
}
