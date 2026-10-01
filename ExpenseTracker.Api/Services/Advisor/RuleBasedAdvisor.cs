using ExpenseTracker.Api.Services.Analytics;

namespace ExpenseTracker.Api.Services.Advisor;

/// <summary>
/// "Pseudo-AI" financial advisor: a transparent scoring model built from plain calculations.
/// The 0-100 health score is the sum of six components (savings rate, spending pace,
/// forecast risk, purchase regret, subscriptions, safety buffer), and every strength,
/// concern and action is generated from the same numbers, so each sentence can be explained.
/// </summary>
public static class RuleBasedAdvisor
{
    public const string EngineName = "Ax rules engine v1";

    private sealed record Candidate(AdvisorAction Action, int Priority);

    public static AdvisorReport Generate(FinancialSnapshot snapshot, string language)
    {
        var t = new Texts(language);
        var now = snapshot.GeneratedAtLocal;
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var dayOfMonth = Math.Max(1, now.Day);
        var monthProgress = (decimal)dayOfMonth / daysInMonth;

        var activeMonths = new[] { snapshot.ThisMonth }
            .Concat(snapshot.PreviousMonths)
            .Where(month => month.Income > 0 || month.Expenses > 0)
            .ToList();

        if (snapshot.TransactionCount < 5 || activeMonths.Count == 0)
        {
            return InsufficientData(snapshot, t);
        }

        var strengths = new List<string>();
        var concerns = new List<string>();
        var candidates = new List<Candidate>();
        var breakdown = new List<ScoreComponent>();

        var totalIncome = activeMonths.Sum(month => month.Income);
        var totalExpenses = activeMonths.Sum(month => month.Expenses);
        var averageIncome = totalIncome / activeMonths.Count;
        var averageExpenses = totalExpenses / activeMonths.Count;

        // ---------- 1. Savings rate (35 points) ----------
        double? savingsRate = totalIncome > 0
            ? Math.Round((double)((totalIncome - totalExpenses) / totalIncome) * 100, 1)
            : null;

        int savingsPoints;
        string savingsExplanation;

        if (savingsRate is null)
        {
            savingsPoints = 10;
            savingsExplanation = t.T("nu exista venituri inregistrate", "no income recorded");
            concerns.Add(t.T(
                "Nu ai venituri inregistrate, asa ca nu pot calcula cat economisesti. Adauga salariul sau alte incasari.",
                "You have no income recorded, so I cannot compute your savings rate. Add your salary or other income."));
            candidates.Add(new Candidate(new AdvisorAction(
                t.T("Inregistreaza si veniturile", "Record your income too"),
                t.T("Fara venituri, scorul si prognoza sunt incomplete. Adauga salariul ca tranzactie de tip Venit.",
                    "Without income, the score and forecast are incomplete. Add your salary as an Income transaction."),
                null), 90));
        }
        else
        {
            var rate = savingsRate.Value;
            savingsExplanation = t.T($"economisesti {t.Percent(rate)} din venit", $"you save {t.Percent(rate)} of income");

            if (rate >= 20)
            {
                savingsPoints = 35;
                strengths.Add(t.T(
                    $"Economisesti {t.Percent(rate)} din venit in ultimele {activeMonths.Count} luni, peste pragul recomandat de 20%.",
                    $"You saved {t.Percent(rate)} of your income over the last {activeMonths.Count} months, above the recommended 20%."));
            }
            else if (rate >= 10)
            {
                savingsPoints = 25 + (int)Math.Round(rate - 10);
                strengths.Add(t.T(
                    $"Economisesti {t.Percent(rate)} din venit. E un ritm bun, iar urmatoarea tinta e 20%.",
                    $"You save {t.Percent(rate)} of your income. That is a good pace; the next target is 20%."));
            }
            else if (rate >= 0)
            {
                savingsPoints = 10 + (int)Math.Round(rate * 1.5);
                concerns.Add(t.T(
                    $"Economisesti doar {t.Percent(rate)} din venit. Recomandarea este de cel putin 10-20%.",
                    $"You only save {t.Percent(rate)} of your income. The usual recommendation is at least 10-20%."));
            }
            else
            {
                savingsPoints = Math.Max(0, 10 + (int)Math.Round(rate / 2));
                concerns.Add(t.T(
                    $"Cheltui mai mult decat castigi: {t.Money(totalExpenses - totalIncome)} peste venituri in ultimele {activeMonths.Count} luni.",
                    $"You spend more than you earn: {t.Money(totalExpenses - totalIncome)} over your income in the last {activeMonths.Count} months."));
            }

            if (rate < 10 && averageIncome > 0)
            {
                var target = Math.Round(averageIncome * 0.10m, 0);
                var gap = Math.Max(0, target - (averageIncome - averageExpenses));

                candidates.Add(new Candidate(new AdvisorAction(
                    t.T("Economiseste automat 10% din venit", "Automatically save 10% of your income"),
                    t.T($"In ziua salariului muta {t.Money(target)} intr-un cont separat, inainte sa apuci sa-i cheltui.",
                        $"On payday, move {t.Money(target)} to a separate account before you get the chance to spend it."),
                    gap > 0 ? Math.Round(gap, 0) : null), 70));
            }
        }

        breakdown.Add(new ScoreComponent("savings", t.T("Rata de economisire", "Savings rate"), savingsPoints, 35, savingsExplanation));

        // ---------- 2. Spending pace vs. your own average (15 points) ----------
        var previousWithSpending = snapshot.PreviousMonths.Where(month => month.Expenses > 0).ToList();
        var averagePrevious = previousWithSpending.Count > 0 ? previousWithSpending.Average(month => month.Expenses) : 0m;
        var projectedThisMonth = Math.Round(snapshot.ThisMonth.Expenses / monthProgress, 0);

        int pacePoints;
        string paceExplanation;

        if (averagePrevious > 0 && dayOfMonth >= 5)
        {
            var change = (double)((projectedThisMonth - averagePrevious) / averagePrevious * 100);
            paceExplanation = t.T(
                $"ritm de {t.Money(projectedThisMonth)} / luna vs media {t.Money(averagePrevious)}",
                $"pace of {t.Money(projectedThisMonth)} / month vs average {t.Money(averagePrevious)}");

            if (change <= -10)
            {
                pacePoints = 15;
                strengths.Add(t.T(
                    $"Luna asta cheltui cu {t.Percent(-change)} mai putin decat media ta din lunile trecute.",
                    $"This month you are spending {t.Percent(-change)} less than your recent average."));
            }
            else if (change <= 10)
            {
                pacePoints = 11;
                strengths.Add(t.T(
                    "Cheltuielile sunt stabile fata de lunile trecute, fara derapaje.",
                    "Your spending is stable compared to previous months."));
            }
            else if (change <= 30)
            {
                pacePoints = 5;
                concerns.Add(t.T(
                    $"La ritmul actual vei cheltui ~{t.Money(projectedThisMonth)} luna asta, cu {t.Percent(change)} peste media ta ({t.Money(averagePrevious)}).",
                    $"At the current pace you will spend ~{t.Money(projectedThisMonth)} this month, {t.Percent(change)} above your average ({t.Money(averagePrevious)})."));
            }
            else
            {
                pacePoints = 0;
                concerns.Add(t.T(
                    $"Cheltuielile au luat-o razna: ritmul lunii ({t.Money(projectedThisMonth)}) e cu {t.Percent(change)} peste media ta.",
                    $"Spending is out of control: this month's pace ({t.Money(projectedThisMonth)}) is {t.Percent(change)} above your average."));
            }
        }
        else
        {
            pacePoints = 8;
            paceExplanation = t.T("prea devreme in luna sau fara istoric", "too early in the month or no history");
        }

        breakdown.Add(new ScoreComponent("pace", t.T("Ritmul cheltuielilor", "Spending pace"), pacePoints, 15, paceExplanation));

        // Category spikes → concrete actions with real savings
        if (dayOfMonth >= 7)
        {
            var spikes = snapshot.CategoryTrends
                .Where(trend => trend.AverageLast3Months > 0)
                .Select(trend => new
                {
                    trend.CategoryName,
                    Average = trend.AverageLast3Months,
                    Projected = Math.Round(trend.ThisMonth / monthProgress, 0)
                })
                .Where(trend => trend.Projected > trend.Average * 1.3m && trend.Projected - trend.Average >= 50)
                .OrderByDescending(trend => trend.Projected - trend.Average)
                .Take(2);

            foreach (var spike in spikes)
            {
                candidates.Add(new Candidate(new AdvisorAction(
                    t.T($"Tine in frau categoria {spike.CategoryName}", $"Rein in {spike.CategoryName}"),
                    t.T($"La ritmul actual ajungi la ~{t.Money(spike.Projected)} pe {spike.CategoryName} luna asta, fata de media ta de {t.Money(spike.Average)}. Pune-ti un plafon saptamanal de {t.Money(Math.Round(spike.Average / 4, 0))}.",
                        $"At this pace you will spend ~{t.Money(spike.Projected)} on {spike.CategoryName} this month vs. your average of {t.Money(spike.Average)}. Set a weekly cap of {t.Money(Math.Round(spike.Average / 4, 0))}."),
                    spike.Projected - spike.Average), 80));
            }
        }

        // ---------- 3. Forecast risk (20 points) ----------
        var forecast = snapshot.Forecast;
        var risk = forecast.ProbabilityNegative;
        var riskPoints = risk <= 0.01 ? 20 : risk <= 0.05 ? 16 : risk <= 0.20 ? 10 : risk <= 0.50 ? 4 : 0;

        if (risk <= 0.05)
        {
            strengths.Add(t.T(
                $"Prognoza Monte Carlo pe 30 de zile iti da un risc de sold negativ de doar {t.Percent(risk * 100)}.",
                $"The 30-day Monte Carlo forecast gives you only a {t.Percent(risk * 100)} risk of a negative balance."));
        }
        else if (risk > 0.20)
        {
            concerns.Add(t.T(
                $"Ai {t.Percent(risk * 100)} sanse sa ajungi pe minus in urmatoarele 30 de zile (scenariul pesimist: {t.Money(forecast.EndPessimistic)}).",
                $"There is a {t.Percent(risk * 100)} chance of going negative in the next 30 days (pessimistic case: {t.Money(forecast.EndPessimistic)})."));

            candidates.Add(new Candidate(new AdvisorAction(
                t.T("Construieste un fond de siguranta", "Build a safety buffer"),
                t.T($"Tinta minima: {t.Money(Math.Round(averageExpenses, 0))}, adica o luna de cheltuieli. Pana atunci, amana cumparaturile mari care nu sunt urgente.",
                    $"Minimum target: {t.Money(Math.Round(averageExpenses, 0))}, one month of expenses. Until then, postpone big non-urgent purchases."),
                null), 85));
        }

        breakdown.Add(new ScoreComponent("forecast", t.T("Riscul din prognoza", "Forecast risk"), riskPoints, 20,
            t.T($"{t.Percent(risk * 100)} sanse de sold negativ in 30 de zile", $"{t.Percent(risk * 100)} chance of a negative balance in 30 days")));

        // ---------- 4. Purchase regret (15 points) ----------
        var regret = snapshot.Regret;
        int regretPoints;
        string regretExplanation;

        if (regret.RatedCount >= 5)
        {
            regretExplanation = t.T($"scor mediu {t.Number(regret.AverageScore)}/5 din {regret.RatedCount} evaluari",
                $"average {t.Number(regret.AverageScore)}/5 from {regret.RatedCount} ratings");

            regretPoints = regret.AverageScore >= 4 ? 15 : regret.AverageScore >= 3 ? 10 : regret.AverageScore >= 2 ? 5 : 0;

            if (regret.AverageScore >= 4)
            {
                strengths.Add(t.T(
                    $"Cheltui constient: cumparaturile tale primesc in medie {t.Number(regret.AverageScore)}/5 la „A meritat?”.",
                    $"You spend mindfully: your purchases score {t.Number(regret.AverageScore)}/5 on average on \"Was it worth it?\"."));
            }
            else if (regret.AverageScore < 3)
            {
                concerns.Add(t.T(
                    $"Regreti multe cumparaturi: scorul mediu e {t.Number(regret.AverageScore)}/5.",
                    $"You regret many purchases: the average score is {t.Number(regret.AverageScore)}/5."));
            }
        }
        else
        {
            regretPoints = 8;
            regretExplanation = t.T("prea putine evaluari (minim 5)", "not enough ratings (min. 5)");
            candidates.Add(new Candidate(new AdvisorAction(
                t.T("Evalueaza-ti cheltuielile", "Rate your expenses"),
                t.T("In Insights → „A meritat?” noteaza cateva cheltuieli. Cu 5+ evaluari iti pot arata exact unde iti pierzi banii.",
                    "In Insights → \"Was it worth it?\" rate a few expenses. With 5+ ratings I can show exactly where your money leaks."),
                null), 20));
        }

        if (regret.WorstCategory is not null)
        {
            var category = regret.ByCategory.FirstOrDefault(item => item.CategoryName == regret.WorstCategory);
            var dayText = regret.WorstDayOfWeek is { } day ? t.T($", mai ales {t.Day(day)}", $", especially on {t.Day(day)}") : "";

            concerns.Add(t.T(
                $"Cele mai multe regrete vin din categoria {regret.WorstCategory}{dayText}.",
                $"Most of your regrets come from {regret.WorstCategory}{dayText}."));

            var monthlyRegret = regret.RegrettedAmountThisMonth > 0
                ? regret.RegrettedAmountThisMonth
                : Math.Round((category?.RegrettedAmount ?? 0) / 3, 0);

            candidates.Add(new Candidate(new AdvisorAction(
                t.T($"Regula de 24 de ore pentru {regret.WorstCategory}", $"24-hour rule for {regret.WorstCategory}"),
                t.T($"Inainte de orice cheltuiala la {regret.WorstCategory}{dayText}, asteapta o zi. Doar luna asta ai regretat {t.Money(regret.RegrettedAmountThisMonth)}.",
                    $"Before any {regret.WorstCategory} purchase{dayText}, wait one day. This month alone you regretted {t.Money(regret.RegrettedAmountThisMonth)}."),
                monthlyRegret > 0 ? monthlyRegret : null), 75));
        }

        breakdown.Add(new ScoreComponent("regret", t.T("Cumparaturi care merita", "Purchases worth it"), regretPoints, 15, regretExplanation));

        // ---------- 5. Subscriptions (10 points) ----------
        var subscriptions = snapshot.Subscriptions;
        var subscriptionsMonthly = subscriptions.Sum(subscription => subscription.MonthlyCost);
        int subscriptionPoints;
        string subscriptionExplanation;

        if (subscriptions.Count == 0)
        {
            subscriptionPoints = 10;
            subscriptionExplanation = t.T("niciun abonament detectat", "no subscriptions detected");
        }
        else if (averageIncome > 0)
        {
            var share = (double)(subscriptionsMonthly / averageIncome) * 100;
            subscriptionPoints = share <= 10 ? 10 : share <= 20 ? 6 : 2;
            subscriptionExplanation = t.T($"{t.Money(subscriptionsMonthly)} / luna = {t.Percent(share)} din venit",
                $"{t.Money(subscriptionsMonthly)} / month = {t.Percent(share)} of income");

            if (share <= 10)
            {
                strengths.Add(t.T(
                    $"Abonamentele sunt sub control: {t.Money(subscriptionsMonthly)} pe luna ({t.Percent(share)} din venit).",
                    $"Subscriptions are under control: {t.Money(subscriptionsMonthly)} per month ({t.Percent(share)} of income)."));
            }
            else
            {
                concerns.Add(t.T(
                    $"Abonamentele iau {t.Percent(share)} din venit: {t.Money(subscriptionsMonthly)} pe luna, adica {t.Money(subscriptionsMonthly * 12)} pe an.",
                    $"Subscriptions take {t.Percent(share)} of your income: {t.Money(subscriptionsMonthly)} per month, {t.Money(subscriptionsMonthly * 12)} per year."));

                var biggest = subscriptions.OrderByDescending(subscription => subscription.MonthlyCost).First();
                candidates.Add(new Candidate(new AdvisorAction(
                    t.T($"Revizuieste abonamentul {biggest.Name}", $"Review the {biggest.Name} subscription"),
                    t.T($"Este cel mai scump abonament recurent ({t.Money(biggest.MonthlyCost)} / luna). Daca nu-l folosesti saptamanal, anuleaza-l sau treci pe un plan mai ieftin.",
                        $"It is your most expensive recurring payment ({t.Money(biggest.MonthlyCost)} / month). If you don't use it weekly, cancel it or downgrade."),
                    biggest.MonthlyCost), 60));
            }
        }
        else
        {
            subscriptionPoints = 5;
            subscriptionExplanation = t.T($"{t.Money(subscriptionsMonthly)} / luna", $"{t.Money(subscriptionsMonthly)} / month");
        }

        foreach (var increased in subscriptions.Where(subscription => subscription.PriceIncreased))
        {
            subscriptionPoints = Math.Max(0, subscriptionPoints - 2);
            concerns.Add(t.T(
                $"{increased.Name} s-a scumpit cu {t.Percent((double)increased.PriceChangePercent)} ({t.Money(increased.PreviousAmount ?? 0)} → {t.Money(increased.CurrentAmount)}) si probabil nici n-ai observat.",
                $"{increased.Name} went up by {t.Percent((double)increased.PriceChangePercent)} ({t.Money(increased.PreviousAmount ?? 0)} → {t.Money(increased.CurrentAmount)}), probably without you noticing."));

            candidates.Add(new Candidate(new AdvisorAction(
                t.T($"Verifica scumpirea la {increased.Name}", $"Check the {increased.Name} price increase"),
                t.T($"Cauta un plan mai ieftin sau o oferta. Altfel, platesti {t.Money(increased.CurrentAmount - (increased.PreviousAmount ?? increased.CurrentAmount))} in plus la fiecare plata.",
                    $"Look for a cheaper plan or a deal. Otherwise you pay {t.Money(increased.CurrentAmount - (increased.PreviousAmount ?? increased.CurrentAmount))} more every time."),
                increased.MonthlyCost), 65));
        }

        breakdown.Add(new ScoreComponent("subscriptions", t.T("Abonamente", "Subscriptions"), subscriptionPoints, 10, subscriptionExplanation));

        // ---------- 6. Safety buffer (5 points) ----------
        var bufferMonths = averageExpenses > 0 ? (double)(snapshot.CurrentBalance / averageExpenses) : 0;
        var bufferPoints = bufferMonths >= 3 ? 5 : bufferMonths >= 1 ? 3 : 0;

        if (bufferMonths >= 3)
        {
            strengths.Add(t.T(
                $"Soldul tau acopera {t.Number(bufferMonths)} luni de cheltuieli, un fond de urgenta solid.",
                $"Your balance covers {t.Number(bufferMonths)} months of expenses, a solid emergency fund."));
        }
        else if (bufferMonths < 1 && averageExpenses > 0)
        {
            concerns.Add(t.T(
                "Soldul acopera mai putin de o luna de cheltuieli, deci orice neprevazut te poate pune in dificultate.",
                "Your balance covers less than one month of expenses, so any surprise could hurt."));
        }

        breakdown.Add(new ScoreComponent("buffer", t.T("Fond de siguranta", "Safety buffer"), bufferPoints, 5,
            t.T($"soldul acopera {t.Number(bufferMonths)} luni de cheltuieli", $"balance covers {t.Number(bufferMonths)} months of expenses")));

        // ---------- Final score ----------
        var score = Math.Clamp(breakdown.Sum(component => component.Points), 0, 100);
        var verdict = score >= 70 ? "good" : score >= 45 ? "ok" : "bad";

        if (strengths.Count == 0)
        {
            strengths.Add(t.T("Iti urmaresti cheltuielile, si asta e primul pas spre control.",
                "You track your spending, which is the first step to being in control."));
        }

        if (concerns.Count == 0)
        {
            concerns.Add(t.T("Nu vad probleme majore in datele tale. Continua asa!",
                "I don't see any major problems in your data. Keep it up!"));
        }

        return new AdvisorReport(
            Score: score,
            Verdict: verdict,
            Headline: Headline(verdict, savingsRate, t),
            Summary: Summary(snapshot, t, dayOfMonth, averagePrevious, projectedThisMonth),
            Strengths: strengths.Take(4).ToList(),
            Concerns: concerns.Take(4).ToList(),
            Actions: PickActions(candidates, snapshot, t),
            FunFact: FunFact(snapshot, t),
            Breakdown: breakdown,
            Language: t.Language,
            GeneratedAt: DateTime.UtcNow,
            Engine: EngineName);
    }

