using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public class SupplierInvoice
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    [ForeignKey(nameof(SupplierId))] public Supplier? Supplier { get; set; }
    [MaxLength(100)] public string? SupplierName { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ICollection<SupplierInvoiceItem> Items { get; set; } = new List<SupplierInvoiceItem>();
    public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
    // لا يسمح بمتبقي سالب
    public decimal Remaining => Math.Max(0, TotalAmount - TotalPaid);
}