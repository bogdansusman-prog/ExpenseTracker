using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Tests;

public class BalanceForecasterTests
{
    private static readonly DateOnly Today = new(2026, 6, 30);

    [Fact]
    public void Constant_daily_spending_gives_a_deterministic_forecast()
    {
        // -10 lei every day for the last 90 days
        var history = Enumerable.Range(0, 90)
            .Select(back => new CashFlow(Today.AddDays(-back).ToDateTime(new TimeOnly(12, 0)), -10m));

        var forecast = BalanceForecaster.Forecast(history, 1000m, Today, horizonDays: 30, seed: 42);

        Assert.Equal(30, forecast.Points.Count);
        Assert.Equal(700m, forecast.EndExpected);
        Assert.Equal(700m, forecast.EndPessimistic);
        Assert.Equal(700m, forecast.EndOptimistic);
        Assert.Equal(0.0, forecast.ProbabilityNegative);
    }

    [Fact]
    public void Band_is_ordered_and_widens_with_uncertain_history()
    {
        var random = new Random(1);
        var history = Enumerable.Range(0, 90)
            .Select(back => new CashFlow(
                Today.AddDays(-back).ToDateTime(TimeOnly.MinValue),
                random.Next(0, 3) == 0 ? -random.Next(50, 400) : 0));

        var forecast = BalanceForecaster.Forecast(history, 2000m, Today, 30, seed: 7);

        Assert.All(forecast.Points, point =>
        {
            Assert.True(point.Pessimistic <= point.Expected);
            Assert.True(point.Expected <= point.Optimistic);
        });

        var firstWidth = forecast.Points[0].Optimistic - forecast.Points[0].Pessimistic;
        var lastWidth = forecast.Points[^1].Optimistic - forecast.Points[^1].Pessimistic;
        Assert.True(lastWidth > firstWidth);
    }

    [Fact]
    public void Same_seed_gives_same_result()
    {
        var history = new[] { new CashFlow(Today.ToDateTime(TimeOnly.MinValue), -100m) };

        var first = BalanceForecaster.Forecast(history, 500m, Today, seed: 3);
        var second = BalanceForecaster.Forecast(history, 500m, Today, seed: 3);

        Assert.Equal(first.EndExpected, second.EndExpected);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.5, 3.0)]
    [InlineData(1.0, 5.0)]
    public void Percentile_interpolates(double percentile, double expected)
    {
        Assert.Equal(expected, BalanceForecaster.Percentile([1, 2, 3, 4, 5], percentile), 6);
    }
}
