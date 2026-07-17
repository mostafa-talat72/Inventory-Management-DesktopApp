using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class PaymentEditDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Payment _payment;
    private readonly Invoice _invoice;
    private readonly decimal _maxAllowed;

    public PaymentEditDialog(AppDbContext db, Invoice invoice, Payment payment)
    {
        InitializeComponent();
        _db = db;
        _invoice = invoice;
        _payment = payment;

        _maxAllowed = _invoice.Remaining + _payment.Amount;

        LoadPayment();
    }

    private void LoadPayment()
    {
        TxtTitle.Text = $"تعديل الدفعة #{_payment.Id}";
        TxtSubtitle.Text = $"الحد الأقصى للتعديل: {_maxAllowed:N2} ج.م";
        TxtAmount.Text = _payment.Amount.ToString("N2");
        TxtMaxHint.Text = $"يمكنك التعديل حتى {_maxAllowed:N2} ج.م";

        foreach (ComboBoxItem item in CmbMethod.Items)
        {
            if (item.Content.ToString() == _payment.PaymentMethod)
            {
                CmbMethod.SelectedItem = item;
                break;
            }
        }

        DatePicker.SelectedDate = _payment.PaymentDate;
        TxtNotes.Text = _payment.Notes ?? "";
    }

    private void TxtAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsDigitOrDot(e.Text);
    }

    private void TxtAmount_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string))!;
            if (!IsDigitOrDot(text))
                e.CancelCommand();
        }
        else
            e.CancelCommand();
    }

    private static bool IsDigitOrDot(string text)
    {
        foreach (char c in text)
            if (!char.IsDigit(c) && c != '.')
                return false;
        return true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtAmount.Text, out var amount) || amount <= 0)
        {
            NotificationManager.ShowError("الرجاء إدخال مبلغ صحيح");
            return;
        }

        if (amount > _maxAllowed)
        {
            NotificationManager.ShowError($"لا يمكن أن يتجاوز المبلغ {_maxAllowed:N2} ج.م");
            return;
        }

        if (DatePicker.SelectedDate == null)
        {
            NotificationManager.ShowError("الرجاء اختيار تاريخ الدفع");
            return;
        }

        var paymentMethod = (CmbMethod.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "نقدي";

        _payment.Amount = amount;
        _payment.PaymentMethod = paymentMethod;
        _payment.PaymentDate = DatePicker.SelectedDate.Value;
        _payment.Notes = TxtNotes.Text.Trim();

        _invoice.TotalPaid = _invoice.Payments.Sum(p => p.Amount);
        _invoice.Status = _invoice.Remaining <= 0 ? InvoiceStatus.Paid
            : _invoice.TotalPaid > 0 ? InvoiceStatus.PartiallyPaid
            : InvoiceStatus.Open;

        _db.SaveChanges();

        NotificationManager.ShowSuccess("تم تعديل الدفعة بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
