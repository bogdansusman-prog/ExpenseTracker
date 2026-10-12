using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>What Ax understood from a question, shown as small chips under the answer.</summary>
public record AxReply(string Text, IReadOnlyList<string> Understood);

/// <summary>An understood question: intent, details and how sure Ax is.</summary>
public record Frame(Intent Intent, Slots Slots, double Confidence, bool FollowUp);

/// <summary>
/// Ax's "brain", built from scratch: a Naive Bayes intent classifier, a slot extractor with typo
/// tolerance, short-term memory for follow-up questions and a query engine over the real data.
/// No external AI: everything runs locally and every number is computed, never invented.
/// </summary>
public static class AxBrain
{
    private const double ConfidentEnough = 0.45;

    private static readonly Lazy<IntentClassifier> Classifier = new(() => new IntentClassifier(TrainingData.Examples()));

    private static readonly HashSet<Intent> SmallTalk = [Intent.Greeting, Intent.Thanks, Intent.Identity, Intent.Joke, Intent.Help];

    /// <summary>Questions about the data, which a follow-up can continue.</summary>
    private static readonly HashSet<Intent> DataIntents =
    [
        Intent.SpendTotal, Intent.IncomeTotal, Intent.Count, Intent.TopCategories, Intent.TopMerchants,
        Intent.Biggest, Intent.Compare, Intent.Average, Intent.LastTime, Intent.WorkHours
    ];

    private static readonly string[] FollowUpStarts = ["si", "dar", "iar", "and", "what about", "how about", "but"];

    public static AxReply Respond(
        IReadOnlyList<string> userMessages,
        BrainContext context,
        FinancialSnapshot snapshot,
        AdvisorReport report,
        string language)
    {
        var t = new Texts(language);
        var categories = context.Transactions.Select(x => x.Category).Distinct().ToList();
        var merchants = context.Transactions.Where(x => x.Merchant is not null).Select(x => x.Merchant!).Distinct().ToList();

        // Replay the conversation so "si luna trecuta?" knows what the previous question was about.
        Frame? frame = null;
        foreach (var message in userMessages.TakeLast(6))
        {
            frame = Understand(message, frame, context.Today, categories, merchants);
        }

        if (frame is null)
        {
            return new AxReply(AdvisorChatEngine_Help(t), []);
        }

        var text = frame.Intent switch
        {
            Intent.Greeting => Pick(userMessages[^1], t.T("Salut! Sunt Ax. Intreaba-ma orice despre banii tai.", "Hi! I'm Ax. Ask me anything about your money."),
                t.T("Hei! Ce vrei sa aflam azi?", "Hey! What shall we find out today?"),
                t.T("Buna! Am cifrele pregatite. Ce te intereseaza?", "Hello! I've got the numbers ready. What would you like to know?")),
            Intent.Thanks => Pick(userMessages[^1], t.T("Cu placere!", "You're welcome!"), t.T("Oricand!", "Anytime!"),
                t.T("Mereu bucuros sa ajut. Spor la economisit!", "Always happy to help. Happy saving!")),
            Intent.Identity => t.T(
                "Sunt Ax, un axolotl cu ochelari si un mic „creier” facut de la zero: un clasificator Naive Bayes antrenat pe exemple de intrebari, " +
                "potrivire aproximativa ca sa inteleg si greselile de tastare, memorie pentru intrebarile de continuare si un motor care calculeaza raspunsurile din tranzactiile tale. " +
                "Nu folosesc niciun AI extern si nu inventez cifre.",
                "I'm Ax, an axolotl with glasses and a small \"brain\" built from scratch: a Naive Bayes classifier trained on example questions, " +
                "fuzzy matching so I understand typos, memory for follow-up questions and an engine that computes answers from your transactions. " +
                "I use no external AI and never make up numbers."),
            Intent.Joke => Pick(userMessages[^1],
                t.T("De ce nu tin axolotlii bani in portofel? Pentru ca le cresc la loc doar picioarele, nu si economiile.",
                    "Why don't axolotls keep cash in their wallets? They can regrow legs, but not savings."),
                t.T("Bugetul meu e ca un abonament uitat: il descopar abia la sfarsitul lunii.",
                    "My budget is like a forgotten subscription: I only notice it at the end of the month."),
                t.T("Am incercat sa economisesc pe cafea. Acum doar ma uit la ea si plang. Gratis, macar.",
                    "I tried saving on coffee. Now I just look at it and cry. At least that's free.")),
            Intent.Help => Help(t),
            Intent.Overview => AdvisorChatEngine.OverviewAnswer(report, t),
            Intent.Savings => AdvisorChatEngine.SavingsAnswer(report, t),
            Intent.Subscriptions => AdvisorChatEngine.SubscriptionsAnswer(snapshot, t),
            Intent.Forecast => AdvisorChatEngine.ForecastAnswer(snapshot, t),
            Intent.Regret => AdvisorChatEngine.RegretAnswer(snapshot, t),
            Intent.WorkHours when frame.Slots.Category is null && frame.Slots.Merchant is null && frame.Slots.Period is null
                => AdvisorChatEngine.WorkHoursAnswer(snapshot, t),
            Intent.WorkHours or Intent.SpendTotal => QueryEngine.SpendTotal(context, frame.Slots, t),
            Intent.IncomeTotal => QueryEngine.IncomeTotal(context, frame.Slots, t),
            Intent.Count => QueryEngine.Count(context, frame.Slots, t),
            Intent.TopCategories => QueryEngine.TopCategories(context, frame.Slots, t),
            Intent.TopMerchants => QueryEngine.TopMerchants(context, frame.Slots, t),
            Intent.Biggest => QueryEngine.Biggest(context, frame.Slots, t),
            Intent.Compare => QueryEngine.Compare(context, frame.Slots, t),
            Intent.Average => QueryEngine.Average(context, frame.Slots, t),
            Intent.LastTime => QueryEngine.LastTime(context, frame.Slots, t),
            Intent.Balance => QueryEngine.Balance(context, t),
            _ => NotSure(userMessages[^1], t)
        };

        return new AxReply(text, Chips(frame, t));
    }

