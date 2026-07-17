using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public enum UnitType { Carton, Box, Piece }

public class ProductUnit
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public UnitType UnitType { get; set; }

    [MaxLength(100)]
    public string? Barcode { get; set; }

    public int? ParentUnitId { get; set; }

    [ForeignKey(nameof(ParentUnitId))]
    public ProductUnit? ParentUnit { get; set; }

    public int QuantityPerParent { get; set; } = 1;

    public decimal RetailPrice { get; set; }

    public decimal WholesalePrice { get; set; }

    public bool IsBaseUnit { get; set; }

    public ICollection<ProductUnit> ChildUnits { get; set; } = new List<ProductUnit>();
}
