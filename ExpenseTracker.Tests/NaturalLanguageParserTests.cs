using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.QuickAdd;

namespace ExpenseTracker.Tests;

public class NaturalLanguageParserTests
{
    // Thursday
    private static readonly DateOnly Today = new(2026, 10, 1);

    private static readonly CategoryOption[] Categories =
    [
        new(1, "Mâncare"),
        new(2, "Transport"),
        new(3, "Salariu"),
        new(4, "Divertisment"),
        new(5, "Utilități")
    ];

    [Fact]
    public void Parses_romanian_sentence()
    {
        var result = NaturalLanguageParser.Parse("ieri 45 lei pizza cu Andrei", Categories, Today);

        Assert.Equal(45m, result.Amount);
        Assert.Equal(TransactionType.Expense, result.Type);
        Assert.Equal(Today.AddDays(-1), result.Date);
        Assert.Equal(1, result.CategoryId);
        Assert.Equal("Pizza cu andrei", result.Description);
        Assert.Empty(result.MissingFields);
    }

    [Fact]
    public void Detects_income_in_english()
    {
        var result = NaturalLanguageParser.Parse("salary 4500 yesterday", Categories, Today);

        Assert.Equal(4500m, result.Amount);
        Assert.Equal(TransactionType.Income, result.Type);
        Assert.Equal(3, result.CategoryId);
    }

    [Fact]
    public void Understands_decimal_comma_weekday_and_keywords()
    {
        var result = NaturalLanguageParser.Parse("am dat 23,50 pe uber vineri", Categories, Today);

        Assert.Equal(23.50m, result.Amount);
        Assert.Equal(new DateOnly(2026, 9, 25), result.Date);
        Assert.Equal(2, result.CategoryId);
    }

    [Fact]
    public void Explicit_date_is_not_taken_as_amount()
    {
        var result = NaturalLanguageParser.Parse("15.09 factura curent 210 lei", Categories, Today);

        Assert.Equal(210m, result.Amount);
        Assert.Equal(new DateOnly(2026, 9, 15), result.Date);
        Assert.Equal(5, result.CategoryId);
    }

    [Fact]
    public void Reports_missing_fields()
    {
        var result = NaturalLanguageParser.Parse("ceva fara suma", Categories, Today);

        Assert.Null(result.Amount);
        Assert.Contains("amount", result.MissingFields);
        Assert.Contains("category", result.MissingFields);
    }
}
