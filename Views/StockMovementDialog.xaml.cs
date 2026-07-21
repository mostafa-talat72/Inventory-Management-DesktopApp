using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class StockMovementDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly Product _product;

    public StockMovementDialog(AppDbContext db, Product product)
    {
        InitializeComponent();
        _db = db;
        _product = product;
        _inv = new InventoryService(db);

        LoadSummary();
        LoadMovements();
    }

    private void LoadSummary()
    {
        var batches = _db.InventoryBatches
            .Where(b => b.ProductId == _product.Id && b.RemainingQuantity > 0)
            .ToList();

        int totalPieces = batches.Sum(b => b.RemainingQuantity);
        decimal fifoValue = batches.Sum(b => b.RemainingQuantity * b.CostPricePerPiece);
        int movementCount = _db.InventoryMovements.Count(m => m.ProductId == _product.Id);

        string stockDisplay = _inv.GetStockDisplay(_product);
        TxtCurrentStock.Text = stockDisplay;
        TxtStockValue.Text = $"{fifoValue:0.##} ج.م";
        TxtMovementCount.Text = movementCount.ToString();
    }

    private void LoadMovements()
    {
        var allMovements = _db.InventoryMovements
            .Where(m => m.ProductId == _product.Id)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        int runningStock = 0;
        var items = new List<MovementItem>();

        foreach (var m in allMovements)
        {
            var (typeDisplay, typeColor, sign, delta) = m.MovementType switch
            {
                MovementType.StockIn => ("وارد", "#2E7D32", "+", m.Quantity),
                MovementType.StockOut => ("صادر", "#C62828", "-", -m.Quantity),
                MovementType.Adjustment => ("تعديل", "#F57F17", "±", m.Quantity),
                MovementType.Return => ("مرتجع", "#1565C0", "+", m.Quantity),
                MovementType.ReturnToSupplier => ("مرتجع للمورد", "#E65100", "-", -m.Quantity),
                MovementType.Shortage => ("عجز", "#C62828", "-", -m.Quantity),
                _ => ("", "#78909C", "", 0)
            };

            runningStock += delta;

            string reason = m.Notes ?? "-";
            bool canDelete = true;

            if (m.ReferenceId.HasValue)
            {
                if (m.ReferenceType == ReferenceType.Sale)
                {
                    var order = _db.Orders.Include(o => o.Invoice).ThenInclude(i => i.Customer).FirstOrDefault(o => o.Id == m.ReferenceId);
                    if (order != null)
                    {
                        var invoice = order.Invoice;
                        string refText = $"طلب #{order.Id}";
                        if (invoice != null)
                        {
                            refText += $" - فاتورة #{invoice.Id}";
                            if (invoice.Customer != null)
                                refText += $" - عميل: {invoice.Customer.Name}";
                            else if (!string.IsNullOrWhiteSpace(invoice.CustomerName))
                                refText += $" - عميل: {invoice.CustomerName}";
                        }
                        canDelete = false;
                        if (reason == "-" || reason == m.Notes)
                            reason = refText;
                        else
                            reason = $"{reason} ({refText})";
                    }
                }
            }

            items.Add(new MovementItem
            {
                DateDisplay = m.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                TypeDisplay = typeDisplay,
                TypeColor = typeColor,
                QuantityDisplay = $"{sign} {FormatQuantity(m.Quantity)}",
                UnitPriceDisplay = m.CostPrice > 0 ? $"{m.CostPrice:0.##}" : "-",
                TotalDisplay = m.CostPrice > 0 ? $"{(m.Quantity * m.CostPrice):0.##} ج.م" : "-",
                ReasonDisplay = reason,
                StockAfterDisplay = FormatQuantity(runningStock),
                MovementId = m.Id,
                CanDelete = canDelete,
                Quantity = m.Quantity,
                CostPrice = m.CostPrice,
                Notes = m.Notes ?? "",
                MovementType = m.MovementType
            });
        }

        items.Reverse();

        MovementGrid.ItemsSource = items;
        TxtSubtitle.Text = $"{_product.Name}";
        TxtSubtitle2.Text = $"إجمالي {items.Count} حركة";
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EditMovement_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not MovementItem item) return;
        if (!item.CanDelete) return;

        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new MovementEditDialog(_db, _product, item);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                LoadSummary();
                LoadMovements();
            }
        };
    }

    private void DeleteMovement_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not MovementItem item) return;
        if (!item.CanDelete) return;

        ConfirmDialog.Show("تأكيد الحذف", "هل أنت متأكد من حذف هذه الحركة؟", result => {
            if (!result) return;

            var movement = _db.InventoryMovements.Find(item.MovementId);
            if (movement == null) return;

            // إذا كانت الحركة إضافة مخزون، نطرح الكمية من الـ batch المرتبط
            if (movement.MovementType == MovementType.StockIn)
            {
                var batch = FindLinkedBatch(movement);
                if (batch == null)
                {
                    NotificationManager.ShowError("لم يتم العثور على الدفعة المرتبطة بهذه الحركة");
                    return;
                }

                // التحقق أن المخزون المتاح كافٍ لطرح الكمية
                if (batch.RemainingQuantity < movement.Quantity)
                {
                    int consumed = batch.InitialQuantity - batch.RemainingQuantity;
                    NotificationManager.ShowWarning(
                        $"لا يمكن حذف هذه الحركة.\n" +
                        $"تم استهلاك {consumed} قطعة من هذه الدفعة بالفعل.\n" +
                        $"المتبقي في الدفعة: {batch.RemainingQuantity} قطعة فقط.");
                    return;
                }

                // طرح الكمية من الدفعة
                batch.RemainingQuantity -= movement.Quantity;
                batch.InitialQuantity -= movement.Quantity;

                // إذا أصبحت الدفعة فارغة تماماً احذفها
                if (batch.InitialQuantity <= 0)
                    _db.InventoryBatches.Remove(batch);
                else
                    _db.Entry(batch).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            _db.InventoryMovements.Remove(movement);
            _db.SaveChanges();
            NotificationManager.ShowSuccess("تم حذف الحركة وتحديث المخزون بنجاح");
            LoadSummary();
            LoadMovements();
        }, ConfirmDialog.DialogType.Danger);
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

        // أولاً: بحث بتطابق دقيق في الوقت (نفس الثانية)
        var exact = batches.FirstOrDefault(b =>
            Math.Abs((b.PurchaseDate - movement.CreatedAt).TotalSeconds) < 2 &&
            b.InitialQuantity == movement.Quantity);

        if (exact != null) return exact;

        // ثانياً: بحث بالكمية والوقت بهامش 5 دقائق
        return batches.FirstOrDefault(b =>
            Math.Abs((b.PurchaseDate - movement.CreatedAt).TotalMinutes) < 5 &&
            b.InitialQuantity == movement.Quantity);
    }

    private string FormatQuantity(int totalPieces)
    {
        var units = _db.ProductUnits.Where(u => u.ProductId == _product.Id).OrderBy(u => u.UnitType).ToList();
        bool hasCarton = units.Any(u => u.UnitType == UnitType.Carton);
        bool hasBox = units.Any(u => u.UnitType == UnitType.Box);
        bool hasPiece = units.Any(u => u.UnitType == UnitType.Piece);

        int ppc = _inv.GetPiecesPerCarton(_product);
        int ppb = _inv.GetPiecesPerBox(_product);
        int bpc = _inv.GetBoxesPerCarton(_product);

        var parts = new List<string>();

        if (hasCarton && hasBox && hasPiece)
        {
            int cartons = totalPieces / ppc;
            int afterCartons = totalPieces % ppc;
            int boxes = afterCartons / ppb;
            int pieces = afterCartons % ppb;
            if (cartons > 0) parts.Add($"{cartons} كرتونة");
            if (boxes > 0) parts.Add($"{boxes} علبة");
            if (pieces > 0) parts.Add($"{pieces} قطعة");
        }
        else if (hasCarton && hasBox && !hasPiece)
        {
            int cartons = totalPieces / bpc;
            int remBoxes = totalPieces % bpc;
            if (cartons > 0) parts.Add($"{cartons} كرتونة");
            if (remBoxes > 0) parts.Add($"{remBoxes} علبة");
        }
        else if (hasCarton && !hasBox && hasPiece)
        {
            int cartons = totalPieces / ppc;
            int pieces = totalPieces % ppc;
            if (cartons > 0) parts.Add($"{cartons} كرتونة");
            if (pieces > 0) parts.Add($"{pieces} قطعة");
        }
        else if (hasCarton && !hasBox && !hasPiece)
        {
            if (totalPieces > 0) parts.Add($"{totalPieces} كرتونة");
        }
        else if (!hasCarton && hasBox && hasPiece)
        {
            int boxes = totalPieces / ppb;
            int pieces = totalPieces % ppb;
            if (boxes > 0) parts.Add($"{boxes} علبة");
            if (pieces > 0) parts.Add($"{pieces} قطعة");
        }
        else if (!hasCarton && hasBox && !hasPiece)
        {
            if (totalPieces > 0) parts.Add($"{totalPieces} علبة");
        }
        else
        {
            if (totalPieces > 0) parts.Add($"{totalPieces} قطعة");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "0";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}

public class MovementItem
{
    public required string DateDisplay { get; set; }
    public required string TypeDisplay { get; set; }
    public required string TypeColor { get; set; }
    public required string QuantityDisplay { get; set; }
    public required string UnitPriceDisplay { get; set; }
    public required string TotalDisplay { get; set; }
    public required string ReasonDisplay { get; set; }
    public required string StockAfterDisplay { get; set; }
    public int MovementId { get; set; }
    public bool CanDelete { get; set; } = true;
    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public string Notes { get; set; } = "";
    public MovementType MovementType { get; set; }
}
