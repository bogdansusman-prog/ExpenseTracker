# Bank sync (Enable Banking, PSD2)

Ax Tracker can read the transactions of your bank account (for example **BCR / George**) and import them,
so you don't type them by hand. It uses [Enable Banking](https://enablebanking.com), a licensed PSD2
account-information provider. Access is **read-only**: the app can never make payments, and your bank
password is only ever typed in your bank's own app.

For your own accounts Enable Banking is free ("restricted mode": only the accounts you link yourself are visible).

## One-time setup

1. Create an account on [enablebanking.com](https://enablebanking.com) and open the **Control Panel**.
2. Go to **API applications** → register a new application:
   - Environment: **Production**
   - Redirect URL: `https://localhost:7007/api/bank/return`
     (Enable Banking only accepts https, so the bank returns to the API, which forwards you to
     `http://localhost:4200/bank/callback`)
   - Your browser downloads a private key named `<application id>.pem`.
3. Move the `.pem` file to a folder **outside the repository** (for example `C:\Users\<you>\keys\`).
   `*.pem` is in `.gitignore`, but it is safer to keep it out of the project entirely.
4. In the Control Panel, use **Link accounts** on the application: log in to George and link your BCR account.
   This activates the application in restricted mode for your own accounts.
5. Tell the API about the application (from the `ExpenseTracker.Api/ExpenseTracker.Api` folder):

   ```bash
   dotnet user-secrets set "EnableBanking:ApplicationId" "<application id>"
   dotnet user-secrets set "EnableBanking:PrivateKeyPath" "C:\Users\<you>\keys\<application id>.pem"
   ```

6. Create the database tables and restart the API:

   ```bash
   dotnet ef migrations add BankSync
   dotnet ef database update
   dotnet run --launch-profile https
   ```

## How it works

```text
Bank page ── POST /api/bank/connect ──▶ API ── POST /auth ──▶ Enable Banking
    ◀──────── bank login URL ─────────
User approves read-only access in George
George ──▶ https://localhost:7007/api/bank/return?code=…&state=… ──▶ http://localhost:4200/bank/callback?code=…&state=…
Callback page ── POST /api/bank/callback ──▶ API ── POST /sessions ──▶ accounts saved
Bank page ── GET /api/bank/connections/{id}/preview ──▶ transactions, deduplicated and categorized
User reviews ── POST /api/bank/connections/{id}/import ──▶ transactions saved
```

- Every call to Enable Banking is signed with a short-lived RS256 JWT made from your private key.
- The `state` sent to the bank is random and stored per user, so a bank redirect can only finish the
  connection of the user who started it.
- Each imported transaction keeps an `ExternalId` (`eb:{account}:{bank id}`) with a unique index per user,
  so syncing twice never creates duplicates.
- Categories are suggested from your own history first (same merchant → same category as last time),
  then from the same keyword rules as quick add (`LIDL` → Food, `BOLT` → Transport, `NETFLIX` → Entertainment).
- Pending card payments are skipped; they are imported once the bank books them.
- Consent lasts up to 90 days (or what the bank allows). After that, disconnect and connect again.

## API

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/bank/status` | Is Enable Banking configured, connected banks and accounts |
| GET | `/api/bank/banks?country=RO` | Banks available in a country |
| POST | `/api/bank/connect` | `{ "aspspName": "...", "country": "RO" }` → `{ "url": "<bank login>" }` |
| GET | `/api/bank/return` | Where the bank redirects (https); forwards to the Angular callback page |
| POST | `/api/bank/callback` | `{ "code": "...", "state": "..." }` → the new connection |
| GET | `/api/bank/connections/{id}/preview?days=90` | Bank transactions as drafts (not saved) |
| POST | `/api/bank/connections/{id}/import` | `{ "items": [...] }` → `{ "imported": n, "skipped": n }` |
| DELETE | `/api/bank/connections/{id}` | Closes the consent; imported transactions stay |
