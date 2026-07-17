using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class InvoicesPage : Page
{
    private readonly AppDbContext _db;
    private Invoice? _selectedInvoice;
    private bool _loaded;
    private int? _contextCustomerId;
    private string? _contextCustomerName;
    private bool _isCustomerContext;

    public InvoicesPage()
    {
        _db = new AppDbContext();
        InitializeComponent();
        LoadInvoices();
        _loaded = true;
    }

    private void LoadInvoices(string? search = null, int? customerId = null)
    {
        var query = _db.Invoices.Include(i => i.Customer).AsQueryable();

        if (customerId.HasValue)
            query = query.Where(i => i.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => i.CustomerName!.Contains(search) || i.Id.ToString().Contains(search));

        var invoices = query.OrderByDescending(i => i.CreatedAt).ToList().Select(i => new
        {
            i.Id,
            DisplayText = $"فاتورة #{i.Id} - {i.CustomerName ?? "نقدي"} - {i.TotalAmount:N2} ج.م",
            RemainingDisplay = i.Remaining > 0 ? $"المتبقي: {i.Remaining:N2} ج.م" : "مدفوعة بالكامل",
            StatusColor = i.Status.ToString(),
            Invoice = i
        }).ToList();

        InvoiceList.ItemsSource = invoices;
    }

    public void ShowCustomerContext(int customerId, string customerName)
    {
        _isCustomerContext = true;
        _contextCustomerId = customerId;
        _contextCustomerName = customerName;
        CustomerContextPanel.Visibility = Visibility.Visible;
        TxtCustomerContextName.Text = customerName;

        var unpaidInvoices = _db.Invoices
            .Where(i => i.CustomerId == customerId && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
            .ToList();

        TxtCustomerContextInfo.Text = unpaidInvoices.Count > 0
            ? $"لديه {unpaidInvoices.Count} فاتورة غير مدفوعة"
            : "لا توجد فواتير غير مدفوعة";

        LoadInvoices(null, customerId);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        LoadInvoices(SearchBox.Text, _isCustomerContext ? _contextCustomerId : null);
    }

    private void InvoiceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InvoiceList.SelectedItem == null) return;
        dynamic item = InvoiceList.SelectedItem;
        _selectedInvoice = item.Invoice;
        ShowInvoiceDetail(_selectedInvoice);
    }

    private void ShowInvoiceDetail(Invoice invoice)
    {
        DetailPanel.Visibility = Visibility.Visible;
        NoSelectionText.Visibility = Visibility.Collapsed;

        TxtInvoiceHeader.Text = $"فاتورة رقم {invoice.Id} - {invoice.CustomerName ?? "نقدي"}";
        TxtTotal.Text = invoice.TotalAmount.ToString("N2") + " ج.م";
        TxtPaid.Text = invoice.TotalPaid.ToString("N2") + " ج.م";
        TxtRemaining.Text = invoice.Remaining.ToString("N2") + " ج.م";

        var orders = _db.OrderItems
            .Where(oi => oi.Order.InvoiceId == invoice.Id)
            .Select(oi => new
            {
                ProductName = oi.Product.Name,
                PriceTypeDisplay = oi.PriceType == PriceType.Retail ? "قطاعي" : "جملة",
                TotalDisplay = oi.Total.ToString("N2") + " ج.م"
            }).ToList();
        OrdersGrid.ItemsSource = orders;

        var payments = _db.Payments
            .Where(p => p.InvoiceId == invoice.Id)
            .Select(p => new
            {
                PaymentDateDisplay = p.PaymentDate.ToString("yyyy-MM-dd HH:mm"),
                AmountDisplay = p.Amount.ToString("N2") + " ج.م",
                p.PaymentMethod
            }).ToList();
        PaymentsGrid.ItemsSource = payments;
    }

    private void AddOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInvoice == null) return;

        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new OrderDialog(_db, _selectedInvoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_selectedInvoice).Reload();
                ShowInvoiceDetail(_selectedInvoice);
                LoadInvoices(SearchBox.Text, _isCustomerContext ? _contextCustomerId : null);
            }
        };
    }

    private void AddOrderForCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (_contextCustomerId == null || _contextCustomerName == null) return;

        var openInvoice = _db.Invoices
            .Where(i => i.CustomerId == _contextCustomerId && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
            .FirstOrDefault();

        var mainWindow = (MainWindow)Window.GetWindow(this);

        if (openInvoice != null)
        {
            var dialog = new OrderDialog(_db, openInvoice);
            mainWindow.ShowOverlay(dialog);
            dialog.DialogClosed += (s, r) =>
            {
                mainWindow.HideOverlay();
                if (r == true)
                {
                    _db.Entry(openInvoice).Reload();
                    ShowInvoiceDetail(openInvoice);
                    ShowCustomerContext(_contextCustomerId!.Value, _contextCustomerName!);
                }
            };
            return;
        }

        var invoice = new Invoice
        {
            CustomerId = _contextCustomerId,
            CustomerName = _contextCustomerName,
            InvoiceDate = DateTime.Now,
            Status = InvoiceStatus.Open
        };
        _db.Invoices.Add(invoice);
        _db.SaveChanges();

        var newDialog = new OrderDialog(_db, invoice);
        mainWindow.ShowOverlay(newDialog);
        newDialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(invoice).Reload();
                ShowInvoiceDetail(invoice);
                LoadInvoices(null, _contextCustomerId);
            }
            else
            {
                // If user cancelled, remove the empty invoice
                _db.Entry(invoice).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                _db.Invoices.Remove(invoice);
                _db.SaveChanges();
                ShowCustomerContext(_contextCustomerId!.Value, _contextCustomerName!);
            }
        };
    }

    private void AddPayment_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInvoice == null) return;

        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ConfirmPaymentDialog(_db, _selectedInvoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_selectedInvoice).Reload();
                ShowInvoiceDetail(_selectedInvoice);
                LoadInvoices(SearchBox.Text, _isCustomerContext ? _contextCustomerId : null);
            }
        };
    }

    public void SelectInvoice(int invoiceId)
    {
        var item = InvoiceList.ItemsSource?.OfType<dynamic>()
            .FirstOrDefault(i => i.Id == invoiceId);
        if (item != null)
            InvoiceList.SelectedItem = item;
    }

    private void PrintInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInvoice == null) return;
        var printer = new ReceiptPrinter(_db);
        printer.Print(_selectedInvoice);
    }

    private void AddCashInvoice_Click(object sender, RoutedEventArgs e)
    {
        var invoice = new Invoice
        {
            CustomerName = "نقدي",
            InvoiceDate = DateTime.Now,
            Status = InvoiceStatus.Open
        };
        _db.Invoices.Add(invoice);
        _db.SaveChanges();

        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new OrderDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(invoice).Reload();
                ShowInvoiceDetail(invoice);
                LoadInvoices();
                InvoiceList.SelectedItem = InvoiceList.ItemsSource?.OfType<dynamic>()
                    .FirstOrDefault(i => i.Id == invoice.Id);
            }
            else
            {
                _db.Invoices.Remove(invoice);
                _db.SaveChanges();
            }
        };
    }
}