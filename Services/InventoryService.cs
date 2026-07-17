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
        int totalPieces = GetAvailableStock(product);
        int ppc = GetPiecesPerCarton(product);
        int ppb = GetPiecesPerBox(product);
        int bpc = GetBoxesPerCarton(product);

        if (ppc > 1)
        {
            int cartons = totalPieces / ppc;
            int remainder = totalPieces % ppc;

            if (bpc > 1)
            {
                int boxes = remainder / ppb;
                int pieces = remainder % ppb;
                return $"{cartons} كرتونة, {boxes} علبة, {pieces} قطعة";
            }
            return $"{cartons} كرتونة, {remainder} قطعة";
        }
        return $"{totalPieces} قطعة";
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

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = MovementType.StockIn,
            Quantity = totalPieces,
            CostPrice = costPerPiece,
            ReferenceType = ReferenceType.Purchase,
            Notes = notes ?? $"وارد - {cartonQty} كرتونة, {boxQty} علبة, {pieceQty} قطعة"
        });

        await _db.SaveChangesAsync();
    }
}
