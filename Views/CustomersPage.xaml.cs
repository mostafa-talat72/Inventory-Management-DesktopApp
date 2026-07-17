using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class CustomersPage : Page
{
    private readonly AppDbContext _db;
    private bool _loaded;

    public CustomersPage()
    {
        _db = new AppDbContext();
        InitializeComponent();
        LoadCustomers();
        _loaded = true;
    }

    private void LoadCustomers(string? search = null)
    {
        var query = _db.Customers.Include(c => c.Invoices).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search));

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
        LoadCustomers(SearchBox.Text);
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
        ConfirmDialog.Show("تأكيد الحذف", $"هل أنت متأكد من حذف {customer.Name}؟\nسيتم حذف جميع فواتير ومعاملات العميل.", result => {
            if (!result) return;
            _db.Invoices.RemoveRange(_db.Invoices.Where(i => i.CustomerId == customer.Id));
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