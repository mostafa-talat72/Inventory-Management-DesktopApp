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
        var units = _db.ProductUnits.Where(u => u.ProductId == product.Id).ToList();
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
        var units = _db.ProductUnits.Where(u => u.ProductId == product.Id).ToList();
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
        var units = _db.ProductUnits.Where(u => u.ProductId == product.Id).ToList();
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
        var units = _db.ProductUnits.Where(u => u.ProductId == product.Id).OrderBy(u => u.UnitType).ToList();
        bool hasCarton = units.Any(u => u.UnitType == UnitType.Carton);
        bool hasBox = units.Any(u => u.UnitType == UnitType.Box);
        bool hasPiece = units.Any(u => u.UnitType == UnitType.Piece);

        int total = GetAvailableStock(product);

        // Carton → Box → Piece (full hierarchy)
        if (hasCarton && hasBox && hasPiece)
        {
            int ppc = GetPiecesPerCarton(product);
            int ppb = GetPiecesPerBox(product);
            int cartons = total / ppc;
            int afterCartons = total % ppc;
            int boxes = afterCartons / ppb;
            int pieces = afterCartons % ppb;
            return $"{cartons} كرتونة, {boxes} علبة, {pieces} قطعة";
        }

        // Carton → Box (no piece)
        if (hasCarton && hasBox && !hasPiece)
        {
            int bpc = GetBoxesPerCarton(product);
            int cartons = total / bpc;
            int remBoxes = total % bpc;
            if (cartons > 0 && remBoxes > 0)
                return $"{cartons} كرتونة, {remBoxes} علبة";
            if (cartons > 0)
                return $"{cartons} كرتونة";
            return $"{remBoxes} علبة";
        }

        // Carton → Piece (no box)
        if (hasCarton && !hasBox && hasPiece)
        {
            int ppc = GetPiecesPerCarton(product);
            int cartons = total / ppc;
            int pieces = total % ppc;
            return $"{cartons} كرتونة, {pieces} قطعة";
        }

        // Carton only
        if (hasCarton && !hasBox && !hasPiece)
            return $"{total} كرتونة";

        // Box → Piece (no carton)
        if (!hasCarton && hasBox && hasPiece)
        {
            int ppb = GetPiecesPerBox(product);
            int boxes = total / ppb;
            int pieces = total % ppb;
            return boxes > 0 ? $"{boxes} علبة, {pieces} قطعة" : $"{pieces} قطعة";
        }

        // Box only
        if (!hasCarton && hasBox && !hasPiece)
            return $"{total} علبة";

        // Piece only (or fallback)
        return $"{total} قطعة";
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
        if (cartonQty > 0) reasonParts += $" - {cartonQty} كرتونة";
        if (boxQty > 0) reasonParts += $" - {boxQty} علبة";
        if (pieceQty > 0) reasonParts += $" - {pieceQty} قطعة";

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
}
