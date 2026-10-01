using System.Globalization;
using System.Text;

namespace ExpenseTracker.Api.Services.Analytics;

public static class TextNormalizer
{
    /// <summary>
    /// Lower-cases the text and removes diacritics ("Mâncare" becomes "mancare"),
    /// so Romanian text matches with or without special characters.
    /// </summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        // ș/ț with comma below are not decomposed in every runtime; map them explicitly.
        return builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('ș', 's').Replace('ş', 's')
            .Replace('ț', 't').Replace('ţ', 't');
    }

    /// <summary>
    /// Key used to group recurring payments: folded text with digits and punctuation removed
    /// ("Netflix 03/2026" and "NETFLIX" both become "netflix").
    /// </summary>
    public static string GroupingKey(string? text)
    {
        var folded = Fold(text);
        var builder = new StringBuilder(folded.Length);

        foreach (var character in folded)
        {
            builder.Append(char.IsLetter(character) ? character : ' ');
        }

        return string.Join(' ', builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
