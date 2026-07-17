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
        TxtInvoiceDate.Text = _invoice.CreatedAt.ToString("yyyy/MM/dd HH:mm");

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
        TxtTotal.Text = $"{_invoice.TotalAmount:N2} ج.م";
        TxtDiscount.Text = _invoice.Discount > 0 ? $"{_invoice.Discount:N2} ج.م" : "لا يوجد";
        TxtPaid.Text = $"{_invoice.TotalPaid:N2} ج.م";
        TxtRemaining.Text = $"{_invoice.Remaining:N2} ج.م";

        // Show/hide payment buttons
        BtnPayFull.Visibility = _invoice.Status == InvoiceStatus.Paid || _invoice.Status == InvoiceStatus.Cancelled
            ? Visibility.Collapsed : Visibility.Visible;
        BtnPayPartial.Visibility = BtnPayFull.Visibility;

        // Show/hide delete button
        BtnDeleteInvoice.Visibility = _invoice.Status == InvoiceStatus.Cancelled
            ? Visibility.Collapsed : Visibility.Visible;

        // Order items
        var items = _invoice.Orders
            .SelectMany(o => o.Items)
            .Select(oi =>
            {
                string qty = "";
                if (oi.CartonQuantity > 0) qty += $"{oi.CartonQuantity} كرتونة, ";
                if (oi.BoxQuantity > 0) qty += $"{oi.BoxQuantity} علبة, ";
                if (oi.PieceQuantity > 0) qty += $"{oi.PieceQuantity} قطعة, ";
                qty = qty.TrimEnd(',', ' ');
                return new { oi, qty };
            }).ToList();

        TxtItemCount.Text = $"{items.Count} منتج";
        ItemsPanel.Children.Clear();
        foreach (var item in items)
        {
            var card = CreateOrderItemCard(item.oi, item.qty);
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
                Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
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
                Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
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
        ProductsSection.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DiscountsSection.Visibility = _invoice.Discount > 0 ? Visibility.Visible : Visibility.Collapsed;
        PaymentsSection.Visibility = payments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateOrderItemCard(OrderItem oi, string qtyDisplay)
    {
        string priceTypeText = oi.PriceType == PriceType.Retail ? "قطاعي" : "جملة";
        string priceTypeBg = oi.PriceType == PriceType.Retail ? "#E3F2FD" : "#F3E5F5";
        string priceTypeFg = oi.PriceType == PriceType.Retail ? "#1565C0" : "#7B1FA2";

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Product icon
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

        // Product name & qty
        var nameStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        nameStack.Children.Add(new TextBlock { Text = oi.Product.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")! });
        nameStack.Children.Add(new TextBlock { Text = qtyDisplay, FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(nameStack);
        Grid.SetColumn(nameStack, 1);

        // Price type badge
        var priceBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = (Brush)new BrushConverter().ConvertFrom(priceTypeBg)!,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock { Text = priceTypeText, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom(priceTypeFg)! }
        };
        grid.Children.Add(priceBadge);
        Grid.SetColumn(priceBadge, 2);

        // Price & total
        var priceStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        priceStack.Children.Add(new TextBlock { Text = $"{oi.Total:N2} ج.م", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")!, HorizontalAlignment = HorizontalAlignment.Right });
        priceStack.Children.Add(new TextBlock { Text = $"{oi.UnitPrice:N2} للواحدة", FontSize = 10, Foreground = (Brush)new BrushConverter().ConvertFrom("#B0BEC5")!, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 1, 0, 0) });
        grid.Children.Add(priceStack);
        Grid.SetColumn(priceStack, 3);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!,
            Margin = new Thickness(16, 0, 16, 8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = grid
        };
    }

    private Border CreatePaymentCard(Payment p)
    {
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
        infoStack.Children.Add(new TextBlock { Text = $"{p.Amount:N2} ج.م", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")! });
        infoStack.Children.Add(new TextBlock { Text = $"{p.PaymentDate:yyyy/MM/dd HH:mm}  •  {p.PaymentMethod ?? "-"}", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, Margin = new Thickness(0, 2, 0, 0) });
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
                Fill = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
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
            Background = (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!,
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
            $"هل أنت متأكد من حذف هذه الدفعة بقيمة {payment.Amount:N2} ج.م؟\nسيتم تحديث رصيد الفاتورة تلقائياً.",
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
        infoStack.Children.Add(new TextBlock { Text = $"{_invoice.Discount:N2} ج.م", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#E65100")! });
        infoStack.Children.Add(new TextBlock { Text = "خصم على الفاتورة", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, Margin = new Thickness(0, 2, 0, 0) });
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
                Fill = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
            }
        };
        editBtn.Click += (_, _) => EditDiscount();

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
        deleteBtn.Click += (_, _) => DeleteDiscount();

        actionStack.Children.Add(editBtn);
        actionStack.Children.Add(deleteBtn);
        grid.Children.Add(actionStack);
        Grid.SetColumn(actionStack, 2);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!,
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
            $"هل أنت متأكد من حذف الخصم بقيمة {_invoice.Discount:N2} ج.م؟",
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

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}


