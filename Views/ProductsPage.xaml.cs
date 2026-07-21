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
    private int _currentPage = 1;
    private int _pageSize = 12;
    private int _totalItems;
    private string? _currentSearch;
    private bool _loaded;
    private bool _isLoading;

    public ProductsPage()
    {
        _db = new AppDbContext();
        InitializeComponent();
        PageSizeCombo.SelectionChanged += PageSize_Changed;
        PageJumpCombo.SelectionChanged += PageJump_Changed;
        _loaded = true;
        LoadProducts();
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

        _totalItems = query.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / _pageSize));
        _currentPage = Math.Clamp(_currentPage, 1, totalPages);
        var skip = (_currentPage - 1) * _pageSize;

        var pageProducts = _pageSize > 0
            ? query.OrderBy(p => p.Name).Skip(skip).Take(_pageSize).ToList()
            : query.OrderBy(p => p.Name).ToList();

        var totalStockPieces = 0;
        var lowStockCount = 0;
        var inv = new InventoryService(_db);

        var cards = new List<object>();
        foreach (var p in pageProducts)
        {
            var units = _db.ProductUnits.AsNoTracking().Where(u => u.ProductId == p.Id).OrderBy(u => u.UnitType).ToList();
            var stockDisplay = inv.GetStockDisplay(p);
            var stockPieces = inv.GetAvailableStock(p);
            totalStockPieces += stockPieces;

            var isLowStock = stockPieces <= 0;
            if (isLowStock) lowStockCount++;

            var (stockBg, stockFg) = isLowStock
                ? ("#FFEBEE", "#C62828")
                : ("#E8F5E9", "#2E7D32");

            cards.Add(new
            {
                p.Name,
                UnitsDisplay = string.Join(" → ", units.Select(u => u.Name)),
                StockDisplay = stockDisplay,
                StockBgColor = stockBg,
                StockFgColor = stockFg,
                RetailDisplay = units.Count > 0 ? units.Min(u => u.RetailPrice).ToString("0.##") : "-",
                WholesaleDisplay = units.Count > 0 ? units.Min(u => u.WholesalePrice).ToString("0.##") : "-",
                Product = p,
                SelectCommand = new RelayCommand(() => OpenUnitLevelsDialog(p)),
                EditCommand = new RelayCommand(() => OpenEditDialog(p)),
                DeleteCommand = new RelayCommand(() => DeleteProduct(p))
            });
        }

        ProductsList.ItemsSource = cards;
        TxtTotalProducts.Text = _totalItems.ToString();
        TxtTotalStock.Text = totalStockPieces.ToString("0");
        TxtLowStock.Text = lowStockCount.ToString();

        TxtPageInfo.Text = $"{_totalItems} منتج - صفحة {_currentPage} من {totalPages}";
        BtnPrevPage.Opacity = _currentPage > 1 ? 1.0 : 0.3;
        BtnNextPage.Opacity = _currentPage < totalPages ? 1.0 : 0.3;

        PageJumpCombo.Items.Clear();
        for (int i = 1; i <= totalPages; i++)
            PageJumpCombo.Items.Add($"صفحة {i}");
        if (totalPages > 0 && _currentPage <= totalPages)
            PageJumpCombo.SelectedIndex = _currentPage - 1;

        PageSizeCombo.SelectedIndex = _pageSize switch
        {
            12 => 0, 24 => 1, 48 => 2, _ => 3
        };
        }
        finally { _isLoading = false; }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        if (text == WatermarkBehavior.GetWatermark(SearchBox))
            return;
        _currentSearch = string.IsNullOrWhiteSpace(text) ? null : text;
        _currentPage = 1;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        _pageSize = PageSizeCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out var ps) && ps > 0
            ? ps : int.MaxValue;
        _currentPage = 1;
        LoadProducts();
    }

    private void PrevPage_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentPage > 1) { _currentPage--; LoadProducts(); }
    }

    private void NextPage_Click(object sender, MouseButtonEventArgs e)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / _pageSize));
        if (_currentPage < totalPages) { _currentPage++; LoadProducts(); }
    }

    private void PageJump_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || _isLoading || PageJumpCombo.SelectedIndex < 0) return;
        _currentPage = PageJumpCombo.SelectedIndex + 1;
        LoadProducts();
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
        ConfirmDialog.Show("تأكيد الحذف", $"هل أنت متأكد من حذف {product.Name}؟", result => {
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

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
