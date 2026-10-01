using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Tests;

public class SubscriptionDetectorTests
{
    private static ExpensePoint Expense(int id, string date, decimal amount, string? description, string category = "Divertisment") =>
        new(id, DateTime.Parse(date), amount, description, category);

    [Fact]
    public void Detects_monthly_subscription_and_flags_price_increase()
    {
        var expenses = new[]
        {
            Expense(1, "2026-01-05", 49.99m, "Netflix"),
            Expense(2, "2026-02-05", 49.99m, "NETFLIX 02/2026"),
            Expense(3, "2026-03-06", 49.99m, "netflix"),
            Expense(4, "2026-04-05", 54.99m, "Netflix"),
            Expense(5, "2026-03-12", 120m, "Pizza", "Mancare")
        };

        var result = SubscriptionDetector.Detect(expenses);

        var netflix = Assert.Single(result);
        Assert.Equal("monthly", netflix.Frequency);
        Assert.Equal(4, netflix.Occurrences);
        Assert.Equal(54.99m, netflix.CurrentAmount);
        Assert.Equal(49.99m, netflix.PreviousAmount);
        Assert.True(netflix.PriceIncreased);
        Assert.Equal(10.0m, netflix.PriceChangePercent);
        Assert.Equal(new DateTime(2026, 5, 5), netflix.NextExpectedDate.Date);
    }

    [Fact]
    public void Ignores_irregular_spending()
    {
        var expenses = new[]
        {
            Expense(1, "2026-01-02", 45m, "Pizza"),
            Expense(2, "2026-01-04", 60m, "Pizza"),
            Expense(3, "2026-02-20", 38m, "Pizza"),
            Expense(4, "2026-02-21", 52m, "Pizza")
        };

        Assert.Empty(SubscriptionDetector.Detect(expenses));
    }

    [Fact]
    public void Needs_at_least_three_charges()
    {
        var expenses = new[]
        {
            Expense(1, "2026-01-01", 30m, "Spotify"),
            Expense(2, "2026-02-01", 30m, "Spotify")
        };

        Assert.Empty(SubscriptionDetector.Detect(expenses));
    }

    [Fact]
    public void Groups_by_category_and_amount_when_description_is_missing()
    {
        var expenses = new[]
        {
            Expense(1, "2026-01-01", 1500m, null, "Chirie"),
            Expense(2, "2026-02-01", 1500m, null, "Chirie"),
            Expense(3, "2026-03-01", 1500m, "", "Chirie")
        };

        var rent = Assert.Single(SubscriptionDetector.Detect(expenses));
        Assert.Equal("Chirie", rent.Name);
        Assert.False(rent.PriceIncreased);
    }
}
