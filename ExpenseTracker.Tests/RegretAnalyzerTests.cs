using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Tests;

public class RegretAnalyzerTests
{
    [Fact]
    public void Finds_worst_category_and_regretted_amount()
    {
        var now = new DateTime(2026, 10, 20);
        var rated = new List<RatedExpense>
        {
            new(new DateTime(2026, 10, 2, 23, 0, 0), 120m, 1, "Mâncare"),   // Friday
            new(new DateTime(2026, 10, 9, 23, 0, 0), 90m, 2, "Mâncare"),    // Friday
            new(new DateTime(2026, 10, 5), 300m, 5, "Educație"),
            new(new DateTime(2026, 10, 6), 250m, 4, "Educație"),
            new(new DateTime(2026, 9, 4), 80m, 1, "Mâncare")
        };

        var summary = RegretAnalyzer.Analyze(rated, now);

        Assert.Equal(5, summary.RatedCount);
        Assert.Equal("Mâncare", summary.WorstCategory);
        Assert.Equal((int)DayOfWeek.Friday, summary.WorstDayOfWeek);
        Assert.Equal(210m, summary.RegrettedAmountThisMonth);
        Assert.Equal(290m, summary.RegrettedAmountTotal);
    }

    [Fact]
    public void Empty_input_returns_empty_summary()
    {
        var summary = RegretAnalyzer.Analyze([], DateTime.Now);

        Assert.Equal(0, summary.RatedCount);
        Assert.Null(summary.WorstCategory);
    }
}
