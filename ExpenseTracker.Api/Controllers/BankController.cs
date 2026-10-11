using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Services.Banking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Api.Controllers;

/// <summary>
/// Bank connection through Enable Banking (PSD2 account information): connect, preview, import.
/// </summary>
[ApiController]
[Route("api/bank")]
public class BankController(BankSyncService bank, InsightsService insights, IOptions<EnableBankingOptions> options) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<BankStatusDto>> Status(CancellationToken cancellationToken) =>
        Ok(await bank.GetStatusAsync(cancellationToken));

    [HttpGet("banks")]
    public Task<ActionResult> Banks([FromQuery] string country = "RO", CancellationToken cancellationToken = default) =>
        Run(async () => Ok(await bank.GetBanksAsync(country, cancellationToken)));

    [HttpPost("connect")]
    public Task<ActionResult> Connect(BankConnectRequestDto dto, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var settings = await insights.GetSettingsAsync(cancellationToken);
            var url = await bank.StartAsync(dto.AspspName, dto.Country, settings.Language, cancellationToken);
            return Ok(new BankConnectResponseDto(url));
        });

    /// <summary>
    /// The bank redirects the browser here (https is required by Enable Banking). We only pass the
    /// query parameters on to the Angular callback page, which completes the connection with the
    /// user's token. The target is fixed in configuration, so this cannot be used as an open redirect.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("return")]
    public IActionResult Return([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrEmpty(code)) query["code"] = code;
        if (!string.IsNullOrEmpty(state)) query["state"] = state;
        if (!string.IsNullOrEmpty(error)) query["error"] = error;

        return Redirect(QueryHelpers.AddQueryString(options.Value.FrontendCallbackUrl, query));
    }

    [HttpPost("callback")]
    public Task<ActionResult> Callback(BankCallbackRequestDto dto, CancellationToken cancellationToken) =>
        Run(async () => Ok(await bank.CompleteAsync(dto.Code, dto.State, cancellationToken)));

    [HttpGet("connections/{id:int}/preview")]
    public Task<ActionResult> Preview(int id, [FromQuery] int? days, CancellationToken cancellationToken) =>
        Run(async () => Ok(await bank.PreviewAsync(id, days, cancellationToken)));

    [HttpPost("connections/{id:int}/import")]
    public Task<ActionResult> Import(int id, BankImportRequestDto dto, CancellationToken cancellationToken) =>
        Run(async () => Ok(await bank.ImportAsync(id, dto.Items, cancellationToken)));

    [HttpDelete("connections/{id:int}")]
    public Task<ActionResult> Disconnect(int id, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            await bank.DisconnectAsync(id, cancellationToken);
            return NoContent();
        });

    /// <summary>Turns the known failure modes into clear HTTP answers for the frontend.</summary>
    private async Task<ActionResult> Run(Func<Task<ActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (BankSyncException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (EnableBankingException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, exception.Message);
        }
    }
}
