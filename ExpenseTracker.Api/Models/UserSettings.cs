using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Api.Models;

/// <summary>
/// Preferences of one user (one row per user). Row Id = 1 is seeded without a user and is
/// claimed by the first account that registers.
/// </summary>
public class UserSettings : IUserOwned
{
    public const int SeedId = 1;

    public int Id { get; set; }

    public string? UserId { get; set; }

    public AppUser? User { get; set; }

    /// <summary>Net income per hour of work, used to show prices as "hours of work".</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? HourlyRate { get; set; }

    /// <summary>Language for AI answers and the natural-language parser: "ro" or "en".</summary>
    [Required]
    [MaxLength(5)]
    public string Language { get; set; } = "ro";
}
