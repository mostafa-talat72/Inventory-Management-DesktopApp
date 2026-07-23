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

public partial class InvoiceDetailsDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private Invoice _invoice = null!;

    public InvoiceDetailsDialog(AppDbContext db, Invoice invoice)
    {
        InitializeComponent();
        _db = db;
        _invoice = invoice;

        LoadData();
    }

    private void LoadData()
    {
        _invoice = _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.Product)
            .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.ProductUnit)
            .Include(i => i.Payments)
            .First(i => i.Id == _invoice.Id);

        var customerName = _invoice.Customer?.Name ?? _invoice.CustomerName ?? "نقدي";
        TxtTitle.Text = $"فاتورة #{_invoice.Id}";
        TxtCustomerName.Text = customerName;
        TxtInvoiceDate.Text = $"{_invoice.CreatedAt:yyyy/MM/dd hh:mm} {(_invoice.CreatedAt.Hour < 12 ? "ص" : "م")}";

        // Status badge
        var (statusText, statusBg, statusFg) = _invoice.Status switch
        {
            InvoiceStatus.Paid => ("مدفوعة", "#E8F5E9", "#2E7D32"),
            InvoiceStatus.PartiallyPaid => ("مدفوعة جزئياً", "#FFF8E1", "#F57F17"),
            InvoiceStatus.Cancelled => ("ملغاة", "#F5F5F5", "#9E9E9E"),
            _ => ("غير مدفوعة", "#FFEBEE", "#C62828")
        };
        StatusBadge.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(statusBg)!;
        TxtStatus.Text = statusText;
        TxtStatus.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(statusFg)!;

        // Summary
        TxtTotal.Text = $"{_invoice.TotalAmount:0.##} ج.م";
        TxtDiscount.Text = _invoice.Discount > 0 ? $"{_invoice.Discount:0.##} ج.م" : "لا يوجد";
        TxtPaid.Text = $"{_invoice.TotalPaid:0.##} ج.م";
        TxtRemaining.Text = $"{_invoice.Remaining:0.##} ج.م";

        // Show/hide payment buttons
        BtnPayFull.Visibility = _invoice.Status == InvoiceStatus.Paid || _invoice.Status == InvoiceStatus.Cancelled
            ? Visibility.Collapsed : Visibility.Visible;
        BtnPayPartial.Visibility = BtnPayFull.Visibility;

        // Show/hide delete button
        BtnDeleteInvoice.Visibility = _invoice.Status == InvoiceStatus.Cancelled
            ? Visibility.Collapsed : Visibility.Visible;

        // Show/hide discount button
        BtnDiscount.Visibility = _invoice.Status == InvoiceStatus.Paid || _invoice.Status == InvoiceStatus.Cancelled
            ? Visibility.Collapsed : Visibility.Visible;

        // Order items — group by product, merge retail + wholesale
        var productGroups = _invoice.Orders
            .SelectMany(o => o.Items)
            .GroupBy(oi => oi.Product)
            .ToList();

        TxtItemCount.Text = $"{productGroups.Count} منتج";
        ItemsPanel.Children.Clear();
        foreach (var group in productGroups)
        {
            var retail = group.Where(oi => oi.PriceType == PriceType.Retail).ToList();
            var wholesale = group.Where(oi => oi.PriceType == PriceType.Wholesale).ToList();

            var card = CreateProductCard(group.Key,
                retail.Sum(oi => oi.CartonQuantity), retail.Sum(oi => oi.BoxQuantity), retail.Sum(oi => oi.PieceQuantity), retail.Sum(oi => oi.Total),
                wholesale.Sum(oi => oi.CartonQuantity), wholesale.Sum(oi => oi.BoxQuantity), wholesale.Sum(oi => oi.PieceQuantity), wholesale.Sum(oi => oi.Total));
            ItemsPanel.Children.Add(card);
        }

        // Payments
        var payments = _invoice.Payments
            .OrderByDescending(p => p.PaymentDate)
            .ToList();

        TxtPaymentCount.Text = $"{payments.Count} دفعة";

        // Discounts
        TxtDiscountCount.Text = _invoice.Discount > 0 ? "1 خصم" : "0 خصم";
        DiscountsPanel.Children.Clear();
        if (_invoice.Discount > 0)
        {
            DiscountsPanel.Children.Add(CreateDiscountCard());
        }
        else
        {
            DiscountsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد خصومات",
                FontSize = 13,
                Foreground = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            });
        }

        PaymentsPanel.Children.Clear();
        if (payments.Count == 0)
        {
            PaymentsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد مدفوعات بعد",
                FontSize = 13,
                Foreground = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            });
        }
        else
        {
            foreach (var p in payments)
                PaymentsPanel.Children.Add(CreatePaymentCard(p));
        }

        // Show/hide body sections
        ProductsSection.Visibility = productGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DiscountsSection.Visibility = _invoice.Discount > 0 ? Visibility.Visible : Visibility.Collapsed;
        PaymentsSection.Visibility = payments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateProductCard(Product product,
        int rCarton, int rBox, int rPiece, decimal rTotal,
        int wCarton, int wBox, int wPiece, decimal wTotal)
    {
        var cardBg      = Application.Current.TryFindResource("CardBackground")    as Brush ?? Brushes.White;
        var surfaceBg   = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!;
        var headingFg   = Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var primaryFg   = Application.Current.TryFindResource("PrimaryTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#1A237E")!;
        var bodyFg      = Application.Current.TryFindResource("BodyTextBrush")     as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;

        string RetailQty()
        {
            string s = "";
            if (rCarton > 0) s += $"{rCarton} كرتونة, ";
            if (rBox > 0) s += $"{rBox} علبة, ";
            if (rPiece > 0) s += $"{rPiece} قطعة, ";
            return s.TrimEnd(',', ' ');
        }
        string WholesaleQty()
        {
            string s = "";
            if (wCarton > 0) s += $"{wCarton} كرتونة, ";
            if (wBox > 0) s += $"{wBox} علبة, ";
            if (wPiece > 0) s += $"{wPiece} قطعة, ";
            return s.TrimEnd(',', ' ');
        }

        bool hasRetail = rTotal > 0;
        bool hasWholesale = wTotal > 0;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Icon
        var iconBorder = new Border
        {
            Width = 40, Height = 40,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)new BrushConverter().ConvertFrom("#E8EAF6")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 20, Height = 20,
                Fill = (Brush)new BrushConverter().ConvertFrom("#3F51B5")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        // Right side: product name + retail/wholesale rows + total
        var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };

        // Product name
        rightStack.Children.Add(new TextBlock
        {
            Text = product.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = headingFg
        });

        // Retail row
        if (hasRetail)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            row.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = (Brush)new BrushConverter().ConvertFrom("#1565C0")!, Padding = new Thickness(5, 1, 5, 1), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "قطاعي", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White } });
            row.Children.Add(new TextBlock { Text = $" {RetailQty()}", FontSize = 11, Foreground = bodyFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
            rightStack.Children.Add(row);
        }

        // Wholesale row
        if (hasWholesale)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            row.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = (Brush)new BrushConverter().ConvertFrom("#00897B")!, Padding = new Thickness(5, 1, 5, 1), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "جملة", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White } });
            row.Children.Add(new TextBlock { Text = $" {WholesaleQty()}", FontSize = 11, Foreground = bodyFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
            rightStack.Children.Add(row);
        }

        // Total
        decimal combined = rTotal + wTotal;
        rightStack.Children.Add(new TextBlock
        {
            Text = $"{combined:0.##} ج.م",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = primaryFg,
            Margin = new Thickness(0, 3, 0, 0)
        });

        grid.Children.Add(rightStack);
        Grid.SetColumn(rightStack, 1);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = surfaceBg,
            Margin = new Thickness(16, 0, 16, 8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = grid
        };
    }

    private Border CreatePaymentCard(Payment p)
    {
        var surfaceBg  = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!;
        var headingFg  = Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg    = Application.Current.TryFindResource("MutedTextBrush")    as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;
        var bodyFg     = Application.Current.TryFindResource("BodyTextBrush")     as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            Width = 36, Height = 36,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)new BrushConverter().ConvertFrom("#E8F5E9")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 18, Height = 18,
                Fill = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        infoStack.Children.Add(new TextBlock { Text = $"{p.Amount:0.##} ج.م", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = headingFg });
        infoStack.Children.Add(new TextBlock { Text = $"{p.PaymentDate:yyyy/MM/dd HH:mm}  •  {p.PaymentMethod ?? "-"}", FontSize = 11, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);



        // Action buttons
        var actionStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };

        var editBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "تعديل",
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = bodyFg,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
            }
        };
        var paymentCopy = p;
        editBtn.Click += (_, _) => EditPayment(paymentCopy);

        var deleteBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "حذف",
            Margin = new Thickness(4, 0, 0, 0),
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = (Brush)new BrushConverter().ConvertFrom("#E53935")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z")
            }
        };
        deleteBtn.Click += (_, _) => DeletePayment(paymentCopy);

        actionStack.Children.Add(editBtn);
        actionStack.Children.Add(deleteBtn);
        grid.Children.Add(actionStack);
        Grid.SetColumn(actionStack, 3);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = surfaceBg,
            Margin = new Thickness(16, 0, 16, 6),
            Padding = new Thickness(16, 10, 16, 10),
            Child = grid
        };
    }

    private void EditPayment(Payment payment)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new PaymentEditDialog(_db, _invoice, payment);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_invoice).Reload();
                LoadData();
            }
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void DeletePayment(Payment payment)
    {
        ConfirmDialog.Show("حذف الدفعة",
            $"هل أنت متأكد من حذف هذه الدفعة بقيمة {payment.Amount:0.##} ج.م؟\nسيتم تحديث رصيد الفاتورة تلقائياً.",
            result =>
            {
                if (result != true) return;
                _db.Payments.Remove(payment);
                _invoice.TotalPaid = _invoice.Payments.Sum(p => p.Amount);
                _invoice.Status = _invoice.Remaining <= 0 ? InvoiceStatus.Paid
                    : _invoice.TotalPaid > 0 ? InvoiceStatus.PartiallyPaid
                    : InvoiceStatus.Open;
                _db.SaveChanges();
                _db.Entry(_invoice).Reload();
                LoadData();
                NotificationManager.ShowSuccess("تم حذف الدفعة بنجاح");
            },
            ConfirmDialog.DialogType.Warning);
    }

    private Border CreateDiscountCard()
    {
        var surfaceBg  = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!;
        var mutedFg    = Application.Current.TryFindResource("MutedTextBrush")    as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;
        var bodyFg     = Application.Current.TryFindResource("BodyTextBrush")     as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            Width = 36, Height = 36,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)new BrushConverter().ConvertFrom("#FFF8E1")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 18, Height = 18,
                Fill = (Brush)new BrushConverter().ConvertFrom("#F57F17")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        infoStack.Children.Add(new TextBlock { Text = $"{_invoice.Discount:0.##} ج.م", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#E65100")! });
        infoStack.Children.Add(new TextBlock { Text = "خصم على الفاتورة", FontSize = 11, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        // Action buttons
        var actionStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };

        var editBtn2 = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "تعديل",
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = bodyFg,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
            }
        };
        editBtn2.Click += (_, _) => EditDiscount();

        var deleteBtn2 = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "حذف",
            Margin = new Thickness(4, 0, 0, 0),
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = (Brush)new BrushConverter().ConvertFrom("#E53935")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z")
            }
        };
        deleteBtn2.Click += (_, _) => DeleteDiscount();

        actionStack.Children.Add(editBtn2);
        actionStack.Children.Add(deleteBtn2);
        grid.Children.Add(actionStack);
        Grid.SetColumn(actionStack, 2);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = surfaceBg,
            Margin = new Thickness(16, 0, 16, 6),
            Padding = new Thickness(16, 10, 16, 10),
            Child = grid
        };
    }

    private void EditDiscount()
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DiscountDialog(_db, _invoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_invoice).Reload();
                LoadData();
            }
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void DeleteDiscount()
    {
        ConfirmDialog.Show("حذف الخصم",
            $"هل أنت متأكد من حذف الخصم بقيمة {_invoice.Discount:0.##} ج.م؟",
            result =>
            {
                if (result != true) return;
                _invoice.Discount = 0;
                _db.SaveChanges();
                _db.Entry(_invoice).Reload();
                LoadData();
                NotificationManager.ShowSuccess("تم حذف الخصم بنجاح");
            },
            ConfirmDialog.DialogType.Warning);
    }

    private void BtnDiscount_Click(object sender, RoutedEventArgs e)
    {
        EditDiscount();
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var printer = new ReceiptPrinter(_db);
        printer.Print(_invoice);
    }

    private void BtnPayFull_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ConfirmPaymentDialog(_db, _invoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_invoice).Reload();
                LoadData();
            }
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void BtnPayPartial_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new PaymentDialog(_db, _invoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_invoice).Reload();
                LoadData();
            }
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void BtnDeleteInvoice_Click(object sender, RoutedEventArgs e)
    {
        ConfirmDialog.Show("حذف الفاتورة",
            $"هل أنت متأكد من حذف الفاتورة #{_invoice.Id}؟\nلا يمكن التراجع عن هذا الإجراء.",
            result =>
            {
                if (result != true) return;
                _db.Entry(_invoice).Reload();
                _db.Entry(_invoice).Collection(i => i.Orders).Load();
                foreach (var order in _invoice.Orders)
                {
                    _db.Entry(order).Collection(o => o.Items).Load();
                    foreach (var item in order.Items)
                    {
                        _db.Entry(item).Reference(oi => oi.Product).Load();
                        _db.Entry(item).Reference(oi => oi.ProductUnit).Load();
                        if (item.ProductUnit != null)
                        {
                            var inv = new InventoryService(_db);
                            int totalPieces = inv.CalculatePieceEquivalent(item.Product, item.CartonQuantity, item.BoxQuantity, item.PieceQuantity);
                            if (totalPieces > 0)
                            {
                                var batch = _db.InventoryBatches
                                    .Where(b => b.ProductId == item.ProductId && b.RemainingQuantity > 0)
                                    .OrderByDescending(b => b.PurchaseDate)
                                    .FirstOrDefault();
                                if (batch != null)
                                    batch.RemainingQuantity += totalPieces;
                                else
                                {
                                    _db.InventoryBatches.Add(new InventoryBatch
                                    {
                                        ProductId = item.ProductId,
                                        CostPricePerPiece = item.CostPrice / totalPieces,
                                        InitialQuantity = totalPieces,
                                        RemainingQuantity = totalPieces,
                                        PurchaseDate = DateTime.Now
                                    });
                                }
                                _db.InventoryMovements.Add(new InventoryMovement
                                {
                                    ProductId = item.ProductId,
                                    MovementType = MovementType.Return,
                                    Quantity = totalPieces,
                                    CostPrice = item.CostPrice / totalPieces,
                                    ReferenceType = ReferenceType.Return,
                                    ReferenceId = _invoice.Id,
                                    Notes = $"مرتجعات بيع - فاتورة #{_invoice.Id}"
                                });
                            }
                        }
                    }
                    _db.OrderItems.RemoveRange(order.Items);
                }
                _db.Payments.RemoveRange(_invoice.Payments);
                _db.Orders.RemoveRange(_invoice.Orders);
                _db.Invoices.Remove(_invoice);
                _db.SaveChanges();
                NotificationManager.ShowSuccess("تم حذف الفاتورة وترجيع الكميات للمخزن بنجاح");
                DialogClosed?.Invoke(this, true);
            },
            ConfirmDialog.DialogType.Warning);
    }

    private void BtnAddOrder_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new AddOrderDialog(_db, _invoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_invoice).Reload();
                LoadData();
            }
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void BtnManageOrders_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ManageOrdersDialog(_db, _invoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                // تحقق إن الفاتورة لسه موجودة قبل ما نحاول نحملها
                bool invoiceExists = _db.Invoices.Any(i => i.Id == _invoice.Id);
                if (!invoiceExists)
                {
                    // الفاتورة اتحذفت — أغلق نافذة التفاصيل
                    DialogClosed?.Invoke(this, true);
                    return;
                }
                _db.Entry(_invoice).Reload();
                LoadData();
            }
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void BtnTransfer_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ChangeCustomerDialog(_db, _invoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_invoice).Reload();
                LoadData();
            }
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}


