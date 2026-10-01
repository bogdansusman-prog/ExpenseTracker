using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Api.Models;

public class FinancialTransaction
{
    public int Id { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public TransactionType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    /// <summary>
    /// "Was it worth it?" score given by the user a few days after an expense
    /// (1 = total regret, 5 = absolutely worth it). Null while not rated.
    /// </summary>
    [Range(1, 5)]
    public int? RegretScore { get; set; }

    public DateTime? RegretRatedAt { get; set; }

}