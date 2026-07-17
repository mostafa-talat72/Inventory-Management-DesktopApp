using System.Windows;
using System.Windows.Input;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class LoginDialog : Window
{
    public LoginDialog(AppConfig config)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(config.LocationName))
            TxtLocation.Text = config.LocationName;

        TxtPassword.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        TryLogin();
    }

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryLogin();
    }

    private void TryLogin()
    {
        var config = AppConfig.Load();

        if (config.VerifyPassword(TxtPassword.Password))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            TxtError.Text = "الرقم السري غير صحيح";
            TxtError.Visibility = Visibility.Visible;
            TxtPassword.Password = "";
            TxtPassword.Focus();
        }
    }
}
