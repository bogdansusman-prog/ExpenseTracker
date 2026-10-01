using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Services.Ai;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/advisor")]
public class AdvisorController(AdvisorService advisor, InsightsService insights) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<AdvisorStatusDto> Status() =>
        Ok(new AdvisorStatusDto(advisor.IsConfigured, advisor.Model));

    /// <summary>AI verdict on your finances: score, strengths, concerns and 3 concrete actions.</summary>
    [HttpGet("report")]
    public async Task<ActionResult<AdvisorReport>> Report(
        [FromQuery] string? language,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var lang = language ?? (await insights.GetSettingsAsync(cancellationToken)).Language;
            return Ok(await advisor.GetReportAsync(lang, refresh, cancellationToken));
        }
        catch (AiUnavailableException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
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
        catch (AiUnavailableException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
