using System.Security.Cryptography;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Analytics;
using ExpenseTracker.Api.Services.QuickAdd;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Api.Services.Banking;

/// <summary>A user-facing problem (bad request), as opposed to a bug.</summary>
public class BankSyncException(string message) : Exception(message);

/// <summary>
/// Connects a bank through Enable Banking and imports its transactions:
/// start consent → bank redirect → session → preview (deduplicated, categorized) → import.
/// </summary>
public class BankSyncService(
    AppDbContext context,
    EnableBankingClient client,
    IOptions<EnableBankingOptions> options,
    AppClock clock)
{
    private static readonly TimeSpan AuthRequestLifetime = TimeSpan.FromMinutes(30);
    private const int FirstSyncDays = 90;
    private const int OverlapDays = 7;

    private readonly EnableBankingOptions _options = options.Value;

    public async Task<BankStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var connections = await context.BankConnections
            .AsNoTracking()
            .Include(connection => connection.Accounts)
            .OrderByDescending(connection => connection.CreatedAt)
            .ToListAsync(cancellationToken);

        return new BankStatusDto(
            client.IsConfigured,
            _options.RedirectUrl,
            connections.Select(ToDto).ToList());
    }

    public async Task<List<BankOptionDto>> GetBanksAsync(string country, CancellationToken cancellationToken)
    {
        var aspsps = await client.GetAspspsAsync(country.ToUpperInvariant(), cancellationToken);

        return aspsps
            .Where(aspsp => aspsp.PsuTypes is null || aspsp.PsuTypes.Contains("personal"))
            .OrderBy(aspsp => aspsp.Name)
            .Select(aspsp => new BankOptionDto(aspsp.Name, aspsp.Country, aspsp.Logo))
            .ToList();
    }

    /// <summary>Returns the bank's login page; the user is sent there by the frontend.</summary>
    public async Task<string> StartAsync(string aspspName, string country, string language, CancellationToken cancellationToken)
    {
        country = country.ToUpperInvariant();

        var aspsp = (await client.GetAspspsAsync(country, cancellationToken))
            .FirstOrDefault(item => item.Name.Equals(aspspName, StringComparison.OrdinalIgnoreCase))
            ?? throw new BankSyncException($"Bank '{aspspName}' is not available in {country}.");

        // Ask for the longest consent the bank allows, but at most ConsentDays.
        var validity = TimeSpan.FromDays(Math.Clamp(_options.ConsentDays, 1, 180));
        if (aspsp.MaximumConsentValidity is int seconds && seconds > 0 && TimeSpan.FromSeconds(seconds) < validity)
        {
            validity = TimeSpan.FromSeconds(seconds);
        }

        // Drop abandoned attempts before starting a new one.
        var cutoff = DateTime.UtcNow - AuthRequestLifetime;
        var stale = await context.BankAuthRequests.Where(request => request.CreatedAt < cutoff).ToListAsync(cancellationToken);
        context.BankAuthRequests.RemoveRange(stale);

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        context.BankAuthRequests.Add(new BankAuthRequest
        {
            State = state,
            AspspName = aspsp.Name,
            AspspCountry = aspsp.Country
        });
        await context.SaveChangesAsync(cancellationToken);

        var response = await client.StartAuthorizationAsync(
            new EbAuthRequest(
                new EbAccess(DateTimeOffset.UtcNow.Add(validity)),
                new EbAspspRef(aspsp.Name, aspsp.Country),
                state,
                _options.RedirectUrl,
                "personal",
                language == "en" ? "en" : "ro"),
            cancellationToken);

        return response.Url;
    }

    /// <summary>The bank sent the user back with a code: turn it into a session and save the accounts.</summary>
    public async Task<BankConnectionDto> CompleteAsync(string code, string state, CancellationToken cancellationToken)
    {
        // The query filter makes sure the state belongs to the logged-in user.
        var request = await context.BankAuthRequests.FirstOrDefaultAsync(item => item.State == state, cancellationToken)
                      ?? throw new BankSyncException("This bank authorization is unknown or was already used. Please try again.");

        context.BankAuthRequests.Remove(request);

        if (request.CreatedAt < DateTime.UtcNow - AuthRequestLifetime)
        {
            await context.SaveChangesAsync(cancellationToken);
            throw new BankSyncException("The bank authorization took too long. Please try again.");
        }

        var session = await client.CreateSessionAsync(code, cancellationToken);

        var connection = new BankConnection
        {
            AspspName = session.Aspsp?.Name ?? request.AspspName,
            AspspCountry = session.Aspsp?.Country ?? request.AspspCountry,
            SessionId = session.SessionId,
            ValidUntil = (session.Access?.ValidUntil ?? DateTimeOffset.UtcNow.AddDays(90)).UtcDateTime
        };

        foreach (var account in session.Accounts ?? new List<EbAccount>())
        {
            connection.Accounts.Add(new BankAccount
            {
                Uid = account.Uid,
                Iban = account.AccountId?.Iban,
                Currency = account.Currency,
                Name = account.Name ?? account.Product
            });
        }

        context.BankConnections.Add(connection);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(connection);
    }

    /// <summary>
    /// Reads the bank transactions and returns them as drafts: already imported ones are flagged,
    /// and each one gets a suggested category (from your own history first, then from keywords).
    /// </summary>
    public async Task<BankPreviewDto> PreviewAsync(int connectionId, int? days, CancellationToken cancellationToken)
    {
        var connection = await FindConnectionAsync(connectionId, cancellationToken);
        EnsureNotExpired(connection);

        var to = clock.Today;
        var from = days is > 0
            ? to.AddDays(-Math.Min(days.Value, 730))
            : connection.LastSyncedAt is { } last
                ? DateOnly.FromDateTime(last).AddDays(-OverlapDays)
                : to.AddDays(-FirstSyncDays);

        var categories = await context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryOption(category.Id, category.Name))
            .ToListAsync(cancellationToken);

        var history = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Description != null)
            .OrderByDescending(transaction => transaction.Date)
            .Select(transaction => new { transaction.Description, transaction.CategoryId })
            .Take(2000)
            .ToListAsync(cancellationToken);

        // Most recent category used for the same (normalized) description, e.g. "lidl oradea" → Mancare.
        var learned = new Dictionary<string, int>();
        foreach (var item in history)
        {
            var key = TextNormalizer.GroupingKey(item.Description);
            if (key.Length > 0)
            {
                learned.TryAdd(key, item.CategoryId);
            }
        }

        var drafts = new List<(BankDraft Draft, BankAccount Account)>();

        foreach (var account in connection.Accounts)
        {
            List<EbTransaction> transactions;
            try
            {
                transactions = await client.GetTransactionsAsync(account.Uid, from, to, cancellationToken);
            }
            catch (EnableBankingException exception) when (exception.ConsentProblem)
            {
                throw new BankSyncException("The bank no longer accepts this connection (the consent expired or was revoked). Please reconnect the bank.");
            }

            foreach (var transaction in transactions)
            {
                if (BankTransactionMapper.Map(transaction, account.Uid) is { } draft)
                {
                    drafts.Add((draft, account));
                }
            }
        }

        var ids = drafts.Select(item => item.Draft.ExternalId).Distinct().ToList();
        var imported = (await context.Transactions
                .Where(transaction => transaction.ExternalId != null && ids.Contains(transaction.ExternalId))
                .Select(transaction => transaction.ExternalId!)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var items = drafts
            .GroupBy(item => item.Draft.ExternalId)
            .Select(group => group.First())
            .OrderByDescending(item => item.Draft.Date)
            .Select(item => new BankPreviewItemDto(
                item.Draft.ExternalId,
                item.Draft.Date,
                item.Draft.Amount,
                item.Draft.Type,
                item.Draft.Description,
                item.Draft.Currency,
                BankTransactionMapper.MaskIban(item.Account.Iban) ?? item.Account.Name,
                Suggest(item.Draft, categories, learned),
                imported.Contains(item.Draft.ExternalId)))
            .ToList();

        return new BankPreviewDto(from, to, items);
    }

    public async Task<BankImportResultDto> ImportAsync(int connectionId, List<BankImportItemDto> items, CancellationToken cancellationToken)
    {
        var connection = await FindConnectionAsync(connectionId, cancellationToken);

        var prefixes = connection.Accounts
            .Select(account => $"{BankTransactionMapper.ExternalIdPrefix}{account.Uid}:")
            .ToList();

        var categoryIds = (await context.Categories.Select(category => category.Id).ToListAsync(cancellationToken)).ToHashSet();

        var ids = items.Select(item => item.ExternalId).ToList();
        var existing = (await context.Transactions
                .Where(transaction => transaction.ExternalId != null && ids.Contains(transaction.ExternalId))
                .Select(transaction => transaction.ExternalId!)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var imported = 0;
        var skipped = 0;

        foreach (var item in items)
        {
            var valid = item.Amount > 0
                        && Enum.IsDefined(item.Type)
                        && categoryIds.Contains(item.CategoryId)
                        && prefixes.Any(prefix => item.ExternalId.StartsWith(prefix, StringComparison.Ordinal));

            if (!valid || !existing.Add(item.ExternalId))
            {
                skipped++;
                continue;
            }

            context.Transactions.Add(new FinancialTransaction
            {
                Amount = Math.Round(item.Amount, 2),
                Date = clock.LocalDayToUtc(item.Date),
                Type = item.Type,
                CategoryId = item.CategoryId,
                Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                ExternalId = item.ExternalId
            });
            imported++;
        }

        connection.LastSyncedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new BankImportResultDto(imported, skipped);
    }

    public async Task DisconnectAsync(int connectionId, CancellationToken cancellationToken)
    {
        var connection = await FindConnectionAsync(connectionId, cancellationToken);

        try
        {
            await client.DeleteSessionAsync(connection.SessionId, cancellationToken);
        }
        catch (EnableBankingException)
        {
            // Expired or already revoked: removing it locally is still what the user wants.
        }

        // Imported transactions stay; only the link to the bank is removed.
        context.BankConnections.Remove(connection);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static int? Suggest(BankDraft draft, IReadOnlyList<CategoryOption> categories, Dictionary<string, int> learned)
    {
        var key = TextNormalizer.GroupingKey(draft.Description);
        if (key.Length > 0 && learned.TryGetValue(key, out var categoryId))
        {
            return categoryId;
        }

        var text = draft.Type == TransactionType.Income ? $"{draft.Description} venit" : draft.Description;
        return NaturalLanguageParser.SuggestCategory(text, categories)?.Id;
    }

    private async Task<BankConnection> FindConnectionAsync(int id, CancellationToken cancellationToken) =>
        await context.BankConnections
            .Include(connection => connection.Accounts)
            .FirstOrDefaultAsync(connection => connection.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException();

    private static void EnsureNotExpired(BankConnection connection)
    {
        if (connection.ValidUntil <= DateTime.UtcNow)
        {
            throw new BankSyncException("The consent for this bank expired. Please reconnect the bank.");
        }
    }

    private static BankConnectionDto ToDto(BankConnection connection) => new(
        connection.Id,
        connection.AspspName,
        connection.AspspCountry,
        DateTime.SpecifyKind(connection.ValidUntil, DateTimeKind.Utc),
        connection.ValidUntil <= DateTime.UtcNow,
        connection.CreatedAt,
        connection.LastSyncedAt,
        connection.Accounts
            .OrderBy(account => account.Id)
            .Select(account => new BankAccountDto(account.Id, BankTransactionMapper.MaskIban(account.Iban), account.Currency, account.Name))
            .ToList());
}
