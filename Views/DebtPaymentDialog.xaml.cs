using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class DebtPaymentDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Debt _debt;
    private readonly DebtPayment? _payment;

    public DebtPaymentDialog(AppDbContext db, int debtId, int? paymentId = null)
    {
        InitializeComponent();
        _db = db;
        _debt = _db.Debts.Include(d => d.Payments).First(d => d.Id == debtId);

        if (paymentId != null)
        {
            _payment = _debt.Payments.FirstOrDefault(p => p.Id == paymentId);
            TxtHeader.Text = "تعديل دفعة";
            if (_payment != null)
            {
                TxtAmount.Text = _payment.Amount.ToString("0.##");
                TxtNotes.Text = _payment.Notes ?? "";
                var meth = _payment.PaymentMethod;
                for (int i = 0; i < CmbMethod.Items.Count; i++)
                    if ((CmbMethod.Items[i] as ComboBoxItem)?.Content?.ToString() == meth)
                    { CmbMethod.SelectedIndex = i; break; }
            }
        }
        else
        {
            TxtAmount.Text = _debt.Remaining.ToString("0.##");
        }

        var displayName = string.IsNullOrWhiteSpace(_debt.AccountName) ? "بدون شخص" : _debt.AccountName;
        TxtDebtInfo.Text = $"{(_debt.Direction == DebtDirection.OnMe ? "عليا" : "لي")} — {displayName}";
        UpdateRemaining();
    }

    private decimal MaxAllowed => _debt.Remaining + (_payment?.Amount ?? 0);

    private void UpdateRemaining()
    {
        TxtRemaining.Text = $"{MaxAllowed:0.##} ج.م";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtAmount.Text.Trim().Replace(',', '.'), out decimal amount) || amount <= 0)
        {
            NotificationManager.ShowError("الرجاء إدخال مبلغ صحيح أكبر من صفر");
            return;
        }
        if (amount > MaxAllowed)
        {
            NotificationManager.ShowError($"المبلغ أكبر من المتبقي ({MaxAllowed:0.##} ج.م)");
            return;
        }

        if (_payment == null)
        {
            _db.DebtPayments.Add(new DebtPayment
            {
                DebtId = _debt.Id,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = (CmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                Notes = TxtNotes.Text.Trim()
            });
        }
        else
        {
            _db.Entry(_payment).Reload();
            _payment.Amount = amount;
            _payment.PaymentDate = DateTime.Now;
            _payment.PaymentMethod = (CmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString();
            _payment.Notes = TxtNotes.Text.Trim();
        }

        RecomputeDebt();
        _db.SaveChanges();
        App.NotifyDataChanged();
        App.AppBackup?.BackupIfOnOperation();
        NotificationManager.ShowSuccess(_payment == null ? "تم تسجيل الدفعة" : "تم تعديل الدفعة");
        DialogClosed?.Invoke(this, true);
    }

    private void RecomputeDebt()
    {
        _db.Entry(_debt).Reload();
        _db.Entry(_debt).Collection(d => d.Payments).Load();
        _debt.TotalPaid = _debt.Payments.Sum(p => p.Amount);
        if (_debt.TotalPaid > _debt.TotalAmount) _debt.TotalPaid = _debt.TotalAmount;
        _debt.Status = _debt.Remaining <= 0
            ? InvoiceStatus.Paid
            : (_debt.TotalPaid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Open);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }

    private void Amount_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
        {
            if (!char.IsDigit(c) && c != '.' && c != ',')
            {
                e.Handled = true;
                return;
            }
        }
    }
}