using System.ComponentModel.DataAnnotations;

namespace ProductApp.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    [MaxLength(100)]
    public string? Barcode { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<ProductUnit> Units { get; set; } = new List<ProductUnit>();
    public ICollection<InventoryBatch> InventoryBatches { get; set; } = new List<InventoryBatch>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
