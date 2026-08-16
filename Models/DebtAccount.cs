using System.ComponentModel.DataAnnotations;

namespace ProductApp.Models;

public class DebtAccount
{
    public int Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(50)]  public string? Phone { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ICollection<Debt> Debts { get; set; } = new List<Debt>();
}