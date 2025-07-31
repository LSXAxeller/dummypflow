using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Code;
using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Interfaces;
using ProseFlow.Infrastructure.Data;
using ProseFlow.Infrastructure.Security;

namespace ProseFlow.Infrastructure.Services.AiProviders;

/// <summary>
/// A unified AI provider implementation using the LlmTornado library.
/// This single provider can handle various cloud APIs (OpenAI, Anthropic, etc.)
/// and local servers (like Ollama) by configuring it based on user settings.
/// </summary>
public class CloudProvider(IDbContextFactory<AppDbContext> dbContextFactory, ApiKeyProtector apiKeyProtector) : IAiProvider
{
    public string Name => "Cloud";

    public async Task<string> GenerateResponseAsync(string instruction, string input, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.ProviderSettings.FindAsync([1], cancellationToken: cancellationToken)
                       ?? throw new InvalidOperationException("Provider settings not found in the database.");
        
        // LlmTornado can be initialized with multiple keys. It will automatically
        // select the correct key based on the requested model.
        var authentications = new List<ProviderAuthentication>();

        // Add Cloud key if it exists
        if (!string.IsNullOrWhiteSpace(settings.CloudApiKey))
        {
            var decryptedApiKey = apiKeyProtector.Unprotect(settings.CloudApiKey);
            authentications.Add(new ProviderAuthentication(LLmProviders.OpenAi, decryptedApiKey));
        }
            
        // TODO: Add support for more providers
        
        // NOTE: To support more providers, simply add their keys here.
        // For example:
        // if (!string.IsNullOrWhiteSpace(settings.AnthropicApiKey))
        // {
        //     var decryptedKey = apiKeyProtector.Unprotect(settings.AnthropicApiKey);
        //     authentications.Add(new ProviderAuthentication(LLmProviders.Anthropic, decryptedKey));
        // }

        if (authentications.Count == 0 && string.IsNullOrEmpty(settings.BaseUrl))
            throw new InvalidOperationException("No cloud API keys are configured. Please add one in settings.");
            
        var api = string.IsNullOrEmpty(settings.BaseUrl) ? new TornadoApi(authentications) : new TornadoApi(new Uri(settings.BaseUrl), settings.CloudApiKey);
        var model = settings.CloudModel;

        try
        {
            var conversation = api.Chat.CreateConversation(new ChatRequest
            {
                Model = model,
                Temperature = settings.CloudTemperature,
            });

            conversation.AppendSystemMessage(instruction);
            conversation.AppendUserInput(input);

            var response = await conversation.GetResponse(cancellationToken);
            
            return response ?? string.Empty;
        }
        catch (Exception ex)
        {
            // LlmTornado may throw its own specific exceptions, but a general catch
            // provides a good fallback for any provider communication errors.
            throw new InvalidOperationException($"Failed to get response from provider for model '{model}'. Reason: {ex.Message}", ex);
        }
    }
}