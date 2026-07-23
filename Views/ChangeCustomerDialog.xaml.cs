using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class ChangeCustomerDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Invoice _invoice;
    private readonly List<Customer> _allCustomers;
    private Customer? _selectedCustomer;
    private bool _transferToCash;

    public ChangeCustomerDialog(AppDbContext db, Invoice invoice)
    {
        InitializeComponent();
        _db = db;
        _invoice = invoice;

        var customerName = invoice.Customer?.Name ?? invoice.CustomerName ?? "نقدي";
        TxtSubtitle.Text = $"فاتورة #{invoice.Id}";
        TxtCurrentCustomer.Text = customerName;
        TxtInvoiceInfo.Text = $"#{invoice.Id}  •  {invoice.TotalAmount:0.##} ج.م  •  {invoice.InvoiceDate:yyyy/MM/dd}";

        // Exclude current customer from list
        _allCustomers = _db.Customers
            .OrderBy(c => c.Name)
            .AsEnumerable()
            .Where(c => c.Id != invoice.CustomerId)
            .ToList();

        RenderCustomers(_allCustomers);

        // If invoice is already cash, disable cash option
        if (invoice.CustomerId == null)
        {
            CashCard.IsEnabled = false;
            CashCard.Opacity = 0.4;
            CashCard.Cursor = Cursors.Arrow;
        }

        // Default: highlight customer tab and show customer area
        CustomerCard.Background = (Brush)new BrushConverter().ConvertFrom("#E3F2FD")!;
        CustomerCard.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#1976D2")!;
        CustomerArea.Visibility = Visibility.Visible;
        TxtSelectHint.Visibility = Visibility.Visible;    }

    private void RenderCustomers(List<Customer> customers)
    {
        CustomersPanel.Children.Clear();
        foreach (var c in customers)
        {
            var card = CreateCustomerCard(c);
            CustomersPanel.Children.Add(card);
        }
        TxtNoResults.Visibility = customers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtSelectHint.Visibility = customers.Count > 0 && _selectedCustomer == null
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateCustomerCard(Customer customer)
    {
        var headingFg  = Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg    = Application.Current.TryFindResource("MutedTextBrush")    as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            Width = 36, Height = 36,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)new BrushConverter().ConvertFrom("#E3F2FD")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 18, Height = 18,
                Fill = (Brush)new BrushConverter().ConvertFrom("#1976D2")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M12 12c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm0-10c4.2 0 8 3.22 8 8.2 0 3.32-2.67 7.25-8 11.8-5.33-4.55-8-8.48-8-11.8C4 5.22 7.8 2 12 2z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        var invoiceCount = _db.Invoices.Count(i => i.CustomerId == customer.Id);

        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        infoStack.Children.Add(new TextBlock { Text = customer.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = headingFg });
        infoStack.Children.Add(new TextBlock { Text = $"{invoiceCount} فاتورة", FontSize = 11, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        var checkPath = new Path
        {
            Width = 18, Height = 18,
            Fill = (Brush)new BrushConverter().ConvertFrom("#1976D2")!,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"),
            Visibility = _selectedCustomer?.Id == customer.Id ? Visibility.Visible : Visibility.Collapsed
        };
        grid.Children.Add(checkPath);
        Grid.SetColumn(checkPath, 2);

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = _selectedCustomer?.Id == customer.Id
                ? (Brush)new BrushConverter().ConvertFrom("#E3F2FD")!
                : Brushes.Transparent,
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = Cursors.Hand,
            Child = grid
        };

        var custCopy = customer;
        border.MouseDown += (_, _) => SelectCustomer(custCopy);

        return border;
    }

    private void SelectCustomer(Customer customer)
    {
        _selectedCustomer = customer;
        _transferToCash = false;
        BtnSave.IsEnabled = true;
        RenderCustomers(_allCustomers);
        TxtSelectHint.Visibility = Visibility.Collapsed;
    }

    private void CashCard_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_invoice.CustomerId == null) return;

        _selectedCustomer = null;
        _transferToCash = true;
        BtnSave.IsEnabled = true;

        // Highlight cash card
        CashCard.Background = (Brush)new BrushConverter().ConvertFrom("#FFF8E1")!;
        CashCard.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#FFB300")!;
        CustomerCard.Background = Brushes.Transparent;
        CustomerCard.BorderBrush = Application.Current.TryFindResource("BorderBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;

        CashConfirmArea.Visibility = Visibility.Visible;
        CustomerArea.Visibility = Visibility.Collapsed;
    }

    private void CustomerCard_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _transferToCash = false;
        // Don't auto-select first customer - user picks from list

        // Highlight customer card
        CustomerCard.Background = (Brush)new BrushConverter().ConvertFrom("#E3F2FD")!;
        CustomerCard.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#1976D2")!;
        CashCard.Background = Brushes.Transparent;
        CashCard.BorderBrush = Application.Current.TryFindResource("BorderBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;

        CashConfirmArea.Visibility = Visibility.Collapsed;
        CustomerArea.Visibility = Visibility.Visible;
        TxtSelectHint.Visibility = _selectedCustomer == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = TxtSearch.Text.Trim();
        SearchWatermark.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrEmpty(query))
        {
            RenderCustomers(_allCustomers);
        }
        else
        {
            var filtered = _allCustomers
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            RenderCustomers(filtered);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!_transferToCash && _selectedCustomer == null) return;

        _db.Entry(_invoice).Reload();

        if (_transferToCash)
        {
            _invoice.CustomerId = null;
            _invoice.CustomerName = "نقدي";
        }
        else
        {
            _invoice.CustomerId = _selectedCustomer!.Id;
            _invoice.CustomerName = _selectedCustomer.Name;
        }

        _db.SaveChanges();
        NotificationManager.ShowSuccess("تم نقل الفاتورة بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
