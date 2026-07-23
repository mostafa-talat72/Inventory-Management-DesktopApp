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

        // Theme-aware brushes
        var cardBg     = Application.Current.TryFindResource("CardBackground")    as Brush ?? Brushes.White;
        var hoverBg    = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)_bc.ConvertFrom("#F5F5F5")!;
        var cardBorder = Application.Current.TryFindResource("BorderBrushLight")  as Brush ?? (Brush)_bc.ConvertFrom("#E0E0E0")!;
        var headingFg  = Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)_bc.ConvertFrom("#263238")!;
        var primaryFg  = Application.Current.TryFindResource("PrimaryTextBrush")  as Brush ?? (Brush)_bc.ConvertFrom("#1A237E")!;
        var mutedFg    = Application.Current.TryFindResource("MutedTextBrush")    as Brush ?? (Brush)_bc.ConvertFrom("#90A4AE")!;
        var avatarBg   = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)_bc.ConvertFrom("#E8EAF6")!;
        var activeBorder = (Brush)_bc.ConvertFrom("#1565C0")!;

        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = cardBg,
            BorderBrush = cardBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            Tag = customer
        };
        border.MouseLeftButtonDown += (_, _) => SelectCustomer(customer);
        border.MouseEnter += (_, _) =>
        {
            border.Background = hoverBg;
            border.BorderBrush = activeBorder;
        };
        border.MouseLeave += (_, _) =>
        {
            border.Background = cardBg;
            border.BorderBrush = cardBorder;
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(12),
            Background = avatarBg,
            Child = new TextBlock
            {
                Text = customer.Name.Length > 0 ? customer.Name[..1] : "?",
                FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = primaryFg,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        row.Children.Add(avatar);

        var infoStack = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = customer.Name,
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = headingFg
        });
        if (!string.IsNullOrEmpty(customer.Phone))
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = customer.Phone,
                FontSize = 11, Foreground = mutedFg,
                Margin = new Thickness(0, 3, 0, 0)
            });
        }
        Grid.SetColumn(infoStack, 1);
        row.Children.Add(infoStack);

        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = (Brush)_bc.ConvertFrom(statusColor)!,
            Padding = new Thickness(8, 4, 8, 4),
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
