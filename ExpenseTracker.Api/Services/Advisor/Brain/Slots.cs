using System.Globalization;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>A date range from a question ("luna trecuta", "in septembrie", "ultimele 10 zile").</summary>
public sealed record Period(DateOnly From, DateOnly To, string LabelRo, string LabelEn, bool IsMonth = false)
{
    private static readonly CultureInfo Romanian = CultureInfo.GetCultureInfo("ro-RO");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public int Days => To.DayNumber - From.DayNumber + 1;

    public string Label(bool english) => english ? LabelEn : LabelRo;

    public static Period Today(DateOnly today) => new(today, today, "azi", "today");

    public static Period ThisMonth(DateOnly today) =>
        new(new DateOnly(today.Year, today.Month, 1), today, "luna asta", "this month", true);

    public static Period LastDays(DateOnly today, int days) =>
        new(today.AddDays(-(days - 1)), today, $"in ultimele {days} zile", $"in the last {days} days");

    public static Period Month(int year, int month, DateOnly today)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        if (last > today)
        {
            last = today;
        }

        var ro = first.ToString("MMMM yyyy", Romanian);
        var en = first.ToString("MMMM yyyy", English);
        return new Period(first, last, $"in {ro}", $"in {en}", true);
    }

    /// <summary>
    /// The period right before this one, with the same length. For a month that is still running
    /// (e.g. 1–12 October) it is the same days of the previous month, so the comparison is fair.
    /// </summary>
    public Period Previous()
    {
        if (IsMonth)
        {
            var first = From.AddMonths(-1);
            var fullLength = DateTime.DaysInMonth(first.Year, first.Month);
            var monthIsComplete = To.Day == DateTime.DaysInMonth(To.Year, To.Month);
            var last = first.AddDays((monthIsComplete ? fullLength : Math.Min(To.Day, fullLength)) - 1);

            return new Period(first, last,
                $"in {first.ToString("MMMM", Romanian)}" + (monthIsComplete ? "" : $" (1–{last.Day})"),
                $"in {first.ToString("MMMM", English)}" + (monthIsComplete ? "" : $" (1–{last.Day})"),
                true);
        }

        var to = From.AddDays(-1);
        var from = to.AddDays(-(Days - 1));
        return new Period(from, to, "in perioada dinainte", "in the period before");
    }
}

/// <summary>The details found in a question, next to its intent.</summary>
public sealed record Slots(Period? Period, string? Category, string? Merchant, TransactionType? Type, int? TopN)
{
    public static readonly Slots Empty = new(null, null, null, null, null);

    public bool IsEmpty => Period is null && Category is null && Merchant is null && Type is null && TopN is null;

    /// <summary>
    /// For follow-ups ("si luna trecuta?"): what this question says wins, the rest comes from the previous one.
    /// Naming a new category drops the old merchant and the other way round.
    /// </summary>
    public Slots MergeOnto(Slots previous) => new(
        Period ?? previous.Period,
        Category ?? (Merchant is null ? previous.Category : null),
        Merchant ?? (Category is null ? previous.Merchant : null),
        Type ?? previous.Type,
        TopN ?? previous.TopN);
}
