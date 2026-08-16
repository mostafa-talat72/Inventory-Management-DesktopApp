using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class PaySupplierDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Supplier _supplier;
    private readonly List<SupplierInvoice> _unpaidInvoices;
    private readonly decimal _totalRemaining;

    public PaySupplierDialog(AppDbContext db, Supplier supplier)
    {
        InitializeComponent();
        _db = db;
        _supplier = db.Suppliers.First(s => s.Id == supplier.Id);

        // الفواتير غير المسددة بالكامل — من الأقدم للأحدث
        _unpaidInvoices = db.SupplierInvoices
            .Where(i => i.SupplierId == _supplier.Id
                && i.Status != InvoiceStatus.Paid
                && i.Status != InvoiceStatus.Cancelled)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToList();

        TxtSupplierInfo.Text = $"{_supplier.Name} — {_unpaidInvoices.Count} فاتورة غير مسددة";

        _totalRemaining = _unpaidInvoices.Sum(i => i.Remaining);
        TxtTotalRemaining.Text = $"{_totalRemaining:0.##} ج.م";

        Loaded += (_, _) => TxtAmount.Focus();
    }

    private void TxtAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var tb = (TextBox)sender;
        if (e.Text == "." && tb.Text.Contains("."))
        {
            e.Handled = true;
            return;
        }
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.]$");
    }

    private void TxtAmount_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            var text = (string)e.DataObject.GetData(DataFormats.Text)!;
            if (!Regex.IsMatch(text, @"^[0-9]*\.?[0-9]*$"))
                e.CancelCommand();
        }
        else
            e.CancelCommand();
    }

    private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (decimal.TryParse(TxtAmount.Text, out decimal amount) && amount > _totalRemaining)
            TxtAmount.Foreground = (Brush)new BrushConverter().ConvertFrom("#C62828")!;
        else
            TxtAmount.Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")!;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_unpaidInvoices.Count == 0)
        {
            NotificationManager.ShowError("لا توجد فواتير غير مدفوعة لهذا المورد");
            return;
        }

        if (!decimal.TryParse(TxtAmount.Text, out decimal amount) || amount <= 0)
        {
            NotificationManager.ShowError("الرجاء إدخال مبلغ صحيح");
            return;
        }

        if (amount > _totalRemaining)
        {
            NotificationManager.ShowError($"المبلغ لا يمكن أن يتجاوز إجمالي المتبقي ({_totalRemaining:0.##} ج.م)");
            return;
        }

        var method = (CmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "نقدي";
        var notes = TxtNotes.Text?.Trim();

        decimal remaining = amount;
        int paidInvoices = 0;

        foreach (var invoice in _unpaidInvoices)
        {
            if (remaining <= 0) break;
            if (invoice.Remaining <= 0) continue;

            decimal take = Math.Min(remaining, invoice.Remaining);
            _db.SupplierPayments.Add(new SupplierPayment
            {
                SupplierInvoiceId = invoice.Id,
                Amount = take,
                PaymentDate = DateTime.Now,
                PaymentMethod = method,
                Notes = notes
            });

            invoice.TotalPaid += take;
            invoice.Status = invoice.Remaining <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            remaining -= take;
            paidInvoices++;
        }

        _db.SaveChanges();
        App.NotifyDataChanged();

        App.AppBackup?.BackupIfOnOperation();

        NotificationManager.ShowSuccess($"تم دفع {amount:0.##} ج.م على {paidInvoices} فاتورة مورد");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}