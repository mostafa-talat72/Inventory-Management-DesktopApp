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

public partial class AddOrderDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly Customer _customer;
    private Invoice _invoice = null!;
    private List<Product> _allProducts = [];
    private bool _loaded;

    public AddOrderDialog(AppDbContext db, Customer customer)
    {
        InitializeComponent();
        _db = db;
        _inv = new InventoryService(db);
        _customer = customer;

        TxtSubtitle.Text = customer.Name;

        FindOrCreateInvoice();
        LoadProducts();
        _loaded = true;
    }

    private void FindOrCreateInvoice()
    {
        _invoice = _db.Invoices
            .Where(i => i.CustomerId == _customer.Id
                && i.Status != InvoiceStatus.Paid
                && i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefault()!;

        if (_invoice == null)
        {
            _invoice = new Invoice
            {
                CustomerId = _customer.Id,
                CustomerName = _customer.Name,
                Status = InvoiceStatus.Open,
                InvoiceDate = DateTime.Now,
                TotalAmount = 0,
                TotalPaid = 0,
                Discount = 0
            };
            _db.Invoices.Add(_invoice);
            _db.SaveChanges();
        }

        TxtInvoiceId.Text = _invoice.Id.ToString();
        TxtTitle.Text = $"إضافة طلب - فاتورة #{_invoice.Id}";
    }

    private void LoadProducts(string? search = null)
    {
        var query = _db.Products.Include(p => p.Units).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));

        _allProducts = query.OrderBy(p => p.Name).ToList();

        ProductsPanel.Children.Clear();
        foreach (var product in _allProducts)
        {
            var card = CreateProductCard(product);
            ProductsPanel.Children.Add(card);
        }

        if (_allProducts.Count == 0)
        {
            ProductsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد منتجات",
                FontSize = 14,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 30)
            });
        }
    }

    private Border CreateProductCard(Product product)
    {
        var units = product.Units.OrderBy(u => u.UnitType).ToList();
        var cartonUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
        var boxUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        var pieceUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);

        var stockDisplay = _inv.GetStockDisplay(product);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Product info
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock { Text = product.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")! });
        infoStack.Children.Add(new TextBlock { Text = stockDisplay, FontSize = 10, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, Margin = new Thickness(0, 1, 0, 0) });
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 0);

        // Carton qty
        if (cartonUnit != null)
        {
            var stack = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "كرتونة", FontSize = 10, Foreground = (Brush)new BrushConverter().ConvertFrom("#78909C")! });
            var tb = new TextBox { Width = 55, Text = "0", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Tag = product };
            tb.TextChanged += (_, _) => { };
            stack.Children.Add(tb);
            grid.Children.Add(stack);
            Grid.SetColumn(stack, 1);
        }

        // Box qty
        if (boxUnit != null)
        {
            var stack = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "علبة", FontSize = 10, Foreground = (Brush)new BrushConverter().ConvertFrom("#78909C")! });
            var tb = new TextBox { Width = 55, Text = "0", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Tag = product };
            tb.TextChanged += (_, _) => { };
            stack.Children.Add(tb);
            grid.Children.Add(stack);
            Grid.SetColumn(stack, 2);
        }

        // Piece qty
        if (pieceUnit != null)
        {
            var stack = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "قطعة", FontSize = 10, Foreground = (Brush)new BrushConverter().ConvertFrom("#78909C")! });
            var tb = new TextBox { Width = 55, Text = "0", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Tag = product };
            tb.TextChanged += (_, _) => { };
            stack.Children.Add(tb);
            grid.Children.Add(stack);
            Grid.SetColumn(stack, 3);
        }

        // Price type
        var cmb = new ComboBox
        {
            Width = 70,
            FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = product
        };
        cmb.Items.Add("قطاعي");
        cmb.Items.Add("جملة");
        cmb.SelectedIndex = 0;
        grid.Children.Add(cmb);
        Grid.SetColumn(cmb, 4);

        // Add button
        var addBtn = new Button
        {
            Content = "إضافة",
            Height = 32,
            Cursor = Cursors.Hand,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)new BrushConverter().ConvertFrom("#1565C0")!,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(14, 0, 14, 0),
            Tag = product
        };
        addBtn.Click += (_, _) => AddProduct(product, cartonUnit, boxUnit, pieceUnit);

        var borderBtn = new Border
        {
            CornerRadius = new CornerRadius(6),
            Child = addBtn,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        // Re-template button
        addBtn.Template = CreateAddButtonTemplate();
        addBtn.ApplyTemplate();

        grid.Children.Add(borderBtn);
        Grid.SetColumn(borderBtn, 5);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!,
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 6),
            Child = grid
        };
    }

    private static ControlTemplate CreateAddButtonTemplate()
    {
        var tf = new FrameworkElementFactory(typeof(Border));
        tf.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        tf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        tf.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
        tf.Name = "border";

        var sp = new FrameworkElementFactory(typeof(StackPanel));
        sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var path = new FrameworkElementFactory(typeof(Path));
        path.SetValue(Path.WidthProperty, 14.0);
        path.SetValue(Path.HeightProperty, 14.0);
        path.SetValue(Path.FillProperty, Brushes.White);
        path.SetValue(Path.StretchProperty, Stretch.Uniform);
        path.SetValue(Path.VerticalAlignmentProperty, VerticalAlignment.Center);
        path.SetValue(Path.DataProperty, Geometry.Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"));
        sp.AppendChild(path);
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        cp.SetValue(ContentPresenter.MarginProperty, new Thickness(8, 0, 0, 0));
        sp.AppendChild(cp);
        tf.AppendChild(sp);

        var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.OpacityProperty, 0.85, "border"));

        var template = new ControlTemplate(typeof(Button));
        template.VisualTree = tf;
        template.Triggers.Add(trigger);
        return template;
    }

    private void AddProduct(Product product, ProductUnit? cartonUnit, ProductUnit? boxUnit, ProductUnit? pieceUnit)
    {
        // Find the TextBoxes and ComboBox for this product
        var cardBorder = ProductsPanel.Children
            .OfType<Border>()
            .FirstOrDefault(b =>
            {
                var grid = b.Child as Grid;
                return grid?.Children
                    .OfType<StackPanel>()
                    .Any(s => s.Children.OfType<TextBox>().Any(t => t.Tag == product)) == true;
            });

        if (cardBorder?.Child is not Grid cardGrid) return;

        var stacks = cardGrid.Children.OfType<StackPanel>().ToList();

        int cartonQty = 0, boxQty = 0, pieceQty = 0;

        if (cartonUnit != null && stacks.Count > 0)
        {
            var tb = stacks[0].Children.OfType<TextBox>().FirstOrDefault();
            if (tb != null) int.TryParse(tb.Text, out cartonQty);
        }
        if (boxUnit != null && stacks.Count > 1)
        {
            var idx = cartonUnit != null ? 1 : 0;
            var tb = stacks[idx].Children.OfType<TextBox>().FirstOrDefault();
            if (tb != null) int.TryParse(tb.Text, out boxQty);
        }
        if (pieceUnit != null)
        {
            int idx = (cartonUnit != null ? 1 : 0) + (boxUnit != null ? 1 : 0);
            if (idx < stacks.Count)
            {
                var tb = stacks[idx].Children.OfType<TextBox>().FirstOrDefault();
                if (tb != null) int.TryParse(tb.Text, out pieceQty);
            }
        }

        if (cartonQty == 0 && boxQty == 0 && pieceQty == 0)
        {
            NotificationManager.ShowWarning("الرجاء إدخال كمية");
            return;
        }

        var cmb = cardGrid.Children.OfType<ComboBox>().FirstOrDefault();
        bool isWholesale = cmb?.SelectedIndex == 1;

        if (!_inv.IsStockSufficient(product, cartonQty, boxQty, pieceQty))
        {
            int available = _inv.GetAvailableStock(product);
            NotificationManager.ShowWarning($"الكمية المطلوبة تتجاوز المخزون المتاح ({available})");
            return;
        }

        int totalPieces = _inv.CalculatePieceEquivalent(product, cartonQty, boxQty, pieceQty);
        var (fifoCost, consumed) = _inv.CalculateFifoCost(product, totalPieces);

        decimal unitPrice = 0;
        int? usedUnitId = null;

        if (cartonQty > 0 && cartonUnit != null)
        {
            unitPrice = isWholesale ? cartonUnit.WholesalePrice : cartonUnit.RetailPrice;
            usedUnitId = cartonUnit.Id;
        }
        else if (boxQty > 0 && boxUnit != null)
        {
            unitPrice = isWholesale ? boxUnit.WholesalePrice : boxUnit.RetailPrice;
            usedUnitId = boxUnit.Id;
        }
        else if (pieceQty > 0 && pieceUnit != null)
        {
            unitPrice = isWholesale ? pieceUnit.WholesalePrice : pieceUnit.RetailPrice;
            usedUnitId = pieceUnit.Id;
        }

        decimal total = 0;
        if (cartonUnit != null)
        {
            decimal p = isWholesale ? cartonUnit.WholesalePrice : cartonUnit.RetailPrice;
            total += cartonQty * p;
        }
        if (boxUnit != null)
        {
            decimal p = isWholesale ? boxUnit.WholesalePrice : boxUnit.RetailPrice;
            total += boxQty * p;
        }
        if (pieceUnit != null)
        {
            decimal p = isWholesale ? pieceUnit.WholesalePrice : pieceUnit.RetailPrice;
            total += pieceQty * p;
        }

        var order = new Order { InvoiceId = _invoice.Id };
        _db.Orders.Add(order);
        _db.SaveChanges();

        _db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            ProductUnitId = usedUnitId ?? 0,
            CartonQuantity = cartonQty,
            BoxQuantity = boxQty,
            PieceQuantity = pieceQty,
            UnitPrice = unitPrice,
            PriceType = isWholesale ? PriceType.Wholesale : PriceType.Retail,
            Total = total,
            CostPrice = fifoCost
        });

        _invoice.TotalAmount += total;

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = MovementType.StockOut,
            Quantity = totalPieces,
            CostPrice = totalPieces > 0 ? fifoCost / totalPieces : 0,
            SellingPrice = total,
            ReferenceType = ReferenceType.Sale,
            ReferenceId = order.Id,
            Notes = $"بيع {cartonQty} كرتونة, {boxQty} علبة, {pieceQty} قطعة - فاتورة #{_invoice.Id}"
        });

        foreach (var batch in consumed)
            _db.Entry(batch).State = EntityState.Modified;

        _db.SaveChanges();

        // Clear qty fields
        foreach (var stack in stacks)
        {
            var tb = stack.Children.OfType<TextBox>().FirstOrDefault();
            if (tb != null) tb.Text = "0";
        }

        NotificationManager.ShowSuccess($"تم إضافة {product.Name} للفاتورة #{_invoice.Id}");
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        LoadProducts(TxtSearch.Text);
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
