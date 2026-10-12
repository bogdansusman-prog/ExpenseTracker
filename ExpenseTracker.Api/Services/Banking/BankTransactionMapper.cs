using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services.Banking;

/// <summary>A bank transaction turned into something Ax Tracker understands (not saved yet).</summary>
public record BankDraft(
    string ExternalId,
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string Description,
    string? Counterparty,
    string Currency);

/// <summary>
/// Pure mapping from Enable Banking transactions to drafts: amount and direction, date,
/// a readable description and a stable id used to never import the same transaction twice.
/// </summary>
public static partial class BankTransactionMapper
{
    public const string ExternalIdPrefix = "eb:";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // BCR card payments: "... Locatie: 21309850 RO CARREFOUR MK CLUJ GARA" (sometimes followed by ". Data ...").
    [GeneratedRegex(@"Locatie:\s*\S+\s+[A-Z]{2}\s+(?<merchant>.+?)(?:\.\s*Dat\w*\b.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex CardMerchant();

    /// <summary>Returns null for pending transactions or ones without a usable amount/date.</summary>
    public static BankDraft? Map(EbTransaction transaction, string accountUid)
    {
        // Pending card payments change or disappear; they come back later as booked.
        if (string.Equals(transaction.Status, "PDNG", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!decimal.TryParse(transaction.TransactionAmount?.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var raw)
            || raw == 0)
        {
            return null;
        }

        var date = ParseDate(transaction.BookingDate) ?? ParseDate(transaction.ValueDate) ?? ParseDate(transaction.TransactionDate);
        if (date is null)
        {
            return null;
        }

        var isCredit = transaction.CreditDebitIndicator is { } indicator
            ? indicator.Equals("CRDT", StringComparison.OrdinalIgnoreCase)
            : raw > 0;

        var type = isCredit ? TransactionType.Income : TransactionType.Expense;

        // Money in: who paid us (debtor). Money out: who we paid (creditor).
        var counterparty = Clean(isCredit ? transaction.Debtor?.Name : transaction.Creditor?.Name);
        var remittance = Readable(Clean(string.Join(' ', transaction.RemittanceInformation ?? new List<string>())));

        var description = counterparty ?? remittance ?? (isCredit ? "Incasare" : "Plata card");
        // Add the bank text only when it says something the name doesn't ("SPOTIFY" next to "Spotify AB" adds nothing).
        if (counterparty is not null && remittance is not null
            && !Fold(remittance).Contains(Fold(counterparty))
            && !Fold(counterparty).Contains(Fold(remittance)))
        {
            description = $"{counterparty} – {remittance}";
        }

        return new BankDraft(
            ExternalIdFor(transaction, accountUid, date.Value, raw),
            date.Value,
            Math.Abs(raw),
            type,
            Truncate(description, 200),
            counterparty,
            (transaction.TransactionAmount?.Currency ?? "RON").ToUpperInvariant());
    }

    /// <summary>
    /// "eb:{accountUid}:{bank id}". When the bank gives no id we hash the transaction's content,
    /// which is stable between two syncs of the same data.
    /// </summary>
    public static string ExternalIdFor(EbTransaction transaction, string accountUid, DateOnly date, decimal amount)
    {
        var bankId = transaction.TransactionId ?? transaction.EntryReference;

        if (string.IsNullOrWhiteSpace(bankId))
        {
            var content = string.Join('|',
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                amount.ToString(CultureInfo.InvariantCulture),
                transaction.CreditDebitIndicator,
                transaction.Creditor?.Name,
                transaction.Debtor?.Name,
                string.Join(' ', transaction.RemittanceInformation ?? new List<string>()));

            bankId = "h" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))[..32].ToLowerInvariant();
        }

        return Truncate($"{ExternalIdPrefix}{accountUid}:{bankId.Trim()}", 200);
    }

    /// <summary>"RO49RNCB0082004567890123" → "RO49 •••• 0123".</summary>
    public static string? MaskIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return null;
        }

        var compact = iban.Replace(" ", "");
        return compact.Length <= 8 ? compact : $"{compact[..4]} •••• {compact[^4..]}";
    }

    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>
    /// Long bank texts become the merchant name:
    /// "Apple Pay, Tranzactie comerciant - Tranz: Nr card 4493..., Locatie: 21309850 RO CARREFOUR MK CLUJ GARA"
    /// → "CARREFOUR MK CLUJ GARA". Card numbers, tokens and references are dropped.
    /// </summary>
    public static string? Readable(string? remittance)
    {
        if (remittance is null)
        {
            return null;
        }

        var match = CardMerchant().Match(remittance);
        if (match.Success)
        {
            var merchant = match.Groups["merchant"].Value.Trim().TrimEnd('.', ',');
            if (merchant.Length > 0)
            {
                return merchant;
            }
        }

        return remittance;
    }

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Spaces().Replace(text, " ").Trim();
    }

    private static string Fold(string text) => Analytics.TextNormalizer.Fold(text);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
