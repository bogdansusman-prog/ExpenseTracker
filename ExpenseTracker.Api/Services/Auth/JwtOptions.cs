namespace ExpenseTracker.Api.Services.Auth;

/// <summary>
/// Bound from the "Jwt" configuration section. Set the signing key with:
/// <c>dotnet user-secrets set "Jwt:Key" "a-long-random-secret-of-at-least-32-characters"</c>
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? Key { get; set; }

    public string Issuer { get; set; } = "ExpenseTracker";

    public string Audience { get; set; } = "ExpenseTracker.Client";

    public int ExpiresInHours { get; set; } = 12;
}
