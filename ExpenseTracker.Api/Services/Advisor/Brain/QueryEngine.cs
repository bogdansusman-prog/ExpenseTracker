using System.Text;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>One transaction as the brain sees it: local date, plus the merchant guessed from the description.</summary>
public record BrainTransaction(DateOnly Date, decimal Amount, TransactionType Type, string Category, string? Description, string? Merchant);

/// <summary>Everything the brain can look at when answering.</summary>
public record BrainContext(IReadOnlyList<BrainTransaction> Transactions, DateOnly Today, decimal? HourlyRate, decimal Balance);

/// <summary>
/// Turns an understood question (intent + slots) into an answer computed from the real transactions.
/// </summary>
public static class QueryEngine
{
    public static string SpendTotal(BrainContext context, Slots slots, Texts t) =>
        Total(context, slots, slots.Type ?? TransactionType.Expense, t);

    public static string IncomeTotal(BrainContext context, Slots slots, Texts t) =>
        Total(context, slots, TransactionType.Income, t);

    public static string Count(BrainContext context, Slots slots, Texts t)
    {
        var period = slots.Period ?? Period.LastDays(context.Today, 90);
        var items = Filter(context, slots, period).Where(x => slots.Type is null || x.Type == slots.Type).ToList();
        var what = Describe(slots, t);

        if (items.Count == 0)
        {
            return t.T($"Nu am gasit nicio tranzactie{what} {period.Label(false)}.", $"I found no transactions{what} {period.Label(true)}.");
        }

        var total = items.Sum(x => x.Amount);
        var average = total / items.Count;
        return t.T(
            $"{items.Count} {(items.Count == 1 ? "tranzactie" : "tranzactii")}{what} {period.Label(false)}, in total {t.Money(total)} (in medie {t.Money(average)} de fiecare data).",
            $"{items.Count} {(items.Count == 1 ? "transaction" : "transactions")}{what} {period.Label(true)}, {t.Money(total)} in total ({t.Money(average)} on average each time).");
    }

    public static string TopCategories(BrainContext context, Slots slots, Texts t)
    {
        var period = slots.Period ?? Period.ThisMonth(context.Today);
        var expenses = Filter(context, slots with { Category = null }, period).Where(x => x.Type == TransactionType.Expense).ToList();

        if (expenses.Count == 0)
        {
            return t.T($"Nu ai cheltuieli {period.Label(false)}.", $"You have no expenses {period.Label(true)}.");
        }

        var total = expenses.Sum(x => x.Amount);
        var builder = new StringBuilder(t.T($"Unde s-au dus banii {period.Label(false)} ({t.Money(total)}):\n", $"Where your money went {period.Label(true)} ({t.Money(total)}):\n"));

        foreach (var group in expenses.GroupBy(x => x.Category).OrderByDescending(g => g.Sum(x => x.Amount)).Take(slots.TopN ?? 5))
        {
            var sum = group.Sum(x => x.Amount);
            builder.AppendLine($"• {group.Key}: {t.Money(sum)} ({t.Percent((double)(sum / total * 100))})");
        }

        return builder.ToString().TrimEnd();
    }

    public static string TopMerchants(BrainContext context, Slots slots, Texts t)
    {
        var period = slots.Period ?? Period.ThisMonth(context.Today);
        var expenses = Filter(context, slots with { Merchant = null }, period)
            .Where(x => x.Type == TransactionType.Expense && x.Merchant is not null)
            .ToList();

        if (expenses.Count == 0)
        {
            return t.T($"Nu am gasit cumparaturi {period.Label(false)}.", $"I found no purchases {period.Label(true)}.");
        }

        var builder = new StringBuilder(t.T($"Unde ai cumparat cel mai mult {period.Label(false)}:\n", $"Where you spent the most {period.Label(true)}:\n"));

        foreach (var group in expenses.GroupBy(x => x.Merchant!).OrderByDescending(g => g.Sum(x => x.Amount)).Take(slots.TopN ?? 5))
        {
            builder.AppendLine(t.T(
                $"• {Display(group.Key)}: {t.Money(group.Sum(x => x.Amount))} ({group.Count()} {(group.Count() == 1 ? "data" : "ori")})",
                $"• {Display(group.Key)}: {t.Money(group.Sum(x => x.Amount))} ({group.Count()} {(group.Count() == 1 ? "time" : "times")})"));
        }

        return builder.ToString().TrimEnd();
    }

