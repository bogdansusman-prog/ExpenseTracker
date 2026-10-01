using System.Globalization;
using System.Text.RegularExpressions;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Api.Services.QuickAdd;

public record CategoryOption(int Id, string Name);

public record ParsedTransaction(
    decimal? Amount,
    TransactionType Type,
    DateOnly Date,
    int? CategoryId,
    string? CategoryName,
    string? Description,
    double Confidence,
    IReadOnlyList<string> MissingFields);

/// <summary>
/// Rule-based parser that turns a sentence such as "ieri 45 lei pizza cu Andrei"
/// or "salary 4500 yesterday" into a transaction draft. Works offline, in Romanian and English.
/// </summary>
public static partial class NaturalLanguageParser
{
    private static readonly Dictionary<string, int> RelativeDays = new()
    {
        ["azi"] = 0, ["astazi"] = 0, ["today"] = 0,
        ["ieri"] = -1, ["yesterday"] = -1,
        ["alaltaieri"] = -2
    };

    private static readonly Dictionary<string, DayOfWeek> WeekDays = new()
    {
        ["luni"] = DayOfWeek.Monday, ["monday"] = DayOfWeek.Monday,
        ["marti"] = DayOfWeek.Tuesday, ["tuesday"] = DayOfWeek.Tuesday,
        ["miercuri"] = DayOfWeek.Wednesday, ["wednesday"] = DayOfWeek.Wednesday,
        ["joi"] = DayOfWeek.Thursday, ["thursday"] = DayOfWeek.Thursday,
        ["vineri"] = DayOfWeek.Friday, ["friday"] = DayOfWeek.Friday,
        ["sambata"] = DayOfWeek.Saturday, ["saturday"] = DayOfWeek.Saturday,
        ["duminica"] = DayOfWeek.Sunday, ["sunday"] = DayOfWeek.Sunday
    };

    private static readonly HashSet<string> IncomeWords =
    [
        "salariu", "salar", "venit", "primit", "incasat", "bursa", "bonus", "rambursare",
        "salary", "income", "received", "refund", "paycheck", "freelance"
    ];

    private static readonly HashSet<string> FillerWords =
    [
        "pe", "pentru", "la", "de", "am", "dat", "platit", "cheltuit", "cumparat", "lei", "ron", "leu",
        "on", "for", "at", "spent", "paid", "bought", "i", "eur", "euro", "usd"
    ];

    /// <summary>keyword → words that a matching category name may contain.</summary>
    private static readonly (string[] Keywords, string[] CategoryHints)[] KeywordMap =
    {
        (new[] {"pizza", "burger", "shaorma", "kebab", "restaurant", "mancare", "pranz", "cina", "kfc", "mcdonalds",
          "glovo", "tazz", "cafea", "coffee", "starbucks", "food", "lunch", "dinner", "breakfast"},
         new[] {"mancare", "food", "restaurant", "alimente", "cafea"}),
        (new[] {"lidl", "kaufland", "carrefour", "mega", "profi", "auchan", "penny", "cumparaturi", "groceries", "supermarket"},
         new[] {"alimente", "cumparaturi", "groceries", "mancare", "food"}),
        (new[] {"uber", "bolt", "taxi", "benzina", "motorina", "carburant", "fuel", "metrou", "bilet", "stb", "tren", "cfr",
          "parcare", "parking", "bus", "autobuz"},
         new[] {"transport", "masina", "combustibil", "car"}),
        (new[] {"netflix", "spotify", "hbo", "disney", "youtube", "cinema", "film", "concert", "steam", "playstation",
          "xbox", "joc", "game", "games"},
         new[] {"divertisment", "entertainment", "abonament", "subscription", "distractie"}),
        (new[] {"curent", "gaz", "apa", "internet", "digi", "orange", "vodafone", "telekom", "enel", "factura", "electricity",
          "water", "phone", "telefon"},
         new[] {"utilitati", "facturi", "utilities", "bills", "casa"}),
        (new[] {"farmacie", "medicamente", "doctor", "dentist", "pharmacy", "medicine", "clinica", "analize"},
         new[] {"sanatate", "health", "medical"}),
        (new[] {"chirie", "rent", "intretinere"},
         new[] {"chirie", "locuinta", "rent", "housing", "casa"}),
        (new[] {"haine", "adidasi", "pantofi", "emag", "altex", "amazon", "zara", "hm", "clothes", "shoes"},
         new[] {"shopping", "cumparaturi", "haine", "clothes"}),
        (new[] {"carte", "carti", "curs", "udemy", "facultate", "book", "books", "course", "school"},
         new[] {"educatie", "education", "carti", "studii"}),
        (new[] {"sala", "gym", "fitness", "sport"},
         new[] {"sport", "sanatate", "gym", "fitness"}),
        (new[] {"salariu", "salary", "paycheck", "bonus", "bursa", "freelance"},
         new[] {"salariu", "venit", "salary", "income", "bursa"})
    };

