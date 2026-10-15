using Microsoft.Web.WebView2.Core;

namespace HyperPOS.Windows;

public sealed class PosForm : Form
{
    // Change this URL if your POS address changes.
    private const string PosUrl = "https://jomjom.sg/admin/pos";

    private readonly Microsoft.Web.WebView2.WinForms.WebView2 browser = new()
    {
        Dock = DockStyle.Fill,
    };

    public PosForm()
    {
        Text = "JomJom POS";
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Controls.Add(browser);

        // F11 toggles fullscreen. Esc exits fullscreen if enabled.
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape && FormBorderStyle == FormBorderStyle.None)
            {
                ExitFullScreen();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.R)
            {
                browser.Reload();
                e.Handled = true;
            }
        };

        Shown += async (_, _) => await InitializeBrowserAsync();
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            await browser.EnsureCoreWebView2Async();

            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            browser.CoreWebView2.Settings.IsZoomControlEnabled = true;

            browser.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess)
                {
                    MessageBox.Show(
                        $"The POS page could not be loaded. Check your internet connection and sign-in.\nError: {e.WebErrorStatus}",
                        "POS connection",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };

            browser.Source = new Uri(PosUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "WebView2 could not start. Install the Microsoft Edge WebView2 Runtime and try again.\n\n" + ex.Message,
                "Startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ToggleFullScreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
            ExitFullScreen();
        else
        {
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
    }

    private void ExitFullScreen()
    {
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState = FormWindowState.Maximized;
    }
}
