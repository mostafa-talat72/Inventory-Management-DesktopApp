using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductApp.Models;

public class DebtPayment
{
    public int Id { get; set; }
    public int DebtId { get; set; }
    [ForeignKey(nameof(DebtId))] public Debt Debt { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    [MaxLength(50)]  public string? PaymentMethod { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}