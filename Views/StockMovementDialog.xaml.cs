using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            var (typeDisplay, typeColor, typeBgHex, sign, delta) = m.MovementType switch
            {
                MovementType.StockIn          => ("وارد",           "#2E7D32", "#E8F5E9", "+",  m.Quantity),
                MovementType.StockOut         => ("صادر",           "#C62828", "#FFEBEE", "-",  -m.Quantity),
                MovementType.Adjustment       => ("تعديل",          "#F57F17", "#FFF8E1", "±",  m.Quantity),
                MovementType.Return           => ("مرتجع",          "#1565C0", "#E3F2FD", "+",  m.Quantity),
                MovementType.ReturnToSupplier => ("مرتجع للمورد",   "#E65100", "#FFF3E0", "-",  -m.Quantity),
                MovementType.Shortage         => ("عجز",            "#C62828", "#FFEBEE", "-",  -m.Quantity),
                _                             => ("",               "#78909C", "#ECEFF1", "",    0)
            };

            runningStock += delta;

            // عرض الكمية بالإشارة الصحيحة — نعرض القيمة المطلقة مع الإشارة
            string qtyDisplay = delta != 0
                ? $"{sign} {FormatQuantity(Math.Abs(delta))}"
                : "0";

            string reason = m.Notes ?? "-";
            bool canDelete = true;

            if (m.ReferenceId.HasValue)
            {
                // أي حركة مرتبطة بطلب أو فاتورة (بيع أو مرتجع) لا يمكن تعديلها أو حذفها
                canDelete = false;

                if (m.ReferenceType == ReferenceType.Sale)
                {
                    var order = _db.Orders
                        .Include(o => o.Invoice).ThenInclude(i => i.Customer)
                        .FirstOrDefault(o => o.Id == m.ReferenceId);
                    if (order != null)
                    {
                        var invoice = order.Invoice;
                        string refText = $"طلب #{order.Id}";
                        if (invoice != null)
                        {
                            refText += $" - فاتورة #{invoice.Id}";
                            if (invoice.Customer != null)
                                refText += $" - {invoice.Customer.Name}";
                            else if (!string.IsNullOrWhiteSpace(invoice.CustomerName))
                                refText += $" - {invoice.CustomerName}";
                        }
                        reason = string.IsNullOrEmpty(m.Notes) || m.Notes == "-"
                            ? refText
                            : $"{m.Notes} ({refText})";
                    }
                }
                else if (m.ReferenceType == ReferenceType.Return)
                {
                    // مرتجع من تعديل/حذف طلب أو فاتورة
                    var invoice = _db.Invoices.FirstOrDefault(i => i.Id == m.ReferenceId);
                    if (invoice != null)
                    {
                        string refText = $"فاتورة #{invoice.Id}";
                        if (!string.IsNullOrWhiteSpace(invoice.CustomerName))
                            refText += $" - {invoice.CustomerName}";
                        reason = string.IsNullOrEmpty(m.Notes) || m.Notes == "-"
                            ? refText
                            : $"{m.Notes} ({refText})";
                    }
                }
            }

            // Parse colors — dot/fg come from typeColor, bg from typeBgHex
            Color ParseHex(string hex)
            {
                hex = hex.TrimStart('#');
                return Color.FromRgb(
                    Convert.ToByte(hex[0..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
            }

            // Badge bg: semi-transparent version of main color
            Color dotColor = ParseHex(typeColor);
            Color fgColor  = dotColor;
            // Light bg: use typeBgHex in light mode; in dark mode the SurfaceBackground handles it
            // We store as Color so XAML can bind to SolidColorBrush.Color
            Color bgColor  = ParseHex(typeBgHex);

            items.Add(new MovementItem
            {
                DateDisplay        = $"{m.CreatedAt:yyyy/MM/dd hh:mm} {(m.CreatedAt.Hour < 12 ? "ص" : "م")}",
                TypeDisplay        = typeDisplay,
                TypeColor          = typeColor,
                TypeDotColor       = dotColor,
                TypeFgColor        = fgColor,
                TypeBgColor        = bgColor,
                QuantityDisplay    = qtyDisplay,
                QuantityForeground = new SolidColorBrush(dotColor),
                UnitPriceDisplay   = m.CostPrice > 0 ? $"{m.CostPrice:0.##}" : "-",
                TotalDisplay       = m.CostPrice > 0 ? $"{(m.Quantity * m.CostPrice):0.##} ج.م" : "-",
                ReasonDisplay      = reason,
                StockAfterDisplay  = runningStock >= 0
                    ? FormatQuantity(runningStock)
                    : $"-({FormatQuantity(Math.Abs(runningStock))})",
                MovementId         = m.Id,
                CanDelete          = canDelete,
                Quantity           = m.Quantity,
                CostPrice          = m.CostPrice,
                Notes              = m.Notes ?? "",
                MovementType       = m.MovementType
            });
        }

        items.Reverse();

        MovementGrid.ItemsSource = items;
        TxtSubtitle.Text = $"{_product.Name}";
        TxtSubtitle2.Text = $"إجمالي {items.Count} حركة";
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (MovementGrid.ItemsSource is not List<MovementItem> items || items.Count == 0)
        {
            MessageBox.Show("لا توجد حركات للتصدير.", "تصدير", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"حركة_مخزون_{_product.Name}_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (saveDialog.ShowDialog() != true) return;

        try
        {
            string[] headers = { "التاريخ", "النوع", "الكمية", "سعر الوحدة", "الإجمالي", "السبب / المرجع", "المخزون بعد" };
            var rows = items.Select(i => new object?[]
            {
                i.DateDisplay, i.TypeDisplay, i.QuantityDisplay,
                i.UnitPriceDisplay, i.TotalDisplay, i.ReasonDisplay, i.StockAfterDisplay
            }).ToList();

            ExcelExportService.Export(saveDialog.FileName, headers, rows);
            NotificationManager.ShowSuccess("تم تصدير حركة المخزون إلى Excel بنجاح");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء التصدير:\n{ex.Message}", "تصدير", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

        ConfirmDialog.Show("تأكيد الحذف",
            "هل أنت متأكد من حذف هذه الحركة؟\nسيتم تعديل المخزون والدفعات المرتبطة بها ولا يمكن التراجع.",
            result => {
            if (!result) return;
            DeleteMovementCore(item.MovementId);
        }, ConfirmDialog.DialogType.Danger, requiredText: "حذف");
    }

    private void UndoLast_Click(object sender, RoutedEventArgs e)
    {
        var last = _db.InventoryMovements
            .Where(m => m.ProductId == _product.Id)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        if (last == null)
        {
            NotificationManager.ShowInfo("لا توجد حركات للتراجع عنها");
            return;
        }

        if (last.ReferenceId.HasValue)
        {
            NotificationManager.ShowWarning("آخر حركة مرتبطة بطلب أو فاتورة ولا يمكن التراجع عنها");
            return;
        }

        var movementId = last.Id;
        ConfirmDialog.Show("التراجع عن آخر حركة",
            "سيتم إلغاء آخر حركة وتحديث المخزون والدفعات.\nلا يمكن التراجع عن هذا الإجراء.",
            result => {
            if (!result) return;
            DeleteMovementCore(movementId);
        }, ConfirmDialog.DialogType.Warning, requiredText: "تراجع");
    }

    private void DeleteMovementCore(int movementId)
    {
        var movement = _db.InventoryMovements.Find(movementId);
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
                var unitName = _db.ProductUnits.AsNoTracking()
                    .Where(u => u.ProductId == _product.Id && u.UnitType == UnitType.Piece)
                    .Select(u => u.Name).FirstOrDefault() ?? "قطعة";
                NotificationManager.ShowWarning(
                    $"لا يمكن حذف هذه الحركة.\n" +
                    $"تم استهلاك {consumed} {unitName} من هذه الدفعة بالفعل.\n" +
                    $"المتبقي في الدفعة: {batch.RemainingQuantity} {unitName} فقط.");
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

        var cartonName = units.FirstOrDefault(u => u.UnitType == UnitType.Carton)?.Name ?? "كرتونة";
        var boxName = units.FirstOrDefault(u => u.UnitType == UnitType.Box)?.Name ?? "علبة";
        var pieceName = units.FirstOrDefault(u => u.UnitType == UnitType.Piece)?.Name ?? "قطعة";

        var parts = new List<string>();

        if (hasCarton && hasBox && hasPiece)
        {
            int cartons = totalPieces / ppc;
            int afterCartons = totalPieces % ppc;
            int boxes = afterCartons / ppb;
            int pieces = afterCartons % ppb;
            if (cartons > 0) parts.Add($"{cartons} {cartonName}");
            if (boxes > 0) parts.Add($"{boxes} {boxName}");
            if (pieces > 0) parts.Add($"{pieces} {pieceName}");
        }
        else if (hasCarton && hasBox && !hasPiece)
        {
            int cartons = totalPieces / bpc;
            int remBoxes = totalPieces % bpc;
            if (cartons > 0) parts.Add($"{cartons} {cartonName}");
            if (remBoxes > 0) parts.Add($"{remBoxes} {boxName}");
        }
        else if (hasCarton && !hasBox && hasPiece)
        {
            int cartons = totalPieces / ppc;
            int pieces = totalPieces % ppc;
            if (cartons > 0) parts.Add($"{cartons} {cartonName}");
            if (pieces > 0) parts.Add($"{pieces} {pieceName}");
        }
        else if (hasCarton && !hasBox && !hasPiece)
        {
            if (totalPieces > 0) parts.Add($"{totalPieces} {cartonName}");
        }
        else if (!hasCarton && hasBox && hasPiece)
        {
            int boxes = totalPieces / ppb;
            int pieces = totalPieces % ppb;
            if (boxes > 0) parts.Add($"{boxes} {boxName}");
            if (pieces > 0) parts.Add($"{pieces} {pieceName}");
        }
        else if (!hasCarton && hasBox && !hasPiece)
        {
            if (totalPieces > 0) parts.Add($"{totalPieces} {boxName}");
        }
        else
        {
            if (totalPieces > 0) parts.Add($"{totalPieces} {pieceName}");
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
    public required string DateDisplay       { get; set; }
    public required string TypeDisplay       { get; set; }
    public required string TypeColor         { get; set; }   // hex — kept for legacy
    public Color           TypeDotColor      { get; set; }
    public Color           TypeFgColor       { get; set; }
    public Color           TypeBgColor       { get; set; }
    public required string QuantityDisplay   { get; set; }
    public Brush?          QuantityForeground { get; set; }
    public required string UnitPriceDisplay  { get; set; }
    public required string TotalDisplay      { get; set; }
    public required string ReasonDisplay     { get; set; }
    public required string StockAfterDisplay { get; set; }
    public int             MovementId        { get; set; }
    public bool            CanDelete         { get; set; } = true;
    public int             Quantity          { get; set; }
    public decimal         CostPrice         { get; set; }
    public string          Notes             { get; set; } = "";
    public MovementType    MovementType      { get; set; }
}
