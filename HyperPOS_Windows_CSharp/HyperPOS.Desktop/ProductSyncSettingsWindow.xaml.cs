using System.Security.Cryptography;
using System.IO;
using System.Text.Json;
using HyperPOS.Desktop.Data;

namespace HyperPOS.Desktop;

public partial class ProductSyncSettingsWindow : Window
{
    private readonly SyncConfigurationStore configurationStore = new();

    public SyncConfiguration? Configuration { get; private set; }

    public ProductSyncSettingsWindow()
    {
        InitializeComponent();
        try
        {
            var existing = configurationStore.Load();
            if (existing is not null)
                BaseUrlBox.Text = existing.BaseUrl;
        }
        catch (Exception ex) when (ex is IOException or JsonException or CryptographicException or FormatException)
        {
            ValidationText.Text = $"Saved sync settings could not be read: {ex.Message}";
        }
    }

    private void SaveAndSync_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseUri = ProductSyncService.ValidateBaseUrl(BaseUrlBox.Text);
            var token = TokenBox.Password;
            if (string.IsNullOrWhiteSpace(token))
                token = configurationStore.Load()?.ApiToken ?? "";

            if (token.Length < 32)
            {
                ValidationText.Text = "Enter the API key from the PHP server. Keys must contain at least 32 characters.";
                return;
            }

            Configuration = new SyncConfiguration(baseUri.AbsoluteUri.TrimEnd('/'), token);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or JsonException or CryptographicException or FormatException)
        {
            ValidationText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
