namespace ExpenseTracker.Api.Services.Banking;

// Shapes of the Enable Banking API (https://enablebanking.com/docs/api/reference/).
// JSON uses snake_case; the client maps it to these PascalCase records.

public record EbAspsp(string Name, string Country, string? Logo, List<string>? PsuTypes, int? MaximumConsentValidity);

public record EbAspspList(List<EbAspsp> Aspsps);

public record EbAspspRef(string Name, string Country);

public record EbAccess(DateTimeOffset ValidUntil);

public record EbAuthRequest(EbAccess Access, EbAspspRef Aspsp, string State, string RedirectUrl, string PsuType, string? Language);

public record EbAuthResponse(string Url, string? AuthorizationId);

public record EbSessionRequest(string Code);

public record EbAccountId(string? Iban);

public record EbAccount(string Uid, EbAccountId? AccountId, string? Currency, string? Name, string? Product);

public record EbSessionResponse(string SessionId, List<EbAccount>? Accounts, EbAspspRef? Aspsp, EbAccess? Access);

public record EbAmount(string Amount, string Currency);

public record EbParty(string? Name);

public record EbTransaction(
    string? EntryReference,
    string? TransactionId,
    EbAmount TransactionAmount,
    string? CreditDebitIndicator,
    string? Status,
    string? BookingDate,
    string? ValueDate,
    string? TransactionDate,
    List<string>? RemittanceInformation,
    EbParty? Creditor,
    EbParty? Debtor);

public record EbTransactionsResponse(List<EbTransaction>? Transactions, string? ContinuationKey);

public record EbError(string? Message, string? Error, string? Detail);

/// <summary>An error returned by Enable Banking (or a network problem talking to it).</summary>
public class EnableBankingException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;

    /// <summary>401/403 on account data usually means the consent expired or was revoked.</summary>
    public bool ConsentProblem => StatusCode is 401 or 403;
}
