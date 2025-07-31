namespace ProseFlow.Core.Interfaces;

/// <summary>
/// Defines the contract for services that interact with the underlying operating system.
/// This version is designed for a global event hook model.
/// </summary>
public interface IOsService : IDisposable
{
    // Events for Hotkeys
    event Action? ActionMenuHotkeyPressed;
    event Action? SmartPasteHotkeyPressed;
    
    /// <summary>
    /// Starts the global hook and begins listening for hotkey events.
    /// </summary>
    /// <param name="actionMenuHotkey">The hotkey string for the action menu (e.g., "Ctrl+J").</param>
    /// <param name="smartPasteHotkey">The hotkey string for smart paste (e.g., "Ctrl+Shift+V").</param>
    Task StartHook(string actionMenuHotkey, string smartPasteHotkey);

    /// <summary>
    /// Asynchronously retrieves the text currently selected by the user in the foreground application.
    /// This is a best-effort operation that relies on simulating a 'Copy' command.
    /// </summary>
    Task<string?> GetSelectedTextAsync();

    /// <summary>
    /// Asynchronously pastes the given text into the currently active application.
    /// This relies on simulating a 'Paste' command.
    /// </summary>
    Task PasteTextAsync(string text);

    /// <summary>
    /// Asynchronously gets the process name of the currently active foreground window.
    /// </summary>
    Task<string> GetActiveWindowProcessNameAsync();
}