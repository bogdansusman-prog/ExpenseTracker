using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Advisor;
using ExpenseTracker.Api.Services.Advisor.Brain;

namespace ExpenseTracker.Tests;

public class AxBrainTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly string[] Categories = ["Mancare", "Transport", "Haine", "Salariu"];
    private static readonly string[] Merchants = ["lidl", "carrefour", "kaufland", "glovo", "netflix", "bolt"];

    private static readonly IntentClassifier Classifier = new(TrainingData.Examples());

    private static BrainTransaction Expense(int month, int day, decimal amount, string category, string merchant) =>
        new(new DateOnly(2026, month, day), amount, TransactionType.Expense, category, merchant.ToUpperInvariant(), merchant);

    private static BrainContext Context() => new(
    [
        Expense(10, 2, 100m, "Mancare", "lidl"),
        Expense(10, 5, 50m, "Mancare", "lidl"),
        Expense(10, 8, 30m, "Transport", "bolt"),
        Expense(10, 10, 400m, "Haine", "zara"),
        Expense(9, 3, 200m, "Mancare", "lidl"),
        Expense(9, 7, 80m, "Mancare", "carrefour"),
        Expense(9, 20, 45m, "Mancare", "netflix"),
        new(new DateOnly(2026, 10, 1), 5000m, TransactionType.Income, "Salariu", "SALARIU", null)
    ], Today, 50m, 4000m);

    [Theory]
    [InlineData("Cât am dat la Lidl luna asta?", Intent.SpendTotal)]
    [InlineData("cat am cheltuit pe mancrae", Intent.SpendTotal)]
    [InlineData("cât am câștigat luna asta?", Intent.IncomeTotal)]
    [InlineData("cate tranzactii am facut saptamana asta", Intent.Count)]
    [InlineData("pe ce s-au dus banii in august", Intent.TopCategories)]
    [InlineData("care a fost cea mai scumpa chestie luna asta", Intent.Biggest)]
    [InlineData("compara luna asta cu luna trecuta", Intent.Compare)]
    [InlineData("cat dau in medie pe mancare pe luna", Intent.Average)]
    [InlineData("când am plătit ultima oară netflix", Intent.LastTime)]
    [InlineData("cati bani am in cont", Intent.Balance)]
    [InlineData("o sa ajung pe minus luna asta?", Intent.Forecast)]
    [InlineData("cum stau cu banii?", Intent.Overview)]
    [InlineData("zi-mi o gluma", Intent.Joke)]
    [InlineData("ce pot sa te intreb?", Intent.Help)]
    [InlineData("cine esti tu?", Intent.Identity)]
    public void Classifier_understands_free_questions(string question, Intent expected)
    {
        var prediction = Classifier.Predict(question);

        Assert.Equal(expected, prediction.Intent);
        Assert.True(prediction.Confidence >= 0.45, $"confidence {prediction.Confidence:0.00}");
    }

    [Fact]
    public void Classifier_returns_unknown_for_words_it_never_saw()
    {
        Assert.Equal(Intent.Unknown, Classifier.Predict("qwxz zzkq").Intent);
    }

    [Theory]
    [InlineData("mancare", "mancrae", true)]
    [InlineData("abonament", "abonamnet", true)]
    [InlineData("lidl", "bolt", false)]
    [InlineData("mai", "may", false)]
    public void Similar_tolerates_small_typos_only(string a, string b, bool expected)
    {
        Assert.Equal(expected, TextTools.Similar(a, b));
    }

    [Fact]
    public void Slots_find_period_category_and_merchant_with_typos()
    {
        var slots = SlotExtractor.Extract("cat am dat pe mancrae la lidl in septembire", Today, Categories, Merchants);

        Assert.Equal("Mancare", slots.Category);
        Assert.Equal("lidl", slots.Merchant);
        Assert.Equal(new DateOnly(2026, 9, 1), slots.Period!.From);
        Assert.Equal(new DateOnly(2026, 9, 30), slots.Period.To);
    }

    [Fact]
    public void Slots_read_last_n_days_and_top_n()
    {
        var slots = SlotExtractor.Extract("top 3 cheltuieli in ultimele 10 zile", Today, Categories, Merchants);

        Assert.Equal(3, slots.TopN);
        Assert.Equal(Today.AddDays(-9), slots.Period!.From);
    }

    [Fact]
    public void Word_mai_is_not_the_month_may_unless_it_follows_in()
    {
        Assert.Null(SlotExtractor.Extract("pe ce cheltui cel mai mult", Today, Categories, Merchants).Period);
        Assert.Equal(5, SlotExtractor.Extract("cat am cheltuit in mai", Today, Categories, Merchants).Period!.From.Month);
    }

    [Fact]
    public void Follow_up_keeps_the_previous_question_and_changes_the_period()
    {
        var first = AxBrain.Understand("cat am dat la lidl luna asta?", null, Today, Categories, Merchants);
        var second = AxBrain.Understand("si luna trecuta?", first, Today, Categories, Merchants);

        Assert.Equal(Intent.SpendTotal, second.Intent);
        Assert.True(second.FollowUp);
        Assert.Equal("lidl", second.Slots.Merchant);
        Assert.Equal(9, second.Slots.Period!.From.Month);
    }

    [Fact]
    public void Follow_up_with_a_new_merchant_replaces_the_old_one()
    {
        var first = AxBrain.Understand("cat am dat la lidl luna trecuta", null, Today, Categories, Merchants);
        var second = AxBrain.Understand("dar la carrefour?", first, Today, Categories, Merchants);

        Assert.Equal(Intent.SpendTotal, second.Intent);
        Assert.Equal("carrefour", second.Slots.Merchant);
        Assert.Equal(9, second.Slots.Period!.From.Month);
    }

    [Fact]
    public void Unclear_question_that_names_a_merchant_becomes_a_spending_question()
    {
        var frame = AxBrain.Understand("cheltuieli la kaufland", null, Today, Categories, Merchants);

        Assert.Equal(Intent.SpendTotal, frame.Intent);
        Assert.Equal("kaufland", frame.Slots.Merchant);
    }

    [Fact]
    public void Spend_total_is_computed_from_the_transactions()
    {
        var t = new Texts("ro");
        var slots = Slots.Empty with { Merchant = "lidl", Period = Period.ThisMonth(Today) };

        var answer = QueryEngine.SpendTotal(Context(), slots, t);

        Assert.Contains(t.Money(150m), answer);
        Assert.Contains("2 tranzactii", answer);
        Assert.Contains("3", answer); // 150 lei / 50 lei per hour = 3 hours of work
    }

    [Fact]
    public void Top_categories_rank_by_amount()
    {
        var answer = QueryEngine.TopCategories(Context(), Slots.Empty, new Texts("en"));

        Assert.True(answer.IndexOf("Haine", StringComparison.Ordinal) < answer.IndexOf("Mancare", StringComparison.Ordinal));
        Assert.DoesNotContain("Salariu", answer);
    }

    [Fact]
    public void Running_month_is_compared_with_the_same_days_of_the_previous_month()
    {
        var previous = Period.ThisMonth(Today).Previous();

        Assert.Equal(new DateOnly(2026, 9, 1), previous.From);
        Assert.Equal(new DateOnly(2026, 9, 12), previous.To);
    }

    [Fact]
    public void Last_time_finds_the_most_recent_transaction()
    {
        var answer = QueryEngine.LastTime(Context(), Slots.Empty with { Merchant = "lidl" }, new Texts("ro"));

        Assert.Contains("05.10.2026", answer);
    }

    [Fact]
    public void Merchant_key_is_the_first_meaningful_word()
    {
        Assert.Equal("lidl", SlotExtractor.MerchantKey("LIDL ORADEA"));
    }
}
