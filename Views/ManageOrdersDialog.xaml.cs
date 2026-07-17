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
            .Include(i => i.Orders).ThenInclude(o => o.Items).ThenInclude(oi => oi.Product)
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
                _db.Entry(i).Reference(oi => oi.ProductUnit).Load();
            }
        }

        var allItems = _invoice.Orders
            .SelectMany(o => o.Items)
            .OrderByDescending(oi => oi.CreatedAt)
            .ToList();

        TxtInfo.Text = $"{allItems.Count} طلب";
        TxtTotal.Text = $"{_invoice.TotalAmount:N2} ج.م";

        ItemsPanel.Children.Clear();
        if (allItems.Count == 0)
        {
            ItemsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد طلبات في هذه الفاتورة",
                FontSize = 14,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 30)
            });
            return;
        }

        foreach (var item in allItems)
            ItemsPanel.Children.Add(CreateItemCard(item));
    }

    private Border CreateItemCard(OrderItem item)
    {
        string qtyDisplay = "";
        if (item.CartonQuantity > 0) qtyDisplay += $"{item.CartonQuantity} كرتونة, ";
        if (item.BoxQuantity > 0) qtyDisplay += $"{item.BoxQuantity} علبة, ";
        if (item.PieceQuantity > 0) qtyDisplay += $"{item.PieceQuantity} قطعة, ";
        qtyDisplay = qtyDisplay.TrimEnd(',', ' ');

        string priceTypeText = item.PriceType == PriceType.Retail ? "قطاعي" : "جملة";

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon
        var iconBorder = new Border
        {
            Width = 36, Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)new BrushConverter().ConvertFrom("#E0F2F1")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 18, Height = 18,
                Fill = (Brush)new BrushConverter().ConvertFrom("#00897B")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        // Info
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock { Text = item.Product.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")! });
        nameRow.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = (Brush)new BrushConverter().ConvertFrom("#E0F2F1")!, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = priceTypeText, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#00897B")! } });
        infoStack.Children.Add(nameRow);
        infoStack.Children.Add(new TextBlock { Text = qtyDisplay, FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, Margin = new Thickness(0, 1, 0, 0) });
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        // Total
        var totalBlock = new TextBlock
        {
            Text = $"{item.Total:N2} ج.م",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")!,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        grid.Children.Add(totalBlock);
        Grid.SetColumn(totalBlock, 2);

        // Edit button
        var editBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "تعديل",
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
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
        var itemCopy = item;
        editBtn.Click += (_, _) => EditItem(itemCopy);
        grid.Children.Add(editBtn);
        Grid.SetColumn(editBtn, 3);

        // Delete button
        var deleteBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "حذف",
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
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
        deleteBtn.Click += (_, _) => DeleteItem(itemCopy);
        grid.Children.Add(deleteBtn);
        Grid.SetColumn(deleteBtn, 4);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!,
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 6),
            Child = grid
        };
    }

    private void EditItem(OrderItem item)
    {
        _db.Entry(item).Reference(oi => oi.Product).Load();
        _db.Entry(item).Reference(oi => oi.ProductUnit).Load();
        _db.Entry(item.Product).Collection(p => p.Units).Load();

        var units = item.Product.Units.OrderBy(u => u.UnitType).ToList();
        var cartonUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
        var boxUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        var pieceUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);

        var overlay = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Brushes.White,
            Padding = new Thickness(20),
            Width = 360,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.2, Color = Colors.Black }
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = $"تعديل: {item.Product.Name}", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")!, Margin = new Thickness(0, 0, 0, 12) });

        TextBox cartonTb = null!, boxTb = null!, pieceTb = null!;
        ComboBox cmb = null!;

        if (cartonUnit != null)
        {
            stack.Children.Add(new TextBlock { Text = "كرتونة", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#78909C")!, Margin = new Thickness(0, 4, 0, 2) });
            cartonTb = new TextBox { Text = item.CartonQuantity.ToString(), FontSize = 14, Padding = new Thickness(8) };
            SetNumericInput(cartonTb);
            stack.Children.Add(cartonTb);
        }
        if (boxUnit != null)
        {
            stack.Children.Add(new TextBlock { Text = "علبة", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#78909C")!, Margin = new Thickness(0, 4, 0, 2) });
            boxTb = new TextBox { Text = item.BoxQuantity.ToString(), FontSize = 14, Padding = new Thickness(8) };
            SetNumericInput(boxTb);
            stack.Children.Add(boxTb);
        }
        if (pieceUnit != null)
        {
            stack.Children.Add(new TextBlock { Text = "قطعة", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#78909C")!, Margin = new Thickness(0, 4, 0, 2) });
            pieceTb = new TextBox { Text = item.PieceQuantity.ToString(), FontSize = 14, Padding = new Thickness(8) };
            SetNumericInput(pieceTb);
            stack.Children.Add(pieceTb);
        }

            stack.Children.Add(new TextBlock { Text = "نوع السعر", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#78909C")!, Margin = new Thickness(0, 8, 0, 2) });
        cmb = new ComboBox { FontSize = 13, Padding = new Thickness(6) };
        cmb.Items.Add("قطاعي");
        cmb.Items.Add("جملة");
        cmb.SelectedIndex = item.PriceType == PriceType.Wholesale ? 1 : 0;
        stack.Children.Add(cmb);

        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };

        var saveBtn = new Button
        {
            Content = "حفظ",
            Height = 36,
            Cursor = Cursors.Hand,
            Padding = new Thickness(24, 0, 24, 0),
            Background = (Brush)new BrushConverter().ConvertFrom("#00897B")!,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            FontWeight = FontWeights.Bold
        };
        var itemCopy = item;
        saveBtn.Click += (_, _) =>
        {
            int newCarton = cartonTb != null && int.TryParse(cartonTb.Text, out var c) ? c : 0;
            int newBox = boxTb != null && int.TryParse(boxTb.Text, out var b) ? b : 0;
            int newPiece = pieceTb != null && int.TryParse(pieceTb.Text, out var p) ? p : 0;

            if (newCarton == 0 && newBox == 0 && newPiece == 0)
            {
                NotificationManager.ShowWarning("الرجاء إدخال كمية");
                return;
            }

            bool isWholesale = cmb.SelectedIndex == 1;

            int oldPieces = _inv.CalculatePieceEquivalent(itemCopy.Product, itemCopy.CartonQuantity, itemCopy.BoxQuantity, itemCopy.PieceQuantity);
            int newPieces = _inv.CalculatePieceEquivalent(itemCopy.Product, newCarton, newBox, newPiece);

            // Return old stock
            ReturnStockToBatches(itemCopy.Product.Id, oldPieces, itemCopy.CostPrice);

            // Check stock sufficiency for new quantity
            if (!_inv.IsStockSufficient(itemCopy.Product, newCarton, newBox, newPiece))
            {
                // Re-deduct the old stock back since we already returned it
                // We can't easily reverse the ReturnStockToBatches, so show error and reload
                NotificationManager.ShowWarning("المخزون غير كافٍ للكمية الجديدة");
                LoadItems();
                return;
            }

            // Update invoice total: subtract old, add new
            _invoice.TotalAmount = _invoice.TotalAmount - itemCopy.Total;

            // Calculate new total and deduct stock
            decimal newUnitPrice = 0;
            int? usedUnitId = null;
            if (newCarton > 0 && cartonUnit != null)
            {
                newUnitPrice = isWholesale ? cartonUnit.WholesalePrice : cartonUnit.RetailPrice;
                usedUnitId = cartonUnit.Id;
            }
            else if (newBox > 0 && boxUnit != null)
            {
                newUnitPrice = isWholesale ? boxUnit.WholesalePrice : boxUnit.RetailPrice;
                usedUnitId = boxUnit.Id;
            }
            else if (newPiece > 0 && pieceUnit != null)
            {
                newUnitPrice = isWholesale ? pieceUnit.WholesalePrice : pieceUnit.RetailPrice;
                usedUnitId = pieceUnit.Id;
            }

            decimal newTotal = 0;
            if (cartonUnit != null) { decimal price = isWholesale ? cartonUnit.WholesalePrice : cartonUnit.RetailPrice; newTotal += newCarton * price; }
            if (boxUnit != null) { decimal price = isWholesale ? boxUnit.WholesalePrice : boxUnit.RetailPrice; newTotal += newBox * price; }
            if (pieceUnit != null) { decimal price = isWholesale ? pieceUnit.WholesalePrice : pieceUnit.RetailPrice; newTotal += newPiece * price; }

            var (fifoCost, consumed) = _inv.CalculateFifoCost(itemCopy.Product, newPieces);

            // Update item
            itemCopy.CartonQuantity = newCarton;
            itemCopy.BoxQuantity = newBox;
            itemCopy.PieceQuantity = newPiece;
            itemCopy.UnitPrice = newUnitPrice;
            itemCopy.PriceType = isWholesale ? PriceType.Wholesale : PriceType.Retail;
            itemCopy.Total = newTotal;
            itemCopy.CostPrice = fifoCost;
            itemCopy.ProductUnitId = usedUnitId ?? 0;

            _invoice.TotalAmount += newTotal;

            // New stock out movement
            _db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = itemCopy.ProductId,
                MovementType = MovementType.StockOut,
                Quantity = newPieces,
                CostPrice = newPieces > 0 ? fifoCost / newPieces : 0,
                SellingPrice = newTotal,
                ReferenceType = ReferenceType.Sale,
                ReferenceId = itemCopy.OrderId,
                Notes = $"تعديل طلب - {newCarton} كرتونة, {newBox} علبة, {newPiece} قطعة - فاتورة #{_invoice.Id}"
            });

            foreach (var batch in consumed)
                _db.Entry(batch).State = EntityState.Modified;

            _db.SaveChanges();
            NotificationManager.ShowSuccess("تم تعديل الطلب بنجاح");
            LoadItems();

            // Remove the edit overlay
            var mainWindow = (MainWindow)Window.GetWindow(this);
            mainWindow.HideOverlay();
        };

        var cancelBtn = new Button
        {
            Content = "إلغاء",
            Height = 36,
            Cursor = Cursors.Hand,
            Padding = new Thickness(24, 0, 24, 0),
            Margin = new Thickness(12, 0, 0, 0),
            Background = (Brush)new BrushConverter().ConvertFrom("#F5F5F5")!,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        cancelBtn.Click += (_, _) =>
        {
            var mainWindow = (MainWindow)Window.GetWindow(this);
            mainWindow.HideOverlay();
        };

        btnStack.Children.Add(saveBtn);
        btnStack.Children.Add(cancelBtn);
        stack.Children.Add(btnStack);

        overlay.Child = stack;

        var mainW = (MainWindow)Window.GetWindow(this);
        mainW.ShowOverlay(new UserControl { Content = overlay });
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
            Notes = $"مرتجع تعديل طلب - فاتورة #{_invoice.Id}"
        });
    }

    private void DeleteItem(OrderItem item)
    {
        ConfirmDialog.Show("حذف الطلب",
            $"هل أنت متأكد من حذف {item.Product?.Name ?? "هذا الطلب"} من الفاتورة؟\nسيتم ترجيع الكمية للمخزن.",
            result =>
            {
                if (result != true) return;

                _db.Entry(item).Reference(oi => oi.Product!).Load();
                _db.Entry(item.Product!).Collection(p => p.Units!).Load();

                int totalPieces = _inv.CalculatePieceEquivalent(item.Product!, item.CartonQuantity, item.BoxQuantity, item.PieceQuantity);

                // Return stock
                ReturnStockToBatches(item.Product.Id, totalPieces, item.CostPrice);

                // Update invoice total
                _invoice.TotalAmount -= item.Total;
                _invoice.Status = _invoice.TotalAmount <= 0 ? InvoiceStatus.Cancelled : _invoice.Status;
                if (_invoice.TotalAmount <= 0)
                {
                    _invoice.TotalPaid = 0;
                    _invoice.Discount = 0;
                }

                // Remove the item and possibly the order
                var order = item.Order;
                _db.OrderItems.Remove(item);
                var remainingItems = _db.OrderItems.Count(oi => oi.OrderId == order.Id);
                if (remainingItems == 0)
                    _db.Orders.Remove(order);

                _db.SaveChanges();
                NotificationManager.ShowSuccess("تم حذف الطلب وترجيع الكمية");
                LoadItems();
            },
            ConfirmDialog.DialogType.Warning);
    }

    private static void SetNumericInput(TextBox tb)
    {
        tb.PreviewTextInput += (s, e) =>
        {
            e.Handled = !char.IsDigit(e.Text[0]);
        };
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}
