using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class MovementEditDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly Product _product;
    private readonly MovementItem _item;
    private InventoryMovement? _movement;
    private readonly int _ppc;
    private readonly int _ppb;

    public MovementEditDialog(AppDbContext db, Product product, MovementItem item)
    {
        InitializeComponent();
        _db = db;
        _product = product;
        _item = item;
        _inv = new InventoryService(db);

        _movement = _db.InventoryMovements.Find(item.MovementId);

        TxtTitle.Text = $"تعديل حركة - {item.TypeDisplay}";
        TxtSubtitle.Text = $"{product.Name} - {item.DateDisplay}";

        _ppc = _inv.GetPiecesPerCarton(product);
        _ppb = _inv.GetPiecesPerBox(product);
        int total = item.Quantity;
        int cartons = _ppc > 0 ? total / _ppc : 0;
        int remainder = _ppc > 0 ? total % _ppc : total;
        int boxes = _ppb > 0 ? remainder / _ppb : 0;
        int pieces = _ppb > 0 ? remainder % _ppb : remainder;

        TxtCartonQty.Text = cartons.ToString();
        TxtBoxQty.Text = boxes.ToString();
        TxtPieceQty.Text = pieces.ToString();

        if (_movement != null)
        {
            decimal totalCost = _movement.Quantity * _movement.CostPrice;
            TxtTotalCost.Text = totalCost.ToString("0.##");
            UpdateCostPerPiece();
        }

        int available = _inv.GetAvailableStock(product);
        decimal fifoValue = _db.InventoryBatches
            .Where(b => b.ProductId == product.Id && b.RemainingQuantity > 0)
            .Sum(b => b.RemainingQuantity * b.CostPricePerPiece);
        int totalBatchPieces = _db.InventoryBatches
            .Where(b => b.ProductId == product.Id && b.RemainingQuantity > 0)
            .Sum(b => b.RemainingQuantity);
        TxtFifoCost.Text = totalBatchPieces > 0
            ? $"{fifoValue / totalBatchPieces:0.##} ج.م/قطعة"
            : "-";
        TxtAvailableStock.Text = _inv.GetStockDisplay(product);
    }

    private void Qty_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_inv == null) return;
        UpdateCostPerPiece();
    }

    private void TotalCost_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_inv == null) return;
        UpdateCostPerPiece();
    }

    private void UpdateCostPerPiece()
    {
        int cartonQty = int.TryParse(TxtCartonQty.Text, out int c) ? c : 0;
        int boxQty = int.TryParse(TxtBoxQty.Text, out int b) ? b : 0;
        int pieceQty = int.TryParse(TxtPieceQty.Text, out int p) ? p : 0;
        int totalPieces = _inv.CalculatePieceEquivalent(_product, cartonQty, boxQty, pieceQty);

        if (decimal.TryParse(TxtTotalCost.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal totalCost)
            && totalPieces > 0 && totalCost > 0)
        {
            TxtCostPerPiece.Text = $"{totalCost / totalPieces:0.##} ج.م";
        }
        else
        {
            TxtCostPerPiece.Text = "-";
        }
    }

    private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_movement == null)
        {
            NotificationManager.ShowError("لم يتم العثور على الحركة");
            return;
        }

        int cartonQty = int.TryParse(TxtCartonQty.Text, out int c) ? c : 0;
        int boxQty = int.TryParse(TxtBoxQty.Text, out int b) ? b : 0;
        int pieceQty = int.TryParse(TxtPieceQty.Text, out int p) ? p : 0;
        int newQty = _inv.CalculatePieceEquivalent(_product, cartonQty, boxQty, pieceQty);

        if (newQty <= 0)
        {
            NotificationManager.ShowError("الرجاء إدخال كمية صحيحة");
            return;
        }

        int qtyDiff = newQty - _item.Quantity;

        if (_item.MovementType == MovementType.StockIn)
        {
            var batch = FindLinkedBatch(_movement);

            if (batch == null)
            {
                NotificationManager.ShowError("لم يتم العثور على الدفعة المرتبطة بهذه الحركة");
                return;
            }

            if (qtyDiff < 0)
            {
                // التحقق: الكمية الجديدة لا تقل عما استُهلك من الدفعة
                int consumed = batch.InitialQuantity - batch.RemainingQuantity;
                if (newQty < consumed)
                {
                    NotificationManager.ShowWarning(
                        $"لا يمكن تقليل الكمية إلى {newQty} قطعة.\n" +
                        $"تم استهلاك {consumed} قطعة من هذه الدفعة بالفعل.\n" +
                        $"الحد الأدنى المسموح: {consumed} قطعة");
                    return;
                }
            }

            decimal.TryParse(TxtTotalCost.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal totalCost);

            // تحديث الدفعة
            batch.RemainingQuantity += qtyDiff;
            batch.InitialQuantity += qtyDiff;
            if (totalCost > 0 && newQty > 0)
            {
                batch.CostPricePerPiece = totalCost / newQty;
                _movement.CostPrice = batch.CostPricePerPiece;
            }

            _db.Entry(batch).State = EntityState.Modified;
        }
        else
        {
            int available = _inv.GetAvailableStock(_product) + _item.Quantity;
            if (newQty > available)
            {
                NotificationManager.ShowWarning($"الكمية المطلوبة ({newQty} قطعة) تتجاوز المخزون المتاح ({available} قطعة).\nالمخزون الحالي: {_inv.GetStockDisplay(_product)}");
                return;
            }

            if (qtyDiff > 0)
            {
                var (extraCost, extraConsumed) = _inv.CalculateFifoCost(_product, qtyDiff);
                foreach (var batch in extraConsumed)
                    _db.Entry(batch).State = EntityState.Modified;
            }
            else if (qtyDiff < 0)
            {
                int addBack = -qtyDiff;
                var recentBatches = _db.InventoryBatches
                    .Where(b => b.ProductId == _product.Id)
                    .OrderByDescending(b => b.PurchaseDate)
                    .ToList();
                foreach (var batch in recentBatches)
                {
                    if (addBack <= 0) break;
                    int space = batch.InitialQuantity - batch.RemainingQuantity;
                    int put = Math.Min(addBack, space);
                    batch.RemainingQuantity += put;
                    addBack -= put;
                    _db.Entry(batch).State = EntityState.Modified;
                }
            }
        }

        string qtyDesc = "";
        if (cartonQty > 0) qtyDesc += $"{cartonQty} كرتونة, ";
        if (boxQty > 0) qtyDesc += $"{boxQty} علبة, ";
        if (pieceQty > 0) qtyDesc += $"{pieceQty} قطعة, ";
        qtyDesc = qtyDesc.TrimEnd(',', ' ');

        _movement.Quantity = newQty;
        _movement.Notes = $"{_item.TypeDisplay} - {qtyDesc}";
        if (decimal.TryParse(TxtTotalCost.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal tc) && tc > 0)
            _movement.Notes += $", التكلفة: {tc:0.##}";

        _db.Entry(_movement).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        NotificationManager.ShowSuccess("تم تعديل الحركة بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }

    /// <summary>
    /// يجد الـ InventoryBatch المرتبط بحركة StockIn عن طريق المطابقة الدقيقة للكمية والوقت
    /// </summary>
    private InventoryBatch? FindLinkedBatch(InventoryMovement movement)
    {
        var batches = _db.InventoryBatches
            .Where(b => b.ProductId == movement.ProductId)
            .OrderBy(b => b.PurchaseDate)
            .ToList();

        // أولاً: تطابق دقيق في الوقت (نفس الثانيتين) والكمية الأصلية
        var exact = batches.FirstOrDefault(b =>
            Math.Abs((b.PurchaseDate - movement.CreatedAt).TotalSeconds) < 2 &&
            b.InitialQuantity == movement.Quantity);

        if (exact != null) return exact;

        // ثانياً: تطابق الكمية الأصلية مع هامش 5 دقائق
        return batches.FirstOrDefault(b =>
            Math.Abs((b.PurchaseDate - movement.CreatedAt).TotalMinutes) < 5 &&
            b.InitialQuantity == movement.Quantity);
    }
}
