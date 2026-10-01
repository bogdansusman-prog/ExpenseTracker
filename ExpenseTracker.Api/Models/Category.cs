using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

public class Category : IUserOwned
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public AppUser? User { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<FinancialTransaction> Transactions { get; set; }
        = new List<FinancialTransaction>();
}