    private static string Headline(string verdict, double? savingsRate, Texts t) => verdict switch
    {
        "good" => savingsRate is { } rate
            ? t.T($"Stai bine: pui deoparte {t.Percent(rate)} din ce castigi.", $"You're doing well: you keep {t.Percent(rate)} of what you earn.")
            : t.T("Stai bine, continua asa.", "You're doing well, keep it up."),
        "ok" => t.T("Esti pe linia de plutire, dar ai cateva scurgeri de bani de oprit.",
            "You're staying afloat, but there are a few money leaks to fix."),
        _ => t.T("Atentie: banii tai o iau pe un drum gresit. Hai sa-l corectam.",
            "Warning: your money is heading the wrong way. Let's fix it.")
    };

    private static string Summary(FinancialSnapshot snapshot, Texts t, int dayOfMonth, decimal averagePrevious, decimal projected)
    {
        var parts = new List<string>
        {
            t.T($"Luna aceasta (pana pe {dayOfMonth}) ai incasat {t.Money(snapshot.ThisMonth.Income)} si ai cheltuit {t.Money(snapshot.ThisMonth.Expenses)}.",
                $"This month (up to day {dayOfMonth}) you earned {t.Money(snapshot.ThisMonth.Income)} and spent {t.Money(snapshot.ThisMonth.Expenses)}.")
        };

        if (averagePrevious > 0 && dayOfMonth >= 5)
        {
            parts.Add(t.T($"La ritmul actual, luna se incheie cu ~{t.Money(projected)} cheltuieli, fata de media ta de {t.Money(averagePrevious)}.",
                $"At the current pace the month will end at ~{t.Money(projected)} in expenses, vs. your average of {t.Money(averagePrevious)}."));
        }

        var forecast = snapshot.Forecast;
        parts.Add(t.T($"Peste 30 de zile vei avea cel mai probabil {t.Money(forecast.EndExpected)} (intre {t.Money(forecast.EndPessimistic)} si {t.Money(forecast.EndOptimistic)}).",
            $"In 30 days you will most likely have {t.Money(forecast.EndExpected)} (between {t.Money(forecast.EndPessimistic)} and {t.Money(forecast.EndOptimistic)})."));

        if (snapshot.ThisMonthExpensesInWorkHours is { } hours && hours > 0)
        {
            parts.Add(t.T($"Cheltuielile lunii te-au costat {t.Number((double)hours)} ore de munca.",
                $"This month's spending cost you {t.Number((double)hours)} hours of work."));
        }

        return string.Join(' ', parts);
    }

