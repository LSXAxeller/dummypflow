using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.Events;
using ProseFlow.Core.Interfaces;
using ProseFlow.Infrastructure.Data;
using System.Diagnostics;
using ProseFlow.Application.DTOs;

namespace ProseFlow.Application.Services;

public class ActionOrchestrationService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOsService _osService;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;

    public ActionOrchestrationService(IServiceScopeFactory scopeFactory, IOsService osService, IEnumerable<IAiProvider> providers)
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
            .Where(a => a.ApplicationContext.Count == 0 || a.ApplicationContext.Contains(activeAppContext, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (availableActions.Count == 0)
        {
            AppEvents.RequestNotification("No actions available for the current application.", NotificationType.Warning);
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

        var action = await dbContext.Actions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == settings.SmartPasteActionId);
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
            // 1. Get Input
            var input = await _osService.GetSelectedTextAsync();
            if (string.IsNullOrWhiteSpace(input))
            {
                AppEvents.RequestNotification("No text selected or clipboard is empty.", NotificationType.Warning);
                return;
            }

            // 2. Get Provider
            var provider = await GetProviderAsync(request.ProviderOverride);
            if (provider is null)
            {
                AppEvents.RequestNotification("No valid AI provider could be found or configured.", NotificationType.Error);
                return;
            }

            // 3. Construct Prompt
            var instruction = request.ActionToExecute.ExplainChanges
                ? $"{request.ActionToExecute.Instruction}\n\nIMPORTANT: After the main response, add a section that starts with '---EXPLANATION---' and explain the changes you made, But make sure to this after the main response."
                : request.ActionToExecute.Instruction;
            var fullInput = $"{request.ActionToExecute.Prefix}{input}";


            // 4. Call AI
            var output = await provider.GenerateResponseAsync(instruction, fullInput, CancellationToken.None);

            // 5. Log to History
            await LogToHistoryAsync(request.ActionToExecute.Name, provider.Name, input, output);
            
            // 6. Handle Output
            // TODO: Simple parsing for explanation. A more robust solution might use JSON output.
            var mainOutput = output;
            string? explanation = null;
            if (request.ActionToExecute.ExplainChanges && output.Contains("---EXPLANATION---"))
            {
                var parts = output.Split(["---EXPLANATION---"], 2, StringSplitOptions.None);
                mainOutput = parts[0].Trim();
                explanation = parts[1].Trim();
            }

            if (request.ForceOpenInWindow || request.ActionToExecute.OpenInWindow)
                AppEvents.RequestResultWindow(new ResultWindowData(
                    ActionName: request.ActionToExecute.Name,
                    MainContent: mainOutput,
                    ExplanationContent: explanation
                ));
            else
                await _osService.PasteTextAsync(mainOutput);

            stopwatch.Stop();
            AppEvents.RequestNotification($"'{request.ActionToExecute.Name}' completed in {stopwatch.Elapsed.TotalSeconds:F2}s.", NotificationType.Success);

        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            AppEvents.RequestNotification($"Error: {ex.Message}", NotificationType.Error);
            Debug.WriteLine($"[ERROR] Processing failed: {ex}");
        }
    }

    private async Task<IAiProvider?> GetProviderAsync(string? providerOverride)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await dbContext.ProviderSettings.AsNoTracking().FirstAsync();

        if (!string.IsNullOrWhiteSpace(providerOverride) && _providers.TryGetValue(providerOverride, out var overriddenProvider))
            return overriddenProvider;

        if (_providers.TryGetValue(settings.PrimaryProvider, out var primaryProvider))
            return primaryProvider;

        if (_providers.TryGetValue(settings.FallbackProvider, out var fallbackProvider))
        {
            AppEvents.RequestNotification($"Primary provider '{settings.PrimaryProvider}' not available. Using fallback.", NotificationType.Warning);
            return fallbackProvider;
        }

        return null;
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
    }
}