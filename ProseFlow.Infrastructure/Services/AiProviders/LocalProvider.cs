using ProseFlow.Core.Interfaces;

namespace ProseFlow.Infrastructure.Services.AiProviders;

// TODO: Implement a real local model engine (e.g., LLamaSharp)
/// <summary>
/// Placeholder implementation for a local Large Language Model provider.
/// This class simulates the behavior of a local model for development and testing purposes.
/// </summary>
public class LocalProvider : IAiProvider
{
    public string Name => "Local";

    public Task<string> GenerateResponseAsync(string instruction, string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var placeholderResponse =
            $"""
             --- LOCAL LLM PROVIDER (PLACEHOLDER) ---
             This is a mock response. The local model engine is not yet implemented.

             Instruction Received:
             ---------------------
             {instruction}

             Input Received:
             ---------------
             {input}
             """;

        return Task.FromResult(placeholderResponse);
    }
}