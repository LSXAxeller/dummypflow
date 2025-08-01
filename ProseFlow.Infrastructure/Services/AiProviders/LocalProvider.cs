using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;

namespace ProseFlow.Infrastructure.Services.AiProviders;

// TODO: Implement a real local model engine (e.g., LLamaSharp)
/// <summary>
/// Placeholder implementation for a local Large Language Model provider.
/// This class simulates the behavior of a local model for development and testing purposes.
/// </summary>
public class LocalProvider : IAiProvider
{
    public string Name => "Local";

    public Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var chatMessages = messages.ToArray();
        var placeholderResponse =
            $"""
             --- LOCAL LLM PROVIDER (PLACEHOLDER) ---
             This is a mock response. The local model engine is not yet implemented.

             Instruction Received:
             ---------------------
             {chatMessages.FirstOrDefault()?.Content}

             Input Received:
             ---------------
             {chatMessages.LastOrDefault()?.Content}
             """;

        return Task.FromResult(placeholderResponse);
    }
}