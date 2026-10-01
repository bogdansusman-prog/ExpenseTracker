namespace ExpenseTracker.Api.Services.Ai;

/// <summary>
/// Bound from the "Anthropic" configuration section.
/// Keep the key out of source control:
/// <c>dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."</c>
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-sonnet-4-5";

    public int MaxTokens { get; set; } = 1500;

    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
}
