namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>
/// The examples Ax learns from. To teach him a new way of asking, add a line here:
/// the classifier is rebuilt from these sentences every time the app starts.
/// </summary>
public static class TrainingData
{
    public static IEnumerable<(Intent Intent, string Text)> Examples()
    {
        foreach (var (intent, sentences) in Sentences)
        {
            foreach (var sentence in sentences)
            {
                yield return (intent, sentence);
            }
        }
    }

    private static readonly (Intent Intent, string[] Sentences)[] Sentences =
    [
        (Intent.Greeting, new[] {
            "salut", "buna", "buna ziua", "hei ax", "hello", "hi", "hey there", "neata", "servus", "ce faci",
            "how are you doing", "salut ax", "buna ax", "hey ax", "hello ax", "noroc", "buna seara", "good morning"
        }),
        (Intent.Thanks, new[] {
            "multumesc", "mersi", "mersi mult", "iti multumesc", "thanks", "thank you", "super multumesc", "ms",
            "esti tare", "mersi ax", "multumesc mult", "thanks a lot", "perfect mersi", "multam", "multumesc frumos",
            "merci"
        }),
        (Intent.Identity, new[] {
            "cine esti", "ce esti tu", "cum functionezi", "esti un ai", "who are you", "what are you", "are you an ai",
            "cum gandesti", "cine te a facut", "how do you work"
        }),
        (Intent.Joke, new[] {
            "spune mi o gluma", "zi o gluma", "fa ma sa rad", "tell me a joke", "make me laugh", "o gluma despre bani",
            "spune mi ceva amuzant", "ceva amuzant", "say something funny"
        }),
        (Intent.Help, new[] {
            "ajutor", "ce poti face", "ce stii sa faci", "ce te pot intreba", "help", "what can you do",
            "what can i ask", "cum te folosesc", "exemple de intrebari", "ce pot sa te intreb", "ce intrebari pot pune",
            "what questions can i ask", "nu stiu ce sa intreb", "cum functioneaza chatul"
        }),
        (Intent.Overview, new[] {
            "cum stau", "cum stau cu banii", "care e scorul meu", "da mi un rezumat", "situatia mea financiara",
            "how am i doing", "what is my score", "give me a summary", "sunt pe plus", "e bine cum stau",
            "verdictul tau", "how are my finances", "cum ma descurc", "sunt ok cu banii", "how is my money"
        }),
        (Intent.SpendTotal, new[] {
            "cat am cheltuit", "cat am cheltuit luna asta", "cat am dat pe mancare", "cat am platit la lidl",
            "cati bani am dat pe transport", "cat m a costat", "suma cheltuielilor",
            "cat am cheltuit pe haine in septembrie", "how much did i spend", "how much did i spend on food",
            "how much did i pay at lidl", "total spending this week", "cat am lasat la carrefour",
            "cheltuielile mele luna trecuta", "cat am dat ieri"
        }),
        (Intent.IncomeTotal, new[] {
            "cat am castigat", "cat am incasat", "cati bani am primit", "cat a intrat in cont", "venitul meu luna asta",
            "how much did i earn", "how much money came in", "my income this month", "cat am primit de la mama",
            "venituri", "cat am castigat luna asta", "cat am castigat in august", "how much did i earn this month",
            "how much did i make", "cat salariu am primit", "cat am primit luna asta", "income", "earnings"
        }),
        (Intent.Count, new[] {
            "de cate ori am fost la lidl", "cate tranzactii am", "de cate ori am comandat", "cate plati am facut",
            "how many times did i go to lidl", "how many transactions", "how many times did i order",
            "numarul de plati"
        }),
        (Intent.TopCategories, new[] {
            "pe ce cheltui cel mai mult", "pe ce s au dus banii", "unde se duc banii", "top categorii",
            "care categorie e cea mai mare", "what do i spend the most on", "where does my money go", "top categories",
            "pe ce am cheltuit luna asta"
        }),
        (Intent.TopMerchants, new[] {
            "unde cumpar cel mai des", "la ce magazine cheltui", "top magazine",
            "la ce comercianti am dat cei mai multi bani", "where do i shop the most", "top merchants",
            "which stores do i spend at", "magazinul preferat", "top magazine luna asta",
            "la ce magazin am dat cel mai mult", "care e magazinul unde cheltui cel mai mult", "top shops",
            "which shop do i use the most"
        }),
        (Intent.Biggest, new[] {
            "care sunt cele mai mari cheltuieli", "cea mai scumpa cumparatura", "cele mai mari plati",
            "top 5 cheltuieli", "biggest expenses", "my largest purchases", "most expensive thing i bought",
            "ce am cumparat scump", "top 3 cheltuieli", "cele mai scumpe lucruri", "most expensive purchases",
            "largest expenses last month", "cea mai mare cheltuiala", "care a fost cea mai mare plata"
        }),
        (Intent.Compare, new[] {
            "compara luna asta cu luna trecuta", "cum stau fata de luna trecuta",
            "am cheltuit mai mult decat luna trecuta", "compare this month with last month",
            "am i doing better than last month", "diferenta fata de saptamana trecuta",
            "mancare luna asta versus luna trecuta", "am cheltuit mai putin", "e mai bine decat inainte"
        }),
        (Intent.Average, new[] {
            "cat cheltui in medie", "media pe zi", "cat cheltui pe zi", "media lunara", "average spending",
            "how much do i spend per day", "daily average", "monthly average", "in medie cat dau pe mancare"
        }),
        (Intent.LastTime, new[] {
            "cand am fost ultima data la lidl", "ultima tranzactie", "ultima plata",
            "cand am platit ultima oara netflix", "when did i last go to lidl", "last transaction", "my last payment",
            "cand am primit ultima data salariul"
        }),
        (Intent.Balance, new[] {
            "cati bani am", "ce sold am", "cat am in cont", "soldul meu", "how much money do i have",
            "what is my balance", "balance", "cati bani mi au ramas", "sold", "cati bani am acum", "cat am pe card"
        }),
        (Intent.Subscriptions, new[] {
            "ce abonamente am", "abonamentele mele", "plati recurente", "cat platesc pe abonamente",
            "netflix spotify abonament", "what subscriptions do i have", "my subscriptions", "recurring payments",
            "s a scumpit vreun abonament", "abonamente", "subscriptions", "ce platesc lunar", "ce abonamente platesc",
            "abonamentele", "care sunt abonamentele"
        }),
        (Intent.Forecast, new[] {
            "cati bani o sa am la sfarsitul lunii", "prognoza", "o sa ajung pe minus", "ce sold voi avea",
            "cat imi ramane", "how much will i have at the end of the month", "forecast", "will i go negative",
            "predictie sold", "o sa am destui bani"
        }),
        (Intent.Regret, new[] {
            "ce am regretat", "la ce regret cel mai mult", "a meritat", "cheltuieli regretate", "what did i regret",
            "which purchases were worth it", "scorul a meritat", "la ce am regretat cel mai mult",
            "ce cumparaturi am regretat", "ce nu a meritat", "what was not worth it"
        }),
        (Intent.Savings, new[] {
            "unde pot sa economisesc", "cum economisesc", "cum pot sa strang bani", "ce pot sa tai", "da mi sfaturi",
            "where can i save", "how can i save money", "give me tips", "cum cheltui mai putin", "vreau sa strang bani"
        }),
        (Intent.WorkHours, new[] {
            "cate ore de munca m au costat cheltuielile", "cate ore am muncit pentru", "in ore de munca",
            "cat ma costa in ore", "how many work hours", "hours of work", "how many hours did i work for this"
        })
    ];
}
