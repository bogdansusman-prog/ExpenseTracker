using ExpenseTracker.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

public class FinancialTransactionRequestDto
{
    public decimal Amount { get; set; }

    public DateTimeOffset? Date { get; set; }

    public TransactionType Type { get; set; }

    public int CategoryId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class FinancialTransactionResponseDto
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    public TransactionType Type { get; set; }

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;
}