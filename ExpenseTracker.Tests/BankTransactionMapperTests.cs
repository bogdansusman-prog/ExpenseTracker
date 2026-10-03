using System.Text.Json;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Banking;
using ExpenseTracker.Api.Services.QuickAdd;

namespace ExpenseTracker.Tests;

public class BankTransactionMapperTests
{
    private const string Account = "acc-1";

    private static EbTransaction Card(
        string amount = "45.90",
        string? indicator = "DBIT",
        string? status = "BOOK",
        string? id = "T123",
        string? creditor = "LIDL ORADEA",
        string? debtor = null,
        string? remittance = null) =>
        new(
            EntryReference: null,
            TransactionId: id,
            TransactionAmount: new EbAmount(amount, "RON"),
            CreditDebitIndicator: indicator,
            Status: status,
            BookingDate: "2026-09-28",
            ValueDate: "2026-09-27",
            TransactionDate: null,
            RemittanceInformation: remittance is null ? new List<string>() : new List<string> { remittance },
            Creditor: creditor is null ? null : new EbParty(creditor),
            Debtor: debtor is null ? null : new EbParty(debtor));

    [Fact]
    public void Maps_card_payment_to_expense()
    {
        var draft = BankTransactionMapper.Map(Card(remittance: "Cumparare POS LIDL ORADEA"), Account);

        Assert.NotNull(draft);
        Assert.Equal(45.90m, draft.Amount);
        Assert.Equal(TransactionType.Expense, draft.Type);
        Assert.Equal(new DateOnly(2026, 9, 28), draft.Date);
        Assert.Equal("LIDL ORADEA", draft.Description);
        Assert.Equal("eb:acc-1:T123", draft.ExternalId);
        Assert.Equal("RON", draft.Currency);
    }

    [Fact]
    public void Maps_incoming_transfer_to_income_with_payer_name()
    {
        var draft = BankTransactionMapper.Map(
            Card(amount: "4500.00", indicator: "CRDT", creditor: null, debtor: "ACME SRL", remittance: "Salariu septembrie"),
            Account);

        Assert.NotNull(draft);
        Assert.Equal(TransactionType.Income, draft.Type);
        Assert.Equal(4500m, draft.Amount);
        Assert.Equal("ACME SRL – Salariu septembrie", draft.Description);
    }

    [Fact]
    public void Negative_amount_without_indicator_is_an_expense()
    {
        var draft = BankTransactionMapper.Map(Card(amount: "-12.50", indicator: null), Account);

        Assert.NotNull(draft);
        Assert.Equal(TransactionType.Expense, draft.Type);
        Assert.Equal(12.50m, draft.Amount);
    }

    [Fact]
    public void Skips_pending_transactions()
    {
        Assert.Null(BankTransactionMapper.Map(Card(status: "PDNG"), Account));
    }

    [Fact]
    public void Id_without_bank_id_is_stable_and_content_based()
    {
        var first = BankTransactionMapper.Map(Card(id: null, remittance: "Plata factura"), Account);
        var again = BankTransactionMapper.Map(Card(id: null, remittance: "Plata factura"), Account);
        var other = BankTransactionMapper.Map(Card(id: null, amount: "46.00", remittance: "Plata factura"), Account);

        Assert.NotNull(first);
        Assert.StartsWith("eb:acc-1:h", first.ExternalId);
        Assert.Equal(first.ExternalId, again!.ExternalId);
        Assert.NotEqual(first.ExternalId, other!.ExternalId);
    }

    [Theory]
    [InlineData("RO49RNCB0082004567890123", "RO49 •••• 0123")]
    [InlineData("RO49 RNCB 0082 0045 6789 0123", "RO49 •••• 0123")]
    [InlineData(null, null)]
    public void Masks_iban(string? iban, string? expected)
    {
        Assert.Equal(expected, BankTransactionMapper.MaskIban(iban));
    }

    [Fact]
    public void Suggests_category_from_bank_description()
    {
        CategoryOption[] categories = [new(1, "Mâncare"), new(2, "Transport"), new(3, "Divertisment")];

        Assert.Equal(1, NaturalLanguageParser.SuggestCategory("LIDL ORADEA", categories)?.Id);
        Assert.Equal(2, NaturalLanguageParser.SuggestCategory("BOLT.EU/O/2609", categories)?.Id);
        Assert.Equal(3, NaturalLanguageParser.SuggestCategory("NETFLIX.COM", categories)?.Id);
        Assert.Null(NaturalLanguageParser.SuggestCategory("XYZ SRL", categories));
    }

    [Fact]
    public void Reads_enable_banking_json()
    {
        const string json = """
            {
              "transactions": [
                {
                  "entry_reference": "E1",
                  "transaction_amount": { "currency": "RON", "amount": "19.99" },
                  "credit_debit_indicator": "DBIT",
                  "status": "BOOK",
                  "booking_date": "2026-09-15",
                  "remittance_information": ["SPOTIFY"],
                  "creditor": { "name": "Spotify AB" }
                }
              ],
              "continuation_key": null
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        var result = JsonSerializer.Deserialize<EbTransactionsResponse>(json, options);
        var draft = BankTransactionMapper.Map(result!.Transactions![0], Account);

        Assert.NotNull(draft);
        Assert.Equal("eb:acc-1:E1", draft.ExternalId);
        Assert.Equal("Spotify AB", draft.Description);
        Assert.Equal(19.99m, draft.Amount);
    }
}
