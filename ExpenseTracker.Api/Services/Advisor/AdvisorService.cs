using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services.Advisor.Brain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services.Advisor;

/// <summary>
/// Orchestrates the rule-based advisor: builds the financial snapshot from the database
/// and hands it to the pure <see cref="RuleBasedAdvisor"/> and <see cref="AdvisorChatEngine"/>.
/// Runs fully locally: no external AI, no API key, no data leaves the machine.
/// </summary>
public class AdvisorService(InsightsService insights, AppDbContext context, AppClock clock)
{
    public string Engine => RuleBasedAdvisor.EngineName;

    public async Task<AdvisorReport> GetReportAsync(string language, CancellationToken cancellationToken)
    {
        var snapshot = await insights.BuildSnapshotAsync(cancellationToken);
        return RuleBasedAdvisor.Generate(snapshot, NormalizeLanguage(language));
    }

    /// <summary>
    /// Chat with Ax: the whole conversation goes to <see cref="AxBrain"/>, which understands the
    /// last question (and follow-ups like "si luna trecuta?") and answers from the real transactions.
    /// </summary>
    public async Task<AxReply> ChatAsync(IReadOnlyList<ChatTurn> history, string language, CancellationToken cancellationToken)
    {
        var userMessages = history
            .Where(turn => turn.Role == "user" && !string.IsNullOrWhiteSpace(turn.Content))
            .Select(turn => turn.Content.Length > 500 ? turn.Content[..500] : turn.Content)
            .ToList();

        if (userMessages.Count == 0)
        {
            throw new ArgumentException("The conversation must contain a user message.");
        }

        language = NormalizeLanguage(language);
        var snapshot = await insights.BuildSnapshotAsync(cancellationToken);
        var report = RuleBasedAdvisor.Generate(snapshot, language);
        var settings = await insights.GetSettingsAsync(cancellationToken);

        var rows = await context.Transactions
            .AsNoTracking()
            .Select(transaction => new
            {
                transaction.Date,
                transaction.Amount,
                transaction.Type,
                CategoryName = transaction.Category.Name,
                transaction.Description
            })
            .ToListAsync(cancellationToken);

        var transactions = rows
            .Select(row => new BrainTransaction(
                DateOnly.FromDateTime(clock.ToLocal(row.Date)),
                row.Amount,
                row.Type,
                row.CategoryName,
                row.Description,
                SlotExtractor.MerchantKey(row.Description)))
            .ToList();

        var brainContext = new BrainContext(transactions, clock.Today, settings.HourlyRate, snapshot.CurrentBalance);
        return AxBrain.Respond(userMessages, brainContext, snapshot, report, language);
    }

    public static string NormalizeLanguage(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ro";
}
