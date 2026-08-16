using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;

namespace ProductApp.Services;

public class InventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    public int GetTotalPieces(Product product)
    {
        var units = _db.ProductUnits.AsNoTracking().Where(u => u.ProductId == product.Id).ToList();
        var carton = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
        var box = units.FirstOrDefault(u => u.UnitType == UnitType.Box);

        int piecesPerBox = box?.QuantityPerParent ?? 1;
        int piecesPerCarton = 1;

        if (carton != null)
        {
            if (box != null && box.ParentUnitId == carton.Id)
                piecesPerCarton = carton.QuantityPerParent * piecesPerBox;
            else
                piecesPerCarton = carton.QuantityPerParent;
        }
        else if (box != null)
        {
            piecesPerCarton = piecesPerBox;
        }

        return piecesPerCarton;
    }

    public int GetPiecesPerBox(Product product)
    {
        var units = _db.ProductUnits.AsNoTracking().Where(u => u.ProductId == product.Id).ToList();
        var box = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        if (box != null)
            return box.QuantityPerParent;
        return 1;
    }

    public int GetPiecesPerCarton(Product product)
    {
        return GetTotalPieces(product);
    }

    public int GetBoxesPerCarton(Product product)
    {
        var units = _db.ProductUnits.AsNoTracking().Where(u => u.ProductId == product.Id).ToList();
        var carton = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
        var box = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        if (carton != null && box != null && box.ParentUnitId == carton.Id)
            return carton.QuantityPerParent;
        return 1;
    }

    public int CalculatePieceEquivalent(Product product, int cartonQty, int boxQty, int pieceQty)
    {
        int ppc = GetPiecesPerCarton(product);
        int ppb = GetPiecesPerBox(product);
        return pieceQty + (boxQty * ppb) + (cartonQty * ppc);
    }

    public bool IsStockSufficient(Product product, int cartonQty, int boxQty, int pieceQty)
    {
        int totalPieces = CalculatePieceEquivalent(product, cartonQty, boxQty, pieceQty);
        int available = GetAvailableStock(product);
        return totalPieces <= available;
    }

    public (decimal cost, List<InventoryBatch> consumed) CalculateFifoCost(Product product, int totalPieces)
    {
        var batches = _db.InventoryBatches
            .Where(b => b.ProductId == product.Id && b.RemainingQuantity > 0)
            .OrderBy(b => b.PurchaseDate)
            .ToList();

        decimal totalCost = 0;
        var consumed = new List<InventoryBatch>();
        int remaining = totalPieces;

        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            int take = Math.Min(remaining, batch.RemainingQuantity);
            totalCost += take * batch.CostPricePerPiece;
            batch.RemainingQuantity -= take;
            remaining -= take;
            consumed.Add(batch);
        }

        return (totalCost, consumed);
    }

    public int GetAvailableStock(Product product)
    {
        return _db.InventoryBatches
            .Where(b => b.ProductId == product.Id)
            .Sum(b => b.RemainingQuantity);
    }

    public string GetStockDisplay(Product product)
    {
        var units = _db.ProductUnits.AsNoTracking().Where(u => u.ProductId == product.Id).OrderBy(u => u.UnitType).ToList();
        int total = GetAvailableStock(product);
        return GetStockDisplay(units, total);
    }

    /// <summary>
    /// نسخة ثابتة لا تستعلم من قاعدة البيانات — تقبل الوحدات والمخزون الكلي
    /// (لتفادي استعلامات N+1 في القوائم الكبيرة).
    /// </summary>
    public static string GetStockDisplay(IEnumerable<ProductUnit> unitsSource, int total)
    {
        var units = unitsSource.OrderBy(u => u.UnitType).ToList();
        bool hasCarton = units.Any(u => u.UnitType == UnitType.Carton);
        bool hasBox = units.Any(u => u.UnitType == UnitType.Box);
        bool hasPiece = units.Any(u => u.UnitType == UnitType.Piece);

        var carton = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
        var box = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        var piece = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);

        var cartonName = carton?.Name ?? "كرتونة";
        var boxName = box?.Name ?? "علبة";
        var pieceName = piece?.Name ?? "قطعة";

        int piecesPerBox = box?.QuantityPerParent ?? 1;
        int piecesPerCarton = carton != null
            ? (box != null && box.ParentUnitId == carton.Id ? carton.QuantityPerParent * piecesPerBox : carton.QuantityPerParent)
            : piecesPerBox;

        // Carton → Box → Piece (full hierarchy)
        if (hasCarton && hasBox && hasPiece)
        {
            int cartons = total / piecesPerCarton;
            int afterCartons = total % piecesPerCarton;
            int boxes = afterCartons / piecesPerBox;
            int piecesLeft = afterCartons % piecesPerBox;
            return $"{cartons} {cartonName}, {boxes} {boxName}, {piecesLeft} {pieceName}";
        }

        // Carton → Box (no piece)
        if (hasCarton && hasBox && !hasPiece)
        {
            int cartons = total / piecesPerCarton;
            int remBoxes = total % piecesPerCarton;
            if (cartons > 0 && remBoxes > 0)
                return $"{cartons} {cartonName}, {remBoxes} {boxName}";
            if (cartons > 0)
                return $"{cartons} {cartonName}";
            return $"{remBoxes} {boxName}";
        }

        // Carton → Piece (no box)
        if (hasCarton && !hasBox && hasPiece)
        {
            int cartons = total / piecesPerCarton;
            int piecesLeft = total % piecesPerCarton;
            return $"{cartons} {cartonName}, {piecesLeft} {pieceName}";
        }

        // Carton only
        if (hasCarton && !hasBox && !hasPiece)
            return $"{total} {cartonName}";

        // Box → Piece (no carton)
        if (!hasCarton && hasBox && hasPiece)
        {
            int boxes = total / piecesPerBox;
            int piecesLeft = total % piecesPerBox;
            return boxes > 0 ? $"{boxes} {boxName}, {piecesLeft} {pieceName}" : $"{piecesLeft} {pieceName}";
        }

        // Box only
        if (!hasCarton && hasBox && !hasPiece)
            return $"{total} {boxName}";

        // Piece only (or fallback)
        return $"{total} {pieceName}";
    }

    public async Task StockIn(Product product, int cartonQty, int boxQty, int pieceQty, decimal totalCost, string? notes = null)
    {
        int ppc = GetPiecesPerCarton(product);
        int ppb = GetPiecesPerBox(product);
        int totalPieces = (cartonQty * ppc) + (boxQty * ppb) + pieceQty;
        decimal costPerPiece = totalPieces > 0 ? totalCost / totalPieces : 0;

        var batch = new InventoryBatch
        {
            ProductId = product.Id,
            CostPricePerPiece = costPerPiece,
            InitialQuantity = totalPieces,
            RemainingQuantity = totalPieces,
            PurchaseDate = DateTime.Now
        };
        _db.InventoryBatches.Add(batch);

        string reasonParts = "وارد";
        if (cartonQty > 0) reasonParts += $" - {cartonQty} {GetUnitName(product, UnitType.Carton)}";
        if (boxQty > 0) reasonParts += $" - {boxQty} {GetUnitName(product, UnitType.Box)}";
        if (pieceQty > 0) reasonParts += $" - {pieceQty} {GetUnitName(product, UnitType.Piece)}";

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = MovementType.StockIn,
            Quantity = totalPieces,
            CostPrice = costPerPiece,
            ReferenceType = ReferenceType.Purchase,
            Notes = notes ?? reasonParts
        });

        await _db.SaveChangesAsync();
    }

    public (decimal unitCost, decimal totalCost) ReturnToBatches(int productId, int totalPieces)
    {
        if (totalPieces <= 0) return (0, 0);
        decimal totalCost = 0;
        int remaining = totalPieces;

        var batches = _db.InventoryBatches
            .Where(b => b.ProductId == productId)
            .OrderByDescending(b => b.PurchaseDate)
            .ToList();

        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            int consumed = batch.InitialQuantity - batch.RemainingQuantity;
            if (consumed <= 0) continue;
            int returnQty = Math.Min(remaining, consumed);
            batch.RemainingQuantity += returnQty;
            totalCost += returnQty * batch.CostPricePerPiece;
            remaining -= returnQty;
        }

        if (remaining > 0)
        {
            _db.InventoryBatches.Add(new InventoryBatch
            {
                ProductId = productId,
                CostPricePerPiece = 0,
                InitialQuantity = remaining,
                RemainingQuantity = remaining,
                PurchaseDate = DateTime.Now
            });
        }

        return (totalPieces > 0 ? totalCost / totalPieces : 0, totalCost);
    }

    public async Task StockOut(Product product, int totalPieces, string? notes = null)
    {
        var batches = _db.InventoryBatches
            .Where(b => b.ProductId == product.Id && b.RemainingQuantity > 0)
            .OrderBy(b => b.PurchaseDate)
            .ToList();

        int toDeduct = totalPieces;
        foreach (var batch in batches)
        {
            if (toDeduct <= 0) break;
            int take = Math.Min(toDeduct, batch.RemainingQuantity);
            batch.RemainingQuantity -= take;
            toDeduct -= take;
        }

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = MovementType.StockOut,
            Quantity = totalPieces,
            ReferenceType = ReferenceType.Adjustment,
            Notes = notes ?? $"منصرف - {totalPieces} {GetUnitName(product, UnitType.Piece)}"
        });

        await _db.SaveChangesAsync();
    }

    private string GetUnitName(Product product, UnitType type)
    {
        var name = _db.ProductUnits.AsNoTracking()
            .Where(u => u.ProductId == product.Id && u.UnitType == type)
            .Select(u => u.Name)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(name)
            ? type switch
            {
                UnitType.Carton => "كرتونة",
                UnitType.Box => "علبة",
                _ => "قطعة"
            }
            : name;
    }
}
