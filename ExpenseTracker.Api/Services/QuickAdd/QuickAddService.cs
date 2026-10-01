using ExpenseTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services.QuickAdd;

public record QuickAddResult(ParsedTransaction Draft, string Source, decimal? WorkHours);

/// <summary>
/// "ieri 45 lei pizza" → transaction draft, using the offline rule-based parser.
/// The draft is NOT saved: the client shows it for confirmation and then calls POST /api/transactions.
/// </summary>
public class QuickAddService(AppDbContext context, AppClock clock, InsightsService insights)
{
    public async Task<QuickAddResult> ParseAsync(string text, CancellationToken cancellationToken)
    {
        var categories = await context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryOption(category.Id, category.Name))
            .ToListAsync(cancellationToken);

        var settings = await insights.GetSettingsAsync(cancellationToken);
        var draft = NaturalLanguageParser.Parse(text, categories, clock.Today);

        var workHours = draft.Amount is { } amount
            ? InsightsService.WorkHours(amount, settings.HourlyRate)
            : null;

        return new QuickAddResult(draft, "rules", workHours);
    }
}
