namespace ExpenseTracker.Api.Services.Banking;

/// <summary>
/// Enable Banking settings. Keep them in user secrets, never in appsettings.json:
///   dotnet user-secrets set "EnableBanking:ApplicationId" "&lt;application id&gt;"
///   dotnet user-secrets set "EnableBanking:PrivateKeyPath" "C:\path\outside\the\repo\&lt;application id&gt;.pem"
/// </summary>
public class EnableBankingOptions
{
    public const string SectionName = "EnableBanking";

    public string BaseUrl { get; set; } = "https://api.enablebanking.com";

    /// <summary>Application id from the Enable Banking Control Panel (also the JWT "kid").</summary>
    public string? ApplicationId { get; set; }

    /// <summary>Path to the private key (.pem) downloaded when the application was registered.</summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>Where the bank sends the user back. Must be whitelisted in the Control Panel.</summary>
    public string RedirectUrl { get; set; } = "http://localhost:4200/bank/callback";

    /// <summary>How long we ask the bank to keep the consent (the bank may allow less).</summary>
    public int ConsentDays { get; set; } = 90;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApplicationId)
        && !string.IsNullOrWhiteSpace(PrivateKeyPath)
        && File.Exists(PrivateKeyPath);
}
