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
    private bool _hasCarton, _hasBox, _hasPiece;

    private static readonly List<DeductionReason> Reasons =
    [
        new("مرتجع للمورد", "#E65100", MovementType.ReturnToSupplier),
        new("تالف",         "#C62828", MovementType.Shortage),
        new("عجز",          "#C62828", MovementType.Shortage),
        new("استخدام داخلي","#F57F17", MovementType.Adjustment),
        new("عينة",         "#1565C0", MovementType.Adjustment),
        new("تبرع",         "#2E7D32", MovementType.Adjustment),
        new("فقد",          "#C62828", MovementType.Shortage),
        new("شطب",          "#546E7A", MovementType.Adjustment),
    ];

    public StockDeductionDialog(AppDbContext db, Product product)
    {
        InitializeComponent();
        _db = db;
        _product = product;
        _inv = new InventoryService(db);

        TxtTitle.Text       = $"خصم من المخزون — {product.Name}";
        TxtProductName.Text = product.Name;
        TxtCurrentStock.Text = _inv.GetStockDisplay(product);
        TxtAfterStock.Text  = _inv.GetStockDisplay(product);

        CmbReason.ItemsSource = Reasons;
        CmbReason.SelectionChanged += (_, _) =>
        {
            var reason = CmbReason.SelectedItem as DeductionReason;
            RecoveredBorder.Visibility = reason?.MovementType == MovementType.ReturnToSupplier
                ? Visibility.Visible : Visibility.Collapsed;
            if (reason?.MovementType != MovementType.ReturnToSupplier)
                ChkCostRecovered.IsChecked = false;
        };
        // Apply visibility for the initially selected reason
        var initial = CmbReason.SelectedItem as DeductionReason;
        RecoveredBorder.Visibility = initial?.MovementType == MovementType.ReturnToSupplier
            ? Visibility.Visible : Visibility.Collapsed;

        // Determine which unit levels exist and hide unused cards
        var units = db.ProductUnits.AsNoTracking()
                       .Where(u => u.ProductId == product.Id).ToList();
        _hasCarton = units.Any(u => u.UnitType == UnitType.Carton);
        _hasBox    = units.Any(u => u.UnitType == UnitType.Box);
        _hasPiece  = units.Any(u => u.UnitType == UnitType.Piece);

        CartonBorder.Visibility = _hasCarton ? Visibility.Visible : Visibility.Collapsed;
        BoxBorder.Visibility    = _hasBox    ? Visibility.Visible : Visibility.Collapsed;
        PieceBorder.Visibility  = _hasPiece  ? Visibility.Visible : Visibility.Collapsed;

        // If only one unit type, show its real name
        if (!_hasCarton && !_hasBox && _hasPiece)
            TxtPieceLabel.Text = units.FirstOrDefault(u => u.UnitType == UnitType.Piece)?.Name ?? "قطعة";
        if (!_hasCarton && _hasBox && !_hasPiece)
            TxtBoxLabel.Text = units.FirstOrDefault(u => u.UnitType == UnitType.Box)?.Name ?? "علبة";
        if (_hasCarton && !_hasBox && !_hasPiece)
            TxtCartonLabel.Text = units.FirstOrDefault(u => u.UnitType == UnitType.Carton)?.Name ?? "كرتونة";
    }

    private void Qty_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Guard: called during XAML init before all controls are ready
        if (TxtCurrentStock is null || TxtAfterStock is null ||
            TxtCartonQty is null || TxtBoxQty is null || TxtPieceQty is null ||
            WarningBorder is null || TxtWarning is null)
            return;

        int carton = int.TryParse(TxtCartonQty.Text, out int c) ? c : 0;
        int box    = int.TryParse(TxtBoxQty.Text,    out int b) ? b : 0;
        int piece  = int.TryParse(TxtPieceQty.Text,  out int p) ? p : 0;

        if (carton == 0 && box == 0 && piece == 0)
        {
            TxtAfterStock.Text = _inv.GetStockDisplay(_product);
            WarningBorder.Visibility = Visibility.Collapsed;
            return;
        }

        int totalPieces = _inv.CalculatePieceEquivalent(_product, carton, box, piece);
        int available   = _inv.GetAvailableStock(_product);
        int remaining   = available - totalPieces;

        // Temporarily create a fake stock state for display
        TxtAfterStock.Text = remaining > 0
            ? FormatRemainingStock(remaining)
            : (remaining == 0 ? "0 (نفاد)" : $"تجاوز بـ {-remaining} قطعة");

        bool exceeded = remaining < 0;
        WarningBorder.Visibility = exceeded ? Visibility.Visible : Visibility.Collapsed;
        if (exceeded)
            TxtWarning.Text = $"الكمية المطلوبة ({totalPieces} قطعة) تتجاوز المخزون ({available} قطعة)";
    }

    private string FormatRemainingStock(int pieces)
    {
        // Reuse inventory service display logic for the remaining count
        var units = _db.ProductUnits.AsNoTracking()
                        .Where(u => u.ProductId == _product.Id).ToList();
        bool hasCarton = units.Any(u => u.UnitType == UnitType.Carton);
        bool hasBox    = units.Any(u => u.UnitType == UnitType.Box);
        bool hasPiece  = units.Any(u => u.UnitType == UnitType.Piece);

        int ppc = _inv.GetPiecesPerCarton(_product);
        int ppb = _inv.GetPiecesPerBox(_product);
        int bpc = _inv.GetBoxesPerCarton(_product);

        var parts = new List<string>();
        if (hasCarton && hasBox && hasPiece)
        {
            int cartons = pieces / ppc; int rem1 = pieces % ppc;
            int boxes   = rem1  / ppb; int rem2 = rem1 % ppb;
            if (cartons > 0) parts.Add($"{cartons} كرتونة");
            if (boxes   > 0) parts.Add($"{boxes} علبة");
            if (rem2    > 0) parts.Add($"{rem2} قطعة");
        }
        else if (hasCarton && hasBox)
        {
            int cartons = pieces / bpc; int rem = pieces % bpc;
            if (cartons > 0) parts.Add($"{cartons} كرتونة");
            if (rem     > 0) parts.Add($"{rem} علبة");
        }
        else if (hasCarton && hasPiece)
        {
            int cartons = pieces / ppc; int rem = pieces % ppc;
            if (cartons > 0) parts.Add($"{cartons} كرتونة");
            if (rem     > 0) parts.Add($"{rem} قطعة");
        }
        else if (hasBox && hasPiece)
        {
            int boxes = pieces / ppb; int rem = pieces % ppb;
            if (boxes > 0) parts.Add($"{boxes} علبة");
            if (rem   > 0) parts.Add($"{rem} قطعة");
        }
        else if (hasCarton) parts.Add($"{pieces} كرتونة");
        else if (hasBox)    parts.Add($"{pieces} علبة");
        else                parts.Add($"{pieces} قطعة");

        return parts.Count > 0 ? string.Join("، ", parts) : "0";
    }

    private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        int carton = int.TryParse(TxtCartonQty.Text, out int c) ? c : 0;
        int box    = int.TryParse(TxtBoxQty.Text,    out int b) ? b : 0;
        int piece  = int.TryParse(TxtPieceQty.Text,  out int p) ? p : 0;

        if (carton == 0 && box == 0 && piece == 0)
        {
            NotificationManager.ShowError("الرجاء إدخال كمية الخصم");
            return;
        }

        if (CmbReason.SelectedItem is not DeductionReason reason)
        {
            NotificationManager.ShowError("الرجاء اختيار سبب الخصم");
            return;
        }

        int totalPieces = _inv.CalculatePieceEquivalent(_product, carton, box, piece);
        int available   = _inv.GetAvailableStock(_product);

        if (totalPieces > available)
        {
            NotificationManager.ShowWarning(
                $"الكمية المطلوبة ({totalPieces} قطعة) تتجاوز المخزون المتاح ({available} قطعة).\n" +
                $"المخزون الحالي: {_inv.GetStockDisplay(_product)}");
            return;
        }

        var parts = new List<string>();
        if (carton > 0) parts.Add($"{carton} كرتونة");
        if (box    > 0) parts.Add($"{box} علبة");
        if (piece  > 0) parts.Add($"{piece} قطعة");
        string qtyDesc = string.Join("، ", parts);

        ConfirmDialog.Show(
            "تأكيد الخصم",
            $"هل تريد خصم {qtyDesc} كـ «{reason.Name}»؟",
            result => { if (result) DoDeduction(totalPieces, reason, qtyDesc); },
            ConfirmDialog.DialogType.Danger);
    }

    private async void DoDeduction(int totalPieces, DeductionReason reason, string qtyDesc)
    {
        var (fifoCost, consumed) = _inv.CalculateFifoCost(_product, totalPieces);

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId      = _product.Id,
            MovementType   = reason.MovementType,
            Quantity       = totalPieces,
            CostPrice      = totalPieces > 0 ? fifoCost / totalPieces : 0,
            IsCostRecovered = ChkCostRecovered.IsChecked == true,
            Notes          = $"{reason.Name} — {qtyDesc}"
        });

        foreach (var batch in consumed)
            _db.Entry(batch).State = Microsoft.EntityFrameworkCore.EntityState.Modified;

        await _db.SaveChangesAsync();
        App.AppBackup?.BackupIfOnOperation();
        NotificationManager.ShowSuccess("تم خصم المخزون بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) =>
        DialogClosed?.Invoke(this, false);
}

public class DeductionReason(string name, string color, MovementType movementType)
{
    public string      Name         { get; } = name;
    public string      Color        { get; } = color;
    public MovementType MovementType { get; } = movementType;
}
