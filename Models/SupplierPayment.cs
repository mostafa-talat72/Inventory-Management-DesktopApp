using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public class SupplierPayment
{
    public int Id { get; set; }
    public int SupplierInvoiceId { get; set; }
    [ForeignKey(nameof(SupplierInvoiceId))] public SupplierInvoice SupplierInvoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    [MaxLength(50)]  public string? PaymentMethod { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}