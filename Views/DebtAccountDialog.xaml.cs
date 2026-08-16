using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class DebtAccountDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly DebtAccount? _account;

    public DebtAccountDialog(AppDbContext db, int? accountId = null)
    {
        InitializeComponent();
        _db = db;

        if (accountId != null)
        {
            _account = _db.DebtAccounts.First(a => a.Id == accountId);
            TxtHeader.Text = "تعديل شخص";
            TxtName.Text = _account.Name;
            TxtPhone.Text = _account.Phone ?? "";
            TxtNotes.Text = _account.Notes ?? "";
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            NotificationManager.ShowError("الرجاء إدخال الاسم");
            return;
        }

        if (_account == null)
        {
            _db.DebtAccounts.Add(new DebtAccount
            {
                Name = name,
                Phone = TxtPhone.Text.Trim(),
                Notes = TxtNotes.Text.Trim(),
                CreatedAt = DateTime.Now
            });
            NotificationManager.ShowSuccess("تم إضافة الشخص");
        }
        else
        {
            _account.Name = name;
            _account.Phone = TxtPhone.Text.Trim();
            _account.Notes = TxtNotes.Text.Trim();
            NotificationManager.ShowSuccess("تم تعديل الشخص");
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
}