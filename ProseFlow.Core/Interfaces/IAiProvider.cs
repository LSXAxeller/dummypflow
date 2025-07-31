namespace ProseFlow.Core.Interfaces;

/// <summary>
/// Defines the contract for an AI provider that can generate text-based responses.
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Gets the unique name of the provider (e.g., "Cloud", "Local").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Asynchronously generates a response from the AI model.
    /// </summary>
    /// <param name="instruction">The system prompt or instruction that guides the AI's behavior.</param>
    /// <param name="input">The user-provided text to be processed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the AI-generated text.</returns>
    Task<string> GenerateResponseAsync(string instruction, string input, CancellationToken cancellationToken);
}