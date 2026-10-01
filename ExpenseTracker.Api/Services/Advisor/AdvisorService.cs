namespace ExpenseTracker.Api.Services.Advisor;

/// <summary>
/// Orchestrates the rule-based advisor: builds the financial snapshot from the database
/// and hands it to the pure <see cref="RuleBasedAdvisor"/> and <see cref="AdvisorChatEngine"/>.
/// Runs fully locally: no external AI, no API key, no data leaves the machine.
/// </summary>
public class AdvisorService(InsightsService insights)
{
    public string Engine => RuleBasedAdvisor.EngineName;

    public async Task<AdvisorReport> GetReportAsync(string language, CancellationToken cancellationToken)
    {
        var snapshot = await insights.BuildSnapshotAsync(cancellationToken);
        return RuleBasedAdvisor.Generate(snapshot, NormalizeLanguage(language));
    }

    public async Task<string> ChatAsync(IReadOnlyList<ChatTurn> history, string language, CancellationToken cancellationToken)
    {
        var question = history.LastOrDefault(turn => turn.Role == "user" && !string.IsNullOrWhiteSpace(turn.Content))?.Content
                       ?? throw new ArgumentException("The conversation must contain a user message.");

        language = NormalizeLanguage(language);
        var snapshot = await insights.BuildSnapshotAsync(cancellationToken);
        var report = RuleBasedAdvisor.Generate(snapshot, language);

        return AdvisorChatEngine.Answer(question, snapshot, report, language);
    }

    public static string NormalizeLanguage(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ro";
}
