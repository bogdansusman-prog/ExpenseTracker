namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>What a question asks for.</summary>
public enum Intent
{
    Unknown,
    Greeting,
    Thanks,
    Identity,
    Joke,
    Help,
    Overview,
    SpendTotal,
    IncomeTotal,
    Count,
    TopCategories,
    TopMerchants,
    Biggest,
    Compare,
    Average,
    LastTime,
    Balance,
    Subscriptions,
    Forecast,
    Regret,
    Savings,
    WorkHours
}
