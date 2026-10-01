using System.Text.Json;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Ai;
using ExpenseTracker.Api.Services.Analytics;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services.QuickAdd;

public record QuickAddResult(ParsedTransaction Draft, string Source, decimal? WorkHours);

/// <summary>
/// "ieri 45 lei pizza" → transaction draft. Uses the offline rule-based parser first and asks
/// Claude only when the rules could not find the amount or the category (and AI is configured).
/// The draft is NOT saved: the client shows it for confirmation and then calls POST /api/transactions.
/// </summary>
public class QuickAddService(AppDbContext context, AppClock clock, IAiClient ai, InsightsService insights, ILogger<QuickAddService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<QuickAddResult> ParseAsync(string text, bool allowAi, CancellationToken cancellationToken)
    {
        var categories = await context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryOption(category.Id, category.Name))
            .ToListAsync(cancellationToken);

        var settings = await insights.GetSettingsAsync(cancellationToken);
        var draft = NaturalLanguageParser.Parse(text, categories, clock.Today);
        var source = "rules";

        if (allowAi && ai.IsConfigured && draft.MissingFields.Count > 0)
        {
            try
            {
                var aiDraft = await ParseWithAiAsync(text, categories, settings.Language, cancellationToken);
                if (aiDraft is not null)
                {
                    draft = Merge(draft, aiDraft);
                    source = "ai";
                }
            }
            catch (AiUnavailableException exception)
            {
                logger.LogInformation(exception, "AI parsing unavailable, keeping rule-based result");
            }
        }

        return new QuickAddResult(draft, source, draft.Amount is { } amount ? InsightsService.WorkHours(amount, settings.HourlyRate) : null);
    }

    private async Task<ParsedTransaction?> ParseWithAiAsync(
        string text, IReadOnlyList<CategoryOption> categories, string language, CancellationToken cancellationToken)
    {
        var categoryList = string.Join(", ", categories.Select(category => $"\"{category.Name}\""));

        var system = $$"""
            You convert a short note about money into a transaction. Today is {{clock.Today:yyyy-MM-dd}}.
            The note may be in Romanian or English. Existing categories: [{{categoryList}}].
            Reply with ONLY JSON: {"amount": number|null, "type": "income"|"expense", "date": "yyyy-MM-dd",
            "category": one of the existing category names or null, "description": short text or null}
            """;

        var answer = await ai.CompleteAsync(system, [new AiMessage("user", text)], 200, cancellationToken);
        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<AiDraft>(answer[start..(end + 1)], Json);
            if (dto is null)
            {
                return null;
            }

            var category = categories.FirstOrDefault(option =>
                TextNormalizer.Fold(option.Name) == TextNormalizer.Fold(dto.Category));

            var date = DateOnly.TryParse(dto.Date, out var parsedDate) && parsedDate <= clock.Today
                ? parsedDate
                : clock.Today;

            var missing = new List<string>();
            if (dto.Amount is not > 0m) missing.Add("amount");
            if (category is null) missing.Add("category");

            return new ParsedTransaction(
                dto.Amount is > 0m ? dto.Amount : null,
                dto.Type == "income" ? TransactionType.Income : TransactionType.Expense,
                date,
                category?.Id,
                category?.Name,
                string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                missing.Count == 0 ? 0.9 : 0.6,
                missing);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Keep everything the rules found; fill only the gaps with the AI answer.</summary>
    private static ParsedTransaction Merge(ParsedTransaction rules, ParsedTransaction fromAi)
    {
        var amount = rules.Amount ?? fromAi.Amount;
        var categoryId = rules.CategoryId ?? fromAi.CategoryId;
        var categoryName = rules.CategoryId is not null ? rules.CategoryName : fromAi.CategoryName;

        var missing = new List<string>();
        if (amount is null) missing.Add("amount");
        if (categoryId is null) missing.Add("category");

        return rules with
        {
            Amount = amount,
            CategoryId = categoryId,
            CategoryName = categoryName,
            Type = rules.Type == TransactionType.Income ? TransactionType.Income : fromAi.Type,
            Description = rules.Description ?? fromAi.Description,
            Confidence = Math.Max(rules.Confidence, fromAi.Confidence),
            MissingFields = missing
        };
    }

    private sealed class AiDraft
    {
        public decimal? Amount { get; set; }
        public string? Type { get; set; }
        public string? Date { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
    }
}
