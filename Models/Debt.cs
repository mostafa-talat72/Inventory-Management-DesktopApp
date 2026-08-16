using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public enum DebtDirection { OnMe, ForMe }

public class Debt
{
    public int Id { get; set; }
    public int? DebtAccountId { get; set; }
    [ForeignKey(nameof(DebtAccountId))] public DebtAccount? DebtAccount { get; set; }
    [MaxLength(150)] public string AccountName { get; set; } = "";
    public DebtDirection Direction { get; set; } = DebtDirection.OnMe;
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ICollection<DebtPayment> Payments { get; set; } = new List<DebtPayment>();
    // لا يسمح بمتبقي سالب
    public decimal Remaining => Math.Max(0, TotalAmount - TotalPaid);
}