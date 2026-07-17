using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Now;

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
