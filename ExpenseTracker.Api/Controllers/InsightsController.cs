using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Services.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/insights")]
public class InsightsController(InsightsService insights) : ControllerBase
{
    /// <summary>Expenses from 7-45 days ago that have not been rated yet ("Was it worth it?").</summary>
    [HttpGet("regret/pending")]
    public async Task<ActionResult<IReadOnlyList<PendingRegret>>> GetPendingRegrets(CancellationToken cancellationToken) =>
        Ok(await insights.GetPendingRegretsAsync(cancellationToken: cancellationToken));

    [HttpPost("regret/{transactionId:int}")]
    public async Task<IActionResult> Rate(int transactionId, RegretRatingDto dto, CancellationToken cancellationToken)
    {
        var updated = await insights.RateAsync(transactionId, dto.Score, cancellationToken);
        return updated ? NoContent() : NotFound("Expense not found.");
    }

    [HttpGet("regret/summary")]
    public async Task<ActionResult<RegretSummary>> GetRegretSummary(CancellationToken cancellationToken) =>
        Ok(await insights.GetRegretSummaryAsync(cancellationToken));

    /// <summary>Recurring payments detected automatically, with silent price increases flagged.</summary>
    [HttpGet("subscriptions")]
    public async Task<ActionResult<IReadOnlyList<DetectedSubscription>>> GetSubscriptions(CancellationToken cancellationToken) =>
        Ok(await insights.GetSubscriptionsAsync(cancellationToken));

    /// <summary>Monte Carlo balance forecast with a pessimistic / expected / optimistic band.</summary>
    [HttpGet("forecast")]
    public async Task<ActionResult<BalanceForecast>> GetForecast([FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        Ok(await insights.GetForecastAsync(Math.Clamp(days, 7, 180), cancellationToken));

    [HttpGet("snapshot")]
    public async Task<ActionResult<FinancialSnapshot>> GetSnapshot(CancellationToken cancellationToken) =>
        Ok(await insights.BuildSnapshotAsync(cancellationToken));
}
