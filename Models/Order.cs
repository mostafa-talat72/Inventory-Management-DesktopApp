using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public class Order
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
