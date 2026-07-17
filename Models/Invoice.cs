using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public enum InvoiceStatus { Open, PartiallyPaid, Paid, Cancelled }

public class Invoice
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    [MaxLength(100)]
    public string? CustomerName { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.Now;

    public decimal TotalAmount { get; set; }

    public decimal TotalPaid { get; set; }

    public decimal Discount { get; set; }

    public decimal NetAmount => TotalAmount - Discount;

    public decimal Remaining => NetAmount - TotalPaid;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
