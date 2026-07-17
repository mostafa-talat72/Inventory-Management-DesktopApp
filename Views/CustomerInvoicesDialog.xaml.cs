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

public partial class CustomerInvoicesDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Customer _customer;
    private List<Invoice> _allInvoices = new();
    private string _filterMode = "Unpaid";

    public CustomerInvoicesDialog(AppDbContext db, Customer customer)
    {
        InitializeComponent();
        _db = db;
        _customer = customer;

        TxtTitle.Text = $"فواتير - {customer.Name}";
        TxtSubtitle.Text = customer.Phone ?? "لا يوجد رقم هاتف";
        LoadData();
    }

    private void LoadData()
    {
        _allInvoices = _db.Invoices
            .Where(i => i.CustomerId == _customer.Id)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        SetFilter("Unpaid");
        UpdateManageOrdersVisibility();
    }

    private void ApplyFilter()
    {
        var filtered = _filterMode switch
        {
            "PartiallyPaid" => _allInvoices.Where(i => i.Status == InvoiceStatus.PartiallyPaid).ToList(),
            "Cancelled" => _allInvoices.Where(i => i.Status == InvoiceStatus.Cancelled).ToList(),
            "Paid" => _allInvoices.Where(i => i.Status == InvoiceStatus.Paid).ToList(),
            "All" => _allInvoices,
            _ => _allInvoices.Where(i => i.Status != InvoiceStatus.Paid).ToList()
        };

        TxtInvoiceCount.Text = filtered.Count.ToString();
        TxtTotalAmount.Text = $"{filtered.Sum(i => i.TotalAmount):N2} ج.م";
        TxtRemainingAmount.Text = $"{filtered.Sum(i => i.Remaining):N2} ج.م";

        var filterLabel = _filterMode switch
        {
            "PartiallyPaid" => "مدفوعة جزئياً",
            "Cancelled" => "ملغاة",
            "Paid" => "مدفوعة",
            "All" => "",
            _ => "غير مدفوعة"
        };

        InvoicesPanel.Children.Clear();
        if (filtered.Count == 0)
        {
            var subtitle = _filterMode == "All"
                ? "لم يتم العثور على أي فواتير لهذا العميل"
                : $"لم يتم العثور على فواتير {filterLabel} لهذا العميل";

            InvoicesPanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#E8E8E8")!,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(40, 48, 40, 48),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new Path
                        {
                            Width = 64, Height = 64,
                            Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#E0E0E0")!,
                            Stretch = System.Windows.Media.Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Data = Geometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53.82 6.24 2.19c.48.38.76.97.76 1.58V19z")
                        },
                        new TextBlock
                        {
                            Text = "لا توجد فواتير",
                            FontSize = 18,
                            FontWeight = System.Windows.FontWeights.SemiBold,
                            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#546E7A")!,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 16, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = subtitle,
                            FontSize = 13,
                            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#90A4AE")!,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            });
        }
        else
        {
            foreach (var invoice in filtered)
                InvoicesPanel.Children.Add(CreateInvoiceCard(invoice));
        }
    }

    private Border CreateInvoiceCard(Invoice invoice)
    {
        var (statusText, statusBg, statusFg) = invoice.Status switch
        {
            InvoiceStatus.Paid => ("مدفوعة", "#E8F5E9", "#2E7D32"),
            InvoiceStatus.PartiallyPaid => ("مدفوعة جزئياً", "#FFF8E1", "#F57F17"),
            InvoiceStatus.Cancelled => ("ملغاة", "#F5F5F5", "#9E9E9E"),
            _ => ("غير مدفوعة", "#FFEBEE", "#C62828")
        };

        var statusBgBrush = (Brush)new BrushConverter().ConvertFrom(statusBg)!;
        var statusFgBrush = (Brush)new BrushConverter().ConvertFrom(statusFg)!;

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Brushes.White,
            BorderBrush = (Brush)new BrushConverter().ConvertFrom("#E8E8E8")!,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(0),
        };

        // Status accent bar on the right
        var accentBar = new Rectangle
        {
            Width = 5,
            Fill = statusFgBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            RadiusX = 2, RadiusY = 2
        };

        var innerGrid = new Grid
        {
            Margin = new Thickness(16, 14, 16, 14),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            }
        };

        // Left: invoice icon + info
        var iconBorder = new Border
        {
            Width = 46, Height = 46,
            CornerRadius = new CornerRadius(12),
            Background = statusBgBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 22, Height = 22,
                Fill = statusFgBrush,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53.82 6.24 2.19c.48.38.76.97.76 1.58V19z")
            }
        };

        // Center: invoice details
        var detailsRow1 = new StackPanel { Orientation = Orientation.Horizontal };
        detailsRow1.Children.Add(new TextBlock { Text = $"فاتورة #{invoice.Id}", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")! });
        detailsRow1.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = statusBgBrush, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = statusText, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = statusFgBrush } });

        var detailsRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        detailsRow2.Children.Add(new TextBlock { Text = invoice.CreatedAt.ToString("yyyy/MM/dd"), FontSize = 12, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")! });
        detailsRow2.Children.Add(new TextBlock { Text = "•", FontSize = 14, Foreground = (Brush)new BrushConverter().ConvertFrom("#CFD8DC")!, Margin = new Thickness(8, 0, 8, 0) });
        detailsRow2.Children.Add(new TextBlock { Text = $"{invoice.TotalAmount:N2} ج.م", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")! });
        if (invoice.Discount > 0)
            detailsRow2.Children.Add(new TextBlock { Text = $"خصم {invoice.Discount:N2} ج.م", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#F57F17")!, Margin = new Thickness(8, 0, 0, 0) });

        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        infoStack.Children.Add(detailsRow1);
        infoStack.Children.Add(detailsRow2);

        // Right: remaining amount
        var remainingStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        remainingStack.Children.Add(new TextBlock { Text = $"{invoice.Remaining:N2} ج.م", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = invoice.Remaining > 0 ? (Brush)new BrushConverter().ConvertFrom("#C62828")! : (Brush)new BrushConverter().ConvertFrom("#2E7D32")!, HorizontalAlignment = HorizontalAlignment.Right });
        remainingStack.Children.Add(new TextBlock { Text = "المتبقي", FontSize = 10, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 1, 0, 0) });

        innerGrid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);
        innerGrid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);
        innerGrid.Children.Add(remainingStack);
        Grid.SetColumn(remainingStack, 2);

        // Action buttons
        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };

        var printBtn = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
            Cursor = Cursors.Hand,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 6, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z") }, new TextBlock { Text = "طباعة", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) } } }
        };
        printBtn.MouseLeftButtonDown += (s, e) => { e.Handled = true; PrintInvoice(invoice); };
        btnStack.Children.Add(printBtn);

        if (invoice.Status != InvoiceStatus.Paid && invoice.Status != InvoiceStatus.Cancelled)
        {
            var payBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!,
                Cursor = Cursors.Hand,
                Padding = new Thickness(10, 6, 10, 6),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z") }, new TextBlock { Text = "دفع كامل", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) } } }
            };
            payBtn.MouseLeftButtonDown += (s, e) => { e.Handled = true; PayInvoice(invoice); };
            btnStack.Children.Add(payBtn);
        }

        innerGrid.Children.Add(btnStack);
        Grid.SetColumn(btnStack, 3);

        card.Child = new Grid
        {
            Children = { accentBar, innerGrid }
        };

        card.MouseLeftButtonDown += (s, e) => OpenInvoice(invoice);
        return card;
    }

    private void OpenInvoice(Invoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new InvoiceDetailsDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void PrintInvoice(Invoice invoice)
    {
        var printer = new ReceiptPrinter(_db);
        printer.Print(invoice);
    }

    private void PayInvoice(Invoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var fullInvoice = _db.Invoices.First(i => i.Id == invoice.Id);
        var dialog = new ConfirmPaymentDialog(_db, fullInvoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
        mainWindow.ShowOverlay(dialog);
    }

    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(21, 101, 192));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(84, 110, 122));

    private void SetFilter(string mode)
    {
        _filterMode = mode;

        foreach (var btn in new[] { BtnUnpaid, BtnPartiallyPaid, BtnCancelled, BtnPaid, BtnAll })
            btn.Background = Brushes.Transparent;
        foreach (var txt in new[] { TxtUnpaid, TxtPartiallyPaid, TxtCancelled, TxtPaid, TxtAll })
        { txt.Foreground = GrayBrush; txt.FontWeight = FontWeights.SemiBold; }

        var activeBtn = mode switch
        {
            "PartiallyPaid" => (BtnPartiallyPaid, (TextBlock)TxtPartiallyPaid),
            "Cancelled" => (BtnCancelled, (TextBlock)TxtCancelled),
            "Paid" => (BtnPaid, (TextBlock)TxtPaid),
            "All" => (BtnAll, (TextBlock)TxtAll),
            _ => (BtnUnpaid, (TextBlock)TxtUnpaid)
        };
        activeBtn.Item1.Background = BlueBrush;
        activeBtn.Item2.Foreground = Brushes.White;
        activeBtn.Item2.FontWeight = FontWeights.Bold;

        ApplyFilter();
    }

    private void BtnUnpaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("Unpaid");
    private void BtnPartiallyPaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("PartiallyPaid");
    private void BtnCancelled_Click(object sender, MouseButtonEventArgs e) => SetFilter("Cancelled");
    private void BtnPaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("Paid");
    private void BtnAll_Click(object sender, MouseButtonEventArgs e) => SetFilter("All");

    private Invoice? GetOpenInvoice()
    {
        return _db.Invoices
            .Where(i => i.CustomerId == _customer.Id
                && i.Status != InvoiceStatus.Paid
                && i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefault();
    }

    private void UpdateManageOrdersVisibility()
    {
        BtnManageOrders.Visibility = GetOpenInvoice() != null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnAddOrder_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new AddOrderDialog(_db, _customer);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void BtnManageOrders_Click(object sender, RoutedEventArgs e)
    {
        var invoice = GetOpenInvoice();
        if (invoice == null) return;
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ManageOrdersDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}
