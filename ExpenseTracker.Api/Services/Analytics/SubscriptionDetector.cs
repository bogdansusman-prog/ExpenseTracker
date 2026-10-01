namespace ExpenseTracker.Api.Services.Analytics;

public record ExpensePoint(
    int Id,
    DateTime Date,
    decimal Amount,
    string? Description,
    string CategoryName);

public record DetectedSubscription(
    string Name,
    string CategoryName,
    string Frequency,
    int IntervalDays,
    int Occurrences,
    decimal CurrentAmount,
    decimal? PreviousAmount,
    decimal PriceChangePercent,
    bool PriceIncreased,
    decimal MonthlyCost,
    decimal YearlyCost,
    DateTime LastChargeDate,
    DateTime NextExpectedDate);

/// <summary>
/// Finds recurring payments (subscriptions, rent, bills) in the expense history
/// without the user tagging anything: groups similar expenses, checks that the gaps
/// between them follow a regular rhythm and flags silent price increases.
/// </summary>
public static class SubscriptionDetector
{
    private sealed record Rhythm(string Name, int Days, int Tolerance);

    private static readonly Rhythm[] Rhythms =
    [
        new("weekly", 7, 2),
        new("biweekly", 14, 3),
        new("monthly", 30, 5),
        new("quarterly", 91, 12),
        new("yearly", 365, 25)
    ];

    private const double AverageDaysPerMonth = 30.44;

    public static IReadOnlyList<DetectedSubscription> Detect(
        IEnumerable<ExpensePoint> expenses,
        int minOccurrences = 3,
        double minRegularity = 0.7)
    {
        var result = new List<DetectedSubscription>();

        var groups = expenses
            .Where(expense => expense.Amount > 0)
            .GroupBy(BuildKey);

        foreach (var group in groups)
        {
            // One charge per day at most: same-day duplicates would break the interval logic.
            var charges = group
                .GroupBy(expense => expense.Date.Date)
                .Select(day => day.OrderByDescending(expense => expense.Id).First())
                .OrderBy(expense => expense.Date)
                .ToList();

            if (charges.Count < minOccurrences)
            {
                continue;
            }

            var intervals = charges
                .Zip(charges.Skip(1), (previous, next) => (next.Date.Date - previous.Date.Date).TotalDays)
                .ToList();

            var median = Median(intervals);
            var rhythm = Rhythms.FirstOrDefault(candidate => Math.Abs(median - candidate.Days) <= candidate.Tolerance);

            if (rhythm is null)
            {
                continue;
            }

            var regularIntervals = intervals.Count(interval => Math.Abs(interval - rhythm.Days) <= rhythm.Tolerance);

            if (regularIntervals < Math.Ceiling(intervals.Count * minRegularity))
            {
                continue;
            }

            // Amounts must be broadly stable (price changes are allowed, random spending is not).
            var amounts = charges.Select(charge => charge.Amount).ToList();
            var medianAmount = Median(amounts.Select(amount => (double)amount).ToList());
            var stableAmounts = amounts.Count(amount => Math.Abs((double)amount - medianAmount) <= medianAmount * 0.35);

            if (stableAmounts < Math.Ceiling(amounts.Count * 0.6))
            {
                continue;
            }

            var last = charges[^1];
            var previousDifferent = charges
                .Take(charges.Count - 1)
                .Reverse()
                .Select(charge => (decimal?)charge.Amount)
                .FirstOrDefault(amount => amount != last.Amount);

            var changePercent = previousDifferent is > 0m
                ? Math.Round((last.Amount - previousDifferent.Value) / previousDifferent.Value * 100m, 1)
                : 0m;

            var monthlyCost = Math.Round(last.Amount * (decimal)(AverageDaysPerMonth / rhythm.Days), 2);

            result.Add(new DetectedSubscription(
                Name: DisplayName(last),
                CategoryName: last.CategoryName,
                Frequency: rhythm.Name,
                IntervalDays: (int)Math.Round(median),
                Occurrences: charges.Count,
                CurrentAmount: last.Amount,
                PreviousAmount: previousDifferent,
                PriceChangePercent: changePercent,
                PriceIncreased: changePercent > 0,
                MonthlyCost: monthlyCost,
                YearlyCost: Math.Round(monthlyCost * 12, 2),
                LastChargeDate: last.Date,
                NextExpectedDate: last.Date.AddDays(Math.Round(median))));
        }

        return result
            .OrderByDescending(subscription => subscription.PriceIncreased)
            .ThenByDescending(subscription => subscription.MonthlyCost)
            .ToList();
    }

    private static string BuildKey(ExpensePoint expense)
    {
        var key = TextNormalizer.GroupingKey(expense.Description);

        // Without a description, fall back to "same category + roughly the same amount".
        return key.Length > 0
            ? key
            : $"{TextNormalizer.Fold(expense.CategoryName)}|{Math.Round(expense.Amount, 0)}";
    }

    private static string DisplayName(ExpensePoint expense)
    {
        if (string.IsNullOrWhiteSpace(expense.Description))
        {
            return expense.CategoryName;
        }

        var text = expense.Description.Trim();
        return char.ToUpper(text[0]) + text[1..];
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
