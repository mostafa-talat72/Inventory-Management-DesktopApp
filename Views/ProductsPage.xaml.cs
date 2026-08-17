using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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
    private const int LoadBatchSize = 300;
    private string? _currentSearch;
    private bool _loaded;
    private bool _isLoading;
    private bool _isLoadingMore;
    private bool _allLoaded;
    private bool _lowStockOnly;
    private string _sortMode = "name";
    private Product? _activeProduct;
    private List<Product>? _allProducts;
    private Dictionary<int, (int Total, decimal Value)>? _stockDataDict;
    private HashSet<int>? _lowStockIds;
    private decimal _totalStockValue;
    private int _totalProductCount;
    private int _totalStockPieces;
    private int _lowStockCount;
    private ScrollViewer? _productsScroll;
    private bool _selectionMode;
    private bool _showTrash;

    private class ProductCardItem : INotifyPropertyChanged
    {
        public required string Name { get; init; }
        public required string UnitsDisplay { get; init; }
        public required string StockDisplay { get; init; }
        public required string StockBgColor { get; init; }
        public required string StockFgColor { get; init; }
        public required string StockValueDisplay { get; init; }
        public required string RetailDisplay { get; init; }
        public required string WholesaleDisplay { get; init; }
        public required string BadgeText { get; init; }
        public required string BadgeBg { get; init; }
        public required string BadgeFg { get; init; }
        public required string HasBadge { get; init; }
        public required Product Product { get; init; }
        public required ICommand SelectCommand { get; init; }
        public required ICommand AddStockCommand { get; init; }
        public required ICommand DeductStockCommand { get; init; }
        public required ICommand HistoryCommand { get; init; }
        public required ICommand EditCommand { get; init; }
        public required ICommand DeleteCommand { get; init; }
        public ICommand? FavoriteCommand { get; set; }

        private ImageSource? _productImage;
        public ImageSource? ProductImage
        {
            get => _productImage;
            set { _productImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasImage)); OnPropertyChanged(nameof(NoImage)); }
        }

        public string HasImage => _productImage != null ? "Visible" : "Collapsed";
        public string NoImage => _productImage == null ? "Visible" : "Collapsed";

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private string _selectionVisible = "Collapsed";
        public string SelectionVisible
        {
            get => _selectionVisible;
            set { _selectionVisible = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public ProductsPage()
    {
        _db = new AppDbContext();
        InitializeComponent();

        RestorePreferences();
        CmbSort.SelectedIndex = _sortMode switch
        {
            "stockAsc" => 1,
            "stockDesc" => 2,
            "valueDesc" => 3,
            "fav" => 4,
            _ => 0
        };
        TglLowStockOnly.IsChecked = _lowStockOnly;

        Loaded += (_, _) =>
        {
            AmountsVisibilityService.VisibilityChanged += OnAmountsVisibilityChanged;
            HookProductsScroll();
        };
        Unloaded += (_, _) =>
        {
            AmountsVisibilityService.VisibilityChanged -= OnAmountsVisibilityChanged;
            _db.Dispose();
        };

        _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            LoadProducts();
        };

        _loaded = true;
        LoadProducts();
        UpdateTrashCount();
    }

    private static Brush Res(string hex) =>
        (Brush)new BrushConverter().ConvertFrom(hex)!;

    private void UpdateTrashCount()
    {
        int count;
        try { count = _db.Products.Count(p => p.IsDeleted); }
        catch { count = 0; }
        BtnTrashToggle.Tag = count > 0 ? $"سلة المحذوفات ({count})" : "سلة المحذوفات";    }

    private void TrashToggle_Changed(object sender, RoutedEventArgs e)
    {
        _showTrash = BtnTrashToggle.IsChecked == true;
        if (_showTrash)
            LoadTrashProducts();
        ApplyTrashVisibility();
    }

    private void ApplyTrashVisibility()
    {
        ProductsList.Visibility = _showTrash ? Visibility.Collapsed : Visibility.Visible;
        TxtEmptyProducts.Visibility = !_showTrash && (_allProducts == null || _allProducts.Count == 0)
            ? Visibility.Visible : Visibility.Collapsed;
        if (!_showTrash)
        {
            TrashScroll.Visibility = Visibility.Collapsed;
            TxtEmptyTrash.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadTrashProducts()
    {
        var deleted = _db.Products.AsNoTracking()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToList();

        TrashPanel.Children.Clear();
        bool empty = deleted.Count == 0;
        TxtEmptyTrash.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        TrashScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        foreach (var p in deleted)
            TrashPanel.Children.Add(CreateTrashCard(p));
    }

    private Border CreateTrashCard(Product p)
    {
        var cardBg = Application.Current.TryFindResource("CardBackground") as Brush ?? Brushes.White;
        var borderB = Application.Current.TryFindResource("BorderBrushLight") as Brush ?? Res("#E0E0E0");
        var headingFg = Application.Current.TryFindResource("HeadingTextBrush") as Brush ?? Res("#37474F");
        var mutedFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? Res("#90A4AE");

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = cardBg,
            BorderBrush = borderB,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var icon = new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(10), Background = Res("#EFEBE9"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                FlowDirection = System.Windows.FlowDirection.LeftToRight,
                Width = 18, Height = 18, Fill = Res("#6D4C41"), Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z")
            }
        };
        grid.Children.Add(icon);
        Grid.SetColumn(icon, 0);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        info.Children.Add(new TextBlock { Text = p.Name, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = headingFg });
        info.Children.Add(new TextBlock
        {
            Text = $"الباركود: {(string.IsNullOrWhiteSpace(p.Barcode) ? "—" : p.Barcode)}   •   حُذف في {p.DeletedAt:yyyy/MM/dd HH:mm}",
            FontSize = 11, Foreground = mutedFg, Margin = new Thickness(0, 3, 0, 0)
        });
        grid.Children.Add(info);
        Grid.SetColumn(info, 1);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var restoreBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = Res("#2E7D32"),
            Cursor = Cursors.Hand, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 6, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { FlowDirection = System.Windows.FlowDirection.LeftToRight, Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M12 5V1L7 6l5 5V7c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6H4c0 4.42 3.58 8 8 8s8-3.58 8-8-3.58-8-8-8z") },
                new TextBlock { Text = "  استعادة", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        restoreBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; RestoreProduct(p); };
        actions.Children.Add(restoreBtn);

        var delBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = Res("#C62828"),
            Cursor = Cursors.Hand, Padding = new Thickness(12, 6, 12, 6),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { FlowDirection = System.Windows.FlowDirection.LeftToRight, Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z") },
                new TextBlock { Text = "  حذف نهائي", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        delBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; PermanentlyDeleteProduct(p); };
        actions.Children.Add(delBtn);

        grid.Children.Add(actions);
        Grid.SetColumn(actions, 2);

        card.Child = grid;
        return card;
    }

    private void RestoreProduct(Product p)
    {
        var tracked = _db.Products.Find(p.Id);
        if (tracked == null) return;
        tracked.IsDeleted = false;
        tracked.DeletedAt = null;
        _db.SaveChanges();
        App.NotifyDataChanged();
        NotificationManager.ShowSuccess($"تمت استعادة المنتج: {p.Name}");
        LoadTrashProducts();
        LoadProducts();
        UpdateTrashCount();
    }

    private void PermanentlyDeleteProduct(Product p)
    {
        ConfirmDialog.Show("حذف نهائي",
            $"هل أنت متأكد من الحذف النهائي لـ {p.Name}؟\nسيتم حذف المخزون وحركاته إلى الأبد ولا يمكن التراجع.",
            result =>
            {
                if (!result) return;
                _db.ProductUnits.RemoveRange(_db.ProductUnits.Where(u => u.ProductId == p.Id));
                _db.InventoryBatches.RemoveRange(_db.InventoryBatches.Where(b => b.ProductId == p.Id));
                _db.InventoryMovements.RemoveRange(_db.InventoryMovements.Where(m => m.ProductId == p.Id));
                var tracked = _db.Products.Find(p.Id);
                if (tracked != null) _db.Products.Remove(tracked);
                _db.SaveChanges();
                App.NotifyDataChanged();
                NotificationManager.ShowSuccess("تم الحذف النهائي للمنتج");
                LoadTrashProducts();
                LoadProducts();
                UpdateTrashCount();
            }, ConfirmDialog.DialogType.Danger, confirmText: "نعم، حذف نهائي", requiredText: "حذف نهائي");
    }

    private void BtnScanBarcode_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow) return;
        var scanner = new BarcodeScannerDialog();
        mainWindow.ShowOverlay(scanner);
        scanner.ScanFinished += (_, code) =>
        {
            mainWindow.HideOverlay();
            if (string.IsNullOrWhiteSpace(code)) return;
            var barcode = code.Trim();
            var product = _db.Products.AsNoTracking().FirstOrDefault(p => !p.IsDeleted && p.Barcode == barcode);
            if (product != null)
            {
                NotificationManager.ShowSuccess($"تم العثور على المنتج: {product.Name}");
                OpenEditDialog(product);
            }
            else
            {
                NotificationManager.ShowSuccess("المنتج غير موجود — سيتم فتح نموذج إضافة منتج جديد");
                OpenProductDialog(null, barcode);
            }
        };
    }

    private void RestorePreferences()
    {
        var cfg = App.AppConfiguration;
        if (cfg == null) return;
        _sortMode = cfg.ProductsSortMode;
        _lowStockOnly = cfg.ProductsLowStockOnly;
    }

    private void SavePreferences()
    {
        var cfg = App.AppConfiguration;
        if (cfg == null) return;
        cfg.ProductsSortMode = _sortMode;
        cfg.ProductsLowStockOnly = _lowStockOnly;
        try { cfg.Save(); } catch { }
    }

    private void OnAmountsVisibilityChanged()
    {
        ApplyAmountsMask();
    }

    private void ApplyAmountsMask()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;

        TxtStockValue.Text = hidden ? mask : $"{_totalStockValue:0.##} ج.م";

        if (_allProducts == null || _stockDataDict == null) return;
        BuildProductCards();
    }

    private void TglLowStockOnly_Changed(object sender, RoutedEventArgs e)
    {
        _lowStockOnly = TglLowStockOnly.IsChecked == true;
        SavePreferences();
        LoadProducts();
    }

    private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        _sortMode = CmbSort.SelectedIndex switch
        {
            1 => "stockAsc",
            2 => "stockDesc",
            3 => "valueDesc",
            4 => "fav",
            _ => "name"
        };
        SavePreferences();
        LoadProducts();
    }

    private void ProductCard_MouseEnter(object sender, MouseEventArgs e)
    {
        _activeProduct = ((FrameworkElement)sender).Tag as Product;
    }

    private void Page_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            OpenShortcuts();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            OpenProductDialog(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F2 && _activeProduct != null && Keyboard.FocusedElement is not TextBox)
        {
            OpenEditDialog(_activeProduct);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _activeProduct != null && Keyboard.FocusedElement is not TextBox)
        {
            DeleteProduct(_activeProduct);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
        {
            SearchBox.Text = "";
            _currentSearch = null;
            e.Handled = true;
        }
    }

    private void SetSortMode(string mode, int comboIndex)
    {
        _sortMode = mode;
        CmbSort.SelectedIndex = comboIndex;
    }

    private void StatTotalProducts_Click(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Text = "";
        _currentSearch = null;
        SetLowFilter(false);
        SetSortMode("name", 0);
        SavePreferences();
        LoadProducts();
    }

    private void StatTotalStock_Click(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Text = "";
        _currentSearch = null;
        SetLowFilter(false);
        SetSortMode("stockDesc", 2);
        SavePreferences();
        LoadProducts();
    }

    private void StatLowStock_Click(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Text = "";
        _currentSearch = null;
        SetLowFilter(true);
        SavePreferences();
        LoadProducts();
    }

    private void StatStockValue_Click(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Text = "";
        _currentSearch = null;
        SetLowFilter(false);
        SetSortMode("valueDesc", 3);
        SavePreferences();
        LoadProducts();
    }

    private void SetLowFilter(bool on)
    {
        TglLowStockOnly.Checked -= TglLowStockOnly_Changed;
        TglLowStockOnly.Unchecked -= TglLowStockOnly_Changed;
        TglLowStockOnly.IsChecked = on;
        TglLowStockOnly.Checked += TglLowStockOnly_Changed;
        TglLowStockOnly.Unchecked += TglLowStockOnly_Changed;
        _lowStockOnly = on;
    }

    private void LoadProducts()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            _lowStockIds = ComputeLowStockIds();

            var baseQuery = BuildBaseQuery();
            _totalProductCount = baseQuery.Count();

            var agg = _db.InventoryBatches
                .Where(b => baseQuery.Any(p => p.Id == b.ProductId))
                .GroupBy(_ => 1)
                .Select(g => new { Pieces = g.Sum(b => b.RemainingQuantity), Value = g.Sum(b => (decimal)b.RemainingQuantity * b.CostPricePerPiece) })
                .FirstOrDefault();
            _totalStockPieces = agg?.Pieces ?? 0;
            _totalStockValue = agg?.Value ?? 0m;

            var lowIds = _lowStockIds;
            _lowStockCount = baseQuery.Count(p => lowIds.Contains(p.Id));

            TxtTotalProducts.Text = _totalProductCount.ToString();
            TxtTotalStock.Text    = _totalStockPieces.ToString("0");
            TxtLowStock.Text      = _lowStockCount.ToString();

            _stockDataDict = _db.InventoryBatches
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(b => b.RemainingQuantity), Value = g.Sum(b => (decimal)b.RemainingQuantity * b.CostPricePerPiece) })
                .ToDictionary(x => x.ProductId, x => (Total: x.Total, Value: x.Value));

            _allLoaded = false;

            if (_sortMode == "name")
            {
                // التصفح العادي: نافذة تدريجية (Keyset) — الباقي يُحمَّل عند التمرير
                _allProducts = BuildWindowQuery(null, 0).Take(LoadBatchSize).ToList();
                _allLoaded = _allProducts.Count < LoadBatchSize;
            }
            else
            {
                // الفرز بالمخزون/القيمة/المفضلة يتطلب القائمة كاملة
                _allProducts = BuildWindowQuery(null, 0).ToList();
                _allLoaded = true;
            }

            BuildProductCards();
            ApplyAmountsMask();

            TxtEmptyProducts.Visibility = _allProducts.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            ApplyTrashVisibility();
        }
        finally { _isLoading = false; }
    }

    /// <summary>
    /// استعلام أساسي بفلترات البحث والمخزون المنخفض — كلها في SQL
    /// حتى لا تُنقل البيانات كلها للذاكرة قبل التصفية.
    /// </summary>
    private IQueryable<Product> BuildBaseQuery()
    {
        string? search = string.IsNullOrWhiteSpace(_currentSearch) ? null : _currentSearch.Trim();
        bool lowOnly = _lowStockOnly;
        var lowIds = _lowStockIds;

        return _db.Products.AsNoTracking().Where(p =>
            (search == null || p.Name.Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)))
            && (!lowOnly || lowIds!.Contains(p.Id)));
    }

    /// <summary>
    /// نافذة تدريجية بترتيب (الاسم، المعرّف) — Keyset Pagination.
    /// CompareTo يُترجم في SQLite إلى مقارنة نصية مباشرة.
    /// </summary>
    private IQueryable<Product> BuildWindowQuery(string? lastName, int lastId)
    {
        IQueryable<Product> q = BuildBaseQuery().Include(p => p.Units);
        if (lastName != null)
            q = q.Where(p => p.Name.CompareTo(lastName) > 0 || (p.Name == lastName && p.Id > lastId));
        return q.OrderBy(p => p.Name).ThenBy(p => p.Id);
    }

    private void LoadMoreProducts()
    {
        if (_isLoading || _isLoadingMore || _allLoaded || _sortMode != "name") return;
        var last = _allProducts?[^1];
        if (last == null) return;

        _isLoadingMore = true;
        try
        {
            var batch = BuildWindowQuery(last.Name, last.Id).Take(LoadBatchSize).ToList();
            if (batch.Count == 0)
            {
                _allLoaded = true;
                return;
            }
            _allProducts!.AddRange(batch);
            _allLoaded = batch.Count < LoadBatchSize;
            BuildProductCards();
        }
        finally { _isLoadingMore = false; }
    }

    private void HookProductsScroll()
    {
        if (_productsScroll != null) return;
        _productsScroll = FindDescendant<ScrollViewer>(ProductsList);
        if (_productsScroll == null) return;

        _productsScroll.ScrollChanged += (_, e) =>
        {
            if (_allLoaded || _isLoading || _isLoadingMore) return;
            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 250)
                LoadMoreProducts();
        };
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// معرّفات المنتجات منخفضة المخزون (مخزون صفر أو وحدة تحت الحد الأدنى) — في SQL.
    /// المجموعتان صغيرتان عادة لأنها تمثل المنتجات المشكلة فقط.
    /// </summary>
    private HashSet<int> ComputeLowStockIds()
    {
        var zeroStockIds = _db.InventoryBatches
            .GroupBy(b => b.ProductId)
            .Where(g => g.Sum(b => b.RemainingQuantity) <= 0)
            .Select(g => g.Key);

        var unitLowIds = _db.ProductUnits
            .Where(u => u.MinStockLevel > 0)
            .Select(u => new
            {
                u.ProductId,
                Stock = _db.InventoryBatches.Where(b => b.ProductId == u.ProductId).Sum(b => b.RemainingQuantity),
                u.QuantityPerParent,
                u.MinStockLevel
            })
            .Where(x => (x.QuantityPerParent > 0 ? x.Stock / x.QuantityPerParent : x.Stock) <= x.MinStockLevel)
            .Select(x => x.ProductId);

        return zeroStockIds.Union(unitLowIds).ToHashSet();
    }

    private static bool IsLowStock(Product p, int totalPieces)
    {
        if (totalPieces <= 0) return true;
        foreach (var u in p.Units)
        {
            if (u.MinStockLevel <= 0) continue;
            int unitStock = u.QuantityPerParent > 0 ? totalPieces / u.QuantityPerParent : totalPieces;
            if (unitStock <= u.MinStockLevel) return true;
        }
        return false;
    }

    private void BuildProductCards()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;
        string selVisibility = _selectionMode ? "Visible" : "Collapsed";

        var cards = new List<ProductCardItem>();
        foreach (var p in _allProducts!)
        {
            var units = p.Units.OrderBy(u => u.UnitType).ToList();
            var data = _stockDataDict!.GetValueOrDefault(p.Id);
            var stockPieces = data.Total;
            var stockValue  = data.Value;
            var stockDisplay = InventoryService.GetStockDisplay(p.Units, stockPieces);

            var isLowStock = stockPieces <= 0;
            var (stockBg, stockFg) = isLowStock
                ? ("#FFEBEE", "#C62828")
                : ("#E8F5E9", "#2E7D32");

            var (badgeText, badgeBg, badgeFg, badgeVisibility) = stockPieces <= 0
                ? ("نفد المخزون", "#EF5350", "White", "Visible")
                : IsLowStock(p, stockPieces)
                    ? ("منخفض", "#FFA726", "#4E342E", "Visible")
                    : ("", "", "", "Collapsed");

            var imageSource = ImageCacheService.Get(p.ImagePath);

            var card = new ProductCardItem
            {
                Name = p.Name,
                UnitsDisplay = string.Join(" → ", units.Select(u => u.Name)),
                StockDisplay = stockDisplay,
                StockBgColor = stockBg,
                StockFgColor = stockFg,
                StockValueDisplay = hidden ? mask : $"{stockValue:0.##} ج.م",
                RetailDisplay = units.Count > 0 ? units.Min(u => u.RetailPrice).ToString("0.##") : "-",
                WholesaleDisplay = units.Count > 0 ? units.Min(u => u.WholesalePrice).ToString("0.##") : "-",
                BadgeText = badgeText,
                BadgeBg = badgeBg,
                BadgeFg = badgeFg,
                HasBadge = badgeVisibility,
                ProductImage = imageSource,
                IsFavorite = p.IsFavorite,
                SelectionVisible = selVisibility,
                Product = p,
                SelectCommand = new RelayCommand(() => CardClicked(p)),
                AddStockCommand = new RelayCommand(() => OpenStockInForProduct(p)),
                DeductStockCommand = new RelayCommand(() => OpenStockDeductionForProduct(p)),
                HistoryCommand = new RelayCommand(() => OpenStockMovementForProduct(p)),
                EditCommand = new RelayCommand(() => OpenEditDialog(p)),
                DeleteCommand = new RelayCommand(() => DeleteProduct(p))
            };
            card.FavoriteCommand = new RelayCommand(() => ToggleFavorite(p, card));
            cards.Add(card);

            // تحميل الصورة من الخلفية عند أول ظهور فقط
            if (imageSource == null && p.ImagePath != null && ImageCacheService.ExistsOnDisk(p.ImagePath))
            {
                var productId = p.Id;
                var path = p.ImagePath;
                _ = ImageCacheService.LoadAsync(path, 84).ContinueWith(t =>
                {
                    var img = t.Result;
                    if (img == null) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (card.Product.Id == productId)
                            card.ProductImage = img;
                    }));
                }, System.Threading.Tasks.TaskScheduler.Default);
            }
        }

        cards = _sortMode switch
        {
            "stockAsc"  => cards.OrderBy(c => _stockDataDict!.GetValueOrDefault(c.Product.Id).Total).ToList(),
            "stockDesc" => cards.OrderByDescending(c => _stockDataDict!.GetValueOrDefault(c.Product.Id).Total).ToList(),
            "valueDesc" => cards.OrderByDescending(c => _stockDataDict!.GetValueOrDefault(c.Product.Id).Value).ToList(),
            "fav"       => cards.OrderByDescending(c => c.IsFavorite).ThenBy(c => c.Name).ToList(),
            _           => cards.OrderBy(c => c.Name).ToList()
        };
        ProductsList.ItemsSource = cards;
        UpdateBulkBar();
    }

    private void CardClicked(Product p)
    {
        if (_selectionMode)
        {
            var card = (ProductsList.ItemsSource as List<ProductCardItem>)?
                .FirstOrDefault(c => c.Product.Id == p.Id);
            if (card != null) card.IsSelected = !card.IsSelected;
            UpdateBulkBar();
        }
        else
        {
            OpenUnitLevelsDialog(p);
        }
    }

    private void ToggleFavorite(Product p, ProductCardItem card)
    {
        var tracked = _db.Products.Find(p.Id);
        if (tracked == null) return;
        tracked.IsFavorite = !tracked.IsFavorite;
        _db.SaveChanges();
        card.IsFavorite = tracked.IsFavorite;
        p.IsFavorite = tracked.IsFavorite;

        if (_sortMode == "fav")
        {
            // إعادة ترتيب فورية
            var list = (ProductsList.ItemsSource as List<ProductCardItem>);
            if (list != null)
                ProductsList.ItemsSource = list
                    .OrderByDescending(c => c.IsFavorite).ThenBy(c => c.Name).ToList();
        }
    }

    // ═══════════ التحديد المتعدد ═══════════

    private void ToggleSelectionMode_Click(object sender, RoutedEventArgs e)
    {
        _selectionMode = BtnSelectMode.IsChecked == true;
        BulkBar.Visibility = _selectionMode ? Visibility.Visible : Visibility.Collapsed;

        string selVisibility = _selectionMode ? "Visible" : "Collapsed";
        var cards = ProductsList.ItemsSource as List<ProductCardItem>;
        if (cards != null)
        {
            if (!_selectionMode)
                foreach (var c in cards) c.IsSelected = false;
            foreach (var c in cards) c.SelectionVisible = selVisibility;
        }
        UpdateBulkBar();
    }

    private void SelectAllCards_Click(object sender, RoutedEventArgs e)
    {
        var cards = ProductsList.ItemsSource as List<ProductCardItem>;
        if (cards == null) return;
        bool allSelected = cards.All(c => c.IsSelected);
        foreach (var c in cards) c.IsSelected = !allSelected;
        UpdateBulkBar();
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var cards = ProductsList.ItemsSource as List<ProductCardItem>;
        var selected = cards?.Where(c => c.IsSelected).Select(c => c.Product).ToList();
        if (selected == null || selected.Count == 0)
        {
            NotificationManager.ShowError("لم يتم تحديد أي منتجات");
            return;
        }

        ConfirmDialog.Show("نقل إلى سلة المحذوفات",
            $"هل أنت متأكد من حذف {selected.Count} منتج؟\nيمكنك استعادتهم لاحقاً من سلة المحذوفات.",
            result =>
            {
                if (!result) return;
                foreach (var p in selected)
                {
                    var tracked = _db.Products.Find(p.Id);
                    if (tracked == null) continue;
                    tracked.IsDeleted = true;
                    tracked.DeletedAt = DateTime.Now;
                }
                _db.SaveChanges();
                App.NotifyDataChanged();
                NotificationManager.ShowSuccess($"تم نقل {selected.Count} منتج إلى سلة المحذوفات");
                if (_selectionMode)
                {
                    _selectionMode = false;
                    BtnSelectMode.IsChecked = false;
                    BulkBar.Visibility = Visibility.Collapsed;
                }
                LoadProducts();
                UpdateTrashCount();
            }, ConfirmDialog.DialogType.Danger, confirmText: "نعم، حذف");
    }

    private void CancelSelection_Click(object sender, RoutedEventArgs e)
    {
        _selectionMode = false;
        BtnSelectMode.IsChecked = false;
        BulkBar.Visibility = Visibility.Collapsed;
        var cards = ProductsList.ItemsSource as List<ProductCardItem>;
        if (cards != null)
        {
            foreach (var c in cards)
            {
                c.IsSelected = false;
                c.SelectionVisible = "Collapsed";
            }
        }
        UpdateBulkBar();
    }

    private void UpdateBulkBar()
    {
        var cards = ProductsList.ItemsSource as List<ProductCardItem>;
        int count = cards?.Count(c => c.IsSelected) ?? 0;
        TxtBulkCount.Text = count.ToString();
        BtnDeleteSelected.IsEnabled = count > 0;
        bool allSelected = cards != null && cards.Count > 0 && cards.All(c => c.IsSelected);
        BtnSelectAllText.Text = allSelected ? "إلغاء تحديد الكل" : "تحديد الكل";
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

    private void OpenShortcuts()
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ShortcutsDialog();
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) => mainWindow.HideOverlay();
    }

    private void Help_Click(object sender, MouseButtonEventArgs e)
    {
        OpenShortcuts();
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
        OpenProductDialog(null);
    }

    private void OpenEditDialog(Product product)
    {
        OpenProductDialog(product);
    }

    private void OpenProductDialog(Product? product, string? prefillBarcode = null)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ProductDialog(_db, product, prefillBarcode);
        mainWindow.ShowOverlay(dialog);

        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true || r == null) LoadProducts();
        };
    }

    private void DeleteProduct(Product product)
    {
        ConfirmDialog.Show("نقل إلى سلة المحذوفات",
            $"هل أنت متأكد من حذف {product.Name}؟\nيمكنك استعادته لاحقاً من سلة المحذوفات.",
            result =>
            {
                if (!result) return;
                var tracked = _db.Products.Find(product.Id);
                if (tracked == null) return;
                tracked.IsDeleted = true;
                tracked.DeletedAt = DateTime.Now;
                _db.SaveChanges();
                App.NotifyDataChanged();
                NotificationManager.ShowSuccess($"تم نقل {product.Name} إلى سلة المحذوفات");
                LoadProducts();
                UpdateTrashCount();
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

    private void OpenStockInForProduct(Product product)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockInDialog();
        dialog.PreSelectProduct(product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadProducts();
        };
    }

    private void OpenStockDeductionForProduct(Product product)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockDeductionDialog(_db, product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadProducts();
        };
    }

    private void OpenStockMovementForProduct(Product product)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockMovementDialog(_db, product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadProducts();
        };
    }

    private void PrintInventory_Click(object sender, RoutedEventArgs e)
    {
        var inv = new InventoryService(_db);
        var printer = new ReceiptPrinter(_db);

        var batchValues = _db.InventoryBatches
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Value = g.Sum(b => (decimal)b.RemainingQuantity * b.CostPricePerPiece) })
            .ToDictionary(x => x.ProductId, x => x.Value);

        // طباعة جميع المنتجات
        var allProducts = _db.Products
            .Include(p => p.Units)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToList();

        var printData = allProducts.Select(p => (
            product: p,
            stockDisplay: inv.GetStockDisplay(p),
            totalPieces:  inv.GetAvailableStock(p),
            stockValue:   batchValues.GetValueOrDefault(p.Id, 0)
        )).ToList();

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