    private static IReadOnlyList<AdvisorAction> PickActions(List<Candidate> candidates, FinancialSnapshot snapshot, Texts t)
    {
        // Actions with a measurable saving first (biggest saving wins), then the rest by priority.
        var ordered = candidates
            .OrderByDescending(candidate => candidate.Action.EstimatedMonthlySavings.HasValue)
            .ThenByDescending(candidate => candidate.Action.EstimatedMonthlySavings ?? 0)
            .ThenByDescending(candidate => candidate.Priority)
            .Select(candidate => candidate.Action)
            .GroupBy(action => action.Title)
            .Select(group => group.First())
            .ToList();

        if (ordered.Count < 3 && snapshot.HourlyRate is null)
        {
            ordered.Add(new AdvisorAction(
                t.T("Afla cate ore de munca te costa fiecare cheltuiala", "See how many work hours each expense costs"),
                t.T("Seteaza venitul pe ora in Setari. „Pizza = 2 ore de munca” schimba felul in care cheltui.",
                    "Set your hourly rate in Settings. \"Pizza = 2 hours of work\" changes the way you spend."),
                null));
        }

        var topCategory = snapshot.CategoryTrends.FirstOrDefault();
        if (ordered.Count < 3 && topCategory is not null && topCategory.ThisMonth > 0)
        {
            ordered.Add(new AdvisorAction(
                t.T($"Fa-ti un buget pentru {topCategory.CategoryName}", $"Set a budget for {topCategory.CategoryName}"),
                t.T($"E categoria pe care cheltui cel mai mult luna asta ({t.Money(topCategory.ThisMonth)}). Un plafon cu 10% mai mic ar insemna {t.Money(Math.Round(topCategory.ThisMonth * 0.1m, 0))} economisiti.",
                    $"It's your biggest category this month ({t.Money(topCategory.ThisMonth)}). A cap 10% lower would save {t.Money(Math.Round(topCategory.ThisMonth * 0.1m, 0))}."),
                Math.Round(topCategory.ThisMonth * 0.1m, 0)));
        }

        if (ordered.Count < 3)
        {
            ordered.Add(new AdvisorAction(
                t.T("Verifica Insights saptamanal", "Check Insights weekly"),
                t.T("5 minute pe saptamana: evalueaza cheltuielile si uita-te la prognoza.",
                    "5 minutes a week: rate your expenses and look at the forecast."),
                null));
        }

        return ordered.Take(3).ToList();
    }

