using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Dtos;

public record BankAccountDto(int Id, string? Iban, string? Currency, string? Name);

public record BankConnectionDto(
    int Id,
    string AspspName,
    string AspspCountry,
    DateTime ValidUntil,
    bool Expired,
    DateTime CreatedAt,
    DateTime? LastSyncedAt,
    List<BankAccountDto> Accounts);

public record BankStatusDto(bool Configured, string RedirectUrl, List<BankConnectionDto> Connections);

public record BankOptionDto(string Name, string Country, string? Logo);

public class BankConnectRequestDto
{
    [Required, MaxLength(200)]
    public string AspspName { get; set; } = string.Empty;

    [Required, StringLength(2, MinimumLength = 2)]
    public string Country { get; set; } = "RO";
}

public record BankConnectResponseDto(string Url);

public class BankCallbackRequestDto
{
    [Required, MaxLength(2000)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string State { get; set; } = string.Empty;
}

public record BankPreviewItemDto(
    string ExternalId,
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string Description,
    string Currency,
    string? AccountLabel,
    int? SuggestedCategoryId,
    bool AlreadyImported);

public record BankPreviewDto(DateOnly From, DateOnly To, List<BankPreviewItemDto> Items);

public class BankImportItemDto
{
    [Required, MaxLength(200)]
    public string ExternalId { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int CategoryId { get; set; }
}

public class BankImportRequestDto
{
    [Required]
    public List<BankImportItemDto> Items { get; set; } = new();
}

public record BankImportResultDto(int Imported, int Skipped);