    /// <summary>Classifier + slots, then decide whether this is a follow-up to the previous question.</summary>
    public static Frame Understand(string message, Frame? previous, DateOnly today, IReadOnlyList<string> categories, IReadOnlyList<string> merchants)
    {
        var prediction = Classifier.Value.Predict(message);
        var slots = SlotExtractor.Extract(message, today, categories, merchants);
        var folded = TextNormalizer.Fold(message).Trim();
        var tokens = TextTools.Tokens(message);

        var startsLikeFollowUp = FollowUpStarts.Any(start => folded == start || folded.StartsWith(start + " "));
        var confident = prediction.Confidence >= ConfidentEnough && prediction.Intent != Intent.Unknown;
        var canContinue = previous is not null && DataIntents.Contains(previous.Intent);

        // "si luna trecuta?" / "dar la Lidl?": no clear intent of its own, only new details for the last question.
        if (canContinue && !slots.IsEmpty && !(confident && SmallTalk.Contains(prediction.Intent)))
        {
            var shortAddOn = tokens.Count <= 4 && (startsLikeFollowUp || !confident);
            if (shortAddOn || !confident)
            {
                return new Frame(previous!.Intent, slots.MergeOnto(previous.Slots), prediction.Confidence, true);
            }
        }

        // "si care au fost cele mai mari?": a new question about the same period / category.
        if (canContinue && startsLikeFollowUp && confident && DataIntents.Contains(prediction.Intent))
        {
            return new Frame(prediction.Intent, slots.MergeOnto(previous!.Slots), prediction.Confidence, true);
        }

        if (confident)
        {
            return new Frame(prediction.Intent, slots, prediction.Confidence, false);
        }

        // Not sure about the intent, but the question names a category, a merchant or a period:
        // the most useful guess is "how much did I spend there".
        return slots.IsEmpty
            ? new Frame(Intent.Unknown, slots, prediction.Confidence, false)
            : new Frame(Intent.SpendTotal, slots, prediction.Confidence, false);
    }

