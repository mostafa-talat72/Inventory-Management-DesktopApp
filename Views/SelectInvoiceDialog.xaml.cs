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

public partial class SelectInvoiceDialog : UserControl
{
    public event Action<Invoice?>? InvoiceSelected;

    private readonly AppDbContext _db;
    private readonly Customer? _customer;
    private readonly BrushConverter _bc = new();

    public SelectInvoiceDialog(AppDbContext db, Customer? customer)
    {
        InitializeComponent();
        _db = db;
        _customer = customer;

        var name = customer?.Name ?? "نقدي";
        TxtSubtitle.Text = name;
        TxtInfo.Text = $"العميل {name} لديه فواتير غير مدفوعة. يمكنك اختيار إحداها لإضافة الطلب إليها أو إنشاء فاتورة جديدة.";

        LoadInvoices();
    }

    private void LoadInvoices()
    {
        var invoices = _db.Invoices
            .Include(i => i.Orders)
            .ThenInclude(o => o.Items)
            .Where(i => (_customer == null ? i.CustomerId == null : i.CustomerId == _customer.Id)
                && (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid))
            .OrderByDescending(i => i.Id)
            .ToList();

        InvoiceListPanel.Children.Clear();
        foreach (var inv in invoices)
            InvoiceListPanel.Children.Add(CreateInvoiceCard(inv));
    }

    private Border CreateInvoiceCard(Invoice inv)
    {
        var orderCount = inv.Orders.Count;
        var itemCount = inv.Orders.Sum(o => o.Items.Count);

        var statusColor = inv.Status == InvoiceStatus.PartiallyPaid ? "#F57F17" : "#1565C0";
        var statusText  = inv.Status == InvoiceStatus.PartiallyPaid ? "مدفوع جزئياً" : "مفتوح";

        // Theme-aware brushes
        var cardBg      = Application.Current.TryFindResource("CardBackground")    as Brush ?? Brushes.White;
        var hoverBg     = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)_bc.ConvertFrom("#F5F5F5")!;
        var cardBorder  = Application.Current.TryFindResource("BorderBrushLight")  as Brush ?? (Brush)_bc.ConvertFrom("#E0E0E0")!;
        var headingFg   = Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)_bc.ConvertFrom("#263238")!;
        var primaryFg   = Application.Current.TryFindResource("PrimaryTextBrush")  as Brush ?? (Brush)_bc.ConvertFrom("#1A237E")!;
        var mutedFg     = Application.Current.TryFindResource("MutedTextBrush")    as Brush ?? (Brush)_bc.ConvertFrom("#90A4AE")!;
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
            Tag = inv
        };
        border.MouseLeftButtonDown += (_, _) => SelectInvoice(inv);
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

        // Icon badge — invoice ID
        var iconBorder = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(12),
            Background = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)_bc.ConvertFrom("#E3F2FD")!,
            Child = new TextBlock
            {
                Text = $"#{inv.Id}",
                FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = (Brush)_bc.ConvertFrom(statusColor)!,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        row.Children.Add(iconBorder);

        var infoStack = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = $"فاتورة #{inv.Id}  •  {inv.CreatedAt:yyyy/MM/dd}",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = headingFg
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{orderCount} طلب  •  {itemCount} صنف",
            FontSize = 11, Foreground = mutedFg,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(infoStack, 1);
        row.Children.Add(infoStack);

        // Amount + remaining
        var amountStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        amountStack.Children.Add(new TextBlock
        {
            Text = $"{inv.NetAmount:0.##} ج.م",
            FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = primaryFg,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        amountStack.Children.Add(new TextBlock
        {
            Text = $"متبقي: {inv.Remaining:0.##} ج.م",
            FontSize = 10, Foreground = (Brush)_bc.ConvertFrom("#C62828")!,
            Margin = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetColumn(amountStack, 2);
        row.Children.Add(amountStack);

        border.Child = row;
        return border;
    }

    private void SelectInvoice(Invoice? invoice)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.HideOverlay();
        InvoiceSelected?.Invoke(invoice);
    }

    private void NewInvoiceCard_Click(object sender, MouseButtonEventArgs e)
    {
        SelectInvoice(null);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.HideOverlay();
    }
}
