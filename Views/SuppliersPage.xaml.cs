using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ProductApp.Converters;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class SuppliersPage : Page
{
    private readonly AppDbContext _db;
    private bool _loaded;
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(300)
    };

    public SuppliersPage()
    {
        _db = new AppDbContext();
        InitializeComponent();
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            LoadSuppliers(SearchBox.Text.Trim());
        };
        LoadSuppliers();
        _loaded = true;
        App.DataChanged += OnAppDataChanged;
        Unloaded += (_, _) =>
        {
            App.DataChanged -= OnAppDataChanged;
            _db.Dispose();
        };
    }

    private void OnAppDataChanged()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_loaded)
                LoadSuppliers(SearchBox.Text.Trim());
        }));
    }

    private void LoadSuppliers(string? search = null)
    {
        // الووترمارك نص وهمي داخل صندوق البحث — لا يُستخدم كفلتر
        var cleanSearch = string.IsNullOrWhiteSpace(search) ? "" : search.Trim();
        var watermark = SearchBox.GetValue(WatermarkBehavior.WatermarkProperty) as string;
        if (!string.IsNullOrEmpty(watermark) && cleanSearch == watermark)
            cleanSearch = "";

        TxtNoSupplierCount.Text = _db.SupplierInvoices.Count(i => i.SupplierId == null).ToString();

        var query = _db.Suppliers.Include(s => s.Invoices).AsQueryable();
        if (!string.IsNullOrWhiteSpace(cleanSearch))
            query = query.Where(s => s.Name.Contains(cleanSearch));

        var suppliers = query.ToList().Select(s =>
        {
            var unpaid = s.Invoices
                .Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
                .ToList();
            var remaining = unpaid.Sum(i => i.Remaining);

            return new
            {
                s.Id,
                s.Name,
                s.Phone,
                StatusDisplay = unpaid.Count > 0 ? "عليه فواتير غير مسددة" : "جميع الفواتير مسددة",
                InvoicesCount = unpaid.Count > 0
                    ? $"غير مدفوعة ({unpaid.Count})"
                    : (s.Invoices.Count > 0 ? "جميع الفواتير مسددة" : "لا توجد فواتير"),
                RemainingDisplay = remaining > 0 ? $"{remaining:0.##} ج.م" : "",
                RemainingVisible = remaining > 0 ? Visibility.Visible : Visibility.Collapsed,
                RemainingBg = remaining > 0 ? "#F57F17" : "#00695C",
                AccentBg = remaining > 0 ? "#F57F17" : "#00695C",
                IconBg = remaining > 0 ? "#FFF3E0" : "#E0F2F1",
                IconFg = remaining > 0 ? "#F57F17" : "#00695C",
                Supplier = s,
                SelectCommand = new RelayCommand(() => OpenSupplier(s))
            };
        }).ToList();

        SupplierList.ItemsSource = suppliers;
    }

    private void OpenSupplier(Supplier supplier)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierInvoicesDialog(_db, supplier);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadSuppliers(SearchBox.Text.Trim());
        };
    }

    private void NoSupplierCard_Click(object sender, MouseButtonEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierInvoicesDialog(_db);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadSuppliers(SearchBox.Text.Trim());
        };
    }

    private void AddOrder_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockInDialog();
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadSuppliers(SearchBox.Text.Trim());
        };
    }

    private void AllInvoices_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierInvoicesDialog(_db, showAll: true);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadSuppliers(SearchBox.Text.Trim());
        };
    }

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierDialog(_db);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadSuppliers(SearchBox.Text.Trim());
        };
    }

    private void EditSupplier_Click(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        dynamic item = fe.DataContext;
        if (item == null) return;
        Supplier supplier = item.Supplier;
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierDialog(_db, supplier);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadSuppliers(SearchBox.Text.Trim());
        };
    }

    private void DeleteSupplier_Click(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        dynamic item = fe.DataContext;
        if (item == null) return;
        Supplier supplier = item.Supplier;
        ConfirmDialog.Show("تأكيد الحذف",
            $"هل أنت متأكد من حذف {supplier.Name}؟\nستبقى فواتيره مسجلة بدون مورد.",
            result =>
            {
                if (!result) return;
                var invoices = _db.SupplierInvoices.Where(i => i.SupplierId == supplier.Id).ToList();
                foreach (var inv in invoices)
                {
                    inv.SupplierId = null;
                    inv.SupplierName = "بدون مورد";
                }
                _db.Suppliers.Remove(supplier);
                _db.SaveChanges();
                App.NotifyDataChanged();
                LoadSuppliers(SearchBox.Text.Trim());
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        _searchTimer.Stop();
        _searchTimer.Start();
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