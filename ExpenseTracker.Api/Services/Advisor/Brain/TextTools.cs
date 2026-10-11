using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>
/// Small language tools written from scratch: tokenizing, a crude RO/EN stemmer
/// and typo-tolerant comparison (Damerau-Levenshtein distance).
/// </summary>
public static class TextTools
{
    private static readonly char[] Separators =
        [' ', ',', '.', '?', '!', ';', ':', '"', '\'', '(', ')', '/', '-', '„', '”', '“', '\n', '\r', '\t'];

    // Longest first, so "urilor" is removed before "or".
    private static readonly string[] Suffixes =
        ["urilor", "urile", "ului", "ilor", "elor", "uri", "ile", "ele", "lor", "ing", "ul", "ii", "ed", "es", "a", "e", "i", "u", "s"];

    /// <summary>"Cât am dat pe Mâncare?" → ["cat", "am", "dat", "pe", "mancare"].</summary>
    public static List<string> Tokens(string? text) =>
        TextNormalizer.Fold(text).Split(Separators, StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Removes one common ending so "abonamentele", "abonamentul" and "abonament" meet.</summary>
    public static string Stem(string token)
    {
        if (token.Length <= 4 || token.Any(char.IsDigit))
        {
            return token;
        }

        foreach (var suffix in Suffixes)
        {
            if (token.Length - suffix.Length >= 4 && token.EndsWith(suffix, StringComparison.Ordinal))
            {
                return token[..^suffix.Length];
            }
        }

        return token;
    }

    /// <summary>Edit distance where swapping two neighbouring letters counts as one typo.</summary>
    public static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }

    /// <summary>Equal, or close enough to be the same word typed in a hurry ("mancrae" ≈ "mancare").</summary>
    public static bool Similar(string a, string b)
    {
        if (a == b)
        {
            return true;
        }

        var longest = Math.Max(a.Length, b.Length);
        var allowed = longest <= 4 ? 0 : longest <= 7 ? 1 : 2;

        return allowed > 0 && Math.Abs(a.Length - b.Length) <= allowed && Distance(a, b) <= allowed;
    }
}
