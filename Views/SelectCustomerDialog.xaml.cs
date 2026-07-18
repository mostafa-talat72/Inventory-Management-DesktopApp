using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;

namespace ProductApp.Views;

public partial class SelectCustomerDialog : UserControl
{
    public event EventHandler<Customer?>? CustomerSelected;

    private readonly AppDbContext _db;
    private readonly BrushConverter _bc = new();

    public SelectCustomerDialog(AppDbContext db)
    {
        InitializeComponent();
        _db = db;
        LoadCustomers();
    }

    private void LoadCustomers(string? search = null)
    {
        var query = _db.Customers.Include(c => c.Invoices).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || (c.Phone != null && c.Phone.Contains(search)));

        var customers = query.OrderBy(c => c.Name).ToList();

        CustomerListPanel.Children.Clear();
        foreach (var customer in customers)
            CustomerListPanel.Children.Add(CreateCustomerCard(customer));
    }

    private Border CreateCustomerCard(Customer customer)
    {
        var unpaidCount = customer.Invoices.Count(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled);
        var statusColor = unpaidCount > 0 ? "#F57F17" : "#2E7D32";
        var statusText = unpaidCount > 0 ? $"{unpaidCount} فاتورة غير مدفوعة" : "لا توجد فواتير غير مدفوعة";

        var border = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = Brushes.White,
            BorderBrush = (Brush)_bc.ConvertFrom("#E0E0E0")!,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            Tag = customer
        };
        border.MouseLeftButtonDown += (_, _) => SelectCustomer(customer);
        border.MouseEnter += (_, _) =>
        {
            border.Background = (Brush)_bc.ConvertFrom("#F5F5F5")!;
            border.BorderBrush = (Brush)_bc.ConvertFrom("#1565C0")!;
        };
        border.MouseLeave += (_, _) =>
        {
            border.Background = Brushes.White;
            border.BorderBrush = (Brush)_bc.ConvertFrom("#E0E0E0")!;
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(10),
            Background = (Brush)_bc.ConvertFrom("#E8EAF6")!,
            Child = new TextBlock
            {
                Text = customer.Name.Length > 0 ? customer.Name[..1] : "?",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = (Brush)_bc.ConvertFrom("#1A237E")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        row.Children.Add(avatar);

        var infoStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = customer.Name,
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)_bc.ConvertFrom("#263238")!
        });
        if (!string.IsNullOrEmpty(customer.Phone))
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = customer.Phone,
                FontSize = 11, Foreground = (Brush)_bc.ConvertFrom("#90A4AE")!,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }
        Grid.SetColumn(infoStack, 1);
        row.Children.Add(infoStack);

        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = (Brush)_bc.ConvertFrom(statusColor)!,
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = statusText,
                FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            }
        };
        Grid.SetColumn(statusBadge, 2);
        row.Children.Add(statusBadge);

        border.Child = row;
        return border;
    }

    private void SelectCustomer(Customer? customer)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.HideOverlay();
        CustomerSelected?.Invoke(this, customer);
    }

    private void CashCard_Click(object sender, MouseButtonEventArgs e)
    {
        SelectCustomer(null);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        LoadCustomers(TxtSearch.Text);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.HideOverlay();
    }
}
