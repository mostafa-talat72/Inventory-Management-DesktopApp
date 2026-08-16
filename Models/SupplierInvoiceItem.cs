using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public class SupplierInvoiceItem
{
    public int Id { get; set; }
    public int SupplierInvoiceId { get; set; }
    [ForeignKey(nameof(SupplierInvoiceId))] public SupplierInvoice SupplierInvoice { get; set; } = null!;
    public int ProductId { get; set; }
    [ForeignKey(nameof(ProductId))] public Product Product { get; set; } = null!;
    public int CartonQuantity { get; set; }
    public int BoxQuantity { get; set; }
    public int PieceQuantity { get; set; }
    public decimal CostPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}