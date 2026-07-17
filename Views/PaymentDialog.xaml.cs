using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class PaymentDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Invoice _invoice;

    public PaymentDialog(AppDbContext db, Invoice invoice)
    {
        InitializeComponent();
        _db = db;
        _invoice = invoice;
        TxtInvoiceInfo.Text = $"فاتورة #{invoice.Id}";
        TxtRemaining.Text = $"{invoice.Remaining:0.##} ج.م";
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
        if (decimal.TryParse(TxtAmount.Text, out decimal amount) && amount > _invoice.Remaining)
        {
            TxtAmount.Foreground = (Brush)new BrushConverter().ConvertFrom("#C62828")!;
        }
        else
        {
            TxtAmount.Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")!;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtAmount.Text, out decimal amount) || amount <= 0)
        {
            NotificationManager.ShowError("الرجاء إدخال مبلغ صحيح");
            return;
        }

        if (amount > _invoice.Remaining)
        {
            NotificationManager.ShowError($"المبلغ لا يمكن أن يتجاوز المتبقي ({_invoice.Remaining:0.##} ج.م)");
            return;
        }
        DoPayment(amount);
    }

    private void DoPayment(decimal amount)
    {
        _db.Payments.Add(new Payment
        {
            InvoiceId = _invoice.Id,
            Amount = amount,
            PaymentDate = DateTime.Now,
            PaymentMethod = (CmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "نقدي",
            Notes = TxtNotes.Text?.Trim()
        });

        _invoice.TotalPaid += amount;
        _invoice.Status = _invoice.Remaining <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        _db.SaveChanges();

        App.AppBackup?.BackupIfOnOperation();

        NotificationManager.ShowSuccess("تم إضافة الدفعة بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}