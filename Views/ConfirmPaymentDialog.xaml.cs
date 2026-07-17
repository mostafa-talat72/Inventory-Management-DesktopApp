using System.Windows;
using System.Windows.Controls;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class ConfirmPaymentDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Invoice _invoice;

    public ConfirmPaymentDialog(AppDbContext db, Invoice invoice)
    {
        InitializeComponent();
        _db = db;
        _invoice = invoice;

        TxtMessage.Text = $"سيتم دفع المبلغ المتبقي بالكامل للفاتورة رقم {invoice.Id}. هل أنت متأكد؟";
        TxtAmount.Text = $"{invoice.Remaining:0.##} ج.م";
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var invoice = _db.Invoices.Find(_invoice.Id);
        if (invoice == null) return;

        var amount = invoice.Remaining;
        if (amount <= 0)
        {
            NotificationManager.ShowInfo("الفاتورة مدفوعة بالكامل بالفعل");
            DialogClosed?.Invoke(this, false);
            return;
        }

        var method = (CmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "نقدي";
        _db.Payments.Add(new Payment
        {
            InvoiceId = invoice.Id,
            Amount = amount,
            PaymentDate = System.DateTime.Now,
            PaymentMethod = method,
            Notes = $"دفع كامل للفاتورة - {method}"
        });

        invoice.TotalPaid += amount;
        invoice.Status = InvoiceStatus.Paid;

        _db.SaveChanges();

        App.AppBackup?.BackupIfOnOperation();

        NotificationManager.ShowSuccess($"تم دفع {amount:0.##} ج.م بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
