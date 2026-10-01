namespace ExpenseTracker.Api.Services.Analytics;

public record CashFlow(DateTime Date, decimal SignedAmount);

public record ForecastPoint(DateOnly Date, decimal Pessimistic, decimal Expected, decimal Optimistic);

public record BalanceForecast(
    decimal CurrentBalance,
    DateOnly From,
    DateOnly To,
    int Simulations,
    int HistoryDays,
    decimal EndPessimistic,
    decimal EndExpected,
    decimal EndOptimistic,
    double ProbabilityNegative,
    IReadOnlyList<ForecastPoint> Points);

/// <summary>
/// Monte Carlo balance forecast. The last <c>historyDays</c> days of real cash flow
/// (including days with no transactions) are resampled at random (bootstrap)
/// to simulate thousands of possible futures. The 10th / 50th / 90th percentiles give a
/// pessimistic / expected / optimistic band instead of a single misleading number.
/// </summary>
public static class BalanceForecaster
{
    public static BalanceForecast Forecast(
        IEnumerable<CashFlow> history,
        decimal currentBalance,
        DateOnly today,
        int horizonDays = 30,
        int simulations = 2000,
        int historyDays = 90,
        int? seed = null)
    {
        horizonDays = Math.Clamp(horizonDays, 1, 365);
        simulations = Math.Clamp(simulations, 100, 20000);
        historyDays = Math.Clamp(historyDays, 7, 730);

        var firstHistoryDay = today.AddDays(-historyDays + 1);
        var dailyNet = new double[historyDays];

        foreach (var flow in history)
        {
            var day = DateOnly.FromDateTime(flow.Date);

            if (day < firstHistoryDay || day > today)
            {
                continue;
            }

            dailyNet[day.DayNumber - firstHistoryDay.DayNumber] += (double)flow.SignedAmount;
        }

        var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        var start = (double)currentBalance;

        // balances[day][simulation]
        var balances = new double[horizonDays][];
        for (var day = 0; day < horizonDays; day++)
        {
            balances[day] = new double[simulations];
        }

        for (var simulation = 0; simulation < simulations; simulation++)
        {
            var running = start;

            for (var day = 0; day < horizonDays; day++)
            {
                running += dailyNet[random.Next(historyDays)];
                balances[day][simulation] = running;
            }
        }

        var points = new List<ForecastPoint>(horizonDays);

        for (var day = 0; day < horizonDays; day++)
        {
            Array.Sort(balances[day]);
            points.Add(new ForecastPoint(
                today.AddDays(day + 1),
                Round(Percentile(balances[day], 0.10)),
                Round(Percentile(balances[day], 0.50)),
                Round(Percentile(balances[day], 0.90))));
        }

        var end = balances[horizonDays - 1];
        var negative = end.Count(balance => balance < 0);

        return new BalanceForecast(
            CurrentBalance: currentBalance,
            From: today,
            To: today.AddDays(horizonDays),
            Simulations: simulations,
            HistoryDays: historyDays,
            EndPessimistic: points[^1].Pessimistic,
            EndExpected: points[^1].Expected,
            EndOptimistic: points[^1].Optimistic,
            ProbabilityNegative: Math.Round((double)negative / simulations, 3),
            Points: points);
    }

    /// <summary>Linear-interpolated percentile of an already sorted array.</summary>
    public static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var position = (sorted.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 2);
}
