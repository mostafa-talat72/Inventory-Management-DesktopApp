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

public partial class ManageOrdersDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private Invoice _invoice;

    public ManageOrdersDialog(AppDbContext db, Invoice invoice)
    {
        InitializeComponent();
        _db = db;
        _inv = new InventoryService(db);
        _invoice = db.Invoices
            .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.Product).ThenInclude(p => p.Units)
            .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.ProductUnit)
            .First(i => i.Id == invoice.Id);

        TxtTitle.Text = $"إدارة الطلبات - فاتورة #{_invoice.Id}";
        TxtSubtitle.Text = _invoice.CustomerName ?? "نقدي";
        LoadItems();
    }

    private void LoadItems()
    {
        _db.Entry(_invoice).Reload();
        _db.Entry(_invoice).Collection(i => i.Orders).Load();
        foreach (var o in _invoice.Orders)
        {
            _db.Entry(o).Collection(o2 => o2.Items).Load();
            foreach (var i in o.Items)
            {
                _db.Entry(i).Reference(oi => oi.Product).Load();
                _db.Entry(i.Product).Collection(p => p.Units).Load();
                _db.Entry(i).Reference(oi => oi.ProductUnit).Load();
            }
        }

        var orders = _invoice.Orders.OrderByDescending(o => o.CreatedAt).ToList();
        int totalItems = orders.Sum(o => o.Items.Count);

        TxtInfo.Text = $"{orders.Count} طلب - {totalItems} منتج";
        TxtTotal.Text = $"{_invoice.TotalAmount:0.##} ج.م";

        ItemsPanel.Children.Clear();
        if (orders.Count == 0 || totalItems == 0)
        {
            ItemsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد طلبات في هذه الفاتورة",
                FontSize = 14,
                Foreground = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 30)
            });
            return;
        }

        int orderIndex = orders.Count;
        foreach (var order in orders)
            ItemsPanel.Children.Add(CreateOrderCard(order, orderIndex--));
    }

    private Border CreateOrderCard(Order order, int orderIndex)
    {
        decimal orderTotal = order.Items.Sum(i => i.Total);

        // ── Outer card ──
        var cardBg2     = Application.Current.TryFindResource("CardBackground")    as Brush ?? Brushes.White;
        var cardBorder2 = Application.Current.TryFindResource("BorderBrushLight")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
        var headingFg2  = Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg2    = Application.Current.TryFindResource("MutedTextBrush")    as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;
        var divider2    = Application.Current.TryFindResource("DividerBrush")      as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F0F0F0")!;
        var surfaceBg2  = Application.Current.TryFindResource("CardBackgroundAlt") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F5F5F5")!;
        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = cardBg2,
            BorderBrush = cardBorder2,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 10),
        };
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.08, Color = Colors.Black };

        var cardStack = new StackPanel();

        // ── Order header ──
        var header = new Border
        {
            Background = surfaceBg2,
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Order number badge
        var badge = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = (Brush)new BrushConverter().ConvertFrom("#1A237E")!,
            Padding = new Thickness(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"طلب #{orderIndex}",
                FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            }
        };
        Grid.SetColumn(badge, 0);
        headerGrid.Children.Add(badge);

        // Date + items count
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{order.CreatedAt:yyyy/MM/dd - hh:mm} {(order.CreatedAt.Hour < 12 ? "ص" : "م")}",
            FontSize = 11, Foreground = mutedFg2
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{order.Items.Count} منتج",
            FontSize = 10, Foreground = mutedFg2
        });
        Grid.SetColumn(infoStack, 1);
        headerGrid.Children.Add(infoStack);

        // Total
        var totalBlock = new TextBlock
        {
            Text = $"{orderTotal:0.##} ج.م",
            FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = Application.Current.TryFindResource("PrimaryTextBrush") as Brush
                         ?? (Brush)new BrushConverter().ConvertFrom("#1A237E")!,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(totalBlock, 2);
        headerGrid.Children.Add(totalBlock);

        // Edit button
        var bodyFg2 = Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;
        var editBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = "تعديل",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = bodyFg2,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
            }
        };
        var orderRef = order;
        editBtn.Click += (_, _) => EditOrder(orderRef);
        Grid.SetColumn(editBtn, 3);
        headerGrid.Children.Add(editBtn);

        // Delete button
        var deleteBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = "حذف",
            VerticalAlignment = VerticalAlignment.Center,
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
        deleteBtn.Click += (_, _) => DeleteOrder(orderRef);
        Grid.SetColumn(deleteBtn, 4);
        headerGrid.Children.Add(deleteBtn);

        header.Child = headerGrid;
        cardStack.Children.Add(header);

        // ── Items inside order ──
        var itemsStack = new StackPanel { Margin = new Thickness(14, 6, 14, 10) };
        foreach (var item in order.Items.OrderBy(i => i.Product.Name))
        {
            string qtyDisplay = "";
            if (item.CartonQuantity > 0) qtyDisplay += $"{item.CartonQuantity} كرتونة  ";
            if (item.BoxQuantity    > 0) qtyDisplay += $"{item.BoxQuantity} علبة  ";
            if (item.PieceQuantity  > 0) qtyDisplay += $"{item.PieceQuantity} قطعة";
            qtyDisplay = qtyDisplay.Trim();

            string priceTypeText = item.PriceType == PriceType.Retail ? "قطاعي" : "جملة";

            var rowGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(new TextBlock
            {
                Text = item.Product.Name, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = headingFg2
            });
            nameRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = item.PriceType == PriceType.Wholesale
                    ? (Brush)new BrushConverter().ConvertFrom("#E8F5E9")!
                    : (Brush)new BrushConverter().ConvertFrom("#E3F2FD")!,
                Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = priceTypeText, FontSize = 9, FontWeight = FontWeights.Bold,
                    Foreground = item.PriceType == PriceType.Wholesale
                        ? (Brush)new BrushConverter().ConvertFrom("#2E7D32")!
                        : (Brush)new BrushConverter().ConvertFrom("#1565C0")!
                }
            });
            nameStack.Children.Add(nameRow);
            nameStack.Children.Add(new TextBlock
            {
                Text = qtyDisplay, FontSize = 10,
                Foreground = mutedFg2,
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(nameStack, 0);
            rowGrid.Children.Add(nameStack);

            var itemTotal = new TextBlock
            {
                Text = $"{item.Total:0.##} ج.م", FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = Application.Current.TryFindResource("PrimaryTextBrush") as Brush
                             ?? (Brush)new BrushConverter().ConvertFrom("#1A237E")!,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(itemTotal, 1);
            rowGrid.Children.Add(itemTotal);

            itemsStack.Children.Add(rowGrid);

            // Divider between items (not after last)
            if (item != order.Items.OrderBy(i => i.Product.Name).Last())
                itemsStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = divider2,
                    Margin = new Thickness(0, 2, 0, 2)
                });
        }
        cardStack.Children.Add(itemsStack);
        card.Child = cardStack;
        return card;
    }

    private static Button CreateIconButton(string color, string pathData, string tooltip)
    {
        return new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = tooltip,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = (Brush)new BrushConverter().ConvertFrom(color)!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse(pathData)
            }
        };
    }

    private void EditOrder(Order order)
    {
        // فتح AddOrderDialog مع تحميل بيانات الطلب الحالي للتعديل
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new AddOrderDialog(_db, _invoice, order);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (_, result) =>
        {
            mainWindow.HideOverlay();
            if (result == true)
            {
                // إعادة تحميل الفاتورة بعد التعديل
                _invoice = _db.Invoices
                    .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.Product).ThenInclude(p => p.Units)
                    .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.ProductUnit)
                    .First(i => i.Id == _invoice.Id);
                LoadItems();
            }
        };
    }

    private void DeleteOrder(Order order)
    {
        int itemCount = order.Items.Count;
        ConfirmDialog.Show("حذف الطلب",
            $"هل أنت متأكد من حذف هذا الطلب ({itemCount} منتج)؟\nسيتم ترجيع جميع الكميات للمخزن.",
            result =>
            {
                if (result != true) return;

                // ترجيع مخزون كل item في الطلب
                foreach (var item in order.Items.ToList())
                {
                    _db.Entry(item).Reference(oi => oi.Product!).Load();
                    _db.Entry(item.Product!).Collection(p => p.Units!).Load();

                    int pieces = _inv.CalculatePieceEquivalent(item.Product!, item.CartonQuantity, item.BoxQuantity, item.PieceQuantity);
                    ReturnStockToBatches(item.Product!.Id, pieces, item.CostPrice);

                    _invoice.TotalAmount -= item.Total;
                }

                if (_invoice.TotalAmount < 0) _invoice.TotalAmount = 0;
                if (_invoice.TotalAmount <= 0) { _invoice.TotalPaid = 0; _invoice.Discount = 0; }

                // حذف الـ order (cascade يحذف الـ items)
                _db.Orders.Remove(order);
                _db.SaveChanges();

                // لو الفاتورة فضلت فارغة تتحذف تلقائياً
                var remainingOrders = _db.Orders.Count(o => o.InvoiceId == _invoice.Id);
                if (remainingOrders == 0)
                {
                    var payments = _db.Payments.Where(p => p.InvoiceId == _invoice.Id).ToList();
                    _db.Payments.RemoveRange(payments);
                    _db.Invoices.Remove(_invoice);
                    _db.SaveChanges();

                    NotificationManager.ShowSuccess($"تم حذف الفاتورة #{_invoice.Id} تلقائياً لأنها أصبحت فارغة");
                    DialogClosed?.Invoke(this, true);
                    return;
                }

                NotificationManager.ShowSuccess($"تم حذف الطلب وترجيع {itemCount} منتج للمخزن");
                _invoice = _db.Invoices
                    .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.Product).ThenInclude(p => p.Units)
                    .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.ProductUnit)
                    .First(i => i.Id == _invoice.Id);
                LoadItems();
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void ReturnStockToBatches(int productId, int totalPieces, decimal costPrice)
    {
        if (totalPieces <= 0) return;
        var batch = _db.InventoryBatches
            .Where(b => b.ProductId == productId && b.RemainingQuantity > 0)
            .OrderByDescending(b => b.PurchaseDate)
            .FirstOrDefault();
        if (batch != null)
            batch.RemainingQuantity += totalPieces;
        else
        {
            _db.InventoryBatches.Add(new InventoryBatch
            {
                ProductId = productId,
                CostPricePerPiece = totalPieces > 0 ? costPrice / totalPieces : 0,
                InitialQuantity = totalPieces,
                RemainingQuantity = totalPieces,
                PurchaseDate = DateTime.Now
            });
        }

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = productId,
            MovementType = MovementType.Return,
            Quantity = totalPieces,
            CostPrice = totalPieces > 0 ? costPrice / totalPieces : 0,
            ReferenceType = ReferenceType.Return,
            ReferenceId = _invoice.Id,
            Notes = $"مرتجع حذف طلب - فاتورة #{_invoice.Id}"
        });
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}
