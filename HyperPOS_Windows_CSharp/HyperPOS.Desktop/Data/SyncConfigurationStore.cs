using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HyperPOS.Desktop.Data;

public sealed record SyncConfiguration(string BaseUrl, string ApiToken);

public sealed class SyncConfigurationStore
{
    private const string EntropyText = "HyperPOS.Desktop.ProductSync.v1";
    private readonly string settingsPath;

    public SyncConfigurationStore()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HyperPOS.Desktop");
        Directory.CreateDirectory(dataDirectory);
        settingsPath = Path.Combine(dataDirectory, "sync-settings.json");
    }

    public bool IsConfigured => File.Exists(settingsPath);

    public SyncConfiguration? Load()
    {
        if (!File.Exists(settingsPath))
            return null;

        var stored = JsonSerializer.Deserialize<StoredConfiguration>(
            File.ReadAllText(settingsPath))
            ?? throw new InvalidDataException("The saved product-sync settings are invalid.");

        var protectedToken = Convert.FromBase64String(stored.ProtectedToken);
        var tokenBytes = ProtectedData.Unprotect(
            protectedToken,
            Encoding.UTF8.GetBytes(EntropyText),
            DataProtectionScope.CurrentUser);
        return new SyncConfiguration(stored.BaseUrl, Encoding.UTF8.GetString(tokenBytes));
    }

    public void Save(string baseUrl, string apiToken)
    {
        var protectedToken = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiToken),
            Encoding.UTF8.GetBytes(EntropyText),
            DataProtectionScope.CurrentUser);
        var stored = new StoredConfiguration(baseUrl, Convert.ToBase64String(protectedToken));
        var temporaryPath = settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(stored));
        File.Move(temporaryPath, settingsPath, overwrite: true);
    }

    private sealed record StoredConfiguration(string BaseUrl, string ProtectedToken);
}
