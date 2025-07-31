using ProseFlow.Application.DTOs;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Events;

/// <summary>
/// A request to execute a specific action with potential overrides.
/// </summary>
/// <param name="ActionToExecute">The action chosen by the user.</param>
/// <param name="ForceOpenInWindow">Whether the user overrode the default behavior to force opening a new window.</param>
/// <param name="ProviderOverride">Optional provider name to override the default for this single execution.</param>
public record ActionExecutionRequest(Action ActionToExecute, bool ForceOpenInWindow, string? ProviderOverride);

public enum NotificationType { Info, Success, Warning, Error }

public static class AppEvents
{
    /// <summary>
    /// Raised when the Action Orchestration Service needs the UI to display the Floating Action Menu.
    /// The UI layer subscribes to this, shows the menu, and returns the user's selection.
    /// The Func returns a task that resolves to the user's choice, or null if cancelled.
    /// </summary>
    public static event Func<IEnumerable<Action>, string, Task<ActionExecutionRequest?>>? ShowFloatingMenuRequested;

    /// <summary>
    /// Invokes the ShowFloatingMenuRequested event.
    /// </summary>
    public static async Task<ActionExecutionRequest?> RequestFloatingMenuAsync(IEnumerable<Action> availableActions, string activeAppContext) =>
        ShowFloatingMenuRequested is not null
            ? await ShowFloatingMenuRequested.Invoke(availableActions, activeAppContext)
            : await Task.FromResult<ActionExecutionRequest?>(null);


    /// <summary>
    /// Raised when a result needs to be displayed in the dedicated Result Window.
    /// The UI layer subscribes to this and handles window creation.
    /// </summary>
    public static event Action<ResultWindowData>? ShowResultWindowRequested;

    /// <summary>
    /// Invokes the ShowResultWindowRequested event.
    /// </summary>
    public static void RequestResultWindow(ResultWindowData data) =>
        ShowResultWindowRequested?.Invoke(data);


    /// <summary>
    /// Raised to show a system notification (toast).
    /// The UI layer subscribes to this to display feedback.
    /// </summary>
    public static event Action<string, NotificationType>? ShowNotificationRequested;

    /// <summary>
    /// Invokes the ShowNotificationRequested event.
    /// </summary>
    public static void RequestNotification(string message, NotificationType type) =>
        ShowNotificationRequested?.Invoke(message, type);
}