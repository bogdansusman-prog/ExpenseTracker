using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Analytics;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public record PendingRegret(
    int TransactionId,
    DateTime Date,
    decimal Amount,
    string CategoryName,
    string? Description,
    int DaysAgo,
    decimal? WorkHours);

public record MonthTotals(string Month, decimal Income, decimal Expenses, decimal Net, double SavingsRate);

public record CategoryTrend(string CategoryName, decimal ThisMonth, decimal AverageLast3Months, double ChangePercent);

public record TopExpense(DateTime Date, decimal Amount, string CategoryName, string? Description);

/// <summary>Everything the AI advisor (and the Insights page) needs, computed from the database.</summary>
public record FinancialSnapshot(
    DateTime GeneratedAtLocal,
    string Currency,
    decimal CurrentBalance,
    decimal? HourlyRate,
    MonthTotals ThisMonth,
    IReadOnlyList<MonthTotals> PreviousMonths,
    IReadOnlyList<CategoryTrend> CategoryTrends,
    IReadOnlyList<TopExpense> BiggestExpensesThisMonth,
    decimal? ThisMonthExpensesInWorkHours,
    RegretSummary Regret,
    IReadOnlyList<DetectedSubscription> Subscriptions,
    BalanceForecast Forecast,
    int TransactionCount);

/// <summary>
/// Database side of the "smart" features: loads transactions once and feeds
/// the pure algorithms in <see cref="Analytics"/>.
/// </summary>
public class InsightsService(AppDbContext context, AppClock clock)
{
    public const int RegretDelayDays = 7;
    public const int RegretWindowDays = 45;

    public async Task<UserSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await context.Settings.FindAsync([UserSettings.SingletonId], cancellationToken);

