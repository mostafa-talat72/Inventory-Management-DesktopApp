using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ProductApp.Services;
using ProductApp.Views;

namespace ProductApp;

public partial class MainWindow : Window
{
    private readonly Stack<UserControl> _overlayStack = new();
    private string _currentPage = "Products";

    public MainWindow()
    {
        InitializeComponent();

        var config = AppConfig.Load();
        UpdateLocationName(config.LocationName);

        NavigateToPage("Products");
    }

    public void UpdateLocationName(string name)
    {
        TxtLocationDisplay.Text = string.IsNullOrWhiteSpace(name) ? "" : name;
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
        foreach (var btn in new[] { BtnProducts, BtnCustomers, BtnInvoices, BtnReports, BtnSettings })
        {
            var isActive = btn.Tag?.ToString() == _currentPage;
            btn.BorderBrush = isActive
                ? System.Windows.Media.Brushes.White
                : System.Windows.Media.Brushes.Transparent;
            btn.Background = isActive
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#14FFFFFF")!
                : System.Windows.Media.Brushes.Transparent;
            btn.Foreground = isActive
                ? System.Windows.Media.Brushes.White
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#90CAF9")!;
        }
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
}
