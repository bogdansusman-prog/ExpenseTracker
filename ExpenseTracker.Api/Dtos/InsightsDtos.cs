using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Api.Services.Ai;

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

    public bool UseAi { get; set; } = true;
}

public class AdvisorChatRequestDto
{
    [Required]
    [MinLength(1)]
    public List<ChatTurn> Messages { get; set; } = [];

    public string? Language { get; set; }
}

public record AdvisorChatResponseDto(string Reply);

public record AdvisorStatusDto(bool Configured, string Model);
