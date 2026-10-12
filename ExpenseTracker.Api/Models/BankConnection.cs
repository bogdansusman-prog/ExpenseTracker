using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

/// <summary>
/// A consent given by the user to read one bank's accounts through Enable Banking (PSD2).
/// The session id is what lets the API read the accounts until <see cref="ValidUntil"/>.
/// </summary>
public class BankConnection : IUserOwned
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public AppUser? User { get; set; }

    [MaxLength(200)]
    public string AspspName { get; set; } = string.Empty;

    [MaxLength(2)]
    public string AspspCountry { get; set; } = string.Empty;

    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    public DateTime ValidUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSyncedAt { get; set; }

    public ICollection<BankAccount> Accounts { get; set; } = new List<BankAccount>();
}

/// <summary>One account (IBAN) shared through a <see cref="BankConnection"/>.</summary>
public class BankAccount : IUserOwned
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public AppUser? User { get; set; }

    public int BankConnectionId { get; set; }

    public BankConnection BankConnection { get; set; } = null!;

    /// <summary>Enable Banking account uid, used to read balances and transactions.</summary>
    [MaxLength(100)]
    public string Uid { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Iban { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }
}

/// <summary>
/// A started (not yet finished) bank authorization. The random state ties the bank's redirect
/// back to the user who started it, so nobody can attach their bank session to someone else.
/// </summary>
public class BankAuthRequest : IUserOwned
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public AppUser? User { get; set; }

    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AspspName { get; set; } = string.Empty;

    [MaxLength(2)]
    public string AspspCountry { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
