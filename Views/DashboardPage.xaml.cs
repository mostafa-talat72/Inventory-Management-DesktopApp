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
            TxtDashboardDate.Text = $"آخر تحديث: {DateTime.Now:yyyy/MM/dd - hh:mm tt}";

            var now = DateTime.Now;
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
        var remaining = inv.Remaining;

        var border = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(0, 10, 0, 10),
            Cursor = Cursors.Hand,
            Tag = inv.Id
        };
        border.MouseLeftButtonDown += (_, _) => OpenInvoice(inv.Id);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = (Brush)new BrushConverter().ConvertFrom(brush)!,
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = statusText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            }
        };
        row.Children.Add(statusBadge);

        var infoStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        infoStack.Children.Add(new TextBlock
        {
            Text = $"فاتورة #{inv.Id} - {(inv.CustomerName ?? "عميل نقدي")}",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#263238")!
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{inv.CreatedAt:yyyy/MM/dd - hh:mm tt}",
            FontSize = 10,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(infoStack, 1);
        row.Children.Add(infoStack);

        var amountBlock = new TextBlock
        {
            Text = $"{inv.NetAmount:0.##} ج.م",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")!,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(amountBlock, 2);
        row.Children.Add(amountBlock);

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
