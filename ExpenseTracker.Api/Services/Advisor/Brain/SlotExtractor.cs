using System.Globalization;
using System.Text.RegularExpressions;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>
/// Finds the details of a question: the period, a category, a merchant, money in or out and "top N".
/// Categories and merchants come from the user's own data, matched with typo tolerance.
/// </summary>
public static partial class SlotExtractor
{
    private static readonly string[] MonthsRo =
        ["ianuarie", "februarie", "martie", "aprilie", "mai", "iunie", "iulie", "august", "septembrie", "octombrie", "noiembrie", "decembrie"];

    private static readonly string[] MonthsEn =
        ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];

    /// <summary>Words that are never a merchant, even if a shop description happens to start with them.</summary>
    private static readonly HashSet<string> NotMerchants =
    [
        "cat", "cati", "cate", "cand", "unde", "care", "pentru", "fost", "ultima", "data", "oara", "luna", "asta", "trecuta",
        "anul", "saptamana", "zile", "cheltuit", "cheltuieli", "platit", "dat", "primit", "castigat", "bani", "lei", "ron",
        "how", "much", "many", "did", "the", "and", "with", "this", "last", "month", "week", "year", "spend", "spent",
        "paid", "pay", "money", "plata", "transfer", "tranzactie", "tranzactii", "card", "pos", "apple", "comerciant",
        "cumparare", "incasare", "compara", "fata", "decat", "top", "cele", "mai", "mare", "mari", "multe", "multi"
    ];

    [GeneratedRegex(@"\b(?:ultimele|ultimii|ultima|last|past)\s+(\d{1,3})\s+(zile|zi|days?|saptamani|saptamana|weeks?|luni|months?)\b")]
    private static partial Regex LastN();

    [GeneratedRegex(@"\btop\s*(\d{1,2})\b|\b(\d{1,2})\s+(?:cele\s+mai|biggest|largest|most)\b")]
    private static partial Regex TopN();

    public static Slots Extract(string text, DateOnly today, IReadOnlyList<string> categories, IReadOnlyList<string> merchants)
    {
        var folded = TextNormalizer.Fold(text);
        var tokens = TextTools.Tokens(text);

        var category = FindCategory(tokens, categories);
        var merchant = FindMerchant(tokens, merchants, category);

        return new Slots(FindPeriod(folded, tokens, today), category, merchant, FindType(tokens), FindTopN(folded));
    }

    public static Period? FindPeriod(string folded, IReadOnlyList<string> tokens, DateOnly today)
    {
        bool Has(params string[] phrases) => phrases.Any(phrase => Regex.IsMatch(folded, $@"\b{Regex.Escape(phrase)}\b"));

        var lastN = LastN().Match(folded);
        if (lastN.Success)
        {
            var n = Math.Clamp(int.Parse(lastN.Groups[1].Value, CultureInfo.InvariantCulture), 1, 730);
            var unit = lastN.Groups[2].Value;
            var days = unit.StartsWith("sapt") || unit.StartsWith("week") ? n * 7
                : unit.StartsWith("lun") || unit.StartsWith("month") ? n * 30
                : n;
            return Period.LastDays(today, days);
        }

        if (Has("azi", "astazi", "today")) return Period.Today(today);
        if (Has("ieri", "yesterday")) return new Period(today.AddDays(-1), today.AddDays(-1), "ieri", "yesterday");

        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        if (Has("saptamana asta", "saptamana aceasta", "saptamana curenta", "this week"))
            return new Period(monday, today, "saptamana asta", "this week");
        if (Has("saptamana trecuta", "saptamana precedenta", "last week"))
            return new Period(monday.AddDays(-7), monday.AddDays(-1), "saptamana trecuta", "last week");

        if (Has("luna asta", "luna aceasta", "luna curenta", "this month")) return Period.ThisMonth(today);
        if (Has("luna trecuta", "luna precedenta", "last month"))
        {
            var previous = today.AddMonths(-1);
            var month = Period.Month(previous.Year, previous.Month, today);
            return month with { LabelRo = "luna trecuta", LabelEn = "last month" };
        }

        if (Has("anul asta", "anul acesta", "this year"))
            return new Period(new DateOnly(today.Year, 1, 1), today, "anul asta", "this year");
        if (Has("anul trecut", "last year"))
            return new Period(new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31), "anul trecut", "last year");

        // Month names, with typo tolerance ("septembire"). "mai" (May) is also the word "more",
        // so it only counts after "in", "din" or "luna".
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var month = Array.FindIndex(MonthsRo, name => name == token || (name.Length >= 6 && TextTools.Similar(name, token)));
            if (month < 0)
            {
                month = Array.FindIndex(MonthsEn, name => name == token || (name.Length >= 6 && TextTools.Similar(name, token)));
            }

            if (month < 0 || (token == "mai" && (i == 0 || tokens[i - 1] is not ("in" or "din" or "luna"))))
            {
                continue;
            }

            var year = today.Year;
            if (i + 1 < tokens.Count && int.TryParse(tokens[i + 1], out var explicitYear) && explicitYear is > 2000 and < 2100)
            {
                year = explicitYear;
            }
            else if (month + 1 > today.Month)
            {
                year--; // "in decembrie" asked in October means last December
            }

            return Period.Month(year, month + 1, today);
        }

        return null;
    }

    public static string? FindCategory(IReadOnlyList<string> tokens, IReadOnlyList<string> categories)
    {
        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var category in categories)
        {
            foreach (var part in TextTools.Tokens(category).Where(part => part.Length >= 3))
            {
                var partStem = TextTools.Stem(part);

                foreach (var token in tokens.Where(token => token.Length >= 3))
                {
                    var tokenStem = TextTools.Stem(token);
                    if (!TextTools.Similar(tokenStem, partStem) && !TextTools.Similar(token, part))
                    {
                        continue;
                    }

                    var distance = Math.Min(TextTools.Distance(tokenStem, partStem), TextTools.Distance(token, part));
                    if (distance < bestDistance)
                    {
                        best = category;
                        bestDistance = distance;
                    }
                }
            }
        }

        return best;
    }

    public static string? FindMerchant(IReadOnlyList<string> tokens, IReadOnlyList<string> merchants, string? category)
    {
        var categoryWords = category is null ? new List<string>() : TextTools.Tokens(category);

        foreach (var token in tokens.Where(token => token.Length >= 3 && !NotMerchants.Contains(token) && !token.All(char.IsDigit)))
        {
            if (categoryWords.Contains(token))
            {
                continue;
            }

            var match = merchants.FirstOrDefault(merchant => merchant == token)
                        ?? merchants.FirstOrDefault(merchant => merchant.Length >= 5 && TextTools.Similar(merchant, token));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static TransactionType? FindType(IReadOnlyList<string> tokens)
    {
        var stems = tokens.Select(TextTools.Stem).ToList();

        if (stems.Any(stem => stem.StartsWith("venit") || stem.StartsWith("incas") || stem.StartsWith("castig")
                              || stem.StartsWith("primit") || stem.StartsWith("income") || stem.StartsWith("earn")))
        {
            return TransactionType.Income;
        }

        if (stems.Any(stem => stem.StartsWith("cheltu") || stem.StartsWith("platit") || stem.StartsWith("spent")
                              || stem.StartsWith("spend") || stem.StartsWith("paid") || stem.StartsWith("cumpar")))
        {
            return TransactionType.Expense;
        }

        return null;
    }

    private static int? FindTopN(string folded)
    {
        var match = TopN().Match(folded);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return Math.Clamp(int.Parse(value, CultureInfo.InvariantCulture), 1, 20);
    }

    /// <summary>"LIDL ORADEA" → "lidl": the first meaningful word of a description is its merchant.</summary>
    public static string? MerchantKey(string? description)
    {
        foreach (var word in TextNormalizer.GroupingKey(description).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length >= 3 && !NotMerchants.Contains(word))
            {
                return word;
            }
        }

        return null;
    }
}
