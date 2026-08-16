using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class DebtDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Debt? _debt;
    private readonly DebtAccount _account;

    public DebtDialog(AppDbContext db, DebtAccount account, int? debtId = null)
    {
        InitializeComponent();
        _db = db;
        _account = account;

        TxtSubHeader.Text = $"الشخص: {_account.Name}";
        TxtAccountName.Text = _account.Name;

        if (debtId != null)
        {
            _debt = _db.Debts.First(d => d.Id == debtId);
            TxtHeader.Text = "تعديل الدين";
            TxtAmount.Text = _debt.TotalAmount.ToString("0.##");
            TxtNotes.Text = _debt.Notes ?? "";
            if (_debt.Direction == DebtDirection.ForMe)
                SetDirection(DebtDirection.ForMe);
            else
                SetDirection(DebtDirection.OnMe);
        }
        else
        {
            SetDirection(DebtDirection.OnMe);
        }
    }

    private DebtDirection _direction = DebtDirection.OnMe;

    private void SetDirection(DebtDirection dir)
    {
        _direction = dir;
        var onMe = dir == DebtDirection.OnMe;

        DirOnMe.Background = (Brush)new BrushConverter().ConvertFrom(onMe ? "#FFEBEE" : "#E8EAF6")!;
        DirOnMe.BorderBrush = (Brush)new BrushConverter().ConvertFrom(onMe ? "#EF9A9A" : "#C5CAE9")!;
        DirForMe.Background = (Brush)new BrushConverter().ConvertFrom(!onMe ? "#E8F5E9" : "#E8EAF6")!;
        DirForMe.BorderBrush = (Brush)new BrushConverter().ConvertFrom(!onMe ? "#A5D6A7" : "#C5CAE9")!;
    }

    private void DirOnMe_Click(object sender, MouseButtonEventArgs e) => SetDirection(DebtDirection.OnMe);
    private void DirForMe_Click(object sender, MouseButtonEventArgs e) => SetDirection(DebtDirection.ForMe);

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtAmount.Text.Trim().Replace(',', '.'), out decimal amount) || amount <= 0)
        {
            NotificationManager.ShowError("الرجاء إدخال مبلغ صحيح أكبر من صفر");
            return;
        }

        if (_debt == null)
        {
            _db.Debts.Add(new Debt
            {
                DebtAccountId = _account.Id,
                AccountName = _account.Name,
                Direction = _direction,
                TotalAmount = amount,
                Notes = TxtNotes.Text.Trim(),
                CreatedAt = DateTime.Now
            });
            NotificationManager.ShowSuccess("تم إضافة الدين");
        }
        else
        {
            _db.Entry(_debt).Reload();
            _debt.DebtAccountId = _account.Id;
            _debt.AccountName = _account.Name;
            _debt.Direction = _direction;
            _debt.TotalAmount = amount;
            _debt.Notes = TxtNotes.Text.Trim();
            if (_debt.TotalPaid > _debt.TotalAmount)
                _debt.TotalPaid = _debt.TotalAmount;
            _debt.Status = _debt.Remaining <= 0
                ? (_debt.TotalPaid > 0 ? InvoiceStatus.Paid : InvoiceStatus.Open)
                : (_debt.TotalPaid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Open);
            NotificationManager.ShowSuccess("تم تعديل الدين");
        }

        _db.SaveChanges();
        App.NotifyDataChanged();
        App.AppBackup?.BackupIfOnOperation();
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
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