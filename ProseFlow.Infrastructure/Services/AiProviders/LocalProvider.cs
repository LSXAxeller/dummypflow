using System.Text;
using LLama;
using LLama.Batched;
using LLama.Sampling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Data;

namespace ProseFlow.Infrastructure.Services.AiProviders;

/// <summary>
/// An AI provider that uses a local model via the LLamaSharp library and a BatchedExecutor.
/// It supports both stateless and stateful (session-based) inference.
/// </summary>
public class LocalProvider(
    ILogger<LocalProvider> logger,
    LocalModelManagerService modelManager,
    LocalSessionService sessionService,
    IDbContextFactory<AppDbContext> dbContextFactory) : IAiProvider
{
    public string Name => "Local";
    public ProviderType Type => ProviderType.Local;

    public async Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken, Guid? sessionId = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.ProviderSettings.FindAsync([1], cancellationToken: cancellationToken)
                       ?? throw new InvalidOperationException("Provider settings not found in the database.");

        if (!modelManager.IsLoaded || modelManager.Executor is null)
            await modelManager.LoadModelAsync(settings);

        if (!modelManager.IsLoaded || modelManager.Executor is null)
        {
            var errorMessage = $"Local model is not loaded. Status: {modelManager.Status}. Error: {modelManager.ErrorMessage}";
            logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var executor = modelManager.Executor;

        // Determine if we are using a persistent session or creating a temporary one.
        Conversation? conversation;
        bool isTemporarySession;

        if (sessionId.HasValue)
        {
            conversation = sessionService.GetSession(sessionId.Value);
            isTemporarySession = false;
        }
        else
        {
            conversation = executor.Create();
            isTemporarySession = true;
        }
        
        if (conversation is null)
            throw new InvalidOperationException($"Could not find or create a local conversation session (ID: {sessionId}).");
        
        try
        {
            // Format the prompt
            var formattedPrompt = BuildPrompt(messages, executor.Model, isNewSession: conversation.TokenCount == 0);
            
            // Prompt the model
            var tokens = executor.Context.Tokenize(formattedPrompt);
            conversation.Prompt(tokens);
            
            // Perform the inference loop
            var responseBuilder = new StringBuilder();
            var sampler = new DefaultSamplingPipeline { Temperature = settings.LocalModelTemperature };
            var decoder = new StreamingTokenDecoder(executor.Context);
            
            var maxTokensToGenerate = settings.LocalModelMaxTokens;
            for (var i = 0; i < maxTokensToGenerate; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                await executor.Infer(cancellationToken);

                if (!conversation.RequiresSampling) continue;
                
                var token = sampler.Sample(executor.Context.NativeHandle, conversation.GetSampleIndex());
                if (token.IsEndOfGeneration(executor.Model.NativeHandle)) break;
                
                decoder.Add(token);
                responseBuilder.Append(decoder.Read());
                
                conversation.Prompt(token);
            }
            
            logger.LogInformation("Local inference completed, generated {CharCount} characters.", responseBuilder.Length);
            return responseBuilder.ToString().Trim();
        }
        finally
        {
            // If we created a temporary conversation, ensure it's disposed of immediately.
            if (isTemporarySession) conversation.Dispose();
        }
    }

    /// <summary>
    /// Builds a prompt string from chat history using ChatML format.
    /// </summary>
    /// <param name="messages">The list of chat messages.</param>
    /// <param name="model">The loaded LLamaWeights model containing the template.</param>
    /// <param name="isNewSession">If true, the entire history is rendered. If false, only the last user message is rendered.</param>
    private string BuildPrompt(IEnumerable<ChatMessage> messages, LLamaWeights model, bool isNewSession)
    {
        var messageList = messages.ToList();
        var template = new LLamaTemplate(model.NativeHandle);
        
        if (isNewSession)
        {
            // For a new conversation, build the entire history.
            foreach (var message in messageList)
            {
                template.Add(message.Role, message.Content);
            }
        }
        else
        {
            // For an existing conversation, just append the latest user message.
            var lastUserMessage = messageList.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (lastUserMessage != null) template.Add("user", lastUserMessage.Content);
        }
        
        // Always add the start token for the assistant's turn.
        template.AddAssistant = true;

        var result = Encoding.UTF8.GetString(template.Apply());
        template.Clear();
        
        return result;
    }
}