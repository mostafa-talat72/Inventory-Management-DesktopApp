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

        int ppc = _inv.GetPiecesPerCarton(product);
        int ppb = _inv.GetPiecesPerBox(product);
        int total = item.Quantity;
        int cartons = ppc > 0 ? total / ppc : 0;
        int remainder = ppc > 0 ? total % ppc : total;
        int boxes = ppb > 0 ? remainder / ppb : 0;
        int pieces = ppb > 0 ? remainder % ppb : remainder;

        TxtCartonQty.Text = cartons.ToString();
        TxtBoxQty.Text = boxes.ToString();
        TxtPieceQty.Text = pieces.ToString();

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
            var batch = _db.InventoryBatches
                .Where(b => b.ProductId == _product.Id)
                .OrderBy(b => b.PurchaseDate)
                .ToList()
                .FirstOrDefault(b => Math.Abs((b.CreatedAt - _movement.CreatedAt).TotalSeconds) < 60);

            if (batch == null)
            {
                NotificationManager.ShowError("لم يتم العثور على الدفعة المرتبطة بهذه الحركة");
                return;
            }

            if (qtyDiff < 0 && batch.RemainingQuantity + qtyDiff < 0)
            {
                int consumed = batch.InitialQuantity - batch.RemainingQuantity;
                NotificationManager.ShowWarning($"لا يمكن تقليل الكمية. تم استهلاك {consumed} قطعة من هذه الدفعة.\nالحد الأدنى المسموح: {consumed} قطعة");
                return;
            }

            batch.RemainingQuantity += qtyDiff;
            _movement.CostPrice = _item.CostPrice > 0 ? _item.CostPrice : 0;

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

        _db.Entry(_movement).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        NotificationManager.ShowSuccess("تم تعديل الحركة بنجاح");
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
