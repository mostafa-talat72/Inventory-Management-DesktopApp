using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ProductApp.Data;
using ProductApp.Models;
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
        SelectedItemsList.ItemsSource = _selectedEntries;
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

        var units = _db.ProductUnits.Where(u => u.ProductId == product.Id).ToList();

        _selectedEntries.Add(new StockInEntry
        {
            ProductId = product.Id,
            ProductName = product.Name,
            HasCarton = units.Any(u => u.UnitType == UnitType.Carton),
            HasBox = units.Any(u => u.UnitType == UnitType.Box),
            HasPiece = units.Any(u => u.UnitType == UnitType.Piece)
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

        foreach (var entry in _selectedEntries)
        {
            if (entry.TotalCost <= 0 && (entry.CartonQty > 0 || entry.BoxQty > 0 || entry.PieceQty > 0))
            {
                NotificationManager.ShowError($"الرجاء إدخال التكلفة الإجمالية لـ {entry.ProductName}");
                return;
            }
            if (!AreQuantitiesValid(entry))
            {
                NotificationManager.ShowError($"الرجاء إدخال أعداد صحيحة للكميات لـ {entry.ProductName}");
                return;
            }
        }

        foreach (var entry in toSave)
        {
            var product = _db.Products.Find(entry.ProductId);
            if (product != null)
            {
                await _inv.StockIn(product, entry.CartonQty, entry.BoxQty, entry.PieceQty, entry.TotalCost);
            }
        }

        App.AppBackup?.BackupIfOnOperation();

        NotificationManager.ShowSuccess("تم إضافة المخزون بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private static readonly HashSet<string> _qtyFields = ["CartonQty", "BoxQty", "PieceQty"];

    private bool AreQuantitiesValid(StockInEntry entry)
    {
        var container = SelectedItemsList.ItemContainerGenerator.ContainerFromItem(entry) as FrameworkElement;
        if (container == null) return true;
        var textBoxes = FindVisualChildren<TextBox>(container);
        foreach (var tb in textBoxes)
        {
            var expr = BindingOperations.GetBindingExpression(tb, TextBox.TextProperty);
            if (expr?.ResolvedSource != entry) continue;
            if (!_qtyFields.Contains(expr.ParentBinding.Path.Path)) continue;
            if (string.IsNullOrEmpty(tb.Text)) continue;
            if (!int.TryParse(tb.Text, out var val) || val < 0)
                return false;
        }
        return true;
    }

    private static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var list = new List<T>();
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) list.Add(t);
            list.AddRange(FindVisualChildren<T>(child));
        }
        return list;
    }

    private void Qty_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
        {
            if (!char.IsDigit(c))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}

public class StockInEntry : INotifyPropertyChanged
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";

    private int _cartonQty;
    public int CartonQty { get => _cartonQty; set { _cartonQty = value; OnPropChanged(); } }

    private int _boxQty;
    public int BoxQty { get => _boxQty; set { _boxQty = value; OnPropChanged(); } }

    private int _pieceQty;
    public int PieceQty { get => _pieceQty; set { _pieceQty = value; OnPropChanged(); } }

    private decimal _totalCost;
    public decimal TotalCost { get => _totalCost; set { _totalCost = value; OnPropChanged(); } }

    public bool HasCarton { get; set; }
    public bool HasBox { get; set; }
    public bool HasPiece { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class StockInRelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
