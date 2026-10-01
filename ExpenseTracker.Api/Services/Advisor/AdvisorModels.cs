namespace ExpenseTracker.Api.Services.Advisor;

public record AdvisorAction(string Title, string Detail, decimal? EstimatedMonthlySavings);

/// <summary>One line of the score breakdown, e.g. "Savings rate: 25 / 35".</summary>
public record ScoreComponent(string Key, string Label, int Points, int MaxPoints, string Explanation);

public record AdvisorReport(
    int Score,
    string Verdict,
    string Headline,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Concerns,
    IReadOnlyList<AdvisorAction> Actions,
    string? FunFact,
    IReadOnlyList<ScoreComponent> Breakdown,
    string Language,
    DateTime GeneratedAt,
    string Engine);

public record ChatTurn(string Role, string Content);
