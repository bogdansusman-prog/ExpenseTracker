using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Api.Services.Ai;

/// <summary>
/// Minimal client for the Anthropic Messages API (POST /v1/messages).
/// </summary>
public class ClaudeClient : IAiClient
{
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;
    private readonly ILogger<ClaudeClient> _logger;

    public ClaudeClient(HttpClient http, IOptions<AnthropicOptions> options, ILogger<ClaudeClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(90);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public string Model => _options.Model;

    public async Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new AiUnavailableException(
                "The AI advisor is not configured. Set Anthropic:ApiKey with dotnet user-secrets.");
        }

        var body = new MessagesRequest(
            _options.Model,
            maxTokens ?? _options.MaxTokens,
            systemPrompt,
            messages.Select(message => new MessageDto(message.Role, message.Content)).ToList());

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Claude API request failed");
            throw new AiUnavailableException("Could not reach the Claude API.", exception);
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Claude API returned {Status}: {Body}", (int)response.StatusCode, json);
                throw new AiUnavailableException($"Claude API error ({(int)response.StatusCode}).");
            }

            var parsed = JsonSerializer.Deserialize<MessagesResponse>(json, JsonOptions);
            var text = string.Concat(parsed?.Content?
                .Where(block => block.Type == "text")
                .Select(block => block.Text) ?? []);

            return text.Trim();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record MessagesRequest(string Model, int MaxTokens, string System, List<MessageDto> Messages);

    private sealed record MessageDto(string Role, string Content);

    private sealed record MessagesResponse(List<ContentBlock>? Content);

    private sealed record ContentBlock(string Type, string? Text);
}
