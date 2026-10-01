# Smart Insights & Ax, the rule-based financial advisor

This update makes Expense Tracker look at **how** you spend money, not just how much.

## Features

| Feature | What it does | How it works |
|---|---|---|
| **"Was it worth it?" (regret score)** | 7 days after an expense, the app asks you to rate it from 1 (total regret) to 5 (absolutely worth it). | Ratings are stored on the transaction. `RegretAnalyzer` builds a category × weekday heatmap and finds where you regret spending most, e.g. "Food on Fridays". |
| **Price in work hours** | Every expense can be shown as hours of your life, e.g. "Sneakers = 14 h of work". | Set your net hourly rate (or monthly salary) in *Settings*. |
| **Subscription detective** | Finds recurring payments automatically and flags **silent price increases**. | `SubscriptionDetector` groups similar expenses by normalized description (or category + amount), checks for a regular rhythm (weekly … yearly, median interval with tolerance) and compares the latest amount with the previous one. |
| **Monte Carlo forecast** | Shows a pessimistic / expected / optimistic balance band for the next 30–90 days, plus the risk of going negative. | `BalanceForecaster` resamples the last 90 days of real daily cash flow (bootstrap) over 2,000 simulations and takes the 10th, 50th and 90th percentiles. |
| **Quick add in natural language** | Type `ieri 45 lei pizza cu Andrei` or `salary 4500 yesterday` and confirm the draft. | `NaturalLanguageParser` (offline, RO + EN): amounts with a decimal comma, dates (`azi`, `ieri`, weekdays, `15.09`), income keywords, keyword → category mapping. |
| **Ax, the financial advisor (animated axolotl mascot)** | A 0–100 financial health score **with a full breakdown**, a verdict, strengths, concerns and 3 concrete actions with estimated savings, plus a chat about your own data. | `RuleBasedAdvisor`: a transparent scoring model with no external AI (see below). `AdvisorChatEngine` detects the intent of a question with RO/EN keywords and answers with computed numbers. |

## How Ax scores your finances

The score is the sum of six components, so every point can be explained:

| Component | Max | Rule |
|---|---|---|
| Savings rate (last 4 months) | 35 | ≥20% → 35 · 10-20% → 25-35 · 0-10% → 10-25 · negative → 0-10 |
| Spending pace vs. your own average | 15 | This month's spending is projected to the end of the month and compared with the previous 3 months |
| Forecast risk | 20 | Probability of a negative balance in 30 days (from the Monte Carlo forecast) |
| Purchases worth it | 15 | Average "Was it worth it?" score (needs at least 5 ratings) |
| Subscriptions | 10 | Recurring payments as a share of income; −2 for every silent price increase |
| Safety buffer | 5 | How many months of expenses the current balance covers |

Verdict: **good** at 70 or more, **ok** from 45 to 69, **bad** below 45.

Actions are generated from the same data: category spikes (projected vs. average), the most regretted category ("24-hour rule"), price increases, an expensive subscription, automatic 10% saving and a safety buffer. They are ranked by estimated monthly savings.

## API

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/insights/regret/pending` | Expenses 7–45 days old that have not been rated yet |
| POST | `/api/insights/regret/{id}` | `{ "score": 1-5 }` |
| GET | `/api/insights/regret/summary` | Heatmap, worst category and weekday, regretted amount |
| GET | `/api/insights/subscriptions` | Detected recurring payments |
| GET | `/api/insights/forecast?days=30` | Monte Carlo forecast |
| GET | `/api/insights/snapshot` | The data Ax works with |
| POST | `/api/quickadd/parse` | `{ "text": "ieri 45 lei pizza" }` returns a draft (not saved) |
| GET | `/api/advisor/status` | Advisor engine info |
| GET | `/api/advisor/report?language=ro` | Score, breakdown, verdict, strengths, concerns, actions |
| POST | `/api/advisor/chat` | `{ "messages": [{ "role": "user", "content": "..." }], "language": "ro" }` |
| GET/PUT | `/api/settings` | `{ "hourlyRate": 25, "language": "ro" }` |

## Setup

```bash
cd ExpenseTracker.Api   # the API project folder
dotnet ef database update
dotnet run --launch-profile https
```

No API key is needed: everything runs locally.

## Tests

```bash
dotnet test
```

The `ExpenseTracker.Tests` project covers the pure algorithms: subscription detection, the Monte Carlo forecast, the natural-language parser, the regret analysis, the advisor's scoring and the chat intents.

## Privacy

All calculations run on your machine. No data is sent to any external service.

## Ax, the mascot

Ax is an original axolotl character with round golden glasses and a bow tie, drawn as inline SVG (`components/ax-mascot`) and animated with CSS only. The `mood` input switches between six states:

| Mood | When | Animation |
|---|---|---|
| `idle` | default | breathing, blinking, gills and tail swaying |
| `thinking` | while the report or a chat answer is computed | head tilt, eyes look up, paw on chin, thought dots |
| `nodding` | while the answer is typed out in the chat | nods and "talks" |
| `happy` | score ≥ 70 | bounces, ^^ eyes, sparkles |
| `worried` | score < 45 or an error | worried brows, sweat drop, head shake |
| `alert` | something needs your attention (e.g. expenses to rate) | raises a paw, "!" badge |

All animations are disabled when the operating system asks for reduced motion.
