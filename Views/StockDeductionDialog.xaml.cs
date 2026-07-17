using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class StockDeductionDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly Product _product;

    private static readonly List<DeductionReason> Reasons =
    [
        new("مرتجع للمورد", "#E65100", MovementType.ReturnToSupplier),
        new("تالف", "#C62828", MovementType.Shortage),
        new("عجز", "#C62828", MovementType.Shortage),
        new("استخدام داخلي", "#F57F17", MovementType.Adjustment),
        new("عينة", "#1565C0", MovementType.Adjustment),
        new("تبرع", "#2E7D32", MovementType.Adjustment),
        new("فقد", "#C62828", MovementType.Shortage),
        new("شطب", "#546E7A", MovementType.Adjustment),
    ];

    public StockDeductionDialog(AppDbContext db, Product product)
    {
        InitializeComponent();
        _db = db;
        _product = product;
        _inv = new InventoryService(db);

        TxtTitle.Text = $"خصم من المخزون - {product.Name}";
        TxtProductName.Text = product.Name;
        TxtCurrentStock.Text = _inv.GetStockDisplay(product);

        CmbReason.ItemsSource = Reasons;
    }

    private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        int cartonQty = int.TryParse(TxtCartonQty.Text, out int c) ? c : 0;
        int boxQty = int.TryParse(TxtBoxQty.Text, out int b) ? b : 0;
        int pieceQty = int.TryParse(TxtPieceQty.Text, out int p) ? p : 0;

        if (cartonQty == 0 && boxQty == 0 && pieceQty == 0)
        {
            NotificationManager.ShowError("الرجاء إدخال كمية الخصم");
            return;
        }

        if (CmbReason.SelectedItem is not DeductionReason reason)
        {
            NotificationManager.ShowError("الرجاء اختيار سبب الخصم");
            return;
        }

        int totalPieces = _inv.CalculatePieceEquivalent(_product, cartonQty, boxQty, pieceQty);
        int available = _inv.GetAvailableStock(_product);

        if (totalPieces > available)
        {
            NotificationManager.ShowWarning($"الكمية المطلوبة ({totalPieces} قطعة) تتجاوز المخزون المتاح ({available} قطعة).\nالمخزون الحالي: {_inv.GetStockDisplay(_product)}");
            return;
        }

        string qtyDesc = "";
        if (cartonQty > 0) qtyDesc += $"{cartonQty} كرتونة, ";
        if (boxQty > 0) qtyDesc += $"{boxQty} علبة, ";
        if (pieceQty > 0) qtyDesc += $"{pieceQty} قطعة, ";
        qtyDesc = qtyDesc.TrimEnd(',', ' ');

        ConfirmDialog.Show("تأكيد الخصم", $"تأكيد خصم {_inv.GetStockDisplay(_product)} كـ {reason.Name}؟", result =>
        {
            if (!result) return;
            DoDeduction(totalPieces, reason, qtyDesc);
        }, ConfirmDialog.DialogType.Danger);
    }

    private async void DoDeduction(int totalPieces, DeductionReason reason, string qtyDesc)
    {
        var (fifoCost, consumed) = _inv.CalculateFifoCost(_product, totalPieces);

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = _product.Id,
            MovementType = reason.MovementType,
            Quantity = totalPieces,
            CostPrice = totalPieces > 0 ? fifoCost / totalPieces : 0,
            Notes = $"{reason.Name} - {qtyDesc}"
        });

        foreach (var batch in consumed)
        {
            _db.Entry(batch).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        }

        await _db.SaveChangesAsync();

        NotificationManager.ShowSuccess("تم خصم المخزون بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}

public class DeductionReason(string name, string color, MovementType movementType)
{
    public string Name { get; } = name;
    public string Color { get; } = color;
    public MovementType MovementType { get; } = movementType;
}
