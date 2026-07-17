using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using ProductApp.Converters;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class ProductsPage : Page
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private Product? _selectedProduct;
    private bool _loaded;
    private List<Product> _allProducts = [];

    public ProductsPage()
    {
        _db = new AppDbContext();
        _inv = new InventoryService(_db);
        InitializeComponent();
        LoadProducts();
        _loaded = true;
    }

    private void LoadProducts(string? search = null)
    {
        var query = _db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));

        var products = query.ToList();
        var cards = products.Select(p =>
        {
            var units = _db.ProductUnits.AsNoTracking().Where(u => u.ProductId == p.Id).OrderBy(u => u.UnitType).ToList();
            return new
            {
                p.Name,
                UnitsDisplay = string.Join(" → ", units.Select(u => u.Name)),
                StockDisplay = _inv.GetStockDisplay(p),
                RetailDisplay = units.Count > 0 ? units.Min(u => u.RetailPrice).ToString("N2") : "-",
                WholesaleDisplay = units.Count > 0 ? units.Min(u => u.WholesalePrice).ToString("N2") : "-",
                Product = p
            };
        }).ToList();

        ProductsList.ItemsSource = cards;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        var text = SearchBox.Text;
        if (text == WatermarkBehavior.GetWatermark(SearchBox))
            return;
        LoadProducts(text);
    }

    private void ProductsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductsList.SelectedItem == null)
        {
            _selectedProduct = null;
            UnitsPanel.Visibility = Visibility.Collapsed;
            DividerLine.Visibility = Visibility.Collapsed;
            return;
        }
        dynamic item = ProductsList.SelectedItem;
        _selectedProduct = item.Product;
        ShowUnits(_selectedProduct);
    }

    private void ProductsList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is ListBoxItem)
                    return;
                source = VisualTreeHelper.GetParent(source);
            }
            ProductsList.SelectedItem = null;
        }
        catch
        {
            // ignore - content elements (Run etc.) may not support visual tree walk
        }
    }

    private void ShowUnits(Product product)
    {
        var units = _db.ProductUnits.Where(u => u.ProductId == product.Id)
            .OrderBy(u => u.UnitType).ToList();

        var cardData = units.Select(u =>
        {
            var (icon, color) = u.UnitType switch
            {
                UnitType.Carton => ("📦", "#1A237E"),
                UnitType.Box => ("📋", "#00897B"),
                UnitType.Piece => ("⚪", "#546E7A"),
                _ => ("📦", "#78909C")
            };

            string contains = "";
            if (u.UnitType == UnitType.Carton)
            {
                var children = units.FirstOrDefault(x => x.ParentUnitId == u.Id);
                if (children != null)
                    contains = $"يحتوي على {u.QuantityPerParent} {children.Name}";
            }
            else if (u.UnitType == UnitType.Box)
            {
                var piece = units.FirstOrDefault(x => x.UnitType == UnitType.Piece && x.ParentUnitId == u.Id);
                if (piece != null)
                    contains = $"يحتوي على {u.QuantityPerParent} قطعة";
            }
            else if (u.UnitType == UnitType.Piece)
                contains = "الوحدة الأساسية";

            return new
            {
                u.Name,
                UnitTypeDisplay = u.UnitType switch
                {
                    UnitType.Carton => "كرتونة",
                    UnitType.Box => "علبة",
                    UnitType.Piece => "قطعة",
                    _ => ""
                },
                ContainsDisplay = contains,
                RetailDisplay = u.RetailPrice,
                WholesaleDisplay = u.WholesalePrice,
                LevelIcon = icon,
                LevelColor = color
            };
        }).ToList();

        UnitsCards.ItemsSource = cardData;
        TxtUnitSubHeader.Text = product.Name;
        UnitsPanel.Visibility = Visibility.Visible;
        DividerLine.Visibility = Visibility.Visible;
    }

    private void RefreshAndRestoreSelection()
    {
        var product = _selectedProduct;
        LoadProducts();
        if (product != null)
        {
            _selectedProduct = product;
            ShowUnits(product);
        }
    }

    private void StockMovement_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null) return;
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockMovementDialog(_db, _selectedProduct);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            RefreshAndRestoreSelection();
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
            if (r == true)
            {
                LoadProducts();
                UnitsPanel.Visibility = Visibility.Collapsed;
                DividerLine.Visibility = Visibility.Collapsed;
            }
        };
    }

    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        dynamic item = btn.DataContext;
        var product = (Product)item.Product;

        OpenEditDialog(product);
    }

    private void EditProduct_FromUnits(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct != null)
            OpenEditDialog(_selectedProduct);
    }

    private void OpenEditDialog(Product product)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ProductDialog(_db, product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
                RefreshAndRestoreSelection();
        };
    }

    private void DeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        dynamic item = btn.DataContext;
        var product = (Product)item.Product;

        ConfirmDialog.Show("تأكيد الحذف", $"هل أنت متأكد من حذف {product.Name}؟", result => {
            if (!result) return;
            _db.ProductUnits.RemoveRange(_db.ProductUnits.Where(u => u.ProductId == product.Id));
            _db.InventoryBatches.RemoveRange(_db.InventoryBatches.Where(b => b.ProductId == product.Id));
            _db.InventoryMovements.RemoveRange(_db.InventoryMovements.Where(m => m.ProductId == product.Id));
            _db.Products.Remove(product);
            _db.SaveChanges();
            LoadProducts();
            UnitsPanel.Visibility = Visibility.Collapsed;
            DividerLine.Visibility = Visibility.Collapsed;
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
            if (r == true)
                RefreshAndRestoreSelection();
        };
    }

    private void StockDeduction_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null) return;
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockDeductionDialog(_db, _selectedProduct);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
                RefreshAndRestoreSelection();
        };
    }
}