    [GeneratedRegex(@"(?<![\d.,/])(\d{1,3}(?:[ .]\d{3})+|\d+)(?:[.,](\d{1,2}))?(?![\d/])\s*(lei|leu|ron|eur|euro|€|usd|\$)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    [GeneratedRegex(@"\b(\d{1,2})[./](\d{1,2})(?:[./](\d{2,4}))?\b")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^\s*(lei|leu|ron|eur|euro|€|usd|\$)", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyAfterRegex();

    [GeneratedRegex(@"\b(?:acum|in urma cu)\s+(\d{1,2})\s+zile\b|\b(\d{1,2})\s+days?\s+ago\b")]
    private static partial Regex DaysAgoRegex();

    public static ParsedTransaction Parse(string text, IReadOnlyList<CategoryOption> categories, DateOnly today)
    {
        var folded = TextNormalizer.Fold(text);
        var remaining = folded;
        var missing = new List<string>();

        // 1. Date
        var date = today;
        var dateFound = false;

        var explicitDate = DateRegex().Match(remaining);
        var looksLikeMoney = explicitDate.Success
            && CurrencyAfterRegex().IsMatch(remaining[(explicitDate.Index + explicitDate.Length)..]);

        if (explicitDate.Success && !looksLikeMoney && TryBuildDate(explicitDate, today, out var parsedDate))
        {
            date = parsedDate;
            dateFound = true;
            remaining = remaining.Remove(explicitDate.Index, explicitDate.Length);
        }

        var daysAgo = DaysAgoRegex().Match(remaining);
        if (!dateFound && daysAgo.Success)
        {
            var value = daysAgo.Groups[1].Success ? daysAgo.Groups[1].Value : daysAgo.Groups[2].Value;
            date = today.AddDays(-int.Parse(value, CultureInfo.InvariantCulture));
            dateFound = true;
            remaining = remaining.Remove(daysAgo.Index, daysAgo.Length);
        }

        var words = Tokenize(remaining);

        if (!dateFound)
        {
            foreach (var word in words)
            {
                if (RelativeDays.TryGetValue(word, out var offset))
                {
                    date = today.AddDays(offset);
                    dateFound = true;
                    words.Remove(word);
                    break;
                }

                if (WeekDays.TryGetValue(word, out var weekDay))
                {
                    var back = ((int)today.DayOfWeek - (int)weekDay + 7) % 7;
                    date = today.AddDays(-back);
                    dateFound = true;
                    words.Remove(word);
                    break;
                }
            }
        }

        remaining = string.Join(' ', words);

        // 2. Amount (prefer a number followed by a currency)
        decimal? amount = null;
        var amountMatches = AmountRegex().Matches(remaining).ToList();
        var amountMatch = amountMatches.FirstOrDefault(match => match.Groups[3].Success)
                          ?? amountMatches.FirstOrDefault();

        if (amountMatch is not null)
        {
            var whole = amountMatch.Groups[1].Value.Replace(" ", "").Replace(".", "");
            var fraction = amountMatch.Groups[2].Success ? amountMatch.Groups[2].Value : "0";

            if (decimal.TryParse($"{whole}.{fraction}", NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                && value > 0)
            {
                amount = value;
                remaining = remaining.Remove(amountMatch.Index, amountMatch.Length);
            }
        }

        if (amount is null)
        {
            missing.Add("amount");
        }

        words = Tokenize(remaining);

        // 3. Type
        var type = words.Any(IncomeWords.Contains) ? TransactionType.Income : TransactionType.Expense;

        // 4. Category
        var category = MatchCategory(words, categories);
        if (category is null)
        {
            missing.Add("category");
        }

        // 5. Description = what is left, without filler words
        var descriptionWords = TrimFiller(words);
        var description = descriptionWords.Count > 0 ? Capitalize(string.Join(' ', descriptionWords)) : null;

        var confidence = (amount is not null ? 0.5 : 0)
                         + (category is not null ? 0.3 : 0)
                         + (dateFound ? 0.1 : 0.05)
                         + (description is not null ? 0.1 : 0);

        return new ParsedTransaction(
            amount,
            type,
            date,
            category?.Id,
            category?.Name,
            description,
            Math.Round(confidence, 2),
            missing);
    }

    private static CategoryOption? MatchCategory(IReadOnlyCollection<string> words, IReadOnlyList<CategoryOption> categories)
    {
        if (categories.Count == 0)
        {
            return null;
        }

        var folded = categories.Select(category => (Category: category, Name: TextNormalizer.Fold(category.Name))).ToList();

        // a) the user typed the category name itself (or its first 4+ letters)
        foreach (var word in words.Where(word => word.Length >= 3))
        {
            var direct = folded.FirstOrDefault(item =>
                item.Name == word
                || (word.Length >= 4 && item.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(part => part.Length >= 4 && (part.StartsWith(word) || word.StartsWith(part)))));

            if (direct.Category is not null)
            {
                return direct.Category;
            }
        }

        // b) a known keyword points to a category name hint ("pizza" → "Mancare")
        foreach (var (keywords, hints) in KeywordMap)
        {
            if (!words.Any(word => keywords.Contains(word)))
            {
                continue;
            }

            foreach (var hint in hints)
            {
                var match = folded.FirstOrDefault(item => item.Name.Contains(hint));
                if (match.Category is not null)
                {
                    return match.Category;
                }
            }
        }

        return null;
    }

    private static bool TryBuildDate(Match match, DateOnly today, out DateOnly date)
    {
        date = today;
        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var year = today.Year;

        if (match.Groups[3].Success)
        {
            year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (year < 100)
            {
                year += 2000;
            }
        }

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateOnly(year, month, day);

        // "15.12" typed in January means last December, not a future date.
        if (!match.Groups[3].Success && date > today)
        {
            date = date.AddYears(-1);
        }

        return true;
    }

    private static List<string> Tokenize(string text) =>
        // ',' is not a separator so that "23,50" stays one token; it is trimmed from word ends instead.
        text.Split(new[] { ' ', ';', '!', '?', ':', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('.', ',', '-', '(', ')', '"', '\''))
            .Where(word => word.Length > 0)
            .ToList();

    private static List<string> TrimFiller(List<string> words)
    {
        var result = words.ToList();

        while (result.Count > 0 && FillerWords.Contains(result[0]))
        {
            result.RemoveAt(0);
        }

        while (result.Count > 0 && FillerWords.Contains(result[^1]))
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static string Capitalize(string text) => char.ToUpper(text[0]) + text[1..];
}
