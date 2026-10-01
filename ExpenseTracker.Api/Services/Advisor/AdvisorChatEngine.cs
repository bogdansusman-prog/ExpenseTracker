using System.Text;
using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Api.Services.Advisor;

/// <summary>
/// Rule-based chat: detects the intent of a question with keywords (RO + EN, with or without
/// diacritics) and answers with numbers computed from the user's own data.
/// </summary>
public static class AdvisorChatEngine
{
    private enum Intent
    {
        Subscriptions,
        WorkHours,
        Forecast,
        Regret,
        Savings,
        Category,
        Trend,
        Biggest,
        Overview,
        Help
    }

    private static readonly (Intent Intent, string[] Keywords)[] Rules =
    {
        (Intent.Subscriptions, new[] { "abonament", "subscri", "netflix", "spotify", "recurent", "recurring" }),
        (Intent.WorkHours, new[] { "ore", "orele", "hours", "munca", "work" }),
        (Intent.Forecast, new[] { "prognoz", "forecast", "sfarsit", "viitor", "future", "end of", "ramane", "predict", "vom avea", "voi avea" }),
        (Intent.Regret, new[] { "regret", "merit", "worth" }),
        (Intent.Savings, new[] { "economis", "save", "saving", "reduc", "taie", "cut", "unde pot", "where can" }),
        (Intent.Trend, new[] { "luna trecuta", "last month", "compar", "progres", "mai bine", "better", "worse", "mai rau", "trend" }),
        (Intent.Biggest, new[] { "cea mai mare", "cele mai mari", "biggest", "largest", "scump", "expensive" }),
        (Intent.Overview, new[] { "scor", "score", "cum stau", "how am i", "how are", "situati", "verdict", "rezumat", "summary" }),
        (Intent.Help, new[] { "ajutor", "help", "ce poti", "what can" })
    };

