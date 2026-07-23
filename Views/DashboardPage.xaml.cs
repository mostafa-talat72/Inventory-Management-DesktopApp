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

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadDashboard();
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

            var allInvoices = _db.Invoices.AsNoTracking().ToList();
            var activeInvoiceIds = allInvoices.Where(i => i.Status != InvoiceStatus.Cancelled).Select(i => i.Id).ToHashSet();

            var allOrders = _db.Orders.AsNoTracking().ToList();
            var activeOrderIds = allOrders.Where(o => activeInvoiceIds.Contains(o.InvoiceId)).Select(o => o.Id).ToHashSet();

            var allOrderItems = _db.OrderItems
                .AsNoTracking()
                .Where(oi => activeOrderIds.Contains(oi.OrderId))
                .ToList();

            var todayInvoiceIds = allInvoices
                .Where(i => i.CreatedAt >= todayStart && i.CreatedAt < todayEnd && i.Status != InvoiceStatus.Cancelled)
                .Select(i => i.Id)
                .ToHashSet();
            var todayOrderIds = allOrders.Where(o => todayInvoiceIds.Contains(o.InvoiceId)).Select(o => o.Id).ToHashSet();
            var todayItems = allOrderItems.Where(oi => todayOrderIds.Contains(oi.OrderId)).ToList();
            var todaySales = todayItems.Sum(oi => oi.Total);
            var todayCost = todayItems.Sum(oi => oi.CostPrice);
            var todayProfit = todaySales - todayCost;
            var todayProfitMargin = todaySales > 0 ? todayProfit / todaySales * 100 : 0;
            var todayInvoices = allInvoices.Count(i => i.CreatedAt >= todayStart && i.CreatedAt < todayEnd && i.Status != InvoiceStatus.Cancelled);

            var totalRevenue = allOrderItems.Sum(oi => oi.Total);
            var totalCost = allOrderItems.Sum(oi => oi.CostPrice);
            var totalProfit = totalRevenue - totalCost;
            var profitMargin = totalRevenue > 0 ? totalProfit / totalRevenue * 100 : 0;

            var activeInvoices = allInvoices.Where(i => i.Status != InvoiceStatus.Cancelled).ToList();
            var pendingInvoices = allInvoices.Where(i => i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid).ToList();
            var pendingAmount = pendingInvoices.Sum(i => i.Remaining);
            var cancelledInvoices = allInvoices.Where(i => i.Status == InvoiceStatus.Cancelled).ToList();
            var cancelledAmount = cancelledInvoices.Sum(i => i.NetAmount);

            var totalCustomers = _db.Customers.Count();
            var newCustomers = _db.Customers.Count(c => c.CreatedAt >= monthStart);

            var recentInvoices = allInvoices.OrderByDescending(i => i.CreatedAt).Take(5).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                TxtTodaySales.Text = $"{todaySales:0.##} ج.م";
                TxtTodayCount.Text = $"{todayInvoices} فاتورة";
                TxtTodayCost.Text = $"{todayCost:0.##} ج.م";
                TxtTodayCostCount.Text = $"{todayInvoices} فاتورة";
                TxtTodayProfit.Text = $"{todayProfit:0.##} ج.م";
                TxtTodayProfitMargin.Text = todayProfitMargin >= 0 ? $"هامش ربح {todayProfitMargin:0.0}%" : $"خسارة {Math.Abs(todayProfitMargin):0.0}%";
                TxtTotalSales.Text = $"{totalRevenue:0.##} ج.م";
                TxtTotalInvoices.Text = $"{activeInvoices.Count} فاتورة";
                TxtTotalCost.Text = $"{totalCost:0.##} ج.م";
                TxtTotalCostCount.Text = $"{activeInvoices.Count} فاتورة";
                TxtTotalProfit.Text = $"{totalProfit:0.##} ج.م";
                TxtProfitMargin.Text = profitMargin >= 0 ? $"هامش ربح {profitMargin:0.0}%" : $"خسارة {Math.Abs(profitMargin):0.0}%";
                TxtTotalCustomers.Text = $"{totalCustomers}";
                TxtNewCustomers.Text = $"{newCustomers} هذا الشهر";
                TxtPendingInvoices.Text = $"{pendingInvoices.Count}";
                TxtPendingAmount.Text = $"{pendingAmount:0.##} ج.م";
                TxtCancelledInvoices.Text = $"{cancelledInvoices.Count}";
                TxtCancelledAmount.Text = $"{cancelledAmount:0.##} ج.م";

                RecentInvoicesPanel.Children.Clear();
                foreach (var inv in recentInvoices)
                {
                    RecentInvoicesPanel.Children.Add(CreateRecentInvoiceCard(inv));
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطأ في تحميل لوحة التحكم: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            Text = $"{inv.NetAmount:0.##} ج.م",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = primaryBrush,
            TextAlignment = TextAlignment.Left
        });

        if (inv.Remaining > 0 && inv.Status != InvoiceStatus.Cancelled)
        {
            amountStack.Children.Add(new TextBlock
            {
                Text = $"متبقي {inv.Remaining:0.##}",
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
