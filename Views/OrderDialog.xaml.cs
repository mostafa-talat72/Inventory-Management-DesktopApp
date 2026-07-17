using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class OrderDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly Invoice _invoice;
    private Product? _selectedProduct;
    private List<Product> _allProducts = [];
    private bool _loaded;

    public OrderDialog(AppDbContext db, Invoice invoice)
    {
        _db = db;
        _inv = new InventoryService(db);
        _invoice = invoice;
        InitializeComponent();
        TxtInvoiceInfo.Text = $"فاتورة رقم {invoice.Id} - {invoice.CustomerName ?? "نقدي"}";

        LoadProductCards();
        LoadExistingItems();
        _loaded = true;
    }

    private void LoadProductCards(string? search = null)
    {
        var query = _db.Products.Include(p => p.Units).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));

        _allProducts = query.ToList();
        var cardItems = _allProducts.Select(p =>
        {
            var units = p.Units.OrderBy(u => u.UnitType).ToList();
            return new
            {
                p.Name,
                UnitsDisplay = string.Join(" → ", units.Select(u => u.Name)),
                StockDisplay = _inv.GetStockDisplay(p),
                PriceDisplay = units.Count > 0
                    ? $"من {units.Min(u => u.RetailPrice):0.##} ج.م"
                    : "-",
                SelectCommand = new RelayCommand(() => SelectProduct(p))
            };
        }).ToList();

        ProductCards.ItemsSource = cardItems;
    }

    private void SelectProduct(Product product)
    {
        _selectedProduct = product;
        TxtSelectedProduct.Text = $"المنتج المحدد: {product.Name}";
        TxtSelectedProduct.Visibility = Visibility.Visible;

        var units = _db.ProductUnits
            .Where(u => u.ProductId == product.Id)
            .OrderBy(u => u.UnitType)
            .ToList();

        var priceItems = units.Select(u => new
        {
            u.Name,
            RetailDisplay = u.RetailPrice.ToString("0.##") + " ج.م",
            WholesaleDisplay = u.WholesalePrice.ToString("0.##") + " ج.م",
            Stock = _inv.GetStockDisplay(product)
        }).ToList();

        PriceList.ItemsSource = priceItems;
        PricesPanel.Visibility = Visibility.Visible;
        QtyPanel.Visibility = Visibility.Visible;

        TxtCartonQty.Text = "0";
        TxtBoxQty.Text = "0";
        TxtPieceQty.Text = "0";
        UpdateTotal();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        LoadProductCards(TxtSearch.Text);
    }

    private void Qty_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        if (_selectedProduct == null) return;

        int.TryParse(TxtCartonQty.Text, out int cartonQty);
        int.TryParse(TxtBoxQty.Text, out int boxQty);
        int.TryParse(TxtPieceQty.Text, out int pieceQty);

        bool isWholesale = CmbPriceType.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "Wholesale";

        var units = _db.ProductUnits.Where(u => u.ProductId == _selectedProduct.Id).ToList();
        var cartonUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
        var boxUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        var pieceUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);

        decimal total = 0;

        if (cartonUnit != null)
        {
            decimal price = isWholesale ? cartonUnit.WholesalePrice : cartonUnit.RetailPrice;
            total += cartonQty * price;
        }
        if (boxUnit != null)
        {
            decimal price = isWholesale ? boxUnit.WholesalePrice : boxUnit.RetailPrice;
            total += boxQty * price;
        }
        if (pieceUnit != null)
        {
            decimal price = isWholesale ? pieceUnit.WholesalePrice : pieceUnit.RetailPrice;
            total += pieceQty * price;
        }

        TotalPanel.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtTotal.Text = total.ToString("0.##") + " ج.م";
    }

    private void LoadExistingItems()
    {
        var items = _db.OrderItems
            .Where(i => i.Order.InvoiceId == _invoice.Id)
            .Select(i => new
            {
                ProductName = i.Product.Name,
                PriceTypeDisplay = i.PriceType == PriceType.Retail ? "قطاعي" : "جملة",
                TotalDisplay = i.Total.ToString("0.##") + " ج.م"
            }).ToList();

        if (items.Count > 0)
        {
            ExistingItemsPanel.Visibility = Visibility.Visible;
            ItemsGrid.ItemsSource = items;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null)
        {
            NotificationManager.ShowError("الرجاء اختيار منتج");
            return;
        }

        int.TryParse(TxtCartonQty.Text, out int cartonQty);
        int.TryParse(TxtBoxQty.Text, out int boxQty);
        int.TryParse(TxtPieceQty.Text, out int pieceQty);

        if (cartonQty == 0 && boxQty == 0 && pieceQty == 0)
        {
            NotificationManager.ShowError("الرجاء إدخال كمية");
            return;
        }

        bool isWholesale = CmbPriceType.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "Wholesale";

        var units = _db.ProductUnits.Where(u => u.ProductId == _selectedProduct.Id).ToList();
        var cartonUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
        var boxUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        var pieceUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);

        decimal total = 0;
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

        int totalPieces = _inv.CalculatePieceEquivalent(_selectedProduct, cartonQty, boxQty, pieceQty);

        if (!_inv.IsStockSufficient(_selectedProduct, cartonQty, boxQty, pieceQty))
        {
            int available = _inv.GetAvailableStock(_selectedProduct);
            NotificationManager.ShowWarning($"الكمية المطلوبة ({totalPieces}) تتجاوز المخزون المتاح ({available}).\nالمخزون الحالي: {_inv.GetStockDisplay(_selectedProduct)}");
            return;
        }

        var (fifoCost, consumed) = _inv.CalculateFifoCost(_selectedProduct, totalPieces);

        var order = new Order
        {
            InvoiceId = _invoice.Id,
            Notes = TxtNotes.Text?.Trim(),
        };
        _db.Orders.Add(order);
        _db.SaveChanges();

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            ProductId = _selectedProduct.Id,
            ProductUnitId = usedUnitId ?? 0,
            CartonQuantity = cartonQty,
            BoxQuantity = boxQty,
            PieceQuantity = pieceQty,
            UnitPrice = unitPrice,
            PriceType = isWholesale ? PriceType.Wholesale : PriceType.Retail,
            Total = total,
            CostPrice = fifoCost
        };
        _db.OrderItems.Add(orderItem);

        _invoice.TotalAmount += total;

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = _selectedProduct.Id,
            MovementType = MovementType.StockOut,
            Quantity = totalPieces,
            CostPrice = totalPieces > 0 ? fifoCost / totalPieces : 0,
            SellingPrice = total,
            ReferenceType = ReferenceType.Sale,
            ReferenceId = order.Id,
            Notes = $"بيع {cartonQty} كرتونة, {boxQty} علبة, {pieceQty} قطعة - فاتورة {_invoice.Id}"
        });

        foreach (var batch in consumed)
        {
            _db.Entry(batch).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        }

        _db.SaveChanges();

        App.AppBackup?.BackupIfOnOperation();

        NotificationManager.ShowSuccess("تم إضافة الطلب بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}