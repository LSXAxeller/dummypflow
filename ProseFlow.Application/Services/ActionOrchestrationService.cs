using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.Events;
using ProseFlow.Core.Interfaces;
using ProseFlow.Infrastructure.Data;
using System.Diagnostics;
using ProseFlow.Application.DTOs;
using ProseFlow.Core.Models;

namespace ProseFlow.Application.Services;

public class ActionOrchestrationService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOsService _osService;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;

    public ActionOrchestrationService(IServiceScopeFactory scopeFactory, IOsService osService,
        IEnumerable<IAiProvider> providers)
    {
        _scopeFactory = scopeFactory;
        _osService = osService;
        // Create a dictionary for easy provider lookup by name
        _providers = providers.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
    }

    public void Initialize()
    {
        _osService.ActionMenuHotkeyPressed += async () => await HandleActionMenuHotkeyAsync();
        _osService.SmartPasteHotkeyPressed += async () => await HandleSmartPasteHotkeyAsync();
    }

    private async Task HandleActionMenuHotkeyAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activeAppContext = await _osService.GetActiveWindowProcessNameAsync();
        var allActions = await dbContext.Actions.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync();

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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await dbContext.GeneralSettings.AsNoTracking().FirstAsync();

        if (settings.SmartPasteActionId is null)
        {
            AppEvents.RequestNotification("Smart Paste action not configured in settings.", NotificationType.Warning);
            return;
        }

        var action = await dbContext.Actions.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == settings.SmartPasteActionId);
        if (action is null)
        {
            AppEvents.RequestNotification("The configured Smart Paste action was not found.", NotificationType.Error);
            return;
        }

        var request = new ActionExecutionRequest(action, action.OpenInWindow, null);
        await ProcessRequestAsync(request);
    }

    private async Task ProcessRequestAsync(ActionExecutionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        AppEvents.RequestNotification("Processing...", NotificationType.Info);

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

                    // Call the provider with the entire conversation history
                    var aiOutput = await provider.GenerateResponseAsync(conversationHistory, CancellationToken.None);

                    // Add the provider's response to the history for the next turn
                    conversationHistory.Add(new ChatMessage("assistant", aiOutput));

                    // Log to DB
                    await LogToHistoryAsync(request.ActionToExecute.Name, provider.Name,
                        conversationHistory.Last(m => m.Role == "user").Content, aiOutput);

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

                // Call provider with the initial history [system, user]
                var output = await provider.GenerateResponseAsync(conversationHistory, CancellationToken.None);

                await LogToHistoryAsync(request.ActionToExecute.Name, provider.Name, initialUserContent, output);
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
            // Provide a user-friendly message but log the detailed exception for debugging.
            var displayMessage = ex is InvalidOperationException ? ex.Message : "An unexpected error occurred.";
            AppEvents.RequestNotification($"Error: {displayMessage}", NotificationType.Error);
            Debug.WriteLine($"[ERROR] Processing failed: {ex}");
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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await dbContext.ProviderSettings.AsNoTracking().FirstAsync();

        // 1. Handle runtime user override from the Floating Action Menu
        if (!string.IsNullOrWhiteSpace(providerOverride) &&
            _providers.TryGetValue(providerOverride, out var overriddenProvider))
            return overriddenProvider;

        // 2. Use the primary service type from settings
        if (_providers.TryGetValue(settings.PrimaryServiceType, out var primaryProvider))
            return primaryProvider;

        // 3. Use the fallback service type if primary fails
        if (!_providers.TryGetValue(settings.FallbackServiceType, out var fallbackProvider)) return null;
        
        AppEvents.RequestNotification($"Primary service type '{settings.PrimaryServiceType}' not available. Using fallback.",
            NotificationType.Warning);
        return fallbackProvider;

    }

    private async Task LogToHistoryAsync(string actionName, string providerName, string input, string output)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var historyService = scope.ServiceProvider.GetRequiredService<HistoryService>();
            await historyService.AddHistoryEntryAsync(actionName, providerName, input, output);
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
}