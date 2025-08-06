using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.Events;
using ProseFlow.Core.Interfaces;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.DTOs;
using ProseFlow.Application.Interfaces;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Services;

public class ActionOrchestrationService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOsService _osService;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;
    private readonly ILocalSessionService _localSessionService;
    private readonly ILogger<ActionOrchestrationService> _logger;


    public ActionOrchestrationService(IServiceScopeFactory scopeFactory, IOsService osService,
        IEnumerable<IAiProvider> providers, ILocalSessionService localSessionService, ILogger<ActionOrchestrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _osService = osService;
        _localSessionService = localSessionService;
        _providers = providers.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public void Initialize()
    {
        _osService.ActionMenuHotkeyPressed += async () => await HandleActionMenuHotkeyAsync();
        _osService.SmartPasteHotkeyPressed += async () => await HandleSmartPasteHotkeyAsync();
    }

    private async Task HandleActionMenuHotkeyAsync()
    {
        var activeAppContext = await _osService.GetActiveWindowProcessNameAsync();
        var allActions = await ExecuteQueryAsync(unitOfWork => unitOfWork.Actions.GetAllOrderedAsync());

        // Filter actions based on context
        var availableActions = allActions
            .Where(a => a.ApplicationContext.Count == 0 ||
                        a.ApplicationContext.Contains(activeAppContext, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (availableActions.Count == 0)
        {
            AppEvents.RequestNotification("No actions available for the current application.",
                NotificationType.Warning);
            return;
        }

        var request = await AppEvents.RequestFloatingMenuAsync(availableActions, activeAppContext);
        if (request is not null)
            await ProcessRequestAsync(request);
    }

    private async Task HandleSmartPasteHotkeyAsync()
    {
        var result = await ExecuteQueryAsync(async unitOfWork =>
        {
            var settings = await unitOfWork.Settings.GetGeneralSettingsAsync();
            if (settings.SmartPasteActionId is null)
            {
                return new { Action = (Action?)null, IsConfigured = false };
            }

            var action = (await unitOfWork.Actions
                .GetByExpressionAsync(a => a.Id == settings.SmartPasteActionId.Value)).FirstOrDefault();
            
            return new { Action = action, IsConfigured = true };
        });

        if (!result.IsConfigured)
        {
            AppEvents.RequestNotification("Smart Paste action not configured in settings.", NotificationType.Warning);
            return;
        }

        if (result.Action is null)
        {
            AppEvents.RequestNotification("The configured Smart Paste action was not found.", NotificationType.Error);
            return;
        }

        var request = new ActionExecutionRequest(result.Action, result.Action.OpenInWindow, null);
        await ProcessRequestAsync(request);
    }

    private async Task ProcessRequestAsync(ActionExecutionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        AppEvents.RequestNotification("Processing...", NotificationType.Info);

        // Local stateful session ID (If local provider is used)
        Guid? localSessionId = null; 
        
        try
        {
            var userInput = await _osService.GetSelectedTextAsync();
            if (string.IsNullOrWhiteSpace(userInput))
            {
                AppEvents.RequestNotification("No text selected or clipboard is empty.", NotificationType.Warning);
                return;
            }

            // Initialize the conversation transcript
            var conversationHistory = new List<ChatMessage>();

            // Add the system prompt (the main rules). This stays constant.
            var systemInstruction = request.ActionToExecute.ExplainChanges
                ? $"{request.ActionToExecute.Instruction}\n\nIMPORTANT: After your main response, add a section that starts with '---EXPLANATION---' and explain the changes you made."
                : request.ActionToExecute.Instruction;
            conversationHistory.Add(new ChatMessage("system", systemInstruction));

            // Add the initial user input
            var initialUserContent = $"{request.ActionToExecute.Prefix}{userInput}";
            conversationHistory.Add(new ChatMessage("user", initialUserContent));


            if (request.ForceOpenInWindow || request.ActionToExecute.OpenInWindow)
            {
                // Windowed processing
                while (true)
                {
                    var provider = await GetProviderAsync(request.ProviderOverride);
                    if (provider is null)
                    {
                        AppEvents.RequestNotification("No valid AI provider configured.", NotificationType.Error);
                        return;
                    }
                    
                    if ((provider.Type == ProviderType.Local || provider.Name == "Local") && localSessionId is null)
                    {
                        // If this is the first turn in a windowed local session. Create a new session.
                        localSessionId = _localSessionService.StartSession();
                        if (localSessionId is null)
                        {
                            AppEvents.RequestNotification("Failed to start a local model session.", NotificationType.Error);
                            return;
                        }
                    }
                    
                    // Start a stopwatch for the provider to get latency
                    var providerStopwatch = Stopwatch.StartNew();

                    // Call the provider with the entire conversation history
                    var (aiOutput, promptTokens, completionTokens, providerName, tokensPerSecond) = await provider.GenerateResponseAsync(conversationHistory, CancellationToken.None, localSessionId);

                    providerStopwatch.Stop();
                    
                    // Add the provider's response to the history for the next turn
                    conversationHistory.Add(new ChatMessage("assistant", aiOutput));

                    // Log to DB
                    await LogToHistoryAsync(
                        actionName: request.ActionToExecute.Name,
                        providerType: provider.Name,
                        modelUsed: providerName,
                        input: conversationHistory.Last(m => m.Role == "user").Content,
                        output: aiOutput,
                        promptTokens: promptTokens,
                        completionTokens: completionTokens,
                        latencyMs: providerStopwatch.Elapsed.TotalMilliseconds,
                        inferenceSpeed: tokensPerSecond);

                    // Parse and show the result window
                    var (mainOutput, explanation) = ParseOutput(aiOutput, request.ActionToExecute.ExplainChanges);

                    // Show the window and wait for the user to either close it or request a refinement
                    var windowData = new ResultWindowData(request.ActionToExecute.Name, mainOutput, explanation);
                    var refinementRequest = await AppEvents.RequestResultWindowAsync(windowData);

                    if (refinementRequest is null)
                        break; // User closed the window. Exit loop.

                    // User wants to refine. Add their new instruction to the history.
                    conversationHistory.Add(new ChatMessage("user", refinementRequest.NewInstruction));
                }
            }
            else
            {
                // In-Place execution
                var provider = await GetProviderAsync(request.ProviderOverride);
                if (provider is null)
                {
                    AppEvents.RequestNotification("No valid AI provider configured.", NotificationType.Error);
                    return;
                }
                
                // Start a stopwatch for the provider to get latency
                var providerStopwatch = Stopwatch.StartNew();
                
                // Call provider with the initial history [system, user]
                var (output, promptTokens, completionTokens, providerName, tokensPerSecond) = await provider.GenerateResponseAsync(conversationHistory, CancellationToken.None);

                providerStopwatch.Stop();
                await LogToHistoryAsync(
                    actionName: request.ActionToExecute.Name,
                    providerType: provider.Name,
                    modelUsed: providerName,
                    input: initialUserContent,
                    output: output,
                    promptTokens: promptTokens,
                    completionTokens: completionTokens,
                    latencyMs: providerStopwatch.Elapsed.TotalMilliseconds,
                    inferenceSpeed: tokensPerSecond);
                await _osService.PasteTextAsync(output);
            }

            stopwatch.Stop();
            AppEvents.RequestNotification(
                $"'{request.ActionToExecute.Name}' completed in {stopwatch.Elapsed.TotalSeconds:F2}s.",
                NotificationType.Success);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing action: {ActionName}", request.ActionToExecute.Name);
            
            // Provide a user-friendly message but log the detailed exception for debugging.
            var displayMessage = ex is InvalidOperationException ? ex.Message : "An unexpected error occurred.";
            AppEvents.RequestNotification($"Error: {displayMessage}", NotificationType.Error);
        }
        finally
        {
            // Clean up the stateful session if one was created.
            if (localSessionId.HasValue) _localSessionService.EndSession(localSessionId.Value);
        }
    }

    private (string MainOutput, string? Explanation) ParseOutput(string rawOutput, bool expectExplanation)
    {
        if (!expectExplanation || !rawOutput.Contains("---EXPLANATION---")) return (rawOutput.Trim(), null);
        
        var parts = rawOutput.Split(["---EXPLANATION---"], 2, StringSplitOptions.None);
        return (parts[0].Trim(), parts[1].Trim());

    }

    private async Task<IAiProvider?> GetProviderAsync(string? providerOverride)
    {
        var settings = await ExecuteQueryAsync(unitOfWork => unitOfWork.Settings.GetProviderSettingsAsync());

        // Handle runtime user override from the Floating Action Menu
        if (!string.IsNullOrWhiteSpace(providerOverride) &&
            _providers.TryGetValue(providerOverride, out var overriddenProvider))
            return overriddenProvider;

        // Use the primary service type from settings
        if (_providers.TryGetValue(settings.PrimaryServiceType, out var primaryProvider))
            return primaryProvider;

        // Use the fallback service type if primary fails
        if (!_providers.TryGetValue(settings.FallbackServiceType, out var fallbackProvider)) return null;
        
        AppEvents.RequestNotification($"Primary service type '{settings.PrimaryServiceType}' not available. Using fallback.",
            NotificationType.Warning);
        return fallbackProvider;

    }

    private async Task LogToHistoryAsync(string actionName, string providerType, string modelUsed, string input, string output, long promptTokens, long completionTokens, double latencyMs, double inferenceSpeed)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var historyService = scope.ServiceProvider.GetRequiredService<HistoryService>();
            await historyService.AddHistoryEntryAsync(actionName, providerType, modelUsed, input, output, promptTokens, completionTokens, latencyMs, inferenceSpeed);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to log history: {ex.Message}");
            AppEvents.RequestNotification("Failed to log history", NotificationType.Warning);
        }
    }

    public void Dispose()
    {
        _osService.Dispose();
        GC.SuppressFinalize(this);
    }
    
    #region Private Helpers

    /// <summary>
    /// Creates a UoW scope and executes a query.
    /// </summary>
    private async Task<T> ExecuteQueryAsync<T>(Func<IUnitOfWork, Task<T>> query)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await query(unitOfWork);
    }

    #endregion
}