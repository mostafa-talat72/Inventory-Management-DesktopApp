using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ProductApp.Converters;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class CustomersPage : Page
{
    private readonly AppDbContext _db;
    private bool _loaded;
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(300)
    };

    public CustomersPage()
    {
        _db = new AppDbContext();
        InitializeComponent();
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            LoadCustomers(SearchBox.Text.Trim());
        };
        LoadCustomers();
        _loaded = true;
        Unloaded += (_, _) => _db.Dispose();
    }

    private void LoadCustomers(string? search = null)
    {
        // الووترمارك نص وهمي داخل صندوق البحث — لا يُستخدم كفلتر
        var cleanSearch = string.IsNullOrWhiteSpace(search) ? "" : search.Trim();
        var watermark = SearchBox.GetValue(WatermarkBehavior.WatermarkProperty) as string;
        if (!string.IsNullOrEmpty(watermark) && cleanSearch == watermark)
            cleanSearch = "";

        var query = _db.Customers.Include(c => c.Invoices).AsQueryable();
        if (!string.IsNullOrWhiteSpace(cleanSearch))
            query = query.Where(c => c.Name.Contains(cleanSearch));

        var customers = query.ToList().Select(c =>
        {
            var unpaidInvoices = c.Invoices.Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled).ToList();
            var hasOverdue = unpaidInvoices.Any(i => (DateTime.Now - i.CreatedAt).TotalDays > 30);
            string status = hasOverdue ? "Overdue" : unpaidInvoices.Any() ? "HasUnpaid" : "Good";
            string statusDisplay = hasOverdue ? "فواتير متأخرة" : unpaidInvoices.Any() ? "عليها فواتير" : "لا توجد فواتير";

            return new
            {
                c.Id,
                c.Name,
                c.Phone,
                StatusColor = status,
                StatusDisplay = statusDisplay,
                InvoicesCount = unpaidInvoices.Count > 0
                    ? $"فاتورة غير مدفوعة ({unpaidInvoices.Count})"
                    : "جميع الفواتير مدفوعة",
                Customer = c,
                SelectCommand = new RelayCommand(() => OpenCustomer(c))
            };
        }).ToList();

        CustomerList.ItemsSource = customers;

        // Update cash invoice count
        int cashCount = _db.Invoices.Count(i => i.CustomerId == null);
        TxtCashInvoiceCount.Text = cashCount > 0 ? $"{cashCount} فاتورة" : "لا توجد فواتير";
    }

    private void AllInvoices_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        mainWindow.NavigateToPage("Invoices");
    }

    private void CashCustomer_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new CustomerInvoicesDialog(_db);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadCustomers();
        };
    }

    private void OpenCustomer(Customer customer)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new CustomerInvoicesDialog(_db, customer);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadCustomers();
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var customers = _db.Customers.Include(c => c.Invoices).ToList();
        if (customers.Count == 0)
        {
            MessageBox.Show("لا يوجد عملاء للتصدير.", "تصدير", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"العملاء_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (saveDialog.ShowDialog() != true) return;

        try
        {
            string[] headers = { "اسم العميل", "رقم الهاتف", "الحالة", "عدد الفواتير غير المدفوعة" };
            var rows = customers.Select(c =>
            {
                var unpaid = c.Invoices
                    .Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
                    .ToList();
                var hasOverdue = unpaid.Any(i => (DateTime.Now - i.CreatedAt).TotalDays > 30);
                string status = hasOverdue ? "فواتير متأخرة" : unpaid.Any() ? "عليها فواتير" : "لا توجد فواتير";
                return new object?[] { c.Name, c.Phone, status, unpaid.Count };
            }).ToList();

            ExcelExportService.Export(saveDialog.FileName, headers, rows);
            NotificationManager.ShowSuccess("تم تصدير العملاء إلى Excel بنجاح");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء التصدير:\n{ex.Message}", "تصدير", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new CustomerDialog(_db);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadCustomers();
        };
    }

    private void EditCustomer_Click(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        dynamic item = fe.DataContext;
        if (item == null) return;
        Customer customer = item.Customer;
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new CustomerDialog(_db, customer);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadCustomers();
        };
    }

    private void DeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        dynamic item = fe.DataContext;
        if (item == null) return;
        Customer customer = item.Customer;
        ConfirmDialog.Show("تأكيد الحذف", $"هل أنت متأكد من حذف {customer.Name}؟\nسيتم نقل جميع فواتيره إلى نقدي ثم حذف العميل.", result => {
            if (!result) return;
            var invoices = _db.Invoices.Where(i => i.CustomerId == customer.Id).ToList();
            foreach (var inv in invoices)
            {
                inv.CustomerId = null;
                inv.CustomerName = "نقدي";
            }
            _db.Customers.Remove(customer);
            _db.SaveChanges();
            LoadCustomers();
        }, ConfirmDialog.DialogType.Danger);
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