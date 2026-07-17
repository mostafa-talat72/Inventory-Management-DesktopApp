using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ProductApp.Views;

namespace ProductApp;

public partial class MainWindow : Window
{
    private readonly Stack<UserControl> _overlayStack = new();
    private string _currentPage = "Products";

    public MainWindow()
    {
        InitializeComponent();
        NavigateToPage("Products");
    }

    private void NavigateToPage(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string page)
            NavigateToPage(page);
    }

    private void NavigateToPage(string page)
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
        }
    }

    private void UpdateNavButtons()
    {
        foreach (var btn in new[] { BtnProducts, BtnCustomers, BtnInvoices, BtnReports })
        {
            btn.IsEnabled = true;
            btn.Foreground = btn.Tag?.ToString() == _currentPage
                ? System.Windows.Media.Brushes.White
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#B0BEC5")!;
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

    public void NavigateToInvoice(int invoiceId)
    {
        _currentPage = "Invoices";
        UpdateNavButtons();
        var page = new InvoicesPage();
        MainFrame.Navigate(page);
        page.SelectInvoice(invoiceId);
    }

    public void NavigateToCustomerInvoices(int customerId, string customerName)
    {
        _currentPage = "Invoices";
        UpdateNavButtons();
        var page = new InvoicesPage();
        MainFrame.Navigate(page);
        page.ShowCustomerContext(customerId, customerName);
    }
}