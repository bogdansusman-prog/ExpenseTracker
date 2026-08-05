using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<FinancialTransaction> Transactions { get; set; }
        = new List<FinancialTransaction>();
}