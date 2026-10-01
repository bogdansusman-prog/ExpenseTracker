# Smart Insights & AI Advisor

This update makes Expense Tracker look at **how** you spend money, not just how much.

## Features

| Feature | What it does | How it works |
|---|---|---|
| **"Was it worth it?" (regret score)** | 7 days after an expense, the app asks you to rate it from 1 (total regret) to 5 (absolutely worth it). | Ratings are stored on the transaction. `RegretAnalyzer` builds a category × weekday heatmap and finds where you regret spending most, e.g. "Food on Fridays". |
| **Price in work hours** | Every expense can be shown as hours of your life, e.g. "Sneakers = 14 h of work". | Set your net hourly rate (or monthly salary) in *Settings*. |
| **Subscription detective** | Finds recurring payments automatically and flags **silent price increases**. | `SubscriptionDetector` groups similar expenses by normalized description (or category + amount), checks for a regular rhythm (weekly … yearly, median interval with tolerance) and compares the latest amount with the previous one. |
| **Monte Carlo forecast** | Shows a pessimistic / expected / optimistic balance band for the next 30–90 days, plus the risk of going negative. | `BalanceForecaster` resamples the last 90 days of real daily cash flow (bootstrap) over 2,000 simulations and takes the 10th, 50th and 90th percentiles. |
| **Quick add in natural language** | Type `ieri 45 lei pizza cu Andrei` or `salary 4500 yesterday` and confirm the draft. | `NaturalLanguageParser` (offline, RO + EN): amounts with a decimal comma, dates (`azi`, `ieri`, weekdays, `15.09`), income keywords, keyword → category mapping. When it cannot find the amount or the category, Claude fills in the gaps. |
| **AI advisor "Owl"** | A 0–100 financial health score, a verdict, strengths, concerns and 3 concrete actions, plus a chat about your own data. | `AdvisorService` sends Claude an anonymized numeric snapshot: monthly totals, category trends, regret stats, subscriptions and the forecast. The answer is structured JSON and is cached for 15 minutes. Romanian or English is set in *Settings*. |

## API

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/insights/regret/pending` | Expenses 7–45 days old that have not been rated yet |
| POST | `/api/insights/regret/{id}` | `{ "score": 1-5 }` |
| GET | `/api/insights/regret/summary` | Heatmap, worst category and weekday, regretted amount |
| GET | `/api/insights/subscriptions` | Detected recurring payments |
| GET | `/api/insights/forecast?days=30` | Monte Carlo forecast |
| GET | `/api/insights/snapshot` | The data the AI advisor sees |
| POST | `/api/quickadd/parse` | `{ "text": "ieri 45 lei pizza", "useAi": true }` returns a draft (not saved) |
| GET | `/api/advisor/status` | Whether the AI is configured |
| GET | `/api/advisor/report?language=ro&refresh=false` | AI report |
| POST | `/api/advisor/chat` | `{ "messages": [{ "role": "user", "content": "..." }], "language": "ro" }` |
| GET/PUT | `/api/settings` | `{ "hourlyRate": 25, "language": "ro" }` |

## Setup

```bash
cd ExpenseTracker.Api

# 1. Database migration (adds RegretScore, RegretRatedAt and the Settings table)
dotnet ef migrations add SmartInsights
dotnet ef database update

# 2. Claude API key (optional: everything except the AI advisor works without it)
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
# Optional: choose another model
dotnet user-secrets set "Anthropic:Model" "claude-sonnet-4-5"

dotnet run
```

The key is stored in .NET User Secrets and is never committed.

## Tests

```bash
dotnet test
```

The `ExpenseTracker.Tests` project covers the pure algorithms: subscription detection, the Monte Carlo forecast, the natural-language parser and the regret analysis.

## Privacy

Only aggregated numbers, category names and transaction descriptions are sent to the AI, with no names or e-mails. The quick-add parser works fully offline and uses the AI only for the fields it could not fill in itself.
