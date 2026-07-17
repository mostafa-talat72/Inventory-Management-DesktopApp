using System.Windows;
using System.Windows.Controls;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class CustomerDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Customer? _customer;

    public CustomerDialog(AppDbContext db, Customer? customer = null)
    {
        InitializeComponent();
        _db = db;
        _customer = customer;

        if (customer != null)
        {
            TxtHeader.Text = "تعديل بيانات العميل";
            TxtName.Text = customer.Name;
            TxtPhone.Text = customer.Phone;
            TxtAddress.Text = customer.Address;
            TxtNotes.Text = customer.Notes;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            NotificationManager.ShowError("الرجاء إدخال اسم العميل");
            return;
        }

        if (_customer != null)
        {
            _customer.Name = TxtName.Text.Trim();
            _customer.Phone = TxtPhone.Text?.Trim();
            _customer.Address = TxtAddress.Text?.Trim();
            _customer.Notes = TxtNotes.Text?.Trim();
        }
        else
        {
            _db.Customers.Add(new Customer
            {
                Name = TxtName.Text.Trim(),
                Phone = TxtPhone.Text?.Trim(),
                Address = TxtAddress.Text?.Trim(),
                Notes = TxtNotes.Text?.Trim()
            });
        }
        _db.SaveChanges();
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}