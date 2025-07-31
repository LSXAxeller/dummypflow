namespace ProseFlow.Core.Enums;

/// <summary>
/// Represents the types of AI providers available in the application.
/// </summary>
public enum ProviderType
{
    OpenAI,
    Anthropic,
    Cohere,
    DeepInfra,
    DeepSeek,
    Google,
    Groq,
    Mistral,
    OpenRouter,
    Perplexity,
    Voyage,
    XAi,
    Custom, // For generic OpenAI-compatible endpoints
    Local
}