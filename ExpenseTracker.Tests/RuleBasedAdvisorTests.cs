using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Services.Advisor;
using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Tests;

public class RuleBasedAdvisorTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0);

    private static FinancialSnapshot Snapshot(
        decimal incomePerMonth,
        decimal expensesPerMonth,
        decimal thisMonthExpenses,
        double probabilityNegative = 0,
        decimal balance = 10000m,
        IReadOnlyList<DetectedSubscription>? subscriptions = null,
        RegretSummary? regret = null,
        IReadOnlyList<CategoryTrend>? trends = null,
        decimal? hourlyRate = null,
        int transactionCount = 50)
    {
        MonthTotals Month(string name, decimal income, decimal expenses) => new(
            name, income, expenses, income - expenses,
            income > 0 ? (double)((income - expenses) / income) * 100 : 0);

        return new FinancialSnapshot(
            GeneratedAtLocal: Now,
            Currency: "lei",
            CurrentBalance: balance,
            HourlyRate: hourlyRate,
            ThisMonth: Month("2026-10", incomePerMonth, thisMonthExpenses),
            PreviousMonths:
            [
                Month("2026-09", incomePerMonth, expensesPerMonth),
                Month("2026-08", incomePerMonth, expensesPerMonth),
                Month("2026-07", incomePerMonth, expensesPerMonth)
            ],
            CategoryTrends: trends ?? [],
            BiggestExpensesThisMonth: [],
            ThisMonthExpensesInWorkHours: hourlyRate is > 0m ? Math.Round(thisMonthExpenses / hourlyRate.Value, 1) : null,
            Regret: regret ?? new RegretSummary(0, 0, 0, 0, null, null, [], []),
            Subscriptions: subscriptions ?? [],
            Forecast: new BalanceForecast(balance, DateOnly.FromDateTime(Now), DateOnly.FromDateTime(Now).AddDays(30),
                2000, 90, balance - 500, balance, balance + 500, probabilityNegative, []),
            TransactionCount: transactionCount);
    }

    [Fact]
    public void Healthy_finances_get_a_good_verdict()
    {
        // saves 40%, pace on track (2/3 of month, 2/3 of usual spending), big buffer, no risk
        var report = RuleBasedAdvisor.Generate(Snapshot(5000m, 3000m, 2000m), "ro");

        Assert.Equal("good", report.Verdict);
        Assert.True(report.Score >= 70, $"score was {report.Score}");
        Assert.Equal(6, report.Breakdown.Count);
        Assert.Equal(report.Score, report.Breakdown.Sum(component => component.Points));
        Assert.Equal(3, report.Actions.Count);
        Assert.NotEmpty(report.Strengths);
    }

    [Fact]
    public void Overspending_with_high_risk_gets_a_bad_verdict()
    {
        var report = RuleBasedAdvisor.Generate(
            Snapshot(3000m, 3600m, 4000m, probabilityNegative: 0.6, balance: 200m), "ro");

        Assert.Equal("bad", report.Verdict);
        Assert.True(report.Score < 45, $"score was {report.Score}");
        Assert.Contains(report.Concerns, concern => concern.Contains("mai mult decat castigi"));
        Assert.Contains(report.Actions, action => action.Title.Contains("fond de siguranta") || action.Title.Contains("10%"));
    }

    [Fact]
    public void Category_spike_becomes_the_top_action_with_estimated_savings()
    {
        // Day 20 of 31: 1000 lei on food so far → ~1550 projected vs 600 average
        var trends = new[] { new CategoryTrend("Mancare", 1000m, 600m, 66.7) };
        var report = RuleBasedAdvisor.Generate(Snapshot(5000m, 3000m, 2000m, trends: trends), "ro");

        var top = report.Actions[0];
        Assert.Contains("Mancare", top.Title);
        Assert.Equal(950m, top.EstimatedMonthlySavings);
    }

    [Fact]
    public void Price_increase_is_reported()
    {
        var netflix = new DetectedSubscription("Netflix", "Divertisment", "monthly", 30, 4, 54.99m, 49.99m, 10.0m, true,
            55.8m, 669.6m, Now.AddDays(-10), Now.AddDays(20));

        var report = RuleBasedAdvisor.Generate(Snapshot(5000m, 3000m, 2000m, subscriptions: [netflix]), "en");

        Assert.Contains(report.Concerns, concern => concern.Contains("Netflix") && concern.Contains("10%"));
        Assert.Equal("en", report.Language);
    }

    [Fact]
    public void Too_little_data_returns_neutral_report()
    {
        var report = RuleBasedAdvisor.Generate(Snapshot(0m, 0m, 50m, transactionCount: 2), "ro");

        Assert.Equal(50, report.Score);
        Assert.Equal("ok", report.Verdict);
        Assert.Empty(report.Breakdown);
    }

    [Theory]
    [InlineData("Unde pot sa economisesc?", "economisi")]
    [InlineData("Ce abonamente am?", "plati recurente")]
    [InlineData("Cati bani o sa am la sfarsitul lunii?", "simulari")]
    [InlineData("Cum stau?", "Scorul tau")]
    [InlineData("salut", "Pot sa-ti raspund")]
    public void Chat_detects_intent(string question, string expectedFragment)
    {
        var netflix = new DetectedSubscription("Netflix", "Divertisment", "monthly", 30, 4, 50m, null, 0m, false,
            50.7m, 608.4m, Now.AddDays(-10), Now.AddDays(20));
        var snapshot = Snapshot(5000m, 3000m, 2000m, subscriptions: [netflix]);
        var report = RuleBasedAdvisor.Generate(snapshot, "ro");

        var answer = AdvisorChatEngine.Answer(question, snapshot, report, "ro");

        Assert.Contains(expectedFragment, answer);
    }

    [Fact]
    public void Chat_answers_about_a_named_category()
    {
        var trends = new[] { new CategoryTrend("Mâncare", 800m, 600m, 33.3) };
        var snapshot = Snapshot(5000m, 3000m, 2000m, trends: trends);
        var report = RuleBasedAdvisor.Generate(snapshot, "ro");

        var answer = AdvisorChatEngine.Answer("Cat am cheltuit pe mancare?", snapshot, report, "ro");

        Assert.StartsWith("Mâncare:", answer);
    }
}
