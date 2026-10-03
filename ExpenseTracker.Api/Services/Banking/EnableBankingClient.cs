using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseTracker.Api.Services.Banking;

/// <summary>
/// Thin client for the Enable Banking API. Every request is signed with a short-lived JWT (RS256)
/// made from the application's private key; the key never leaves this machine.
/// </summary>
public class EnableBankingClient(HttpClient http, IOptions<EnableBankingOptions> options)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object KeyLock = new();
    private static RsaSecurityKey? _cachedKey;
    private static string? _cachedKeyPath;

    private readonly EnableBankingOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<List<EbAspsp>> GetAspspsAsync(string country, CancellationToken cancellationToken)
    {
        var result = await SendAsync<EbAspspList>(HttpMethod.Get, $"/aspsps?country={Uri.EscapeDataString(country)}&psu_type=personal", null, cancellationToken);
        return result.Aspsps;
    }

    public Task<EbAuthResponse> StartAuthorizationAsync(EbAuthRequest request, CancellationToken cancellationToken) =>
        SendAsync<EbAuthResponse>(HttpMethod.Post, "/auth", request, cancellationToken);

    public Task<EbSessionResponse> CreateSessionAsync(string code, CancellationToken cancellationToken) =>
        SendAsync<EbSessionResponse>(HttpMethod.Post, "/sessions", new EbSessionRequest(code), cancellationToken);

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var response = await RawAsync(HttpMethod.Delete, $"/sessions/{Uri.EscapeDataString(sessionId)}", null, cancellationToken);
        // Already gone on their side is fine: we only want it closed.
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw await ErrorAsync(response, cancellationToken);
        }
    }

    /// <summary>All transactions of an account in [from, to], following the pagination.</summary>
    public async Task<List<EbTransaction>> GetTransactionsAsync(string accountUid, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var all = new List<EbTransaction>();
        string? continuationKey = null;

        for (var page = 0; page < 50; page++)
        {
            var url = $"/accounts/{Uri.EscapeDataString(accountUid)}/transactions?date_from={from:yyyy-MM-dd}&date_to={to:yyyy-MM-dd}";
            if (continuationKey is not null)
            {
                url += $"&continuation_key={Uri.EscapeDataString(continuationKey)}";
            }

            var result = await SendAsync<EbTransactionsResponse>(HttpMethod.Get, url, null, cancellationToken);
            all.AddRange(result.Transactions ?? new List<EbTransaction>());

            continuationKey = result.ContinuationKey;
            if (string.IsNullOrEmpty(continuationKey))
            {
                break;
            }
        }

        return all;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await RawAsync(method, path, body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ErrorAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
               ?? throw new EnableBankingException("Enable Banking returned an empty response.");
    }

    private async Task<HttpResponseMessage> RawAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new EnableBankingException("Enable Banking is not configured.");
        }

        using var request = new HttpRequestMessage(method, _options.BaseUrl.TrimEnd('/') + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new EnableBankingException($"Could not reach Enable Banking: {exception.Message}");
        }
    }

    private static async Task<EnableBankingException> ErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<EbError>(Json, cancellationToken);
            message = error?.Message ?? error?.Detail ?? error?.Error;
        }
        catch (JsonException)
        {
            // not JSON: keep the status code only
        }

        return new EnableBankingException(
            $"Enable Banking error {(int)response.StatusCode}{(message is null ? "" : $": {message}")}",
            (int)response.StatusCode);
    }

    private string CreateJwt()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var credentials = new SigningCredentials(LoadKey(), SecurityAlgorithms.RsaSha256);
        var header = new JwtHeader(credentials);
        header["kid"] = _options.ApplicationId;

        var payload = new JwtPayload
        {
            { "iss", "enablebanking.com" },
            { "aud", "api.enablebanking.com" },
            { "iat", now },
            { "exp", now + 3600 }
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private RsaSecurityKey LoadKey()
    {
        lock (KeyLock)
        {
            if (_cachedKey is not null && _cachedKeyPath == _options.PrivateKeyPath)
            {
                return _cachedKey;
            }

            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(_options.PrivateKeyPath!));

            _cachedKey = new RsaSecurityKey(rsa) { KeyId = _options.ApplicationId };
            _cachedKeyPath = _options.PrivateKeyPath;
            return _cachedKey;
        }
    }
}
