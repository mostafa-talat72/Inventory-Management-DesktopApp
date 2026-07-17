using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public enum PriceType { Retail, Wholesale }

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    public int ProductUnitId { get; set; }

    [ForeignKey(nameof(ProductUnitId))]
    public ProductUnit ProductUnit { get; set; } = null!;

    public int CartonQuantity { get; set; }
    public int BoxQuantity { get; set; }
    public int PieceQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public PriceType PriceType { get; set; } = PriceType.Retail;

    public decimal Total { get; set; }

    public decimal CostPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
