using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace ExpenseTracker.Api.Services.Ai;

public record AdvisorAction(string Title, string Detail, decimal? EstimatedMonthlySavings);

public record AdvisorReport(
    int Score,
    string Verdict,
    string Headline,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Concerns,
    IReadOnlyList<AdvisorAction> Actions,
    string? FunFact,
    string Language,
    DateTime GeneratedAt,
    string Model);

public record ChatTurn(string Role, string Content);

/// <summary>
/// AI financial advisor. Sends an anonymous numeric snapshot of the user's finances to Claude
/// (no names or e-mails, only amounts, categories and descriptions) and asks for an honest verdict,
/// or answers free-form questions in a chat.
/// </summary>
public class AdvisorService(IAiClient ai, InsightsService insights, IMemoryCache cache, ILogger<AdvisorService> logger)
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => ai.IsConfigured;

    public string Model => ai.Model;

    public async Task<AdvisorReport> GetReportAsync(string language, bool refresh, CancellationToken cancellationToken)
    {
        language = NormalizeLanguage(language);
        var cacheKey = $"advisor-report:{language}";

        if (!refresh && cache.TryGetValue(cacheKey, out AdvisorReport? cached) && cached is not null)
        {
            return cached;
        }

        var snapshot = await insights.BuildSnapshotAsync(cancellationToken);
        var snapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJson);

        var system = $$"""
            You are "Owl", the personal finance advisor built into an expense-tracking app.
            You receive a JSON snapshot of the user's real data (amounts are in Romanian lei).
            Be honest and specific: say clearly whether they are doing well or badly, and why.
            Only use numbers that appear in the data or that you compute from it. Never invent transactions.
            Use the "regret" data (scores 1-5 the user gave to past purchases), the detected subscriptions,
            the Monte Carlo balance forecast and, if hourlyRate is set, express key amounts in hours of work.
            {{LanguageInstruction(language)}}

            Reply with ONLY a JSON object, no markdown, using exactly this shape:
            {
              "score": 0-100 (financial health),
              "verdict": "good" | "ok" | "bad",
              "headline": "one short sentence",
              "summary": "3-4 sentences",
              "strengths": ["..."],
              "concerns": ["..."],
              "actions": [{ "title": "...", "detail": "...", "estimatedMonthlySavings": number or null }],
              "funFact": "one surprising insight from the data, or null"
            }
            Give 2-4 strengths, 2-4 concerns and exactly 3 actions, most impactful first.
            If there is too little data, say so in the summary and keep the score near 50.
            """;

        var answer = await ai.CompleteAsync(
            system,
            [new AiMessage("user", $"Here is my financial snapshot:\n{snapshotJson}")],
            cancellationToken: cancellationToken);

        var report = ParseReport(answer, language);
        cache.Set(cacheKey, report, TimeSpan.FromMinutes(15));

        return report;
    }

    public async Task<string> ChatAsync(IReadOnlyList<ChatTurn> history, string language, CancellationToken cancellationToken)
    {
        language = NormalizeLanguage(language);

        var snapshot = await insights.BuildSnapshotAsync(cancellationToken);
        var snapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJson);

        var system = $"""
            You are "Owl", the personal finance advisor built into an expense-tracking app.
            Answer the user's questions about their own money using this data snapshot (amounts in lei):
            {snapshotJson}

            Rules: be concise (max ~150 words), concrete and friendly; use numbers from the data;
            if the data cannot answer the question, say so. You are not a licensed financial advisor,
            so for investments or loans give general information, not personal recommendations.
            {LanguageInstruction(language)}
            """;

        var messages = history
            .Where(turn => !string.IsNullOrWhiteSpace(turn.Content))
            .TakeLast(12)
            .Select(turn => new AiMessage(turn.Role == "assistant" ? "assistant" : "user", turn.Content.Trim()))
            .ToList();

        // The API requires the conversation to start with a user message.
        while (messages.Count > 0 && messages[0].Role != "user")
        {
            messages.RemoveAt(0);
        }

        if (messages.Count == 0)
        {
            throw new ArgumentException("The conversation must contain a user message.");
        }

        return await ai.CompleteAsync(system, messages, 600, cancellationToken);
    }

    private AdvisorReport ParseReport(string answer, string language)
    {
        try
        {
            var start = answer.IndexOf('{');
            var end = answer.LastIndexOf('}');

            if (start >= 0 && end > start)
            {
                var dto = JsonSerializer.Deserialize<ReportDto>(answer[start..(end + 1)], ReportJson);

                if (dto is not null)
                {
                    return new AdvisorReport(
                        Math.Clamp(dto.Score, 0, 100),
                        dto.Verdict is "good" or "ok" or "bad" ? dto.Verdict : "ok",
                        dto.Headline ?? string.Empty,
                        dto.Summary ?? string.Empty,
                        dto.Strengths ?? [],
                        dto.Concerns ?? [],
                        dto.Actions?.Select(action => new AdvisorAction(action.Title ?? "", action.Detail ?? "", action.EstimatedMonthlySavings)).ToList() ?? [],
                        dto.FunFact,
                        language,
                        DateTime.UtcNow,
                        ai.Model);
                }
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Could not parse advisor JSON");
        }

        // Fallback: show the raw text rather than failing.
        return new AdvisorReport(50, "ok", "", answer, [], [], [], null, language, DateTime.UtcNow, ai.Model);
    }

    public static string NormalizeLanguage(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ro";

    private static string LanguageInstruction(string language) => language == "en"
        ? "Write all text in English."
        : "Scrie tot textul în limba română (cu diacritice), pe un ton prietenos, la persoana a II-a singular.";

    private sealed class ReportDto
    {
        public int Score { get; set; }
        public string Verdict { get; set; } = "ok";
        public string? Headline { get; set; }
        public string? Summary { get; set; }
        public List<string>? Strengths { get; set; }
        public List<string>? Concerns { get; set; }
        public List<ActionDto>? Actions { get; set; }
        public string? FunFact { get; set; }
    }

    private sealed class ActionDto
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public decimal? EstimatedMonthlySavings { get; set; }
    }
}
