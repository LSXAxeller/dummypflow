using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Code;
using ProseFlow.Core.Interfaces;
using System.Diagnostics;
using ProseFlow.Core.Enums;
using ProseFlow.Infrastructure.Services.Database;
using ChatMessage = ProseFlow.Core.Models.ChatMessage;

namespace ProseFlow.Infrastructure.Services.AiProviders;

/// <summary>
/// A provider that orchestrates requests across a user-defined, ordered chain of cloud services.
/// It leverages LlmTornado to handle different APIs seamlessly.
/// </summary>
public class CloudProvider(CloudProviderManagementService providerService) : IAiProvider
{
    public string Name => "Cloud";

    public async Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var enabledConfigs = (await providerService.GetConfigurationsAsync())
            .Where(c => c.IsEnabled)
            .ToList();

        if (enabledConfigs.Count == 0)
            throw new InvalidOperationException("No enabled cloud providers are configured. Please add and enable one in settings.");

        var authentications = enabledConfigs.Select(config =>
        {
            var provider = MapToLlmTornadoProvider(config.ProviderType);
            return new ProviderAuthentication(provider, config.ApiKey);
        }).ToList();
        
        var api = new TornadoApi(authentications);
        
        // Create the LlmTornado message list from DTO
        var tornadoMessages = messages.Select(m => new LlmTornado.Chat.ChatMessage(
            role: m.Role switch {
                "system" => ChatMessageRoles.System,
                "user" => ChatMessageRoles.User,
                "assistant" => ChatMessageRoles.Assistant,
                _ => ChatMessageRoles.User
            },
            content: m.Content
        )).ToList();

        foreach (var config in enabledConfigs)
        {
            try
            {
                var request = new ChatRequest
                {
                    Model = config.Model,
                    Temperature = config.Temperature,
                    Messages = tornadoMessages,
                    CancellationToken = cancellationToken
                };
                
                // If a custom BaseUrl is provided, override the TornadoApi instance for this specific call.
                var conversationApi = !string.IsNullOrWhiteSpace(config.BaseUrl)
                    ? new TornadoApi(new Uri(config.BaseUrl), config.ApiKey)
                    : api;
                
                
                var response = await conversationApi.Chat.CreateChatCompletion(request);
            
                if (response is { Choices.Count: > 0  } && response.Choices[0].Message != null && !string.IsNullOrWhiteSpace(response.Choices[0].Message!.Content))
                    return response.Choices[0].Message!.Content!;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Provider '{config.Name}' failed: {ex.Message}. Trying next provider...";
                Debug.WriteLine($"[ERROR] {errorMessage}");
                // TODO: Inform the user about the provider failure
                // AppEvents.RequestNotification(errorMessage, NotificationType.Warning);
                // Continue to the next provider in the chain
            }
        }
        
        throw new InvalidOperationException("All configured cloud providers failed to return a valid response.");
    }
    
    private LLmProviders MapToLlmTornadoProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.OpenAI => LLmProviders.OpenAi,
            ProviderType.Groq => LLmProviders.Groq,
            ProviderType.Anthropic => LLmProviders.Anthropic,
            ProviderType.Google => LLmProviders.Google,
            ProviderType.Mistral => LLmProviders.Mistral,
            ProviderType.Perplexity => LLmProviders.Perplexity,
            ProviderType.OpenRouter => LLmProviders.OpenRouter,
            ProviderType.Custom => LLmProviders.Custom,
            ProviderType.Cohere => LLmProviders.Cohere,
            ProviderType.DeepInfra => LLmProviders.DeepInfra,
            ProviderType.DeepSeek => LLmProviders.DeepSeek,
            ProviderType.Voyage => LLmProviders.Voyage,
            ProviderType.XAi => LLmProviders.XAi,
            ProviderType.Local => throw new InvalidOperationException($"Local LLMs are not supported in {nameof(CloudProvider)}, Use {nameof(LocalProvider)}."),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType), $"Unsupported provider type: {providerType}")
        };
    }
}