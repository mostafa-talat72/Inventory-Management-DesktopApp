using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProductApp.Views
{
    public partial class AddOrderDialog : UserControl
    {
        public event EventHandler<bool?>? DialogClosed;

        private readonly AppDbContext _db;
        private readonly InventoryService _inv;
        private readonly Customer? _customer;
        private readonly bool _forceNewInvoice;
        private Invoice? _invoice;
        private Order? _orderToEdit;
        private readonly Dictionary<int, OrderItemEntry> _entries = new();
        private readonly Dictionary<int, System.Windows.Threading.DispatcherTimer> _flashTimers = new();
        private readonly Dictionary<int, Brush?> _flashOriginals = new();
        private readonly System.Windows.Threading.DispatcherTimer _searchTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };

        private void InitSearchTimer()
        {
            _searchTimer.Tick += (_, _) =>
            {
                _searchTimer.Stop();
                LoadProducts(TxtSearch.Text.Trim());
            };
        }

        private class OrderItemEntry
        {
            public Product Product { get; set; } = null!;
            public ProductUnit? CartonUnit, BoxUnit, PieceUnit;
            public Border Container = null!;
            public TextBox RetailCartonTb = null!, RetailBoxTb = null!, RetailPieceTb = null!;
            public TextBox WholesaleCartonTb = null!, WholesaleBoxTb = null!, WholesalePieceTb = null!;
            public TextBlock TotalTb = null!;

            public int RetailCarton => ParseInt(RetailCartonTb?.Text);
            public int RetailBox => ParseInt(RetailBoxTb?.Text);
            public int RetailPiece => ParseInt(RetailPieceTb?.Text);
            public int WholesaleCarton => ParseInt(WholesaleCartonTb?.Text);
            public int WholesaleBox => ParseInt(WholesaleBoxTb?.Text);
            public int WholesalePiece => ParseInt(WholesalePieceTb?.Text);

            public decimal RetailTotal
            {
                get
                {
                    decimal t = 0;
                    if (CartonUnit != null) t += RetailCarton * CartonUnit.RetailPrice;
                    if (BoxUnit != null) t += RetailBox * BoxUnit.RetailPrice;
                    if (PieceUnit != null) t += RetailPiece * PieceUnit.RetailPrice;
                    return t;
                }
            }

            public decimal WholesaleTotal
            {
                get
                {
                    decimal t = 0;
                    if (CartonUnit != null) t += WholesaleCarton * CartonUnit.WholesalePrice;
                    if (BoxUnit != null) t += WholesaleBox * BoxUnit.WholesalePrice;
                    if (PieceUnit != null) t += WholesalePiece * PieceUnit.WholesalePrice;
                    return t;
                }
            }

            public decimal Total => RetailTotal + WholesaleTotal;
        }

        public AddOrderDialog(AppDbContext db, Customer? customer, bool forceNewInvoice = false)
        {
            InitializeComponent();
            _db = db;
            _inv = new InventoryService(db);
            _customer = customer;
            _forceNewInvoice = forceNewInvoice;
            InitSearchTimer();
            Loaded += OnLoaded;
        }

        public AddOrderDialog(AppDbContext db, Invoice invoice)
        {
            InitializeComponent();
            _db = db;
            _inv = new InventoryService(db);
            _invoice = invoice;
            _customer = invoice.Customer;
            InitSearchTimer();
            Loaded += OnLoadedForInvoice;
        }

        // Constructor للتعديل على طلب موجود
        public AddOrderDialog(AppDbContext db, Invoice invoice, Order orderToEdit)
        {
            InitializeComponent();
            _db = db;
            _inv = new InventoryService(db);
            _invoice = invoice;
            _customer = invoice.Customer;
            _orderToEdit = orderToEdit;
            InitSearchTimer();
            Loaded += OnLoadedForEditOrder;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_forceNewInvoice)
                _invoice = FindExistingInvoice();
            TxtSubtitle.Text = _customer?.Name ?? "نقدي";
            if (_invoice != null)
            {
                InvoiceBadge.Visibility = Visibility.Visible;
                TxtInvoiceId.Text = _invoice.Id.ToString();
            }
            else
            {
                InvoiceBadge.Visibility = Visibility.Collapsed;
            }
            LoadProducts();
        }

        private void OnLoadedForInvoice(object sender, RoutedEventArgs e)
        {
            TxtSubtitle.Text = _invoice!.CustomerName ?? "نقدي";
            InvoiceBadge.Visibility = Visibility.Visible;
            TxtInvoiceId.Text = _invoice.Id.ToString();
            LoadProducts();
        }

        private void OnLoadedForEditOrder(object sender, RoutedEventArgs e)
        {
            TxtSubtitle.Text = _invoice!.CustomerName ?? "نقدي";
            InvoiceBadge.Visibility = Visibility.Visible;
            TxtInvoiceId.Text = _invoice.Id.ToString();

            // تغيير نص الزر للتعديل
            if (BtnSave is Button saveBtn)
                saveBtn.Content = "حفظ التعديلات";

            // في وضع التعديل: احمل المنتجات الموجودة في الطلب فقط
            if (_orderToEdit != null)
            {
                _db.Entry(_orderToEdit).Collection(o => o.Items).Load();
                foreach (var item in _orderToEdit.Items)
                {
                    _db.Entry(item).Reference(oi => oi.Product).Load();
                    _db.Entry(item.Product).Collection(p => p.Units).Load();
                }

                var productsInOrder = _orderToEdit.Items
                    .Select(i => i.Product)
                    .DistinctBy(p => p.Id)
                    .ToList();

                foreach (var product in productsInOrder)
                    AddProductToOrder(product);

                PreFillOrderItems(_orderToEdit);
            }

            LoadProducts();
        }

        private void PreFillOrderItems(Order order)
        {
            // تعبئة الـ entries بكميات الطلب القديم
            foreach (var item in order.Items)
            {
                if (!_entries.TryGetValue(item.ProductId, out var entry)) continue;

                bool isWholesale = item.PriceType == PriceType.Wholesale;
                if (isWholesale)
                {
                    if (entry.WholesaleCartonTb != null) entry.WholesaleCartonTb.Text = item.CartonQuantity.ToString();
                    if (entry.WholesaleBoxTb    != null) entry.WholesaleBoxTb.Text    = item.BoxQuantity.ToString();
                    if (entry.WholesalePieceTb  != null) entry.WholesalePieceTb.Text  = item.PieceQuantity.ToString();
                }
                else
                {
                    if (entry.RetailCartonTb != null) entry.RetailCartonTb.Text = item.CartonQuantity.ToString();
                    if (entry.RetailBoxTb    != null) entry.RetailBoxTb.Text    = item.BoxQuantity.ToString();
                    if (entry.RetailPieceTb  != null) entry.RetailPieceTb.Text  = item.PieceQuantity.ToString();
                }
            }
            UpdateSummary();
        }

        private Invoice? FindExistingInvoice()
        {
            if (_customer != null)
                return _db.Invoices
                    .Where(i => i.CustomerId == _customer.Id
                        && (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid))
                    .OrderByDescending(i => i.Id)
                    .FirstOrDefault();
            else
                return _db.Invoices
                    .Where(i => i.CustomerId == null
                        && (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid))
                    .OrderByDescending(i => i.Id)
                    .FirstOrDefault();
        }

        private void LoadProducts(string? filter = null)
        {
            var query = _db.Products.Include(p => p.Units).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter))
                query = query.Where(p => p.Name.Contains(filter) || (p.Barcode != null && p.Barcode.Contains(filter)));

            var products = query.ToList();

            var totals = _db.InventoryBatches
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(b => b.RemainingQuantity) })
                .ToDictionary(x => x.ProductId, x => x.Total);

            var items = products
                .OrderBy(p => p.IsFavorite ? 0 : 1)
                .ThenBy(p => p.Name)
                .Select(p =>
                {
                    var units = p.Units.OrderBy(u => u.UnitType).ToList();
                    string unitInfo = string.Join(" | ", units.Select(u => $"{u.Name} {u.RetailPrice:0.##}"));
                    string priceInfo = units.Any() ? $"قطاعي: {units.Min(u => u.RetailPrice):0.##}  جملة: {units.Min(u => u.WholesalePrice):0.##}" : "";
                    var stock = InventoryService.GetStockDisplay(p.Units, totals.GetValueOrDefault(p.Id));
                    var cmd = new RelayCommand(() => SelectProduct(p));
                    return new { p.Name, UnitsDisplay = unitInfo, StockDisplay = stock, PriceDisplay = priceInfo, SelectCommand = cmd };
                }).ToList();

            ProductCards.ItemsSource = items;
        }

        private void SelectProduct(Product product)
        {
            if (_entries.ContainsKey(product.Id))
            {
                var entry = _entries[product.Id];
                entry.Container.BringIntoView();
                // أوقف timer سابق لنفس المنتج
                if (_flashTimers.TryGetValue(product.Id, out var old))
                {
                    old.Stop();
                    _flashTimers.Remove(product.Id);
                    if (_flashOriginals.TryGetValue(product.Id, out var savedBrush))
                    {
                        entry.Container.Background = savedBrush;
                        _flashOriginals.Remove(product.Id);
                    }
                }
                var original = entry.Container.Background;
                _flashOriginals[product.Id] = original;
                int step = 0;
                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(180) };
                _flashTimers[product.Id] = timer;
                timer.Tick += (_, _) =>
                {
                    step++;
                    entry.Container.Background = step % 2 == 1
                        ? new SolidColorBrush(Color.FromRgb(0x7C, 0x9B, 0xFF))
                        : original;
                    if (step >= 4)
                    {
                        timer.Stop();
                        _flashTimers.Remove(product.Id);
                        _flashOriginals.Remove(product.Id);
                        entry.Container.Background = original;
                    }
                };
                timer.Start();
                return;
            }
            AddProductToOrder(product);
        }

        private static void FlashEntry(Border container)
        {
            FlashBorder(container, Color.FromRgb(0x7C, 0x9B, 0xFF));
        }

        private static void FlashBorder(Border border, Color flashColor)
        {
            // أوقف timer سابق مخزَّن في Tag
            if (border.Tag is System.Windows.Threading.DispatcherTimer old)
            {
                old.Stop();
            }
            // استعد اللون الأصلي إن كان مخزَّناً
            if (border.Tag is Brush savedBrush)
                border.Background = savedBrush;

            var original = border.Background;
            int step = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(180) };
            // خزّن الـ timer والـ original معاً باستخدام Tuple
            border.Tag = (timer, original);
            timer.Tick += (_, _) =>
            {
                step++;
                border.Background = step % 2 == 1
                    ? new SolidColorBrush(flashColor)
                    : original;
                if (step >= 4)
                {
                    timer.Stop();
                    border.Background = original;
                    border.Tag = null;
                }
            };
            timer.Start();
        }

        private void AddProductToOrder(Product product)
        {
            _db.Entry(product).Collection(p => p.Units).Load();
            var units = product.Units.OrderBy(u => u.UnitType).ToList();
            var cartonUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
            var boxUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
            var pieceUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);

            // Theme-aware brushes
            var cardBg      = Application.Current.TryFindResource("CardBackground")    as Brush ?? Brushes.White;
            var surfaceBg   = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!;
            var inputBg     = Application.Current.TryFindResource("InputBackground")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#FAFAFA")!;
            var headingFg   = Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
            var primaryFg   = Application.Current.TryFindResource("PrimaryTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#1A237E")!;
            var bodyFg      = Application.Current.TryFindResource("BodyTextBrush")     as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;
            var cardBorder  = Application.Current.TryFindResource("BorderBrushLight")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
            var borderBrush = Application.Current.TryFindResource("BorderBrush")       as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;

            var entry = new OrderItemEntry
            {
                Product = product,
                CartonUnit = cartonUnit,
                BoxUnit = boxUnit,
                PieceUnit = pieceUnit,
            };

            // ── Outer card ──
            var outer = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = cardBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // name + remove
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // retail
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // wholesale
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // total

            // ── Row 0: Product name + remove button ──
            var nameRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBadgeRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            nameBadgeRow.Children.Add(new Border
            {
                Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                Background = primaryFg, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            nameBadgeRow.Children.Add(new TextBlock
            {
                Text = product.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = headingFg,
                VerticalAlignment = VerticalAlignment.Center
            });
            nameRow.Children.Add(nameBadgeRow);

            var removeBtn = new Button
            {
                Width = 26, Height = 26,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "إزالة المنتج",
                Content = new Path
                {
                    Width = 13, Height = 13,
                    Fill = (Brush)new BrushConverter().ConvertFrom("#E53935")!,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z")
                }
            };
            var productId = product.Id;
            removeBtn.Click += (_, _) => RemoveEntry(productId);
            Grid.SetColumn(removeBtn, 1);
            nameRow.Children.Add(removeBtn);
            Grid.SetRow(nameRow, 0);
            mainGrid.Children.Add(nameRow);

            // ── Row 1: Retail section ──
            var retailBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = surfaceBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var retailGrid = new Grid();
            retailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < 3; i++)
                retailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            int rCol = 0;
            retailGrid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)),
                Padding = new Thickness(7, 2, 7, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock { Text = "قطاعي", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
            });
            Grid.SetColumn(retailGrid.Children[^1], rCol++);

            if (cartonUnit != null) { var s = MakeQtyField(cartonUnit.Name, out var tb, inputBg, headingFg, cardBorder); entry.RetailCartonTb = tb; tb.TextChanged += RecalcAll; Grid.SetColumn(s, rCol++); retailGrid.Children.Add(s); }
            if (boxUnit != null)    { var s = MakeQtyField(boxUnit.Name,   out var tb, inputBg, headingFg, cardBorder); entry.RetailBoxTb    = tb; tb.TextChanged += RecalcAll; Grid.SetColumn(s, rCol++); retailGrid.Children.Add(s); }
            if (pieceUnit != null)  { var s = MakeQtyField(pieceUnit.Name, out var tb, inputBg, headingFg, cardBorder); entry.RetailPieceTb  = tb; tb.TextChanged += RecalcAll; Grid.SetColumn(s, rCol++); retailGrid.Children.Add(s); }
            retailBorder.Child = retailGrid;
            Grid.SetRow(retailBorder, 1);
            mainGrid.Children.Add(retailBorder);

            // ── Row 2: Wholesale section ──
            var wholesaleBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = surfaceBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var wholesaleGrid = new Grid();
            wholesaleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < 3; i++)
                wholesaleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            int wCol = 0;
            wholesaleGrid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(0x00, 0x89, 0x7B)),
                Padding = new Thickness(7, 2, 7, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock { Text = "جملة", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
            });
            Grid.SetColumn(wholesaleGrid.Children[^1], wCol++);

            if (cartonUnit != null) { var s = MakeQtyField(cartonUnit.Name, out var tb, inputBg, headingFg, cardBorder); entry.WholesaleCartonTb = tb; tb.TextChanged += RecalcAll; Grid.SetColumn(s, wCol++); wholesaleGrid.Children.Add(s); }
            if (boxUnit != null)    { var s = MakeQtyField(boxUnit.Name,   out var tb, inputBg, headingFg, cardBorder); entry.WholesaleBoxTb    = tb; tb.TextChanged += RecalcAll; Grid.SetColumn(s, wCol++); wholesaleGrid.Children.Add(s); }
            if (pieceUnit != null)  { var s = MakeQtyField(pieceUnit.Name, out var tb, inputBg, headingFg, cardBorder); entry.WholesalePieceTb  = tb; tb.TextChanged += RecalcAll; Grid.SetColumn(s, wCol++); wholesaleGrid.Children.Add(s); }
            wholesaleBorder.Child = wholesaleGrid;
            Grid.SetRow(wholesaleBorder, 2);
            mainGrid.Children.Add(wholesaleBorder);

            // ── Row 3: Total ──
            entry.TotalTb = new TextBlock
            {
                Text = "0.00 ج.م",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = primaryFg,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(4, 4, 0, 0)
            };
            Grid.SetRow(entry.TotalTb, 3);
            mainGrid.Children.Add(entry.TotalTb);

            outer.Child = mainGrid;
            entry.Container = outer;

            _entries[product.Id] = entry;
            OrderItemsPanel.Children.Add(outer);
            OrderScrollViewer.ScrollToBottom();
            UpdateSummary();
        }

        private static StackPanel MakeQtyField(string label, out TextBox tb, Brush? bgBrush = null, Brush? fgBrush = null, Brush? borderBrush = null)
        {
            var bg  = bgBrush     ?? Application.Current.TryFindResource("InputBackground")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#FAFAFA")!;
            var fg  = fgBrush     ?? Application.Current.TryFindResource("HeadingTextBrush")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
            var brd = borderBrush ?? Application.Current.TryFindResource("BorderBrushLight")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
            var lbl = Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = lbl,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });
            tb = new TextBox
            {
                Text = "0",
                Width = 44,
                Height = 28,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = bg,
                Foreground = fg,
                CaretBrush = fg,
                BorderBrush = brd,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2, 2, 2, 2)
            };
            tb.PreviewTextInput += (s, e) => e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");
            sp.Children.Add(tb);
            return sp;
        }

        private void RemoveEntry(int productId)
        {
            if (!_entries.TryGetValue(productId, out var entry)) return;
            OrderItemsPanel.Children.Remove(entry.Container);
            _entries.Remove(productId);
            UpdateSummary();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_entries.Count == 0) return;
            if (MessageBox.Show("تفريغ كل المنتجات من الطلب؟", "", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            foreach (var entry in _entries.Values)
                OrderItemsPanel.Children.Remove(entry.Container);
            _entries.Clear();
            UpdateSummary();
        }

        private void RecalcAll(object sender, TextChangedEventArgs e)
        {
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int count = _entries.Count;
            TxtItemCount.Text = count > 0 ? $"{count}" : "0";
            bool hasAnyQty = _entries.Values.Any(e => e.Total > 0);
            BtnSave.IsEnabled = hasAnyQty;

            if (count > 0)
            {
                CartBadge.Visibility = Visibility.Visible;
                TxtCartCount.Text = count.ToString();

                decimal grandTotal = 0;
                foreach (var entry in _entries.Values)
                {
                    decimal t = entry.Total;
                    entry.TotalTb.Text = $"{t:0.##} ج.م";
                    grandTotal += t;
                }
                TxtGrandTotal.Text = $"{grandTotal:0.##} ج.م";
            }
            else
            {
                CartBadge.Visibility = Visibility.Collapsed;
                TxtGrandTotal.Text = "0 ج.م";
            }
        }

        private void BtnSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_entries.Count == 0)
                {
                    NotificationManager.ShowWarning("لم يتم إضافة أي منتجات.");
                    return;
                }

                if (!_entries.Values.Any(entry => entry.Total > 0))
                {
                    NotificationManager.ShowWarning("يجب تحديد كمية واحدة على الأقل لأحد المنتجات.");
                    return;
                }

                // لو وضع تعديل — ارجع مخزون الطلب القديم أولاً (بدون فحص مسبق)
                if (_orderToEdit != null)
                {
                    SaveEditOrder();
                    return;
                }

                // Pre-check stock for new orders only
                foreach (var entry in _entries.Values)
                {
                    if (entry.RetailCarton > 0 || entry.RetailBox > 0 || entry.RetailPiece > 0)
                    {
                        if (!_inv.IsStockSufficient(entry.Product, entry.RetailCarton, entry.RetailBox, entry.RetailPiece))
                        {
                            NotificationManager.ShowWarning($"المخزون غير كافٍ لـ {entry.Product.Name} (قطاعي)");
                            return;
                        }
                    }
                    if (entry.WholesaleCarton > 0 || entry.WholesaleBox > 0 || entry.WholesalePiece > 0)
                    {
                        if (!_inv.IsStockSufficient(entry.Product, entry.WholesaleCarton, entry.WholesaleBox, entry.WholesalePiece))
                        {
                            NotificationManager.ShowWarning($"المخزون غير كافٍ لـ {entry.Product.Name} (جملة)");
                            return;
                        }
                    }
                }

                // Create invoice if none existed
                if (_invoice == null)
                {
                    _invoice = new Invoice
                    {
                        CustomerId = _customer?.Id,
                        CustomerName = _customer?.Name ?? "نقدي",
                        InvoiceDate = DateTime.Now,
                        TotalAmount = 0,
                        Status = InvoiceStatus.Open
                    };
                    _db.Invoices.Add(_invoice);
                    _db.SaveChanges();

                    App.AppBackup?.BackupIfOnOperation();
                    InvoiceBadge.Visibility = Visibility.Visible;
                    TxtInvoiceId.Text = _invoice.Id.ToString();
                }

                // Create a new Order for this batch
                var order = new Order { InvoiceId = _invoice.Id };
                _db.Orders.Add(order);
                _db.SaveChanges();

                foreach (var entry in _entries.Values)
                {
                    if (entry.RetailCarton > 0 || entry.RetailBox > 0 || entry.RetailPiece > 0)
                        AddOrderItem(order, entry, PriceType.Retail);
                    if (entry.WholesaleCarton > 0 || entry.WholesaleBox > 0 || entry.WholesalePiece > 0)
                        AddOrderItem(order, entry, PriceType.Wholesale);
                }

                // Save items first, then recalculate total from DB
                _db.SaveChanges();
                var orderIds = _db.Orders.Where(o => o.InvoiceId == _invoice.Id).Select(o => o.Id).ToList();
                _invoice.TotalAmount = _db.OrderItems.Where(oi => orderIds.Contains(oi.OrderId)).Sum(oi => oi.Total);
                _db.SaveChanges();

                App.AppBackup?.BackupIfOnOperation();

                int count = _entries.Count;
                _entries.Clear();
                OrderItemsPanel.Children.Clear();
                UpdateSummary();
                TxtInvoiceId.Text = _invoice.Id.ToString();

                NotificationManager.ShowSuccess($"تم إضافة {count} منتج للفاتورة #{_invoice.Id} بنجاح.");
                OpenInvoiceDialog(_invoice);
                PrintInvoiceAfterSave();
            }
            catch (System.Exception ex)
            {
                LogError(ex);
                NotificationManager.ShowError($"حدث خطأ أثناء الحفظ: {ex.Message}");
            }
        }

        private void LogError(System.Exception ex)
        {
            try
            {
                var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n";
                if (ex.InnerException != null)
                    msg += $"INNER:\n{ex.InnerException}\n";
                var localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                System.IO.File.AppendAllText(localPath, msg);
            }
            catch { }
        }

        private void SaveEditOrder()
        {
            try
            {
                var order = _orderToEdit!;

                // 1- اجمع بيانات القديم لكل منتج (عدد القطع + التكلفة)
                var oldData = new Dictionary<int, (int pieces, decimal cost)>();
                foreach (var oldItem in order.Items.ToList())
                {
                    _db.Entry(oldItem).Reference(oi => oi.Product!).Load();
                    int p = _inv.CalculatePieceEquivalent(oldItem.Product!, oldItem.CartonQuantity, oldItem.BoxQuantity, oldItem.PieceQuantity);
                    if (oldData.ContainsKey(oldItem.ProductId))
                        oldData[oldItem.ProductId] = (oldData[oldItem.ProductId].pieces + p, oldData[oldItem.ProductId].cost + oldItem.CostPrice);
                    else
                        oldData[oldItem.ProductId] = (p, oldItem.CostPrice);
                }

                // 2- احسب الجديد لكل منتج
                var newPiecesTotal = new Dictionary<int, int>();
                foreach (var entry in _entries.Values)
                {
                    int retail = _inv.CalculatePieceEquivalent(entry.Product, entry.RetailCarton, entry.RetailBox, entry.RetailPiece);
                    int wholesale = _inv.CalculatePieceEquivalent(entry.Product, entry.WholesaleCarton, entry.WholesaleBox, entry.WholesalePiece);
                    int total = retail + wholesale;
                    newPiecesTotal[entry.Product.Id] = newPiecesTotal.GetValueOrDefault(entry.Product.Id) + total;
                }

                // 3- افحص المخزون للزيادات فقط
                foreach (var pid in newPiecesTotal.Keys)
                {
                    int oldP = oldData.GetValueOrDefault(pid).pieces;
                    int newP = newPiecesTotal[pid];
                    int diff = newP - oldP;
                    if (diff > 0)
                    {
                        var prod = _db.Products.Find(pid);
                        if (prod != null && !_inv.IsStockSufficient(prod, 0, 0, diff))
                        {
                            NotificationManager.ShowWarning($"المخزون غير كافٍ لـ {prod.Name}");
                            return;
                        }
                    }
                }

                // 4- احذف الـ items القديمة من السياق (المخزون لسه ما ائثرش)
                _invoice!.TotalAmount = 0;
                foreach (var oldItem in order.Items.ToList())
                {
                    _invoice.TotalAmount -= oldItem.Total;
                    _db.OrderItems.Remove(oldItem);
                }

                // 5- طبق الفرق الصافي في المخزون + سجل الحركة
                var newCostByProduct = new Dictionary<int, decimal>();
                var productNewTotalPieces = new Dictionary<int, int>();
                foreach (var pid in oldData.Keys.Union(newPiecesTotal.Keys))
                {
                    int oldP = oldData.GetValueOrDefault(pid).pieces;
                    int newP = newPiecesTotal.GetValueOrDefault(pid, 0);
                    int diff = newP - oldP;
                    decimal oldCost = oldData.GetValueOrDefault(pid).cost;
                    productNewTotalPieces[pid] = newP;

                    if (diff > 0) // زيادة — خصم الفرق فقط
                    {
                        var prod = _db.Products.Find(pid);
                        if (prod != null)
                        {
                            var (fifoCost, consumed) = _inv.CalculateFifoCost(prod, diff);
                            foreach (var batch in consumed)
                            {
                                var b = _db.InventoryBatches.Find(batch.Id);
                                if (b != null) b.RemainingQuantity = batch.RemainingQuantity;
                            }
                            newCostByProduct[pid] = oldCost + fifoCost;

                            _db.InventoryMovements.Add(new InventoryMovement
                            {
                                ProductId = pid,
                                MovementType = MovementType.StockOut,
                                Quantity = diff,
                                CostPrice = diff > 0 ? fifoCost / diff : 0,
                                SellingPrice = 0,
                                ReferenceType = ReferenceType.Sale,
                                ReferenceId = order.Id,
                                Notes = $"زيادة تعديل طلب #{order.Id} - فاتورة #{_invoice.Id}"
                            });
                        }
                    }
                    else if (diff < 0) // نقص — ارجع الفرق فقط
                    {
                        int retQty = -diff;
                        var (unitCost, totalCost) = _inv.ReturnToBatches(pid, retQty);
                        newCostByProduct[pid] = oldP > 0 ? oldCost * newP / oldP : 0;

                        _db.InventoryMovements.Add(new InventoryMovement
                        {
                            ProductId = pid,
                            MovementType = MovementType.Return,
                            Quantity = retQty,
                            CostPrice = unitCost,
                            SellingPrice = totalCost,
                            ReferenceType = ReferenceType.Return,
                            ReferenceId = _invoice.Id,
                            Notes = $"نقص تعديل طلب #{order.Id} - فاتورة #{_invoice.Id}"
                        });
                    }
                    else // diff == 0 — ما فيش تغيير في المخزون
                    {
                        newCostByProduct[pid] = oldCost;
                    }
                }

                _db.SaveChanges();

                // 6- أنشئ OrderItems جديدة (بدون لمس المخزون)
                foreach (var entry in _entries.Values)
                {
                    if (entry.RetailCarton > 0 || entry.RetailBox > 0 || entry.RetailPiece > 0)
                        CreateEditOrderItem(order, entry, PriceType.Retail, newCostByProduct, productNewTotalPieces);
                    if (entry.WholesaleCarton > 0 || entry.WholesaleBox > 0 || entry.WholesalePiece > 0)
                        CreateEditOrderItem(order, entry, PriceType.Wholesale, newCostByProduct, productNewTotalPieces);
                }

                _db.SaveChanges();

                // 7- إعادة حساب إجمالي الفاتورة
                var orderIds = _db.Orders.Where(o => o.InvoiceId == _invoice.Id).Select(o => o.Id).ToList();
                _invoice.TotalAmount = _db.OrderItems.Where(oi => orderIds.Contains(oi.OrderId)).Sum(oi => oi.Total);
                if (_invoice.TotalPaid > _invoice.NetAmount)
                    _invoice.TotalPaid = Math.Max(0, _invoice.NetAmount);
                _invoice.Status = _invoice.Remaining <= 0 ? InvoiceStatus.Paid
                    : _invoice.TotalPaid > 0 ? InvoiceStatus.PartiallyPaid
                    : InvoiceStatus.Open;
                _db.SaveChanges();

                App.AppBackup?.BackupIfOnOperation();
                NotificationManager.ShowSuccess($"تم تعديل الطلب وتحديث الفاتورة #{_invoice.Id} بنجاح.");
                OpenInvoiceDialog(_invoice!);
                PrintInvoiceAfterSave();
            }
            catch (System.Exception ex)
            {
                LogError(ex);
                NotificationManager.ShowError($"حدث خطأ أثناء حفظ التعديل: {ex.Message}");
            }
        }

        private void CreateEditOrderItem(Order order, OrderItemEntry entry, PriceType priceType,
            Dictionary<int, decimal> costByProduct, Dictionary<int, int> productTotalPieces)
        {
            var product = entry.Product;
            int cartonQty = priceType == PriceType.Retail ? entry.RetailCarton : entry.WholesaleCarton;
            int boxQty = priceType == PriceType.Retail ? entry.RetailBox : entry.WholesaleBox;
            int pieceQty = priceType == PriceType.Retail ? entry.RetailPiece : entry.WholesalePiece;
            if (cartonQty == 0 && boxQty == 0 && pieceQty == 0) return;

            int totalPieces = _inv.CalculatePieceEquivalent(product, cartonQty, boxQty, pieceQty);

            ProductUnit? usedUnit = null;
            if (cartonQty > 0 && entry.CartonUnit != null) usedUnit = entry.CartonUnit;
            else if (boxQty > 0 && entry.BoxUnit != null) usedUnit = entry.BoxUnit;
            else if (pieceQty > 0 && entry.PieceUnit != null) usedUnit = entry.PieceUnit;

            decimal totalPrice = 0;
            if (entry.CartonUnit != null) totalPrice += cartonQty * (priceType == PriceType.Retail ? entry.CartonUnit.RetailPrice : entry.CartonUnit.WholesalePrice);
            if (entry.BoxUnit != null) totalPrice += boxQty * (priceType == PriceType.Retail ? entry.BoxUnit.RetailPrice : entry.BoxUnit.WholesalePrice);
            if (entry.PieceUnit != null) totalPrice += pieceQty * (priceType == PriceType.Retail ? entry.PieceUnit.RetailPrice : entry.PieceUnit.WholesalePrice);

            int totalForProd = productTotalPieces.GetValueOrDefault(product.Id, 0);
            decimal itemCost = totalForProd > 0
                ? costByProduct.GetValueOrDefault(product.Id, 0) * totalPieces / totalForProd
                : 0;

            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductUnitId = usedUnit?.Id ?? 0,
                CartonQuantity = cartonQty,
                BoxQuantity = boxQty,
                PieceQuantity = pieceQty,
                UnitPrice = usedUnit != null ? (priceType == PriceType.Retail ? usedUnit.RetailPrice : usedUnit.WholesalePrice) : 0,
                PriceType = priceType,
                Total = totalPrice,
                CostPrice = itemCost
            });
        }

        private void AddOrderItem(Order order, OrderItemEntry entry, PriceType priceType)
        {
            var product = entry.Product;
            int cartonQty = priceType == PriceType.Retail ? entry.RetailCarton : entry.WholesaleCarton;
            int boxQty = priceType == PriceType.Retail ? entry.RetailBox : entry.WholesaleBox;
            int pieceQty = priceType == PriceType.Retail ? entry.RetailPiece : entry.WholesalePiece;

            if (cartonQty == 0 && boxQty == 0 && pieceQty == 0) return;

            int totalPieces = _inv.CalculatePieceEquivalent(product, cartonQty, boxQty, pieceQty);
            var (fifoCost, consumed) = _inv.CalculateFifoCost(product, totalPieces);

            ProductUnit? usedUnit = null;
            if (cartonQty > 0 && entry.CartonUnit != null) usedUnit = entry.CartonUnit;
            else if (boxQty > 0 && entry.BoxUnit != null) usedUnit = entry.BoxUnit;
            else if (pieceQty > 0 && entry.PieceUnit != null) usedUnit = entry.PieceUnit;

            decimal totalPrice = 0;
            if (entry.CartonUnit != null) totalPrice += cartonQty * (priceType == PriceType.Retail ? entry.CartonUnit.RetailPrice : entry.CartonUnit.WholesalePrice);
            if (entry.BoxUnit != null) totalPrice += boxQty * (priceType == PriceType.Retail ? entry.BoxUnit.RetailPrice : entry.BoxUnit.WholesalePrice);
            if (entry.PieceUnit != null) totalPrice += pieceQty * (priceType == PriceType.Retail ? entry.PieceUnit.RetailPrice : entry.PieceUnit.WholesalePrice);

            var item = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductUnitId = usedUnit?.Id ?? 0,
                CartonQuantity = cartonQty,
                BoxQuantity = boxQty,
                PieceQuantity = pieceQty,
                UnitPrice = usedUnit != null ? (priceType == PriceType.Retail ? usedUnit.RetailPrice : usedUnit.WholesalePrice) : 0,
                PriceType = priceType,
                Total = totalPrice,
                CostPrice = fifoCost
            };
            _db.OrderItems.Add(item);

            foreach (var batch in consumed)
            {
                var b = _db.InventoryBatches.Find(batch.Id);
                if (b != null)
                    b.RemainingQuantity = batch.RemainingQuantity;
            }

            _db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                MovementType = MovementType.StockOut,
                Quantity = totalPieces,
                CostPrice = totalPieces > 0 ? fifoCost / totalPieces : 0,
                SellingPrice = totalPrice,
                ReferenceType = ReferenceType.Sale,
                ReferenceId = order.Id,
                Notes = $"{(priceType == PriceType.Retail ? "بيع قطاعي" : "بيع جملة")} - فاتورة #{_invoice!.Id}"
            });
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            var text = TxtSearch.Text.Trim();
            if (text.Length == 0) return;
            var match = _db.Products.FirstOrDefault(p => p.Barcode == text || p.Name == text);
            if (match != null)
            {
                SelectProduct(match);
                e.Handled = true;
            }
        }
        private static int ParseInt(string? text) =>
            text != null && int.TryParse(text, out int v) ? v : 0;

        private void OpenInvoiceDialog(Invoice invoice)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null) return;
            // Show invoice dialog on top of current dialog
            var dialog = new InvoiceDetailsDialog(_db, invoice);
            mainWindow.ShowOverlay(dialog);
            dialog.DialogClosed += (_, _) =>
            {
                mainWindow.HideOverlay(); // pop invoice dialog
                DialogClosed?.Invoke(this, true); // close this dialog
            };
        }

        private void PrintInvoiceAfterSave()
        {
            try
            {
                var invoice = _db.Invoices
                    .Include(i => i.Orders).ThenInclude(o => o.Items)
                    .First(i => i.Id == _invoice!.Id);
                var printer = new ReceiptPrinter(_db);
                printer.Print(invoice);
            }
            catch (System.Exception ex)
            {
                LogError(ex);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogClosed?.Invoke(this, false);
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute?.Invoke();
    }
}
