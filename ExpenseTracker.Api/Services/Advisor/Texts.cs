using System.Globalization;

namespace ExpenseTracker.Api.Services.Advisor;

/// <summary>Tiny RO/EN helper used by the advisor and the chat engine.</summary>
public sealed class Texts(string language)
{
    private static readonly CultureInfo Romanian = CultureInfo.GetCultureInfo("ro-RO");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static readonly string[] DaysRo = ["duminica", "luni", "marti", "miercuri", "joi", "vineri", "sambata"];
    private static readonly string[] DaysEn = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    public bool IsEnglish { get; } = language == "en";

    public string Language => IsEnglish ? "en" : "ro";

    private CultureInfo Culture => IsEnglish ? English : Romanian;

    public string T(string ro, string en) => IsEnglish ? en : ro;

    /// <summary>"1.234 lei" (RO) or "1,234 lei" (EN); small amounts keep decimals.</summary>
    public string Money(decimal value) =>
        (Math.Abs(value) >= 100 ? value.ToString("N0", Culture) : value.ToString("0.##", Culture)) + " lei";

    public string Percent(double value) => value.ToString("0.#", Culture) + "%";

    public string Number(double value) => value.ToString("0.#", Culture);

    public string Day(int dayOfWeek) => (IsEnglish ? DaysEn : DaysRo)[((dayOfWeek % 7) + 7) % 7];
}
