using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Services.QuickAdd;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/quickadd")]
public class QuickAddController(QuickAddService quickAdd) : ControllerBase
{
    /// <summary>
    /// Turns a sentence like "ieri 45 lei pizza" into a transaction draft (not saved).
    /// </summary>
    [HttpPost("parse")]
    public async Task<ActionResult<QuickAddResult>> Parse(QuickAddRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            return BadRequest("Text is required.");
        }

        return Ok(await quickAdd.ParseAsync(dto.Text, cancellationToken));
    }
}
