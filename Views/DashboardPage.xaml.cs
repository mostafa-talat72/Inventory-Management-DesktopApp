using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class DashboardPage : Page
{
    private readonly AppDbContext _db = new();

    // Raw values — stored so we can mask/unmask without re-querying DB
    private decimal _todaySales, _todayCost, _todayDiscount, _todayProfit, _todayProfitMargin;
    private decimal _totalRevenue, _totalCost, _totalDiscount, _totalProfit, _profitMargin;
    private decimal _pendingAmount, _cancelledAmount;
    private decimal _todayDeductionCost, _totalDeductionCost;
    private List<Invoice> _recentInvoices = new();

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LoadDashboard();
            AmountsVisibilityService.VisibilityChanged += OnAmountsVisibilityChanged;
        };
        Unloaded += (_, _) =>
        {
            AmountsVisibilityService.VisibilityChanged -= OnAmountsVisibilityChanged;
            _db.Dispose();
        };
    }

    private void OnAmountsVisibilityChanged()
    {
        ApplyAmountsMask();
        // Rebuild recent invoice cards with updated mask state
        RecentInvoicesPanel.Children.Clear();
        foreach (var inv in _recentInvoices)
            RecentInvoicesPanel.Children.Add(CreateRecentInvoiceCard(inv));
    }

    private void LoadDashboard()
    {
        try
        {
            var now = DateTime.Now;
            TxtDashboardDate.Text = $"آخر تحديث: {now:yyyy/MM/dd - hh:mm} {(now.Hour < 12 ? "ص" : "م")}";

            var todayStart = now.Date;
            var todayEnd = todayStart.AddDays(1);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            // Today's active invoice IDs
            var todayInvoiceIds = _db.Invoices
                .Where(i => i.CreatedAt >= todayStart && i.CreatedAt < todayEnd && i.Status != InvoiceStatus.Cancelled)
                .Select(i => i.Id).ToHashSet();

            // Today's order items
            var todayOrderIds = _db.Orders.Where(o => todayInvoiceIds.Contains(o.InvoiceId)).Select(o => o.Id).ToHashSet();
            var todayItems = _db.OrderItems.Where(oi => todayOrderIds.Contains(oi.OrderId)).ToList();
            var todaySales = todayItems.Sum(oi => oi.Total);
            var todayCost = todayItems.Sum(oi => oi.CostPrice);
            var todayInvoices = todayInvoiceIds.Count;
            var todayDiscount = _db.Invoices
                .Where(i => todayInvoiceIds.Contains(i.Id) && i.Status != InvoiceStatus.Cancelled)
                .Sum(i => i.Discount);
            // Deduction costs (Adjustment, Shortage, ReturnToSupplier with unrecovered cost)
            var todayDeductionCost = _db.InventoryMovements
                .Where(m => m.CreatedAt >= todayStart && m.CreatedAt < todayEnd
                    && (m.MovementType == MovementType.Adjustment
                        || m.MovementType == MovementType.Shortage
                        || (m.MovementType == MovementType.ReturnToSupplier && !m.IsCostRecovered)))
                .Sum(m => (decimal?)m.Quantity * m.CostPrice) ?? 0;
            var todayTotalCost = todayCost + todayDeductionCost;
            var todayProfit = todaySales - todayTotalCost - todayDiscount;
            var todayProfitMargin = todaySales > 0 ? todayProfit / todaySales * 100 : 0;

            // All-time totals (from database)
            var activeInvoiceIds = _db.Invoices
                .Where(i => i.Status != InvoiceStatus.Cancelled)
                .Select(i => i.Id).ToHashSet();
            var activeOrderIds = _db.Orders.Where(o => activeInvoiceIds.Contains(o.InvoiceId)).Select(o => o.Id).ToHashSet();
            var allOrderItems = _db.OrderItems.Where(oi => activeOrderIds.Contains(oi.OrderId)).ToList();
            var totalRevenue = allOrderItems.Sum(oi => oi.Total);
            var totalCost = allOrderItems.Sum(oi => oi.CostPrice);
            var totalDeductionCost = _db.InventoryMovements
                .Where(m => (m.MovementType == MovementType.Adjustment
                        || m.MovementType == MovementType.Shortage
                        || (m.MovementType == MovementType.ReturnToSupplier && !m.IsCostRecovered)))
                .Sum(m => (decimal?)m.Quantity * m.CostPrice) ?? 0;
            var totalDiscount = _db.Invoices.Where(i => i.Status != InvoiceStatus.Cancelled).Sum(i => i.Discount);
            var totalTotalCost = totalCost + totalDeductionCost;
            var totalProfit = totalRevenue - totalTotalCost - totalDiscount;
            var profitMargin = totalRevenue > 0 ? totalProfit / totalRevenue * 100 : 0;

            var pendingAmount = _db.Invoices
                .Where(i => i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid)
                .AsEnumerable()
                .Sum(i => i.TotalAmount - i.Discount - i.TotalPaid);
            var cancelledAmount = _db.Invoices
                .Where(i => i.Status == InvoiceStatus.Cancelled)
                .AsEnumerable()
                .Sum(i => i.TotalAmount - i.Discount);

            var totalCustomers = _db.Customers.Count();
            var newCustomers = _db.Customers.Count(c => c.CreatedAt >= monthStart);

            var recentInvoices = _db.Invoices
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .AsNoTracking()
                .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                _todaySales         = todaySales;
                _todayCost          = todayTotalCost;
                _todayDiscount      = todayDiscount;
                _todayDeductionCost = todayDeductionCost;
                _todayProfit        = todayProfit;
                _todayProfitMargin  = todayProfitMargin;
                _totalRevenue       = totalRevenue;
                _totalCost          = totalTotalCost;
                _totalDiscount      = totalDiscount;
                _totalDeductionCost = totalDeductionCost;
                _totalProfit        = totalProfit;
                _profitMargin       = profitMargin;
                _pendingAmount      = pendingAmount;
                _cancelledAmount    = cancelledAmount;

                // Cache for re-render on toggle
                _recentInvoices = recentInvoices;

                TxtTodayCount.Text  = $"{todayInvoices} فاتورة";
                TxtTodayCostCount.Text = $"مبيعات: {todayCost:0.##} ج.م";
                TxtTodayDeductionCost.Text = todayDeductionCost > 0
                    ? $"خصم مخزون: {todayDeductionCost:0.##} ج.م" : "";
                TxtTodayProfitMargin.Text = todayProfitMargin >= 0 ? $"هامش ربح {todayProfitMargin:0.0}%" : $"خسارة {Math.Abs(todayProfitMargin):0.0}%";
                var activeCount = _db.Invoices.Count(i => i.Status != InvoiceStatus.Cancelled);
                var pendingCount = _db.Invoices.Count(i => i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid);
                var cancelledCount = _db.Invoices.Count(i => i.Status == InvoiceStatus.Cancelled);
                TxtTotalInvoices.Text = $"{activeCount} فاتورة";
                TxtTotalCostCount.Text = $"مبيعات: {totalCost:0.##} ج.م";
                TxtTotalDeductionCost.Text = totalDeductionCost > 0
                    ? $"خصم مخزون: {totalDeductionCost:0.##} ج.م" : "";
                TxtProfitMargin.Text  = profitMargin >= 0 ? $"هامش ربح {profitMargin:0.0}%" : $"خسارة {Math.Abs(profitMargin):0.0}%";
                TxtTotalCustomers.Text = $"{totalCustomers}";
                TxtNewCustomers.Text  = $"{newCustomers} هذا الشهر";
                TxtPendingInvoices.Text = $"{pendingCount}";
                TxtCancelledInvoices.Text = $"{cancelledCount}";

                ApplyAmountsMask();

                RecentInvoicesPanel.Children.Clear();
                foreach (var inv in recentInvoices)
                    RecentInvoicesPanel.Children.Add(CreateRecentInvoiceCard(inv));
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطأ في تحميل لوحة التحكم: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyAmountsMask()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;

        TxtTodaySales.Text       = hidden ? mask : $"{_todaySales:0.##} ج.م";
        TxtTodayCost.Text        = hidden ? mask : $"{_todayCost:0.##} ج.م";
        TxtTodayDiscount.Text    = hidden ? mask : $"{_todayDiscount:0.##} ج.م";
        TxtTodayProfit.Text      = hidden ? mask : $"{_todayProfit:0.##} ج.م";
        TxtTotalSales.Text       = hidden ? mask : $"{_totalRevenue:0.##} ج.م";
        TxtTotalCost.Text        = hidden ? mask : $"{_totalCost:0.##} ج.م";
        TxtTotalDiscount.Text    = hidden ? mask : $"{_totalDiscount:0.##} ج.م";
        TxtTotalProfit.Text      = hidden ? mask : $"{_totalProfit:0.##} ج.م";
        TxtPendingAmount.Text    = hidden ? mask : $"{_pendingAmount:0.##} ج.م";
        TxtCancelledAmount.Text  = hidden ? mask : $"{_cancelledAmount:0.##} ج.م";
    }

    private Border CreateRecentInvoiceCard(Invoice inv)
    {
        var brush = inv.Status switch
        {
            InvoiceStatus.Paid => "#2E7D32",
            InvoiceStatus.PartiallyPaid => "#F57F17",
            InvoiceStatus.Open => "#1565C0",
            InvoiceStatus.Cancelled => "#BDBDBD",
            _ => "#78909C"
        };
        var statusText = inv.Status switch
        {
            InvoiceStatus.Paid => "مدفوع",
            InvoiceStatus.PartiallyPaid => "مدفوع جزئياً",
            InvoiceStatus.Open => "مفتوح",
            InvoiceStatus.Cancelled => "ملغي",
            _ => ""
        };

        // Pull colors from the current theme (works for both light & dark)
        var headingBrush  = Application.Current.TryFindResource("HeadingTextBrush")  as Brush
                             ?? new SolidColorBrush(Color.FromRgb(38, 50, 56));
        var primaryBrush  = Application.Current.TryFindResource("PrimaryTextBrush")  as Brush
                             ?? new SolidColorBrush(Color.FromRgb(26, 35, 126));
        var subtleBrush   = Application.Current.TryFindResource("MutedTextBrush")    as Brush
                             ?? new SolidColorBrush(Color.FromRgb(144, 164, 174));
        var dividerBrush  = Application.Current.TryFindResource("DividerBrush")      as Brush
                             ?? new SolidColorBrush(Color.FromRgb(238, 238, 238));

        var border = new Border
        {
            Margin = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
            Cursor = Cursors.Hand,
            Tag = inv.Id,
            BorderBrush = dividerBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(0)
        };
        border.MouseLeftButtonDown += (_, _) => OpenInvoice(inv.Id);

        // Hover effect
        border.MouseEnter += (_, _) =>
        {
            var hoverBg = Application.Current.TryFindResource("SurfaceBackground") as Brush
                          ?? new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
            border.Background = hoverBg;
        };
        border.MouseLeave += (_, _) => border.Background = null;

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // badge
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // info
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // amount

        // Status badge
        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = (Brush)new BrushConverter().ConvertFrom(brush)!,
            Padding = new Thickness(7, 3, 7, 3),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 70,
            Child = new TextBlock
            {
                Text = statusText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            }
        };
        row.Children.Add(statusBadge);

        // Invoice info
        var infoStack = new StackPanel
        {
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(infoStack, 1);

        infoStack.Children.Add(new TextBlock
        {
            Text = $"فاتورة #{inv.Id}  •  {(inv.CustomerName ?? "عميل نقدي")}",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = headingBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{inv.CreatedAt:yyyy/MM/dd  hh:mm} {(inv.CreatedAt.Hour < 12 ? "ص" : "م")}",
            FontSize = 10,
            Foreground = subtleBrush,
            Margin = new Thickness(0, 3, 0, 0)
        });
        row.Children.Add(infoStack);

        // Amount + remaining
        var amountStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumn(amountStack, 2);

        amountStack.Children.Add(new TextBlock
        {
            Text = AmountsVisibilityService.IsHidden ? "••••••" : $"{inv.NetAmount:0.##} ج.م",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = primaryBrush,
            TextAlignment = TextAlignment.Left
        });

        if (inv.Remaining > 0 && inv.Status != InvoiceStatus.Cancelled)
        {
            amountStack.Children.Add(new TextBlock
            {
                Text = AmountsVisibilityService.IsHidden ? "••••••" : $"متبقي {inv.Remaining:0.##}",
                FontSize = 10,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#F57F17")!,
                Margin = new Thickness(0, 2, 0, 0),
                TextAlignment = TextAlignment.Left
            });
        }

        row.Children.Add(amountStack);

        border.Child = row;
        return border;
    }

    private void OpenInvoice(int invoiceId)
    {
        var invoice = _db.Invoices.Find(invoiceId);
        if (invoice == null) return;
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;
        mainWindow.NavigateToPage("Invoices");
        var invoiceDetails = new InvoiceDetailsDialog(_db, invoice);
        mainWindow.ShowOverlay(invoiceDetails);
        invoiceDetails.DialogClosed += (_, _) =>
        {
            mainWindow.HideOverlay();
            Refresh();
        };
    }

    private void OpenInvoicesPage(object sender, MouseButtonEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.NavigateToPage("Invoices");
    }

    private void OpenNewInvoice(object sender, RoutedEventArgs e)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;

        var db = new AppDbContext();

        if (!db.Customers.Any())
        {
            HandleCustomerSelected(db, mainWindow, null);
            return;
        }

        var dialog = new SelectCustomerDialog(db);
        mainWindow.ShowOverlay(dialog);
        dialog.CustomerSelected += (_, customer) =>
        {
            HandleCustomerSelected(db, mainWindow, customer);
        };
    }

    private void HandleCustomerSelected(AppDbContext db, MainWindow mainWindow, Customer? customer)
    {
        var unpaidInvoices = db.Invoices
            .Where(i => (customer == null ? i.CustomerId == null : i.CustomerId == customer.Id)
                && (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid))
            .OrderByDescending(i => i.Id)
            .ToList();

        if (unpaidInvoices.Count > 0)
        {
            var dialog = new SelectInvoiceDialog(db, customer);
            mainWindow.ShowOverlay(dialog);
            dialog.InvoiceSelected += (invoice) =>
            {
                OpenAddOrder(db, mainWindow, customer, invoice);
            };
        }
        else
        {
            OpenAddOrder(db, mainWindow, customer, null);
        }
    }

    private void OpenAddOrder(AppDbContext db, MainWindow mainWindow, Customer? customer, Invoice? invoice)
    {
        var isNew = false;
        if (invoice == null)
        {
            invoice = new Invoice
            {
                CustomerId = customer?.Id,
                CustomerName = customer?.Name ?? "نقدي",
                CreatedAt = DateTime.Now,
                Status = InvoiceStatus.Open
            };
            db.Invoices.Add(invoice);
            db.SaveChanges();
            isNew = true;
        }
        var addOrder = new AddOrderDialog(db, invoice);
        mainWindow.ShowOverlay(addOrder);
        addOrder.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (isNew && r != true)
            {
                db.Entry(invoice).Collection(i => i.Orders).Load();
                if (!invoice.Orders.Any())
                {
                    db.Invoices.Remove(invoice);
                    db.SaveChanges();
                }
            }
            db.Dispose();
            Refresh();
        };
    }

    private void OpenProductsPage(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.NavigateToPage("Products");
    }

    private void OpenReportsPage(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.NavigateToPage("Reports");
    }

    public void Refresh()
    {
        LoadDashboard();
    }
}
