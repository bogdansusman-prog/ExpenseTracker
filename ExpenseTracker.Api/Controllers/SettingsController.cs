using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(InsightsService insights, AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SettingsDto>> Get(CancellationToken cancellationToken)
    {
        var settings = await insights.GetSettingsAsync(cancellationToken);
        return Ok(new SettingsDto { HourlyRate = settings.HourlyRate, Language = settings.Language });
    }

    [HttpPut]
    public async Task<IActionResult> Update(SettingsDto dto, CancellationToken cancellationToken)
    {
        var settings = await insights.GetSettingsAsync(cancellationToken);

        settings.HourlyRate = dto.HourlyRate is > 0m ? dto.HourlyRate : null;
        settings.Language = dto.Language == "en" ? "en" : "ro";

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
