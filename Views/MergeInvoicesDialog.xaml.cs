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

public partial class MergeInvoicesDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Customer? _customer;
    private readonly bool _isCashMode;
    private List<Invoice> _allInvoices = new();
    private HashSet<int> _selectedIds = new();
    private int? _targetInvoiceId;
    private string _filterMode = "All";

    public MergeInvoicesDialog(AppDbContext db, Customer? customer, bool isCashMode)
    {
        InitializeComponent();
        _db = db;
        _customer = customer;
        _isCashMode = isCashMode;

        TxtCustomerName.Text = isCashMode ? "نقدي" : customer?.Name ?? "نقدي";
        LoadData();
    }

    private void LoadData()
    {
        _allInvoices = _db.Invoices
            .Where(i => _isCashMode ? i.CustomerId == null : i.CustomerId == _customer!.Id)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        SetFilter("All");
    }

    private void ApplyFilter()
    {
        var filtered = _filterMode switch
        {
            "Unpaid" => _allInvoices.Where(i => i.Status == InvoiceStatus.Open).ToList(),
            "PartiallyPaid" => _allInvoices.Where(i => i.Status == InvoiceStatus.PartiallyPaid).ToList(),
            "Paid" => _allInvoices.Where(i => i.Status == InvoiceStatus.Paid).ToList(),
            "Cancelled" => _allInvoices.Where(i => i.Status == InvoiceStatus.Cancelled).ToList(),
            _ => _allInvoices
        };

        InvoicesPanel.Children.Clear();
        foreach (var inv in filtered)
            InvoicesPanel.Children.Add(CreateInvoiceCard(inv));

        TxtHint.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelectionInfo();
    }

    private Border CreateInvoiceCard(Invoice invoice)
    {
        var isSelected = _selectedIds.Contains(invoice.Id);
        var isTarget = _targetInvoiceId == invoice.Id;

        var (statusText, statusBg, statusFg) = invoice.Status switch
        {
            InvoiceStatus.Paid => ("مدفوعة", "#E8F5E9", "#2E7D32"),
            InvoiceStatus.PartiallyPaid => ("مدفوعة جزئياً", "#FFF8E1", "#F57F17"),
            InvoiceStatus.Cancelled => ("ملغاة", "#F5F5F5", "#9E9E9E"),
            _ => ("غير مدفوعة", "#FFEBEE", "#C62828")
        };
        var statusBgBrush = (Brush)new BrushConverter().ConvertFrom(statusBg)!;
        var statusFgBrush = (Brush)new BrushConverter().ConvertFrom(statusFg)!;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Checkbox
        var checkBorder = new Border
        {
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(4),
            Background = isSelected ? (Brush)new BrushConverter().ConvertFrom("#7B1FA2")! : Brushes.Transparent,
            BorderBrush = (Brush)new BrushConverter().ConvertFrom("#BDBDBD")!,
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };
        if (isSelected)
        {
            checkBorder.Child = new Path
            {
                Width = 12, Height = 12,
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z")
            };
        }

        // Icon
        var iconBorder = new Border
        {
            Width = 36, Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = statusBgBrush,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 18, Height = 18,
                Fill = statusFgBrush,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z")
            }
        };

        // Info
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        var row1 = new StackPanel { Orientation = Orientation.Horizontal };
        row1.Children.Add(new TextBlock { Text = $"#{invoice.Id}", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")! });
        row1.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = statusBgBrush, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = statusText, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = statusFgBrush } });
        if (isTarget)
        {
            row1.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = (Brush)new BrushConverter().ConvertFrom("#F3E5F5")!, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "الهدف", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#7B1FA2")! } });
        }
        infoStack.Children.Add(row1);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
        row2.Children.Add(new TextBlock { Text = invoice.CreatedAt.ToString("yyyy/MM/dd"), FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")! });
        row2.Children.Add(new TextBlock { Text = " • ", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#CFD8DC")! });
        row2.Children.Add(new TextBlock { Text = $"{invoice.TotalAmount:N2} ج.م", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")! });
        if (invoice.Discount > 0)
            row2.Children.Add(new TextBlock { Text = $"خصم {invoice.Discount:N2}", FontSize = 10, Foreground = (Brush)new BrushConverter().ConvertFrom("#F57F17")!, Margin = new Thickness(6, 0, 0, 0) });
        infoStack.Children.Add(row2);

        grid.Children.Add(checkBorder);
        Grid.SetColumn(checkBorder, 0);
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 1);
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 2);

        // Target radio (only if selected)
        if (isSelected)
        {
            var radioBorder = new Border
            {
                Width = 20, Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = isTarget ? (Brush)new BrushConverter().ConvertFrom("#7B1FA2")! : Brushes.Transparent,
                BorderBrush = isTarget ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFrom("#BDBDBD")!,
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = "تعيين كفاتورة هدف"
            };
            if (isTarget)
            {
                radioBorder.Child = new Path
                {
                    Width = 8, Height = 8,
                    Fill = Brushes.White,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2z")
                };
            }
            grid.Children.Add(radioBorder);
            Grid.SetColumn(radioBorder, 3);

            var invCopy = invoice;
            radioBorder.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                SetTarget(invCopy.Id);
            };
        }

        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = isTarget ? (Brush)new BrushConverter().ConvertFrom("#F3E5F5")!
                : isSelected ? (Brush)new BrushConverter().ConvertFrom("#F5F5F5")!
                : Brushes.Transparent,
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(4, 0, 4, 4),
            Cursor = Cursors.Hand,
            Child = grid
        };

        // Handle checkbox click separately to avoid double-fire
        var invCopy2 = invoice;
        checkBorder.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            ToggleSelection(invCopy2.Id);
        };

        card.MouseLeftButtonDown += (_, e) =>
        {
            // Only toggle if click is not on an inner handled element
            ToggleSelection(invCopy2.Id);
        };

        return card;
    }

    private void ToggleSelection(int invoiceId)
    {
        if (_selectedIds.Contains(invoiceId))
        {
            _selectedIds.Remove(invoiceId);
            if (_targetInvoiceId == invoiceId)
                _targetInvoiceId = null;
        }
        else
        {
            _selectedIds.Add(invoiceId);
            // When a new invoice is added, clear target so user picks explicitly
            _targetInvoiceId = null;
        }

        ApplyFilter();
        UpdateSelectionInfo();
    }

    private void SetTarget(int invoiceId)
    {
        if (!_selectedIds.Contains(invoiceId)) return;
        _targetInvoiceId = invoiceId;
        ApplyFilter();
        UpdateSelectionInfo();
    }

    private void UpdateSelectionInfo()
    {
        var count = _selectedIds.Count;
        TxtSelectedCount.Text = count.ToString();

        if (count == 0)
        {
            TxtSelectionInfo.Text = "لم يتم اختيار أي فاتورة";
            TxtHint.Visibility = Visibility.Visible;
            TxtHint.Text = "اختر فاتورتين أو أكثر للدمج";
        }
        else if (count == 1)
        {
            TxtSelectionInfo.Text = "تم اختيار فاتورة واحدة، اختر المزيد";
            TxtHint.Visibility = Visibility.Visible;
            TxtHint.Text = "اختر فاتورة أخرى على الأقل";
        }
        else if (_targetInvoiceId == null)
        {
            TxtSelectionInfo.Text = $"تم اختيار {count} فواتير";
            TxtHint.Visibility = Visibility.Visible;
            TxtHint.Text = "اختر الفاتورة الهدف من الدائرة ○ بجانب كل فاتورة";
            TxtHint.Foreground = (Brush)new BrushConverter().ConvertFrom("#7B1FA2")!;
            TxtHint.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            TxtSelectionInfo.Text = $"تم اختيار {count} فواتير";
            TxtHint.Visibility = Visibility.Collapsed;
        }

        BtnMerge.IsEnabled = count >= 2 && _targetInvoiceId != null;

        var target = _targetInvoiceId != null ? _allInvoices.FirstOrDefault(i => i.Id == _targetInvoiceId) : null;
        TargetBadge.Visibility = target != null ? Visibility.Visible : Visibility.Collapsed;
        if (target != null)
        {
            TxtTargetInfo.Text = $"الهدف: فاتورة #{target.Id} ({target.TotalAmount:N2} ج.م)";
        }
    }

    private void BtnMerge_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIds.Count < 2 || _targetInvoiceId == null) return;

        ConfirmDialog.Show("تأكيد دمج الفواتير",
            $"هل أنت متأكد من دمج {_selectedIds.Count} فواتير في فاتورة #{_targetInvoiceId}؟\n" +
            $"سيتم نقل جميع الطلبات والمدفوعات والخصومات إلى الفاتورة الهدف.\n" +
            $"سيتم حذف الفواتير المصدر نهائياً.",
            result =>
            {
                if (result != true) return;

                var target = _db.Invoices
                    .Include(i => i.Orders).ThenInclude(o => o.Items)
                    .Include(i => i.Payments)
                    .First(i => i.Id == _targetInvoiceId);

                foreach (var srcId in _selectedIds.Where(id => id != _targetInvoiceId))
                {
                    var src = _db.Invoices
                        .Include(i => i.Orders).ThenInclude(o => o.Items)
                        .Include(i => i.Payments)
                        .First(i => i.Id == srcId);

                    // Move orders
                    foreach (var order in src.Orders.ToList())
                    {
                        order.InvoiceId = target.Id;
                        target.Orders.Add(order);
                    }

                    // Move payments
                    foreach (var payment in src.Payments.ToList())
                    {
                        payment.InvoiceId = target.Id;
                        target.Payments.Add(payment);
                    }

                    // Sum discounts
                    target.Discount += src.Discount;

                    // Delete source invoice (orders/payments already detached)
                    _db.Invoices.Remove(src);
                }

                // Recalculate target
                target.TotalAmount = target.Orders
                    .SelectMany(o => o.Items)
                    .Sum(oi => oi.Total);
                target.TotalPaid = target.Payments.Sum(p => p.Amount);
                target.Status = target.Remaining <= 0 ? InvoiceStatus.Paid
                    : target.TotalPaid > 0 ? InvoiceStatus.PartiallyPaid
                    : InvoiceStatus.Open;

                _db.SaveChanges();
                NotificationManager.ShowSuccess($"تم دمج {_selectedIds.Count} فواتير بنجاح");
                DialogClosed?.Invoke(this, true);
            },
            ConfirmDialog.DialogType.Warning);
    }

    private void SetFilter(string mode)
    {
        _filterMode = mode;
        var activeColor = (Brush)new BrushConverter().ConvertFrom("#7B1FA2")!;
        var inactiveColor = Brushes.Transparent;

        foreach (var btn in new[] { BtnAll, BtnUnpaid, BtnPartiallyPaid, BtnPaid, BtnCancelled })
            btn.Background = inactiveColor;
        foreach (var txt in new[] { TxtAll, TxtUnpaid, TxtPartiallyPaid, TxtPaid, TxtCancelled })
        { txt.Foreground = (Brush)new BrushConverter().ConvertFrom("#546E7A")!; txt.FontWeight = FontWeights.SemiBold; }

        var (activeBtn, activeTxt) = mode switch
        {
            "Unpaid" => (BtnUnpaid, TxtUnpaid),
            "PartiallyPaid" => (BtnPartiallyPaid, TxtPartiallyPaid),
            "Paid" => (BtnPaid, TxtPaid),
            "Cancelled" => (BtnCancelled, TxtCancelled),
            _ => (BtnAll, TxtAll)
        };
        activeBtn.Background = activeColor;
        activeTxt.Foreground = Brushes.White;
        activeTxt.FontWeight = FontWeights.Bold;

        ApplyFilter();
    }

    private void BtnAll_Click(object sender, MouseButtonEventArgs e) => SetFilter("All");
    private void BtnUnpaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("Unpaid");
    private void BtnPartiallyPaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("PartiallyPaid");
    private void BtnPaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("Paid");
    private void BtnCancelled_Click(object sender, MouseButtonEventArgs e) => SetFilter("Cancelled");

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
