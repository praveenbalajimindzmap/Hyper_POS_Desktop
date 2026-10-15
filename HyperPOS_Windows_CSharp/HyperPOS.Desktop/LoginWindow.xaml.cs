using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyperPOS.Desktop.Data;

namespace HyperPOS.Desktop;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        try
        {
            App.Log("LoginWindow ctor started");
            InitializeComponent();
            App.Log("LoginWindow InitializeComponent done");
            if (CashierSelector != null && CashierSelector.Items.Count > 0)
            {
                CashierSelector.SelectedIndex = 0;
            }
            if (PasswordBox != null)
            {
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
            App.Log("LoginWindow ctor finished");
        }
        catch (Exception ex)
        {
            App.Log($"EXCEPTION in LoginWindow ctor: {ex}");
        }

        Loaded += (s, e) => {
            App.Log("LoginWindow Loaded event fired");
            PasswordBox?.Focus();
        };
        Closed += (s, e) => {
            App.Log("LoginWindow Closed event fired");
        };
    }

    private void CashierSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (ErrorBanner != null)
                ErrorBanner.Visibility = Visibility.Collapsed;

            if (PasswordBox != null)
                PasswordBox.Clear();

            if (CashierSelector?.SelectedItem is ComboBoxItem item && EmailBox != null)
            {
                var text = item.Content?.ToString() ?? "";
                if (text.Contains("Saraa Boutique"))
                    EmailBox.Text = "saraaboutiqueoff@gmail.com";
                else if (text.Contains("Balaji"))
                    EmailBox.Text = "balajim02626@gmail.com";
                else if (text.Contains("Parthiban"))
                    EmailBox.Text = "parthibanvs28@gmail.com";
                else if (text.Contains("Jeyasri"))
                    EmailBox.Text = "jeyasrimindzmap@gmail.com";
                else if (text.Contains("Dharshini"))
                    EmailBox.Text = "dharshinimindzmap@gmail.com";
                else if (text.Contains("Sathish"))
                    EmailBox.Text = "sathishrmindzmap@gmail.com";
                else
                    EmailBox.Text = "praveenbalajimindzmap@gmail.com";
            }
        }
        catch (Exception ex)
        {
            App.Log($"EXCEPTION in CashierSelector_SelectionChanged: {ex}");
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformLogin();
        }
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        PerformLogin();
    }

    private void QuickEnter_Click(object sender, RoutedEventArgs e)
    {
        CashierSelector.SelectedIndex = 0;
        EmailBox.Text = "praveenbalajimindzmap@gmail.com";
        PasswordBox.Focus();
        if (string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            ShowLoginError("Please enter the Super Admin password for praveenbalajimindzmap@gmail.com.");
        }
        else
        {
            PerformLogin();
        }
    }

    private void PerformLogin()
    {
        if (ErrorBanner != null)
            ErrorBanner.Visibility = Visibility.Collapsed;

        var email = !string.IsNullOrWhiteSpace(EmailBox?.Text) ? EmailBox.Text.Trim() : "praveenbalajimindzmap@gmail.com";
        var password = PasswordBox?.Password ?? "";

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowLoginError("Please enter your password.");
            PasswordBox?.Focus();
            return;
        }

        try
        {
            var db = new PosDatabase();
            var validation = db.ValidateAdminLogin(email, password);

            if (!validation.Success)
            {
                ShowLoginError(validation.Message);
                PasswordBox?.SelectAll();
                PasswordBox?.Focus();
                return;
            }

            var cashierName = !string.IsNullOrWhiteSpace(validation.Name) ? validation.Name : "Praveen Balaji";
            OpenPos(cashierName, validation.Email);
        }
        catch (Exception ex)
        {
            App.Log($"Login validation error: {ex}");
            ShowLoginError("Authentication error: " + ex.Message);
        }
    }

    private void ShowLoginError(string message)
    {
        if (ErrorMessageText != null)
            ErrorMessageText.Text = message;
        if (ErrorBanner != null)
            ErrorBanner.Visibility = Visibility.Visible;
    }

    private void OpenPos(string cashierName, string cashierEmail)
    {
        try
        {
            App.Log($"OpenPos called for {cashierName} ({cashierEmail})");
            var pos = new MainWindow(cashierName, cashierEmail);
            Application.Current.MainWindow = pos;
            pos.Show();
            Close();
            App.Log("OpenPos successfully opened MainWindow and closed LoginWindow");
        }
        catch (Exception ex)
        {
            App.Log($"EXCEPTION in OpenPos: {ex}");
            MessageBox.Show($"Could not open POS register:\n\n{ex.Message}\n\n{ex.StackTrace}", "Error Opening POS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
