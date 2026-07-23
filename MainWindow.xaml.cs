using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using ProductApp.Services;
using ProductApp.Views;

namespace ProductApp;

public partial class MainWindow : Window
{
    private readonly Stack<UserControl> _overlayStack = new();
    private string _currentPage = "Dashboard";

    public MainWindow()
    {
        InitializeComponent();

        var config = AppConfig.Load();
        UpdateLocationName(config.LocationName);

        NavigateToPage("Dashboard");
        UpdateThemeToggleButton();
        UpdateAmountsToggleButton();
    }

    public void UpdateLocationName(string name)
    {
        TxtLocationDisplay2.Text = string.IsNullOrWhiteSpace(name) ? "" : name;
    }

    public void RestartAutoBackup()
    {
        App.AppBackup?.StopAutoBackup();
        App.AppBackup?.StartAutoBackup();
    }

    private void NavigateToPage(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string page)
            NavigateToPage(page);
    }

    public void NavigateToPage(string page)
    {
        _currentPage = page;
        UpdateNavButtons();

        switch (page)
        {
            case "Dashboard":
                MainFrame.Navigate(new DashboardPage());
                break;
            case "Products":
                MainFrame.Navigate(new ProductsPage());
                break;
            case "Customers":
                MainFrame.Navigate(new CustomersPage());
                break;
            case "Invoices":
                MainFrame.Navigate(new InvoicesPage());
                break;
            case "Reports":
                MainFrame.Navigate(new ReportsPage());
                break;
            case "Settings":
                MainFrame.Navigate(new SettingsPage());
                break;
        }
    }

    private void UpdateNavButtons()
    {
        var activeBorder  = System.Windows.Media.Brushes.White;
        var activeBg      = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#14FFFFFF")!;
        var activeFg      = System.Windows.Media.Brushes.White;
        var inactiveBg    = System.Windows.Media.Brushes.Transparent;
        var inactiveFg    = (System.Windows.Media.Brush)(Application.Current.TryFindResource("NavTextBrush")
                             ?? new System.Windows.Media.BrushConverter().ConvertFrom("#90CAF9")!);

        foreach (var btn in new[] { BtnDashboard, BtnProducts, BtnCustomers, BtnInvoices, BtnReports, BtnSettings })
        {
            var isActive = btn.Tag?.ToString() == _currentPage;
            btn.BorderBrush = isActive ? activeBorder : System.Windows.Media.Brushes.Transparent;
            btn.Background  = isActive ? activeBg      : inactiveBg;
            btn.Foreground  = isActive ? activeFg      : inactiveFg;
        }
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.Toggle();
        UpdateThemeToggleButton();
        UpdateNavButtons(); // refresh nav brush after theme change
    }

    private void UpdateThemeToggleButton()
    {
        bool isDark = ThemeService.IsDarkMode;
        // Show sun icon in dark mode (click → go light), moon in light mode (click → go dark)
        IconMoon.Visibility = isDark ? Visibility.Collapsed : Visibility.Visible;
        IconSun.Visibility  = isDark ? Visibility.Visible   : Visibility.Collapsed;
        TxtThemeLabel.Text  = isDark ? "الوضع النهاري" : "الوضع الليلي";
    }

    private void BtnAmountsToggle_Click(object sender, RoutedEventArgs e)
    {
        AmountsVisibilityService.Toggle();
        UpdateAmountsToggleButton();
    }

    private void UpdateAmountsToggleButton()
    {
        bool hidden = AmountsVisibilityService.IsHidden;
        // When hidden: show EyeOff icon + "إظهار الأرقام"
        // When visible: show Eye icon + "إخفاء الأرقام"
        IconEyeOff.Visibility = hidden ? Visibility.Visible   : Visibility.Collapsed;
        IconEye.Visibility    = hidden ? Visibility.Collapsed : Visibility.Visible;
        TxtAmountsLabel.Text  = hidden ? "إظهار الأرقام" : "إخفاء الأرقام";
    }

    public void ShowOverlay(UserControl content)
    {
        _overlayStack.Push(content);
        OverlayContent.Content = content;
        OverlayBackground.Visibility = Visibility.Visible;
        OverlayContainer.Visibility = Visibility.Visible;
    }

    public void HideOverlay()
    {
        if (_overlayStack.Count > 0)
            _overlayStack.Pop();

        if (_overlayStack.Count > 0)
            OverlayContent.Content = _overlayStack.Peek();
        else
        {
            OverlayContent.Content = null;
            OverlayBackground.Visibility = Visibility.Collapsed;
            OverlayContainer.Visibility = Visibility.Collapsed;
        }
    }

    private void OverlayBackground_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        HideOverlay();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        ConfirmDialog.Show(
            "تسجيل الخروج",
            "هل أنت متأكد من تسجيل الخروج؟ سيتم إعادة توجيهك إلى شاشة الدخول.",
            confirmed =>
            {
                if (!confirmed) return;

                Hide();

                var login = new LoginDialog(AppConfig.Load());
                login.Owner = null;
                login.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                if (login.ShowDialog() == true)
                {
                    NavigateToPage("Dashboard");
                    Show();
                }
                else
                    Application.Current.Shutdown();
            },
            ConfirmDialog.DialogType.Danger,
            "تسجيل الخروج",
            "إلغاء");
    }
}
