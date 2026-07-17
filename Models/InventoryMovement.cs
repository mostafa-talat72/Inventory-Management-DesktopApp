using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public enum MovementType { StockIn, StockOut, Adjustment, Return, ReturnToSupplier, Shortage }
public enum ReferenceType { Purchase, Sale, Adjustment, Return, ReturnToSupplier, Shortage }

public class InventoryMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    public MovementType MovementType { get; set; }

    public int Quantity { get; set; }

    public decimal CostPrice { get; set; }

    public decimal? SellingPrice { get; set; }

    public ReferenceType ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