    public static string Answer(string question, FinancialSnapshot snapshot, AdvisorReport report, string language)
    {
        var t = new Texts(language);
        var folded = TextNormalizer.Fold(question);
        var tokens = folded.Split(new[] { ' ', ',', '.', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

        bool Matches(string keyword) => keyword.Contains(' ')
            ? folded.Contains(keyword)
            : tokens.Any(token => token.StartsWith(keyword));

        // A category named in the question ("cat am dat pe mancare?") is the most specific intent.
        var category = snapshot.CategoryTrends.FirstOrDefault(trend =>
        {
            var name = TextNormalizer.Fold(trend.CategoryName);
            return name.Length > 0 && (folded.Contains(name) || tokens.Any(token => token.Length >= 4 && name.StartsWith(token)));
        });

        var intent = Rules.FirstOrDefault(rule => rule.Keywords.Any(Matches)).Intent;
        var matchedAny = Rules.Any(rule => rule.Keywords.Any(Matches));

        if (category is not null && (!matchedAny || intent is Intent.Trend or Intent.Savings or Intent.Biggest))
        {
            return CategoryAnswer(category, snapshot, t);
        }

        if (!matchedAny)
        {
            intent = Intent.Help;
        }

        return intent switch
        {
            Intent.Subscriptions => SubscriptionsAnswer(snapshot, t),
            Intent.WorkHours => WorkHoursAnswer(snapshot, t),
            Intent.Forecast => ForecastAnswer(snapshot, t),
            Intent.Regret => RegretAnswer(snapshot, t),
            Intent.Savings => SavingsAnswer(report, t),
            Intent.Trend => TrendAnswer(snapshot, t),
            Intent.Biggest => BiggestAnswer(snapshot, t),
            Intent.Overview => OverviewAnswer(report, t),
            _ => HelpAnswer(t)
        };
    }

    private static string OverviewAnswer(AdvisorReport report, Texts t)
    {
        var builder = new StringBuilder();
        builder.AppendLine(t.T($"Scorul tau financiar este {report.Score}/100. {report.Headline}",
            $"Your financial score is {report.Score}/100. {report.Headline}"));

        foreach (var component in report.Breakdown)
        {
            builder.AppendLine($"• {component.Label}: {component.Points}/{component.MaxPoints} ({component.Explanation})");
        }

        return builder.ToString().TrimEnd();
    }

    private static string SavingsAnswer(AdvisorReport report, Texts t)
    {
        var builder = new StringBuilder(t.T("Iata unde poti economisi, in ordinea impactului:\n", "Here is where you can save, by impact:\n"));

        foreach (var action in report.Actions)
        {
            var saving = action.EstimatedMonthlySavings is { } value
                ? t.T($" (~{t.Money(value)} / luna)", $" (~{t.Money(value)} / month)")
                : "";
            builder.AppendLine($"• {action.Title}{saving}: {action.Detail}");
        }

        var total = report.Actions.Sum(action => action.EstimatedMonthlySavings ?? 0);
        if (total > 0)
        {
            builder.AppendLine(t.T($"In total, aproximativ {t.Money(total)} pe luna, adica {t.Money(total * 12)} pe an.",
                $"In total, roughly {t.Money(total)} per month, or {t.Money(total * 12)} per year."));
        }

        return builder.ToString().TrimEnd();
    }

    private static string SubscriptionsAnswer(FinancialSnapshot snapshot, Texts t)
    {
        var subscriptions = snapshot.Subscriptions;

        if (subscriptions.Count == 0)
        {
            return t.T("Nu am detectat plati recurente. Am nevoie de cel putin 3 plati asemanatoare, la intervale regulate.",
                "I haven't detected any recurring payments. I need at least 3 similar payments at regular intervals.");
        }

        var monthly = subscriptions.Sum(subscription => subscription.MonthlyCost);
        var builder = new StringBuilder(t.T(
            $"Am gasit {subscriptions.Count} plati recurente: {t.Money(monthly)} pe luna, adica {t.Money(monthly * 12)} pe an.\n",
            $"I found {subscriptions.Count} recurring payments: {t.Money(monthly)} per month, {t.Money(monthly * 12)} per year.\n"));

        foreach (var subscription in subscriptions.Take(6))
        {
            var increase = subscription.PriceIncreased
                ? t.T($" — scumpit cu {t.Percent((double)subscription.PriceChangePercent)}!", $" — up {t.Percent((double)subscription.PriceChangePercent)}!")
                : "";
            builder.AppendLine($"• {subscription.Name}: {t.Money(subscription.CurrentAmount)}{increase}");
        }

        var candidate = subscriptions.OrderByDescending(subscription => subscription.MonthlyCost).First();
        builder.Append(t.T($"Daca ar fi sa renunti la unul, incepe cu {candidate.Name}: economisesti {t.Money(candidate.YearlyCost)} pe an.",
            $"If you had to drop one, start with {candidate.Name}: that saves {t.Money(candidate.YearlyCost)} per year."));

        return builder.ToString();
    }

    private static string WorkHoursAnswer(FinancialSnapshot snapshot, Texts t)
    {
        if (snapshot.HourlyRate is not > 0m)
        {
            return t.T("Seteaza mai intai venitul pe ora in Setari, apoi iti pot spune cate ore de munca te costa fiecare cheltuiala.",
                "First set your hourly rate in Settings, then I can tell you how many work hours each expense costs.");
        }

        var rate = snapshot.HourlyRate.Value;
        var builder = new StringBuilder(t.T(
            $"Luna asta ai cheltuit echivalentul a {t.Number((double)Math.Round(snapshot.ThisMonth.Expenses / rate, 1))} ore de munca.\n",
            $"This month you spent the equivalent of {t.Number((double)Math.Round(snapshot.ThisMonth.Expenses / rate, 1))} hours of work.\n"));

        foreach (var trend in snapshot.CategoryTrends.Where(trend => trend.ThisMonth > 0).Take(5))
        {
            builder.AppendLine(t.T($"• {trend.CategoryName}: {t.Number((double)Math.Round(trend.ThisMonth / rate, 1))} ore",
                $"• {trend.CategoryName}: {t.Number((double)Math.Round(trend.ThisMonth / rate, 1))} hours"));
        }

        return builder.ToString().TrimEnd();
    }

    private static string ForecastAnswer(FinancialSnapshot snapshot, Texts t)
    {
        var forecast = snapshot.Forecast;
        var risk = forecast.ProbabilityNegative * 100;

        var advice = risk > 20
            ? t.T("Riscul e mare: amana cheltuielile care nu sunt urgente.", "The risk is high: postpone non-urgent spending.")
            : risk > 5
                ? t.T("Riscul e moderat: fii atent la cheltuielile mari.", "The risk is moderate: watch out for big expenses.")
                : t.T("Riscul e mic, esti in siguranta.", "The risk is low, you're safe.");

        return t.T(
            $"Din {forecast.Simulations} simulari pe baza ultimelor {forecast.HistoryDays} zile: pe {forecast.To:dd.MM} vei avea cel mai probabil {t.Money(forecast.EndExpected)} " +
            $"(pesimist {t.Money(forecast.EndPessimistic)}, optimist {t.Money(forecast.EndOptimistic)}). Sansa de sold negativ: {t.Percent(risk)}. {advice}",
            $"From {forecast.Simulations} simulations based on the last {forecast.HistoryDays} days: on {forecast.To:MMM dd} you will most likely have {t.Money(forecast.EndExpected)} " +
            $"(pessimistic {t.Money(forecast.EndPessimistic)}, optimistic {t.Money(forecast.EndOptimistic)}). Chance of a negative balance: {t.Percent(risk)}. {advice}");
    }

    private static string RegretAnswer(FinancialSnapshot snapshot, Texts t)
    {
        var regret = snapshot.Regret;

        if (regret.RatedCount == 0)
        {
            return t.T("Inca nu ai evaluat nicio cheltuiala. Intra in Insights → „A meritat?” si noteaza cateva.",
                "You haven't rated any expenses yet. Go to Insights → \"Was it worth it?\" and rate a few.");
        }

        var builder = new StringBuilder(t.T(
            $"Ai evaluat {regret.RatedCount} cheltuieli, cu scor mediu {t.Number(regret.AverageScore)}/5. Luna asta ai regretat {t.Money(regret.RegrettedAmountThisMonth)}.\n",
            $"You rated {regret.RatedCount} expenses, average {t.Number(regret.AverageScore)}/5. This month you regretted {t.Money(regret.RegrettedAmountThisMonth)}.\n"));

        foreach (var category in regret.ByCategory.Take(4))
        {
            builder.AppendLine($"• {category.CategoryName}: {t.Number(category.AverageScore)}/5 ({category.Count})");
        }

        if (regret.WorstCategory is not null)
        {
            builder.Append(t.T($"Cel mai des regreti la {regret.WorstCategory}.", $"You regret {regret.WorstCategory} purchases the most."));
        }

        return builder.ToString().TrimEnd();
    }

    private static string TrendAnswer(FinancialSnapshot snapshot, Texts t)
    {
        var last = snapshot.PreviousMonths.FirstOrDefault();

        if (last is null || (last.Income == 0 && last.Expenses == 0))
        {
            return t.T("Nu am date pentru luna trecuta, asa ca nu pot compara inca.", "I have no data for last month, so I can't compare yet.");
        }

        var builder = new StringBuilder(t.T(
            $"Luna trecuta ({last.Month}): venituri {t.Money(last.Income)}, cheltuieli {t.Money(last.Expenses)}, economisit {t.Percent(last.SavingsRate)}.\n" +
            $"Luna asta pana acum: venituri {t.Money(snapshot.ThisMonth.Income)}, cheltuieli {t.Money(snapshot.ThisMonth.Expenses)}.\n",
            $"Last month ({last.Month}): income {t.Money(last.Income)}, expenses {t.Money(last.Expenses)}, saved {t.Percent(last.SavingsRate)}.\n" +
            $"This month so far: income {t.Money(snapshot.ThisMonth.Income)}, expenses {t.Money(snapshot.ThisMonth.Expenses)}.\n"));

        var movers = snapshot.CategoryTrends
            .Where(trend => trend.AverageLast3Months > 0)
            .OrderByDescending(trend => Math.Abs(trend.ChangePercent))
            .Take(3);

        foreach (var trend in movers)
        {
            var direction = trend.ChangePercent >= 0 ? "▲" : "▼";
            builder.AppendLine(t.T(
                $"• {trend.CategoryName}: {t.Money(trend.ThisMonth)} vs media {t.Money(trend.AverageLast3Months)} ({direction} {t.Percent(Math.Abs(trend.ChangePercent))})",
                $"• {trend.CategoryName}: {t.Money(trend.ThisMonth)} vs avg {t.Money(trend.AverageLast3Months)} ({direction} {t.Percent(Math.Abs(trend.ChangePercent))})"));
        }

        return builder.ToString().TrimEnd();
    }

    private static string BiggestAnswer(FinancialSnapshot snapshot, Texts t)
    {
        if (snapshot.BiggestExpensesThisMonth.Count == 0)
        {
            return t.T("Nu ai cheltuieli luna asta.", "You have no expenses this month.");
        }

        var builder = new StringBuilder(t.T("Cele mai mari cheltuieli ale lunii:\n", "This month's biggest expenses:\n"));

        foreach (var expense in snapshot.BiggestExpensesThisMonth)
        {
            var name = string.IsNullOrWhiteSpace(expense.Description) ? expense.CategoryName : $"{expense.Description} ({expense.CategoryName})";
            var hours = snapshot.HourlyRate is > 0m
                ? t.T($" = {t.Number((double)Math.Round(expense.Amount / snapshot.HourlyRate.Value, 1))} ore de munca",
                    $" = {t.Number((double)Math.Round(expense.Amount / snapshot.HourlyRate.Value, 1))} work hours")
                : "";
            builder.AppendLine($"• {expense.Date:dd.MM} {name}: {t.Money(expense.Amount)}{hours}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string CategoryAnswer(CategoryTrend trend, FinancialSnapshot snapshot, Texts t)
    {
        var builder = new StringBuilder(t.T(
            $"{trend.CategoryName}: luna asta {t.Money(trend.ThisMonth)}, media ultimelor 3 luni {t.Money(trend.AverageLast3Months)}.",
            $"{trend.CategoryName}: {t.Money(trend.ThisMonth)} this month, 3-month average {t.Money(trend.AverageLast3Months)}."));

        if (trend.AverageLast3Months > 0)
        {
            builder.Append(trend.ChangePercent > 10
                ? t.T($" Esti cu {t.Percent(trend.ChangePercent)} peste medie.", $" That's {t.Percent(trend.ChangePercent)} above average.")
                : trend.ChangePercent < -10
                    ? t.T($" Bravo, esti cu {t.Percent(-trend.ChangePercent)} sub medie.", $" Nice, that's {t.Percent(-trend.ChangePercent)} below average.")
                    : t.T(" Esti in linie cu media.", " You're in line with your average."));
        }

        var regret = snapshot.Regret.ByCategory.FirstOrDefault(category => category.CategoryName == trend.CategoryName);
        if (regret is not null)
        {
            builder.Append(t.T($" Scorul „A meritat?” aici: {t.Number(regret.AverageScore)}/5.", $" \"Worth it\" score here: {t.Number(regret.AverageScore)}/5."));
        }

        if (snapshot.HourlyRate is > 0m && trend.ThisMonth > 0)
        {
            builder.Append(t.T($" Asta inseamna {t.Number((double)Math.Round(trend.ThisMonth / snapshot.HourlyRate.Value, 1))} ore de munca.",
                $" That's {t.Number((double)Math.Round(trend.ThisMonth / snapshot.HourlyRate.Value, 1))} hours of work."));
        }

        return builder.ToString();
    }

    private static string HelpAnswer(Texts t) => t.T(
        "Pot sa-ti raspund la intrebari despre banii tai, de exemplu:\n" +
        "• Cum stau? (scorul si cum e calculat)\n" +
        "• Unde pot sa economisesc?\n" +
        "• Ce abonamente am?\n" +
        "• Cati bani o sa am la sfarsitul lunii?\n" +
        "• Cum stau fata de luna trecuta?\n" +
        "• Care sunt cele mai mari cheltuieli?\n" +
        "• Cate ore de munca m-au costat cheltuielile?\n" +
        "• Cat am cheltuit pe <categorie>?",
        "I can answer questions about your money, for example:\n" +
        "• How am I doing? (the score and how it's computed)\n" +
        "• Where can I save?\n" +
        "• What subscriptions do I have?\n" +
        "• How much will I have at the end of the month?\n" +
        "• Am I doing better than last month?\n" +
        "• What are my biggest expenses?\n" +
        "• How many work hours did my spending cost?\n" +
        "• How much did I spend on <category>?");
}
