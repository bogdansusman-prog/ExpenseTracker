using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Api.Models;

/// <summary>
/// Application-wide preferences. The app is single-user, so there is exactly one row (Id = 1).
/// </summary>
public class UserSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; }

    /// <summary>Net income per hour of work, used to show prices as "hours of work".</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? HourlyRate { get; set; }

    /// <summary>Language for AI answers and the natural-language parser: "ro" or "en".</summary>
    [Required]
    [MaxLength(5)]
    public string Language { get; set; } = "ro";
}
