namespace ExpenseTracker.Api.Services.Ai;

public record AiMessage(string Role, string Content);

public interface IAiClient
{
    bool IsConfigured { get; }

    string Model { get; }

    Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        int? maxTokens = null,
        CancellationToken cancellationToken = default);
}

public class AiUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
