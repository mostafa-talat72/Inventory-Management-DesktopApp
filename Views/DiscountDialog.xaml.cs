using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class DiscountDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Invoice _invoice;

    public DiscountDialog(AppDbContext db, Invoice invoice)
    {
        InitializeComponent();
        _db = db;
        _invoice = invoice;

        LoadDiscount();
    }

    private void LoadDiscount()
    {
        if (_invoice.Discount > 0)
        {
            TxtTitle.Text = "تعديل الخصم";
            TxtAmount.Text = _invoice.Discount.ToString("0.##");
        }
        else
        {
            TxtTitle.Text = "إضافة خصم";
        }

        TxtSubtitle.Text = $"إجمالي الفاتورة: {_invoice.TotalAmount:0.##} ج.م";
        TxtMaxHint.Text = $"الحد الأقصى للخصم: {_invoice.TotalAmount:0.##} ج.م";
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

        if (amount > _invoice.TotalAmount)
        {
            NotificationManager.ShowError($"لا يمكن أن يتجاوز الخصم {_invoice.TotalAmount:0.##} ج.م");
            return;
        }

        _invoice.Discount = amount;
        _db.SaveChanges();

        App.AppBackup?.BackupIfOnOperation();

        NotificationManager.ShowSuccess("تم حفظ الخصم بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
