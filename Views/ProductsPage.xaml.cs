using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using ProductApp.Converters;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class ProductsPage : Page
{
    private readonly AppDbContext _db;
    private readonly DispatcherTimer _searchTimer = new();
    private string? _currentSearch;
    private bool _loaded;
    private bool _isLoading;

    public ProductsPage()
    {
        _db = new AppDbContext();
        InitializeComponent();

        // Wire up search timer — debounce 300ms
        _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            LoadProducts();
        };

        _loaded = true;
        LoadProducts();
        Unloaded += (_, _) => _db.Dispose();
    }

    private void LoadProducts()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            var query = _db.Products.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(_currentSearch))
                query = query.Where(p => p.Name.Contains(_currentSearch));

            var allProducts = query.Include(p => p.Units).OrderBy(p => p.Name).ToList();

            var stockData = _db.InventoryBatches
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(b => b.RemainingQuantity), Value = g.Sum(b => (decimal)b.RemainingQuantity * b.CostPricePerPiece) })
                .ToDictionary(x => x.ProductId, x => (Total: x.Total, Value: x.Value));

            var inv = new InventoryService(_db);
            var totalStockPieces = 0;
            var lowStockCount = 0;
            var totalStockValue = 0m;

            var cards = new List<object>();
            foreach (var p in allProducts)
            {
                var units = p.Units.OrderBy(u => u.UnitType).ToList();
                var data = stockData.GetValueOrDefault(p.Id);
                var stockPieces = data.Total;
                var stockValue  = data.Value;
                totalStockPieces += stockPieces;
                totalStockValue  += stockValue;
                var stockDisplay = inv.GetStockDisplay(p);

                var isLowStock = stockPieces <= 0;
                if (isLowStock) lowStockCount++;

                var (stockBg, stockFg) = isLowStock
                    ? ("#FFEBEE", "#C62828")
                    : ("#E8F5E9", "#2E7D32");

                cards.Add(new
                {
                    p.Name,
                    UnitsDisplay     = string.Join(" → ", units.Select(u => u.Name)),
                    StockDisplay     = stockDisplay,
                    StockBgColor     = stockBg,
                    StockFgColor     = stockFg,
                    StockValueDisplay = $"{stockValue:0.##} ج.م",
                    RetailDisplay    = units.Count > 0 ? units.Min(u => u.RetailPrice).ToString("0.##")    : "-",
                    WholesaleDisplay = units.Count > 0 ? units.Min(u => u.WholesalePrice).ToString("0.##") : "-",
                    Product          = p,
                    SelectCommand    = new RelayCommand(() => OpenUnitLevelsDialog(p)),
                    EditCommand      = new RelayCommand(() => OpenEditDialog(p)),
                    DeleteCommand    = new RelayCommand(() => DeleteProduct(p))
                });
            }

            ProductsList.ItemsSource = cards;
            TxtTotalProducts.Text = allProducts.Count.ToString();
            TxtTotalStock.Text    = totalStockPieces.ToString("0");
            TxtLowStock.Text      = lowStockCount.ToString();
            TxtStockValue.Text    = $"{totalStockValue:0.##} ج.م";
        }
        finally { _isLoading = false; }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        var text = SearchBox.Text;
        if (text == WatermarkBehavior.GetWatermark(SearchBox)) return;
        _currentSearch = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OpenUnitLevelsDialog(Product product)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new UnitLevelsDialog(_db, product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadProducts();
        };
    }

    private void AddProduct_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ProductDialog(_db);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadProducts();
        };
    }

    private void OpenEditDialog(Product product)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ProductDialog(_db, product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadProducts();
        };
    }

    private void DeleteProduct(Product product)
    {
        ConfirmDialog.Show("تأكيد الحذف", $"هل أنت متأكد من حذف {product.Name}؟", result =>
        {
            if (!result) return;
            _db.ProductUnits.RemoveRange(_db.ProductUnits.Where(u => u.ProductId == product.Id));
            _db.InventoryBatches.RemoveRange(_db.InventoryBatches.Where(b => b.ProductId == product.Id));
            _db.InventoryMovements.RemoveRange(_db.InventoryMovements.Where(m => m.ProductId == product.Id));
            var tracked = _db.Products.Find(product.Id);
            if (tracked != null) _db.Products.Remove(tracked);
            _db.SaveChanges();
            LoadProducts();
        }, ConfirmDialog.DialogType.Danger);
    }

    private void StockIn_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockInDialog();
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadProducts();
        };
    }

    private void PrintInventory_Click(object sender, RoutedEventArgs e)
    {
        var inv = new InventoryService(_db);
        var allProducts = _db.Products
            .Include(p => p.Units)
            .OrderBy(p => p.Name)
            .ToList();

        var batchValues = _db.InventoryBatches
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Value = g.Sum(b => (decimal)b.RemainingQuantity * b.CostPricePerPiece) })
            .ToDictionary(x => x.ProductId, x => x.Value);

        var printData = allProducts.Select(p => (
            product: p,
            stockDisplay: inv.GetStockDisplay(p),
            totalPieces:  inv.GetAvailableStock(p),
            stockValue:   batchValues.GetValueOrDefault(p.Id, 0)
        )).ToList();

        var printer = new ReceiptPrinter(_db);
        printer.PrintInventory(printData);
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
