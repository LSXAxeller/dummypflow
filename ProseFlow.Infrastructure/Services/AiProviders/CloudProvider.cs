using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Code;
using ProseFlow.Core.Interfaces;
using System.Diagnostics;
using ProseFlow.Core.Enums;
using ProseFlow.Infrastructure.Services.Database;

namespace ProseFlow.Infrastructure.Services.AiProviders;

/// <summary>
/// A provider that orchestrates requests across a user-defined, ordered chain of cloud services.
/// It leverages LlmTornado to handle different APIs seamlessly.
/// </summary>
public class CloudProvider(CloudProviderManagementService providerService) : IAiProvider
{
    public string Name => "Cloud";

    public async Task<string> GenerateResponseAsync(string instruction, string input, CancellationToken cancellationToken)
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

        foreach (var config in enabledConfigs)
        {
            try
            {
                var request = new ChatRequest
                {
                    Model = config.Model,
                    Temperature = config.Temperature,
                };
                
                // If a custom BaseUrl is provided, override the TornadoApi instance for this specific call.
                var conversationApi = !string.IsNullOrWhiteSpace(config.BaseUrl)
                    ? new TornadoApi(new Uri(config.BaseUrl), config.ApiKey)
                    : api;
                
                var conversation = conversationApi.Chat.CreateConversation(request);
                conversation.AppendSystemMessage(instruction);
                conversation.AppendUserInput(input);
                
                var response = await conversation.GetResponse(cancellationToken);
                
                if (!string.IsNullOrWhiteSpace(response))
                    return response;
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