using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Services.Advisor;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/advisor")]
public class AdvisorController(AdvisorService advisor, InsightsService insights) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<AdvisorStatusDto> Status() =>
        Ok(new AdvisorStatusDto(true, advisor.Engine));

    /// <summary>Financial health report: score with breakdown, strengths, concerns and 3 concrete actions.</summary>
    [HttpGet("report")]
    public async Task<ActionResult<AdvisorReport>> Report([FromQuery] string? language, CancellationToken cancellationToken)
    {
        var lang = language ?? (await insights.GetSettingsAsync(cancellationToken)).Language;
        return Ok(await advisor.GetReportAsync(lang, cancellationToken));
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AdvisorChatResponseDto>> Chat(AdvisorChatRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var lang = dto.Language ?? (await insights.GetSettingsAsync(cancellationToken)).Language;
            var reply = await advisor.ChatAsync(dto.Messages, lang, cancellationToken);
            return Ok(new AdvisorChatResponseDto(reply));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
