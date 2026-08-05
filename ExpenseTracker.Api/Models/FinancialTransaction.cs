using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Api.Models;

public class FinancialTransaction
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public TransactionType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;
}