# Accounts

Expense Tracker supports multiple users. Every account sees only its own categories, transactions and settings.

## How it works

| Piece | Implementation |
|---|---|
| Users and passwords | **ASP.NET Core Identity** (`AppUser : IdentityUser`). Passwords are hashed with PBKDF2 and stored in the `AspNetUsers` table. |
| Authentication | **JWT bearer tokens**: `POST /api/auth/register` and `POST /api/auth/login` return a signed token that is valid for 12 hours. |
| Data isolation | `Category`, `FinancialTransaction` and `UserSettings` implement `IUserOwned`. `AppDbContext` adds an **EF Core global query filter** (`UserId == CurrentUserId`), so no query can read another user's data, and it sets `UserId` automatically when an entity is created. |
| Protection | `app.MapControllers().RequireAuthorization()`: every endpoint requires a token except register and login. |
| Existing data | Data created before accounts existed is claimed by the **first** account that registers. |
| Angular | `AuthService` (signals plus localStorage session), `authInterceptor` (adds `Authorization: Bearer …` and handles 401), `authGuard` / `guestGuard`, login and register pages. |

## Endpoints

| Method | Endpoint | Body |
|---|---|---|
| POST | `/api/auth/register` | `{ "email", "password", "displayName" }` |
| POST | `/api/auth/login` | `{ "email", "password" }` |
| GET | `/api/auth/me` | — (requires a token) |

Password rules: at least 8 characters, with at least one digit and one lowercase letter.

## Setup

```bash
cd ExpenseTracker.Api   # the API project folder
dotnet ef migrations add Accounts
dotnet ef database update

# JWT signing key (32+ random characters). Without it, a temporary key is generated
# on every run in Development, so you would need to log in again after each restart.
dotnet user-secrets set "Jwt:Key" "<a long random secret>"
```
