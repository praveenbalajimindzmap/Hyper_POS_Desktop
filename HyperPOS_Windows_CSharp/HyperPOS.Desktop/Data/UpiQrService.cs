using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace HyperPOS.Desktop.Data;

public static class UpiQrService
{
    public static string BuildUpiPayload(string vpa, string merchantName, decimal amount, string invoiceNumber)
    {
        // Standard NPCI UPI URI specifications
        var encodedName = Uri.EscapeDataString(merchantName);
        var encodedNote = Uri.EscapeDataString($"Invoice {invoiceNumber}");
        return $"upi://pay?pa={vpa.Trim()}&pn={encodedName}&am={amount:F2}&cu=INR&tn={encodedNote}&tr={invoiceNumber}";
    }

    public static BitmapImage GenerateQrBitmap(string payload, int pixelsPerModule = 10)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(pixelsPerModule);

        var bitmapImage = new BitmapImage();
        using (var stream = new MemoryStream(qrCodeBytes))
        {
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
        }
        bitmapImage.Freeze(); // Makes cross-thread rendering fast and safe
        return bitmapImage;
    }
}
