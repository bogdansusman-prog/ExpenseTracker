namespace ExpenseTracker.Api.Services.Analytics;

public record RatedExpense(DateTime LocalDate, decimal Amount, int Score, string CategoryName);

public record RegretCell(string CategoryName, int DayOfWeek, double AverageScore, int Count, decimal Amount);

public record CategoryRegret(string CategoryName, double AverageScore, int Count, decimal Amount, decimal RegrettedAmount);

public record RegretSummary(
    int RatedCount,
    double AverageScore,
    decimal RegrettedAmountThisMonth,
    decimal RegrettedAmountTotal,
    string? WorstCategory,
    int? WorstDayOfWeek,
    IReadOnlyList<CategoryRegret> ByCategory,
    IReadOnlyList<RegretCell> Heatmap);

/// <summary>
/// Turns "was it worth it?" ratings into patterns: which categories and which days of
/// the week produce the purchases you regret. A score of 1-2 counts as "regretted".
/// </summary>
public static class RegretAnalyzer
{
    public const int RegretThreshold = 2;

    public static RegretSummary Analyze(IReadOnlyCollection<RatedExpense> rated, DateTime localNow)
    {
        if (rated.Count == 0)
        {
            return new RegretSummary(0, 0, 0, 0, null, null, [], []);
        }

        var monthStart = new DateTime(localNow.Year, localNow.Month, 1);

        var byCategory = rated
            .GroupBy(expense => expense.CategoryName)
            .Select(group => new CategoryRegret(
                group.Key,
                Math.Round(group.Average(expense => expense.Score), 2),
                group.Count(),
                group.Sum(expense => expense.Amount),
                group.Where(IsRegretted).Sum(expense => expense.Amount)))
            .OrderBy(category => category.AverageScore)
            .ThenByDescending(category => category.RegrettedAmount)
            .ToList();

        var heatmap = rated
            .GroupBy(expense => (expense.CategoryName, Day: (int)expense.LocalDate.DayOfWeek))
            .Select(group => new RegretCell(
                group.Key.CategoryName,
                group.Key.Day,
                Math.Round(group.Average(expense => expense.Score), 2),
                group.Count(),
                group.Sum(expense => expense.Amount)))
            .ToList();

        // "Worst" only makes sense with enough data points behind it.
        var worstCategory = byCategory
            .Where(category => category.Count >= 2 && category.AverageScore <= 3)
            .Select(category => category.CategoryName)
            .FirstOrDefault();

        var worstDay = rated
            .GroupBy(expense => (int)expense.LocalDate.DayOfWeek)
            .Where(group => group.Count() >= 2)
            .Select(group => new { Day = group.Key, Average = group.Average(expense => expense.Score) })
            .Where(day => day.Average <= 3)
            .OrderBy(day => day.Average)
            .Select(day => (int?)day.Day)
            .FirstOrDefault();

        return new RegretSummary(
            RatedCount: rated.Count,
            AverageScore: Math.Round(rated.Average(expense => expense.Score), 2),
            RegrettedAmountThisMonth: rated
                .Where(expense => expense.LocalDate >= monthStart && IsRegretted(expense))
                .Sum(expense => expense.Amount),
            RegrettedAmountTotal: rated.Where(IsRegretted).Sum(expense => expense.Amount),
            WorstCategory: worstCategory,
            WorstDayOfWeek: worstDay,
            ByCategory: byCategory,
            Heatmap: heatmap);
    }

    public static bool IsRegretted(RatedExpense expense) => expense.Score <= RegretThreshold;
}