        if (settings is null)
        {
            settings = new UserSettings { Id = UserSettings.SingletonId, Language = "ro" };
            context.Settings.Add(settings);
            await context.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    public async Task<IReadOnlyList<PendingRegret>> GetPendingRegretsAsync(int take = 10, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var latest = DateTime.UtcNow.AddDays(-RegretDelayDays);
        var earliest = DateTime.UtcNow.AddDays(-RegretWindowDays);

        var expenses = await context.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Type == TransactionType.Expense
                && transaction.RegretScore == null
                && transaction.Date <= latest
                && transaction.Date >= earliest)
            .OrderByDescending(transaction => transaction.Amount)
            .Take(take)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.Date,
                transaction.Amount,
                CategoryName = transaction.Category.Name,
                transaction.Description
            })
            .ToListAsync(cancellationToken);

        return expenses
            .Select(expense => new PendingRegret(
                expense.Id,
                expense.Date,
                expense.Amount,
                expense.CategoryName,
                expense.Description,
                (int)(DateTime.UtcNow - expense.Date).TotalDays,
                WorkHours(expense.Amount, settings.HourlyRate)))
            .ToList();
    }

    public async Task<bool> RateAsync(int transactionId, int score, CancellationToken cancellationToken = default)
    {
        var transaction = await context.Transactions.FindAsync([transactionId], cancellationToken);

        if (transaction is null || transaction.Type != TransactionType.Expense)
        {
            return false;
        }

        transaction.RegretScore = score;
        transaction.RegretRatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<RegretSummary> GetRegretSummaryAsync(CancellationToken cancellationToken = default)
    {
        var rated = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.RegretScore != null)
            .Select(transaction => new
            {
                transaction.Date,
                transaction.Amount,
                Score = transaction.RegretScore!.Value,
                CategoryName = transaction.Category.Name
            })
            .ToListAsync(cancellationToken);

        return RegretAnalyzer.Analyze(
            rated.Select(item => new RatedExpense(clock.ToLocal(item.Date), item.Amount, item.Score, item.CategoryName)).ToList(),
            clock.LocalNow);
    }

    public async Task<IReadOnlyList<DetectedSubscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddYears(-2);

        var expenses = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Type == TransactionType.Expense && transaction.Date >= since)
            .Select(transaction => new ExpensePoint(
                transaction.Id,
                transaction.Date,
                transaction.Amount,
                transaction.Description,
                transaction.Category.Name))
            .ToListAsync(cancellationToken);

        return SubscriptionDetector.Detect(expenses);
    }

    public async Task<BalanceForecast> GetForecastAsync(int horizonDays = 30, CancellationToken cancellationToken = default)
    {
        const int historyDays = 90;
        var since = DateTime.UtcNow.AddDays(-historyDays - 1);

        var balance = await GetCurrentBalanceAsync(cancellationToken);

        var flows = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Date >= since)
            .Select(transaction => new
            {
                transaction.Date,
                transaction.Amount,
                transaction.Type
            })
            .ToListAsync(cancellationToken);

        return BalanceForecaster.Forecast(
            flows.Select(flow => new CashFlow(
                clock.ToLocal(flow.Date),
                flow.Type == TransactionType.Income ? flow.Amount : -flow.Amount)),
            balance,
            clock.Today,
            horizonDays,
            historyDays: historyDays);
    }

    public async Task<FinancialSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var fourMonthsAgo = clock.StartOfLocalMonthUtc(3);

        var recent = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Date >= fourMonthsAgo)
            .Select(transaction => new
            {
                transaction.Date,
                transaction.Amount,
                transaction.Type,
                transaction.Description,
                CategoryName = transaction.Category.Name
            })
            .ToListAsync(cancellationToken);

        var local = recent
            .Select(item => new
            {
                LocalDate = clock.ToLocal(item.Date),
                item.Amount,
                item.Type,
                item.Description,
                item.CategoryName
            })
            .ToList();

        var now = clock.LocalNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);

        MonthTotals Totals(DateTime monthStart)
        {
            var monthEnd = monthStart.AddMonths(1);
            var items = local.Where(item => item.LocalDate >= monthStart && item.LocalDate < monthEnd).ToList();
            var income = items.Where(item => item.Type == TransactionType.Income).Sum(item => item.Amount);
            var expenses = items.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount);
            var rate = income > 0 ? Math.Round((double)((income - expenses) / income) * 100, 1) : 0;

            return new MonthTotals(monthStart.ToString("yyyy-MM"), income, expenses, income - expenses, rate);
        }

        var thisMonth = Totals(thisMonthStart);
        var previousMonths = Enumerable.Range(1, 3).Select(back => Totals(thisMonthStart.AddMonths(-back))).ToList();

        var thisMonthExpenses = local
            .Where(item => item.Type == TransactionType.Expense && item.LocalDate >= thisMonthStart)
            .ToList();

        var previousExpenses = local
            .Where(item => item.Type == TransactionType.Expense && item.LocalDate < thisMonthStart)
            .ToList();

        var categoryTrends = thisMonthExpenses.Select(item => item.CategoryName)
            .Union(previousExpenses.Select(item => item.CategoryName))
            .Select(category =>
            {
                var current = thisMonthExpenses.Where(item => item.CategoryName == category).Sum(item => item.Amount);
                var average = Math.Round(previousExpenses.Where(item => item.CategoryName == category).Sum(item => item.Amount) / 3m, 2);
                var change = average > 0 ? Math.Round((double)((current - average) / average) * 100, 1) : 0;
                return new CategoryTrend(category, current, average, change);
            })
            .OrderByDescending(trend => trend.ThisMonth)
            .ToList();

        var biggest = thisMonthExpenses
            .OrderByDescending(item => item.Amount)
            .Take(5)
            .Select(item => new TopExpense(item.LocalDate, item.Amount, item.CategoryName, item.Description))
            .ToList();

        return new FinancialSnapshot(
            GeneratedAtLocal: now,
            Currency: "lei",
            CurrentBalance: await GetCurrentBalanceAsync(cancellationToken),
            HourlyRate: settings.HourlyRate,
            ThisMonth: thisMonth,
            PreviousMonths: previousMonths,
            CategoryTrends: categoryTrends,
            BiggestExpensesThisMonth: biggest,
            ThisMonthExpensesInWorkHours: WorkHours(thisMonth.Expenses, settings.HourlyRate),
            Regret: await GetRegretSummaryAsync(cancellationToken),
            Subscriptions: await GetSubscriptionsAsync(cancellationToken),
            Forecast: await GetForecastAsync(30, cancellationToken),
            TransactionCount: await context.Transactions.CountAsync(cancellationToken));
    }

    public async Task<decimal> GetCurrentBalanceAsync(CancellationToken cancellationToken = default)
    {
        var income = await context.Transactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0;

        var expenses = await context.Transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0;

        return income - expenses;
    }

    public static decimal? WorkHours(decimal amount, decimal? hourlyRate) =>
        hourlyRate is > 0m ? Math.Round(amount / hourlyRate.Value, 1) : null;
}
