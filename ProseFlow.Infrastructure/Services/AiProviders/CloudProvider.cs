using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Code;
using ProseFlow.Core.Interfaces;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Models;
using ChatMessage = ProseFlow.Core.Models.ChatMessage;

namespace ProseFlow.Infrastructure.Services.AiProviders;

/// <summary>
/// A provider that orchestrates requests across a user-defined, ordered chain of cloud services.
/// It leverages LlmTornado to handle different APIs seamlessly.
/// </summary>
public class CloudProvider(
    CloudProviderManagementService providerService,
    UsageTrackingService usageService,
    ILogger<CloudProvider> logger) : IAiProvider
{
    public string Name => "Cloud";
    public ProviderType Type => ProviderType.Cloud;

    public async Task<AiResponse> GenerateResponseAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken, Guid? sessionId = null)
    {
        var enabledConfigs = (await providerService.GetConfigurationsAsync())
            .Where(c => c.IsEnabled)
            .ToList();

        if (enabledConfigs.Count == 0)
        {
            AppEvents.RequestNotification("No enabled cloud providers are configured. Please add and enable one in settings.", NotificationType.Warning);
            throw new InvalidOperationException("No enabled cloud providers are configured. Please add and enable one in settings.");
        }

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

                long promptTokens = response?.Usage?.PromptTokens ?? 0;
                long completionTokens = response?.Usage?.CompletionTokens ?? 0;

                // Persist monthly aggregate usage
                if (response?.Usage is not null) await usageService.AddUsageAsync(promptTokens, completionTokens);
            
                if (response is { Choices.Count: > 0 } && response.Choices[0].Message != null && !string.IsNullOrWhiteSpace(response.Choices[0].Message!.Content))
                    return new AiResponse(response.Choices[0].Message!.Content!, promptTokens, completionTokens, config.Name);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Provider '{config.Name}' failed: {ex.Message}. Trying next provider...";
                AppEvents.RequestNotification(errorMessage, NotificationType.Warning);
                logger.LogError(errorMessage);
            }
        }
        
        logger.LogError("All configured cloud providers failed to return a valid response.");
        throw new InvalidOperationException("All configured cloud providers failed to return a valid response.");
    }
    
    private LLmProviders MapToLlmTornadoProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.OpenAi => LLmProviders.OpenAi,
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