    public static string Biggest(BrainContext context, Slots slots, Texts t)
    {
        var period = slots.Period ?? Period.ThisMonth(context.Today);
        var type = slots.Type ?? TransactionType.Expense;
        var items = Filter(context, slots, period).Where(x => x.Type == type)
            .OrderByDescending(x => x.Amount).Take(slots.TopN ?? 5).ToList();

        if (items.Count == 0)
        {
            return t.T($"Nu am gasit nimic{Describe(slots, t)} {period.Label(false)}.", $"I found nothing{Describe(slots, t)} {period.Label(true)}.");
        }

        var builder = new StringBuilder(type == TransactionType.Income
            ? t.T($"Cele mai mari incasari {period.Label(false)}:\n", $"Biggest income {period.Label(true)}:\n")
            : t.T($"Cele mai mari cheltuieli{Describe(slots, t)} {period.Label(false)}:\n", $"Biggest expenses{Describe(slots, t)} {period.Label(true)}:\n"));

        foreach (var item in items)
        {
            builder.AppendLine($"• {item.Date:dd.MM} {Name(item)}: {t.Money(item.Amount)}{Hours(context, item.Amount, t)}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string Compare(BrainContext context, Slots slots, Texts t)
    {
        var period = slots.Period ?? Period.ThisMonth(context.Today);
        var previous = period.Previous();
        var type = slots.Type ?? TransactionType.Expense;

        var now = Filter(context, slots, period).Where(x => x.Type == type).ToList();
        var before = Filter(context, slots, previous).Where(x => x.Type == type).ToList();
        var nowTotal = now.Sum(x => x.Amount);
        var beforeTotal = before.Sum(x => x.Amount);
        var what = type == TransactionType.Income ? t.T("Venituri", "Income") : t.T("Cheltuieli", "Expenses");

        var builder = new StringBuilder(t.T(
            $"{what}{Describe(slots, t)}: {t.Money(nowTotal)} {period.Label(false)} fata de {t.Money(beforeTotal)} {previous.Label(false)}",
            $"{what}{Describe(slots, t)}: {t.Money(nowTotal)} {period.Label(true)} vs {t.Money(beforeTotal)} {previous.Label(true)}"));

        if (beforeTotal > 0)
        {
            var change = (double)((nowTotal - beforeTotal) / beforeTotal * 100);
            var good = type == TransactionType.Expense ? change < 0 : change > 0;
            builder.Append($" ({(change >= 0 ? "▲" : "▼")} {t.Percent(Math.Abs(change))}). ");
            builder.Append(Math.Abs(change) < 5
                ? t.T("Practic la fel.", "Pretty much the same.")
                : good ? t.T("Bravo, merge in directia buna!", "Nice, that's the right direction!") : t.T("Atentie, aici ai crescut.", "Careful, this went up."));
        }
        else
        {
            builder.Append('.');
        }

        // Which categories moved the most (only when no single category was asked for).
        if (slots.Category is null && type == TransactionType.Expense)
        {
            var movers = now.Concat(before)
                .Select(x => x.Category).Distinct()
                .Select(category => (Category: category,
                    Now: now.Where(x => x.Category == category).Sum(x => x.Amount),
                    Before: before.Where(x => x.Category == category).Sum(x => x.Amount)))
                .OrderByDescending(x => Math.Abs(x.Now - x.Before))
                .Take(3)
                .Where(x => x.Now != x.Before)
                .ToList();

            if (movers.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(t.T("Ce s-a schimbat cel mai mult:", "What changed the most:"));
                foreach (var mover in movers)
                {
                    var sign = mover.Now >= mover.Before ? "+" : "−";
                    builder.AppendLine($"• {mover.Category}: {sign}{t.Money(Math.Abs(mover.Now - mover.Before))} ({t.Money(mover.Before)} → {t.Money(mover.Now)})");
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string Average(BrainContext context, Slots slots, Texts t)
    {
        var period = slots.Period ?? Period.LastDays(context.Today, 90);
        var type = slots.Type ?? TransactionType.Expense;
        var items = Filter(context, slots, period).Where(x => x.Type == type).ToList();

        if (items.Count == 0)
        {
            return t.T($"Nu am date{Describe(slots, t)} {period.Label(false)}.", $"I have no data{Describe(slots, t)} {period.Label(true)}.");
        }

        // Count from the first transaction we know, so a new account doesn't look artificially cheap.
        var firstKnown = context.Transactions.Min(x => x.Date);
        var start = period.From < firstKnown ? firstKnown : period.From;
        var days = Math.Max(1, period.To.DayNumber - start.DayNumber + 1);
        var total = items.Sum(x => x.Amount);
        var perDay = total / days;

        return t.T(
            $"{Describe(slots, t, capitalize: true, fallbackRo: "Per total", fallbackEn: "Overall")} {period.Label(false)}: in medie {t.Money(perDay)} pe zi, adica cam {t.Money(perDay * 30)} pe luna ({items.Count} tranzactii in {days} zile).",
            $"{Describe(slots, t, capitalize: true, fallbackRo: "Per total", fallbackEn: "Overall")} {period.Label(true)}: on average {t.Money(perDay)} a day, about {t.Money(perDay * 30)} a month ({items.Count} transactions in {days} days).");
    }

    public static string LastTime(BrainContext context, Slots slots, Texts t)
    {
        var all = new Period(DateOnly.MinValue, context.Today, "", "");
        var item = Filter(context, slots, slots.Period ?? all)
            .Where(x => slots.Type is null || x.Type == slots.Type)
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();

        if (item is null)
        {
            return t.T($"Nu am gasit nicio tranzactie{Describe(slots, t)}.", $"I found no transactions{Describe(slots, t)}.");
        }

        var ago = context.Today.DayNumber - item.Date.DayNumber;
        var when = ago switch
        {
            0 => t.T("azi", "today"),
            1 => t.T("ieri", "yesterday"),
            _ => t.T($"acum {ago} zile", $"{ago} days ago")
        };

        var sign = item.Type == TransactionType.Income ? "+" : "−";
        return t.T(
            $"Ultima data{Describe(slots, t)}: {item.Date:dd.MM.yyyy} ({when}), {Name(item)}, {sign}{t.Money(item.Amount)}.",
            $"Last time{Describe(slots, t)}: {item.Date:MMM dd, yyyy} ({when}), {Name(item)}, {sign}{t.Money(item.Amount)}.");
    }

    public static string Balance(BrainContext context, Texts t)
    {
        var month = Period.ThisMonth(context.Today);
        var monthItems = context.Transactions.Where(x => x.Date >= month.From && x.Date <= month.To).ToList();
        var net = monthItems.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount)
                  - monthItems.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);

        return t.T(
            $"Soldul tau in aplicatie este {t.Money(context.Balance)}. Luna asta esti pe {(net >= 0 ? "plus" : "minus")} cu {t.Money(Math.Abs(net))}.",
            $"Your balance in the app is {t.Money(context.Balance)}. This month you are {(net >= 0 ? "up" : "down")} {t.Money(Math.Abs(net))}.");
    }

    // ---------- helpers ----------

    private static string Total(BrainContext context, Slots slots, TransactionType type, Texts t)
    {
        var period = slots.Period ?? Period.ThisMonth(context.Today);
        var items = Filter(context, slots, period).Where(x => x.Type == type).ToList();
        var what = Describe(slots, t);
        var verbRo = type == TransactionType.Income ? "ai incasat" : "ai cheltuit";
        var verbEn = type == TransactionType.Income ? "you received" : "you spent";

        if (items.Count == 0)
        {
            var ever = context.Transactions.Where(x => Matches(x, slots) && x.Type == type).OrderByDescending(x => x.Date).FirstOrDefault();
            var hint = ever is null
                ? ""
                : t.T($" Ultima data a fost pe {ever.Date:dd.MM.yyyy} ({t.Money(ever.Amount)}).", $" The last one was on {ever.Date:MMM dd, yyyy} ({t.Money(ever.Amount)}).");
            return t.T($"{Capitalize(period.Label(false))} nu {verbRo} nimic{what}.{hint}", $"{Capitalize(period.Label(true))} {verbEn} nothing{what}.{hint}");
        }

        var total = items.Sum(x => x.Amount);
        var builder = new StringBuilder(t.T(
            $"{Capitalize(period.Label(false))} {verbRo} {t.Money(total)}{what} ({items.Count} {(items.Count == 1 ? "tranzactie" : "tranzactii")}).",
            $"{Capitalize(period.Label(true))} {verbEn} {t.Money(total)}{what} ({items.Count} {(items.Count == 1 ? "transaction" : "transactions")})."));

        if (type == TransactionType.Expense)
        {
            builder.Append(Hours(context, total, t, sentence: true));
        }

        var previous = period.Previous();
        var before = Filter(context, slots, previous).Where(x => x.Type == type).Sum(x => x.Amount);
        if (before > 0)
        {
            var change = (double)((total - before) / before * 100);
            builder.Append(Math.Abs(change) < 5
                ? t.T($" Cam la fel ca {previous.Label(false)}.", $" About the same as {previous.Label(true)}.")
                : t.T($" {(change > 0 ? "Cu" : "Cu")} {t.Percent(Math.Abs(change))} {(change > 0 ? "mai mult" : "mai putin")} decat {previous.Label(false)} ({t.Money(before)}).",
                    $" {t.Percent(Math.Abs(change))} {(change > 0 ? "more" : "less")} than {previous.Label(true)} ({t.Money(before)})."));
        }

        return builder.ToString();
    }

    private static IEnumerable<BrainTransaction> Filter(BrainContext context, Slots slots, Period period) =>
        context.Transactions.Where(x => x.Date >= period.From && x.Date <= period.To && Matches(x, slots));

    private static bool Matches(BrainTransaction x, Slots slots) =>
        (slots.Category is null || x.Category == slots.Category)
        && (slots.Merchant is null || x.Merchant == slots.Merchant);

    /// <summary>" pe Mancare la Lidl" — the filters in words, to repeat back what Ax understood.</summary>
    private static string Describe(Slots slots, Texts t, bool capitalize = false, string fallbackRo = "", string fallbackEn = "")
    {
        var parts = new List<string>();
        if (slots.Category is not null) parts.Add(t.T($"pe {slots.Category}", $"on {slots.Category}"));
        if (slots.Merchant is not null) parts.Add(t.T($"la {Display(slots.Merchant)}", $"at {Display(slots.Merchant)}"));

        if (parts.Count == 0)
        {
            return capitalize ? t.T(fallbackRo, fallbackEn) : "";
        }

        var text = string.Join(' ', parts);
        return capitalize ? Capitalize(text) : " " + text;
    }

    private static string Hours(BrainContext context, decimal amount, Texts t, bool sentence = false)
    {
        if (context.HourlyRate is not > 0m)
        {
            return "";
        }

        var hours = t.Number((double)Math.Round(amount / context.HourlyRate.Value, 1));
        return sentence
            ? t.T($" Asta inseamna {hours} ore de munca.", $" That's {hours} hours of work.")
            : t.T($" = {hours} ore", $" = {hours} h");
    }

    private static string Name(BrainTransaction item) =>
        string.IsNullOrWhiteSpace(item.Description) ? item.Category : $"{item.Description} ({item.Category})";

    public static string Display(string merchant) =>
        merchant.Length == 0 ? merchant : char.ToUpperInvariant(merchant[0]) + merchant[1..];

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
