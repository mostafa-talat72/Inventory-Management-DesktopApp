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

public partial class SupplierItemEditDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly SupplierInvoiceItem _item;

    public SupplierItemEditDialog(AppDbContext db, SupplierInvoiceItem item)
    {
        InitializeComponent();
        _db = db;
        _inv = new InventoryService(_db);
        _item = _db.SupplierInvoiceItems.Include(i => i.Product).First(i => i.Id == item.Id);
        LoadData();
    }

    private void LoadData()
    {
        TxtProductName.Text = _item.Product.Name;

        var units = _db.ProductUnits.Where(u => u.ProductId == _item.ProductId).ToList();
        LblCarton.Text = units.FirstOrDefault(u => u.UnitType == UnitType.Carton)?.Name ?? "كرتونة";
        LblBox.Text = units.FirstOrDefault(u => u.UnitType == UnitType.Box)?.Name ?? "علبة";
        LblPiece.Text = units.FirstOrDefault(u => u.UnitType == UnitType.Piece)?.Name ?? "قطعة";

        TxtCarton.Text = _item.CartonQuantity.ToString();
        TxtBox.Text = _item.BoxQuantity.ToString();
        TxtPiece.Text = _item.PieceQuantity.ToString();
        TxtCost.Text = _item.CostPrice.ToString("0.##");
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtCarton.Text.Trim(), out int carton) || carton < 0 ||
            !int.TryParse(TxtBox.Text.Trim(), out int box) || box < 0 ||
            !int.TryParse(TxtPiece.Text.Trim(), out int piece) || piece < 0)
        {
            NotificationManager.ShowError("الرجاء إدخال أعداد صحيحة للكميات");
            return;
        }
        if (!decimal.TryParse(TxtCost.Text.Trim().Replace(',', '.'), out decimal cost) || cost < 0)
        {
            NotificationManager.ShowError("الرجاء إدخال تكلفة صحيحة");
            return;
        }
        if (carton + box + piece == 0)
        {
            NotificationManager.ShowError("الكمية الإجمالية لا يمكن أن تكون صفرًا — احذف الطلبية إذا لم تعد مطلوبة");
            return;
        }

        var product = _db.Products.Find(_item.ProductId);
        if (product == null) return;

        int oldPieces = _inv.CalculatePieceEquivalent(product, _item.CartonQuantity, _item.BoxQuantity, _item.PieceQuantity);
        int newPieces = _inv.CalculatePieceEquivalent(product, carton, box, piece);
        int diff = newPieces - oldPieces;
        decimal costDiff = cost - _item.CostPrice;

        if (diff > 0)
        {
            _inv.StockIn(product, 0, 0, diff, Math.Max(0, costDiff),
                $"تعديل طلبية فاتورة مورد #{_item.SupplierInvoiceId} (+{diff} قطعة)");
        }
        else if (diff < 0)
        {
            _inv.StockOut(product, -diff,
                $"تعديل طلبية فاتورة مورد #{_item.SupplierInvoiceId} ({diff} قطعة)");
        }

        _item.CartonQuantity = carton;
        _item.BoxQuantity = box;
        _item.PieceQuantity = piece;
        _item.CostPrice = cost;

        var invoice = _db.SupplierInvoices.First(i => i.Id == _item.SupplierInvoiceId);
        invoice.TotalAmount = _db.SupplierInvoiceItems
            .Where(i => i.SupplierInvoiceId == invoice.Id)
            .Sum(i => i.CostPrice);
        if (invoice.Remaining <= 0)
            invoice.Status = invoice.TotalPaid > 0 ? InvoiceStatus.Paid : InvoiceStatus.Open;
        else
            invoice.Status = InvoiceStatus.PartiallyPaid;

        _db.SaveChanges();
        App.NotifyDataChanged();
        App.AppBackup?.BackupIfOnOperation();
        NotificationManager.ShowSuccess("تم تعديل الطلبية وتحديث المخزون والفاتورة");

        // طباعة الفاتورة بعد التعديل مثل تعديل طلبات العملاء
        try
        {
            new ReceiptPrinter(_db).PrintSupplierInvoice(invoice);
        }
        catch (System.Exception) { }

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

    private void Qty_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
        {
            if (!char.IsDigit(c))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void Cost_PreviewTextInput(object sender, TextCompositionEventArgs e)
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