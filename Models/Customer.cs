using System.ComponentModel.DataAnnotations;

namespace ProductApp.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
