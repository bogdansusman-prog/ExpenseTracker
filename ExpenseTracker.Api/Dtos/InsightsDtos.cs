using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Api.Services.Advisor;

namespace ExpenseTracker.Api.Dtos;

public class RegretRatingDto
{
    [Range(1, 5)]
    public int Score { get; set; }
}

public class SettingsDto
{
    [Range(0, 100000)]
    public decimal? HourlyRate { get; set; }

    [RegularExpression("^(ro|en)$")]
    public string Language { get; set; } = "ro";
}

public class QuickAddRequestDto
{
    [Required]
    [MaxLength(300)]
    public string Text { get; set; } = string.Empty;
}

public class AdvisorChatRequestDto
{
    [Required]
    [MinLength(1)]
    public List<ChatTurn> Messages { get; set; } = [];

    public string? Language { get; set; }
}

/// <summary>Ax's answer plus what he understood (intent, period, category...), shown as chips.</summary>
public record AdvisorChatResponseDto(string Reply, IReadOnlyList<string> Understood);

public record AdvisorStatusDto(bool Configured, string Engine);