    private static IReadOnlyList<string> Chips(Frame frame, Texts t)
    {
        if (SmallTalk.Contains(frame.Intent) || frame.Intent == Intent.Unknown)
        {
            return [];
        }

        var chips = new List<string> { IntentLabel(frame.Intent, t) };
        if (frame.Slots.Period is { } period) chips.Add(period.Label(t.IsEnglish));
        if (frame.Slots.Category is { } category) chips.Add(category);
        if (frame.Slots.Merchant is { } merchant) chips.Add(QueryEngine.Display(merchant));
        if (frame.FollowUp) chips.Add(t.T("continuare", "follow-up"));
        chips.Add(t.T($"sigur {Math.Round(frame.Confidence * 100)}%", $"{Math.Round(frame.Confidence * 100)}% sure"));
        return chips;
    }

    private static string IntentLabel(Intent intent, Texts t) => intent switch
    {
        Intent.Overview => t.T("rezumat", "overview"),
        Intent.SpendTotal => t.T("cheltuieli", "spending"),
        Intent.IncomeTotal => t.T("venituri", "income"),
        Intent.Count => t.T("numar", "count"),
        Intent.TopCategories => t.T("top categorii", "top categories"),
        Intent.TopMerchants => t.T("top magazine", "top merchants"),
        Intent.Biggest => t.T("cele mai mari", "biggest"),
        Intent.Compare => t.T("comparatie", "comparison"),
        Intent.Average => t.T("medie", "average"),
        Intent.LastTime => t.T("ultima data", "last time"),
        Intent.Balance => t.T("sold", "balance"),
        Intent.Subscriptions => t.T("abonamente", "subscriptions"),
        Intent.Forecast => t.T("prognoza", "forecast"),
        Intent.Regret => t.T("a meritat?", "worth it?"),
        Intent.Savings => t.T("economii", "savings"),
        Intent.WorkHours => t.T("ore de munca", "work hours"),
        _ => intent.ToString()
    };

    private static string NotSure(string message, Texts t) => Pick(message,
        t.T("Hmm, nu sunt sigur ca am inteles. Poti sa intrebi altfel? De exemplu: „cat am dat la Lidl luna asta?” sau „pe ce am cheltuit cel mai mult?”",
            "Hmm, I'm not sure I understood. Could you ask differently? For example: \"how much did I spend at Lidl this month?\" or \"what do I spend the most on?\""),
        t.T("Asta nu am prins-o. Incearca sa pomenesti o categorie, un magazin sau o perioada, de exemplu „mancare in septembrie”.",
            "I didn't catch that. Try mentioning a category, a shop or a period, for example \"food in September\"."));

    private static string Help(Texts t) => t.T(
        "Pot sa-ti raspund la intrebari libere despre banii tai, chiar si cu greseli de tastare. De exemplu:\n" +
        "• Cat am dat la Lidl luna asta?\n" +
        "• Pe ce am cheltuit cel mai mult in septembrie?\n" +
        "• Compara mancarea cu luna trecuta\n" +
        "• De cate ori am comandat Glovo in ultimele 30 de zile?\n" +
        "• Cand am platit ultima data Netflix?\n" +
        "• Cat cheltui in medie pe zi?\n" +
        "• Cum stau? / Unde pot sa economisesc? / Ce abonamente am?\n" +
        "Si poti continua: „si luna trecuta?”, „dar la Carrefour?”",
        "You can ask me free questions about your money, typos included. For example:\n" +
        "• How much did I spend at Lidl this month?\n" +
        "• What did I spend the most on in September?\n" +
        "• Compare food with last month\n" +
        "• How many times did I order Glovo in the last 30 days?\n" +
        "• When did I last pay Netflix?\n" +
        "• How much do I spend per day on average?\n" +
        "• How am I doing? / Where can I save? / What subscriptions do I have?\n" +
        "And you can follow up: \"and last month?\", \"what about Carrefour?\"");

    private static string AdvisorChatEngine_Help(Texts t) => Help(t);

    /// <summary>Picks one of several phrasings, stable for the same message (so answers don't flicker).</summary>
    private static string Pick(string seed, params string[] options)
    {
        var hash = 0;
        foreach (var character in seed)
        {
            hash = unchecked(hash * 31 + character);
        }

        return options[Math.Abs(hash % options.Length)];
    }
}