    private static string? FunFact(FinancialSnapshot snapshot, Texts t)
    {
        var biggest = snapshot.BiggestExpensesThisMonth.FirstOrDefault();

        if (biggest is not null && snapshot.HourlyRate is > 0m)
        {
            var hours = Math.Round(biggest.Amount / snapshot.HourlyRate.Value, 1);
            var name = string.IsNullOrWhiteSpace(biggest.Description) ? biggest.CategoryName : biggest.Description;

            return t.T($"Cea mai mare cheltuiala a lunii, „{name}” ({t.Money(biggest.Amount)}), te-a costat {t.Number((double)hours)} ore de munca.",
                $"Your biggest expense this month, \"{name}\" ({t.Money(biggest.Amount)}), cost you {t.Number((double)hours)} hours of work.");
        }

        var totalThisMonth = snapshot.ThisMonth.Expenses;
        var top = snapshot.CategoryTrends.FirstOrDefault();

        if (top is not null && totalThisMonth > 0 && top.ThisMonth > 0)
        {
            var share = (double)(top.ThisMonth / totalThisMonth) * 100;
            return t.T($"{t.Percent(share)} din cheltuielile lunii au mers pe {top.CategoryName}.",
                $"{t.Percent(share)} of this month's spending went to {top.CategoryName}.");
        }

        var yearly = snapshot.Subscriptions.Sum(subscription => subscription.YearlyCost);
        return yearly > 0
            ? t.T($"Abonamentele te costa {t.Money(yearly)} pe an.", $"Your subscriptions cost {t.Money(yearly)} per year.")
            : null;
    }

