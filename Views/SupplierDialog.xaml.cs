using System.Windows;
using System.Windows.Controls;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class SupplierDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Supplier? _supplier;

    public SupplierDialog(AppDbContext db, Supplier? supplier = null)
    {
        InitializeComponent();
        _db = db;
        _supplier = supplier;

        if (supplier != null)
        {
            TxtHeader.Text = "تعديل بيانات المورد";
            TxtName.Text = supplier.Name;
            TxtPhone.Text = supplier.Phone;
            TxtAddress.Text = supplier.Address;
            TxtNotes.Text = supplier.Notes;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            NotificationManager.ShowError("الرجاء إدخال اسم المورد");
            return;
        }

        if (_supplier != null)
        {
            _supplier.Name = TxtName.Text.Trim();
            _supplier.Phone = TxtPhone.Text?.Trim();
            _supplier.Address = TxtAddress.Text?.Trim();
            _supplier.Notes = TxtNotes.Text?.Trim();
        }
        else
        {
            _db.Suppliers.Add(new Supplier
            {
                Name = TxtName.Text.Trim(),
                Phone = TxtPhone.Text?.Trim(),
                Address = TxtAddress.Text?.Trim(),
                Notes = TxtNotes.Text?.Trim()
            });
        }
        _db.SaveChanges();
        App.NotifyDataChanged();
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}