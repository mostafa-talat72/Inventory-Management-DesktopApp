using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProductApp.Data;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class StockInDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly ObservableCollection<StockInEntry> _selectedEntries = [];
    private List<Models.Product> _allProducts = [];
    private bool _loaded;

    public StockInDialog()
    {
        InitializeComponent();
        _db = new AppDbContext();
        _inv = new InventoryService(_db);
        SelectedItemsGrid.ItemsSource = _selectedEntries;
        LoadProductCards();
        _loaded = true;
    }

    private void LoadProductCards(string? search = null)
    {
        var query = _db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));

        _allProducts = query.ToList();
        var cardItems = _allProducts.Select(p =>
        {
            var units = _db.ProductUnits.Where(u => u.ProductId == p.Id).OrderBy(u => u.UnitType).ToList();
            return new
            {
                p.Name,
                UnitsDisplay = string.Join(" → ", units.Select(u => u.Name)),
                StockDisplay = _inv.GetStockDisplay(p),
                SelectCommand = new StockInRelayCommand(() => AddProduct(p))
            };
        }).ToList();

        ProductCards.ItemsSource = cardItems;
    }

    private void AddProduct(Models.Product product)
    {
        if (_selectedEntries.Any(e => e.ProductId == product.Id))
            return;

        _selectedEntries.Add(new StockInEntry
        {
            ProductId = product.Id,
            ProductName = product.Name,
            CartonQty = 0,
            BoxQty = 0,
            PieceQty = 0,
            TotalCost = 0m
        });
        UpdateSelectedCount();
    }

    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is StockInEntry entry)
        {
            _selectedEntries.Remove(entry);
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount()
    {
        int count = _selectedEntries.Count;
        TxtSelectedBadge.Text = count.ToString();
        TxtSelectedCount.Text = count > 0
            ? $"({count} منتج محدد)"
            : "(لا توجد منتجات محددة)";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        var text = TxtSearch.Text;
        if (text == "بحث عن منتج...")
            return;
        LoadProductCards(text);
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var toSave = _selectedEntries.Where(e => e.CartonQty > 0 || e.BoxQty > 0 || e.PieceQty > 0).ToList();
        if (toSave.Count == 0)
        {
            NotificationManager.ShowError("الرجاء اختيار منتجات وإدخال كميات");
            return;
        }

        foreach (var entry in toSave)
        {
            if (entry.TotalCost <= 0)
            {
                NotificationManager.ShowError($"الرجاء إدخال التكلفة الإجمالية لـ {entry.ProductName}");
                return;
            }
            var product = _db.Products.Find(entry.ProductId);
            if (product != null)
            {
                await _inv.StockIn(product, entry.CartonQty, entry.BoxQty, entry.PieceQty, entry.TotalCost);
            }
        }

        NotificationManager.ShowSuccess("تم إضافة المخزون بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}

public class StockInEntry
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int CartonQty { get; set; }
    public int BoxQty { get; set; }
    public int PieceQty { get; set; }
    public decimal TotalCost { get; set; }
}

public class StockInRelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