    private static AdvisorReport InsufficientData(FinancialSnapshot snapshot, Texts t) => new(
        Score: 50,
        Verdict: "ok",
        Headline: t.T("Am nevoie de mai multe date ca sa te pot judeca corect.", "I need more data to judge your finances fairly."),
        Summary: t.T($"Ai doar {snapshot.TransactionCount} tranzactii inregistrate. Adauga venituri si cheltuieli pentru cel putin cateva saptamani, iar analiza devine personala.",
            $"You only have {snapshot.TransactionCount} transactions. Add income and expenses for at least a few weeks and the analysis becomes personal."),
        Strengths: [t.T("Ai inceput sa-ti urmaresti banii, si asta e cel mai greu pas.", "You started tracking your money, which is the hardest step.")],
        Concerns: [t.T("Prea putine tranzactii pentru concluzii sigure.", "Too few transactions for reliable conclusions.")],
        Actions:
        [
            new AdvisorAction(t.T("Adauga tranzactiile din ultima luna", "Add last month's transactions"),
                t.T("Foloseste adaugarea rapida de pe Dashboard: „ieri 45 lei pizza”.", "Use quick add on the Dashboard: \"yesterday 45 lei pizza\"."), null),
            new AdvisorAction(t.T("Inregistreaza si veniturile", "Record your income too"),
                t.T("Fara venituri nu pot calcula rata de economisire.", "Without income I cannot compute your savings rate."), null),
            new AdvisorAction(t.T("Seteaza venitul pe ora", "Set your hourly rate"),
                t.T("Din Setari, ca sa vezi cheltuielile in ore de munca.", "In Settings, to see expenses in hours of work."), null)
        ],
        FunFact: null,
        Breakdown: [],
        Language: t.Language,
        GeneratedAt: DateTime.UtcNow,
        Engine: EngineName);
}
