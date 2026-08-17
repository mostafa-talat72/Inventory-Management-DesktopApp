using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
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
    private readonly SupplierInvoice? _targetInvoice;

    private readonly Dictionary<int, System.Windows.Threading.DispatcherTimer> _flashTimers = new();
    private readonly Dictionary<int, Brush?> _originalBrushes = new();
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(300)
    };

    public StockInDialog() : this(null)
    {
    }

    public StockInDialog(SupplierInvoice? invoice)
    {
        InitializeComponent();
        _db = new AppDbContext();
        _inv = new InventoryService(_db);
        _targetInvoice = invoice != null
            ? _db.SupplierInvoices.Include(i => i.Items).First(i => i.Id == invoice.Id)
            : null;
        SelectedItemsList.ItemsSource = _selectedEntries;
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            LoadProductCards(TxtSearch.Text.Trim());
        };
        if (_targetInvoice != null)
        {
            TxtDialogTitle.Text = $"إضافة طلبية لفاتورة المورد #{_targetInvoice.Id}";
            TxtDialogSubtitle.Text = $"{_targetInvoice.SupplierName ?? "بدون مورد"} — المنتجات الجديدة تُضاف للمخزون وتُسجل على الفاتورة";
            SupplierBar.Visibility = Visibility.Collapsed;
        }
        LoadSuppliers();
        LoadProductCards();
        _loaded = true;
        Unloaded += (_, _) => { _db.Dispose(); };
    }

    private void LoadProductCards(string? search = null)
    {
        var query = _db.Products.Where(p => !p.IsDeleted).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)));

        var products = query.Include(p => p.Units).ToList();
        _allProducts = products;

        var totals = _db.InventoryBatches
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(b => b.RemainingQuantity) })
            .ToDictionary(x => x.ProductId, x => x.Total);

        var cardItems = products.Select(p =>
        {
            var units = p.Units.OrderBy(u => u.UnitType).ToList();
            return new
            {
                p.Name,
                UnitsDisplay = string.Join(" → ", units.Select(u => u.Name)),
                StockDisplay = InventoryService.GetStockDisplay(p.Units, totals.GetValueOrDefault(p.Id)),
                SelectCommand = new StockInRelayCommand(() => AddProduct(p))
            };
        }).ToList();

        ProductCards.ItemsSource = cardItems;
    }

    private void LoadSuppliers()
    {
        var suppliers = _db.Suppliers.OrderByDescending(s => s.CreatedAt).ToList();
        CmbSupplier.Items.Clear();
        foreach (var s in suppliers)
        {
            var item = new ComboBoxItem { Content = s.Name, Tag = s };
            CmbSupplier.Items.Add(item);
        }
    }

    public void SetSupplier(Supplier supplier)
    {
        for (int i = 0; i < CmbSupplier.Items.Count; i++)
        {
            if (CmbSupplier.Items[i] is ComboBoxItem item && item.Tag is Supplier s && s.Id == supplier.Id)
            {
                CmbSupplier.SelectedIndex = i;
                return;
            }
        }

        // المورد جديد ولم يظهر في القائمة (أضيف بعد فتح الشاشة)
        var newItem = new ComboBoxItem { Content = supplier.Name, Tag = supplier };
        CmbSupplier.Items.Add(newItem);
        CmbSupplier.SelectedItem = newItem;
    }

    public void PreSelectProduct(Models.Product product) => AddProduct(product);

    private void AddProduct(Models.Product product)
    {// لو موجود — اسكرول إليه وأضئه
        var existing = _selectedEntries.FirstOrDefault(e => e.ProductId == product.Id);
        if (existing != null)
        {
            ScrollToEntry(existing, highlight: true);
            return;
        }

        var units = _db.ProductUnits.Where(u => u.ProductId == product.Id).ToList();

        var entry = new StockInEntry
        {
            ProductId = product.Id,
            ProductName = product.Name,
            HasCarton = units.Any(u => u.UnitType == UnitType.Carton),
            HasBox    = units.Any(u => u.UnitType == UnitType.Box),
            HasPiece  = units.Any(u => u.UnitType == UnitType.Piece),
            CartonName = units.FirstOrDefault(u => u.UnitType == UnitType.Carton)?.Name ?? "كرتونة",
            BoxName    = units.FirstOrDefault(u => u.UnitType == UnitType.Box)?.Name ?? "علبة",
            PieceName  = units.FirstOrDefault(u => u.UnitType == UnitType.Piece)?.Name ?? "قطعة"
        };
        _selectedEntries.Add(entry);
        UpdateSelectedCount();

        // اسكرول للعنصر الجديد بعد render
        Dispatcher.InvokeAsync(() => ScrollToEntry(entry, highlight: false),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ScrollToEntry(StockInEntry entry, bool highlight)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var container = SelectedItemsList.ItemContainerGenerator
                .ContainerFromItem(entry) as FrameworkElement;
            if (container == null) return;

            container.BringIntoView();

            if (!highlight) return;

            var border = FindFirstBorder(container);
            if (border == null) return;

            // أوقف timer سابق لنفس المنتج إن وجد
            if (_flashTimers.TryGetValue(entry.ProductId, out var old))
            {
                old.Stop();
                _flashTimers.Remove(entry.ProductId);
                // استعد اللون الأصلي
                if (_originalBrushes.TryGetValue(entry.ProductId, out var saved))
                {
                    border.Background = saved;
                    _originalBrushes.Remove(entry.ProductId);
                }
            }

            var original = border.Background;
            _originalBrushes[entry.ProductId] = original;

            int step = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(180) };
            _flashTimers[entry.ProductId] = timer;

            timer.Tick += (_, _) =>
            {
                step++;
                border.Background = step % 2 == 1
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x96))
                    : original;
                if (step >= 4)
                {
                    timer.Stop();
                    _flashTimers.Remove(entry.ProductId);
                    _originalBrushes.Remove(entry.ProductId);
                    border.Background = original;
                }
            };
            timer.Start();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static Border? FindFirstBorder(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Border b) return b;
            var found = FindFirstBorder(child);
            if (found != null) return found;
        }
        return null;
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
        if (text == ProductApp.Converters.WatermarkBehavior.GetWatermark(TxtSearch)) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _searchTimer.Stop();
        var text = TxtSearch.Text.Trim();
        if (text.Length == 0) return;
        var match = _db.Products.FirstOrDefault(p => !p.IsDeleted && (p.Barcode == text || p.Name == text));
        if (match != null)
        {
            AddProduct(match);
            e.Handled = true;
        }
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
                // الوارد ضمن فاتورة مورد — يُسجل اسم المورد في سجل المخزون
                string? supplierName = null;
                if (_targetInvoice != null)
                    supplierName = _targetInvoice.SupplierName;
                else if (CmbSupplier.SelectedItem is ComboBoxItem selItem && selItem.Tag is Supplier selSupplier)
                    supplierName = selSupplier.Name;
                supplierName ??= "بدون مورد";
                await _inv.StockIn(product, entry.CartonQty, entry.BoxQty, entry.PieceQty, entry.TotalCost,
                    supplierName: supplierName);
            }
        }

        if (_targetInvoice != null)
        {
            foreach (var entry in toSave)
            {
                _targetInvoice.Items.Add(new SupplierInvoiceItem
                {
                    SupplierInvoiceId = _targetInvoice.Id,
                    ProductId = entry.ProductId,
                    CartonQuantity = entry.CartonQty,
                    BoxQuantity = entry.BoxQty,
                    PieceQuantity = entry.PieceQty,
                    CostPrice = entry.TotalCost
                });
            }
            _targetInvoice.TotalAmount += toSave.Sum(e => e.TotalCost);
            if (_targetInvoice.Remaining <= 0)
                _targetInvoice.Status = _targetInvoice.TotalPaid > 0 ? InvoiceStatus.Paid : InvoiceStatus.Open;
            else
                _targetInvoice.Status = InvoiceStatus.PartiallyPaid;
            await _db.SaveChangesAsync();
            App.NotifyDataChanged();
            App.AppBackup?.BackupIfOnOperation();
            NotificationManager.ShowSuccess($"تمت إضافة الطلبية بنجاح على فاتورة المورد #{_targetInvoice.Id}");
            DialogClosed?.Invoke(this, true);
            return;
        }

        var supplierItem = CmbSupplier.SelectedItem as ComboBoxItem;
        var supplier = supplierItem?.Tag as Supplier;

        // فاتورة المورد تُسجل دائمًا: بالمورد المختار، أو باسم «بدون مورد» لو لم يُختر مورد
        var invItems = new List<SupplierInvoiceItem>();
        foreach (var entry in toSave)
        {
            invItems.Add(new SupplierInvoiceItem
            {
                ProductId = entry.ProductId,
                CartonQuantity = entry.CartonQty,
                BoxQuantity = entry.BoxQty,
                PieceQuantity = entry.PieceQty,
                CostPrice = entry.TotalCost
            });
        }

        var total = invItems.Sum(i => i.CostPrice);

        if (total > 0)
        {
            // الفاتورة تُسجل غير مدفوعة (Open) — تمامًا كفواتير العملاء — والدفع يتم لاحقًا
            var invoice = new SupplierInvoice
            {
                SupplierId = supplier?.Id,
                SupplierName = supplier?.Name ?? "بدون مورد",
                TotalAmount = total,
                TotalPaid = 0,
                Status = InvoiceStatus.Open,
                Items = invItems
            };
            _db.SupplierInvoices.Add(invoice);
            await _db.SaveChangesAsync();
            App.NotifyDataChanged();
        }

        App.AppBackup?.BackupIfOnOperation();

        var invoiceLabel = supplier?.Name ?? "بدون مورد";
        NotificationManager.ShowSuccess($"تم إضافة المخزون بنجاح وسُجلت فاتورة مورد غير مدفوعة ({invoiceLabel})");
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

    public string CartonName { get; set; } = "كرتونة";
    public string BoxName { get; set; } = "علبة";
    public string PieceName { get; set; } = "قطعة";

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
