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

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<ProductUnit> Units { get; set; } = new List<ProductUnit>();
    public ICollection<InventoryBatch> InventoryBatches { get; set; } = new List<InventoryBatch>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
