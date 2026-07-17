using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public class InventoryBatch
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    public decimal CostPricePerPiece { get; set; }

    public int InitialQuantity { get; set; }

    public int RemainingQuantity { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.Now;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
