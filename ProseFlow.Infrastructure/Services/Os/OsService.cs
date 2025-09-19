#region Usings

using System.Diagnostics.CodeAnalysis;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;
using SharpHook;
using TextCopy;
using Action = System.Action;
using EventMask = SharpHook.Data.EventMask;
using KeyCode = SharpHook.Data.KeyCode;

#endregion

namespace ProseFlow.Infrastructure.Services.Os;

/// <summary>
/// Implements OS-level interactions using SharpHook for cross-platform global hotkeys
/// and platform-specific code for other features.
/// </summary>
public sealed class OsService(IActiveWindowTracker activeWindowTracker) : IOsService
{
    private readonly TaskPoolGlobalHook _hook = new();
    private readonly EventSimulator _simulator = new();

    private (KeyCode key, EventMask modifiers) _actionMenuCombination;
    private (KeyCode key, EventMask modifiers) _smartPasteCombination;

    public event Action? ActionMenuHotkeyPressed;
    public event Action? SmartPasteHotkeyPressed;

    public Task StartHookAsync()
    {
        _hook.KeyPressed += OnKeyPressed;
        return _hook.RunAsync();
    }
    
    public void UpdateHotkeys(string actionMenuHotkey, string smartPasteHotkey)
    {
        _actionMenuCombination = ParseHotkeyStringToSharpHook(actionMenuHotkey);
        _smartPasteCombination = ParseHotkeyStringToSharpHook(smartPasteHotkey);
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var currentKey = e.Data.KeyCode;
        var rawModifiers = e.RawEvent.Mask;

        // Normalize the pressed modifiers to their generic equivalents.
        var normalizedModifiers = EventMask.None;
        if (rawModifiers.HasFlag(EventMask.LeftCtrl) || rawModifiers.HasFlag(EventMask.RightCtrl))
            normalizedModifiers |= EventMask.Ctrl;
        if (rawModifiers.HasFlag(EventMask.LeftShift) || rawModifiers.HasFlag(EventMask.RightShift))
            normalizedModifiers |= EventMask.Shift;
        if (rawModifiers.HasFlag(EventMask.LeftAlt) || rawModifiers.HasFlag(EventMask.RightAlt))
            normalizedModifiers |= EventMask.Alt;
        if (rawModifiers.HasFlag(EventMask.LeftMeta) || rawModifiers.HasFlag(EventMask.RightMeta))
            normalizedModifiers |= EventMask.Meta;
        
        // Check for Action Menu Hotkey
        if (currentKey == _actionMenuCombination.key && normalizedModifiers == _actionMenuCombination.modifiers)
            ActionMenuHotkeyPressed?.Invoke();

        // Check for Smart Paste Hotkey
        if (currentKey == _smartPasteCombination.key && normalizedModifiers == _smartPasteCombination.modifiers)
            SmartPasteHotkeyPressed?.Invoke();
    }

    public async Task<string?> GetSelectedTextAsync()
    {
        var originalClipboardText = await ClipboardService.GetTextAsync();

        // Clear clipboard temporarily to reliably detect if copy worked
        await ClipboardService.SetTextAsync(string.Empty);

        await SimulateCopyKeyPressAsync();
        
        // Give the OS a moment to process the copy
        await Task.Delay(150);

        var selectedText = await ClipboardService.GetTextAsync();

        // Restore original clipboard content if it existed
        if (originalClipboardText != null) await ClipboardService.SetTextAsync(originalClipboardText);

        // If the clipboard has new, non-empty content, it's our selected text.
        return !string.IsNullOrEmpty(selectedText) ? selectedText : null;
    }

    public async Task PasteTextAsync(string text)
    {
        var originalClipboardText = await ClipboardService.GetTextAsync();

        await ClipboardService.SetTextAsync(text);
        await SimulatePasteKeyPressAsync();

        // Give the OS a moment to process the paste, then restore the clipboard.
        await Task.Delay(150);
        if (originalClipboardText != null) await ClipboardService.SetTextAsync(originalClipboardText);
    }

    public Task<string> GetActiveWindowProcessNameAsync()
    {
        return activeWindowTracker.GetActiveWindowProcessNameAsync();
    }

    public void SetLaunchAtLogin(bool isEnabled)
    {
#if WINDOWS
        SetLaunchAtLogin_Windows(isEnabled);
#elif OSX
    SetLaunchAtLogin_MacOS(isEnabled);
#elif LINUX
    SetLaunchAtLogin_Linux(isEnabled);
#endif
    }

    public void Dispose()
    {
        _hook.KeyPressed -= OnKeyPressed;
        _hook.Dispose();
    }

    #region Simulation and Parsing Helpers

    private Task SimulateCopyKeyPressAsync()
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyCode.VcLeftMeta : KeyCode.VcLeftControl;
        _simulator.SimulateKeyPress(modifier);
        _simulator.SimulateKeyPress(KeyCode.VcC);
        _simulator.SimulateKeyRelease(KeyCode.VcC);
        _simulator.SimulateKeyRelease(modifier);
        return Task.CompletedTask;
    }

    private Task SimulatePasteKeyPressAsync()
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyCode.VcLeftMeta : KeyCode.VcLeftControl;
        _simulator.SimulateKeyPress(modifier);
        _simulator.SimulateKeyPress(KeyCode.VcV);
        _simulator.SimulateKeyRelease(KeyCode.VcV);
        _simulator.SimulateKeyRelease(modifier);
        return Task.CompletedTask;
    }

    private (KeyCode key, EventMask modifiers) ParseHotkeyStringToSharpHook(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return (KeyCode.VcUndefined, EventMask.None);

        var modifiers = EventMask.None;
        var key = KeyCode.VcUndefined;

        var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                    modifiers |= EventMask.Ctrl;
                    break;
                case "SHIFT":
                    modifiers |= EventMask.Shift;
                    break;
                case "ALT":
                    modifiers |= EventMask.Alt;
                    break;
                case "CMD":
                case "WIN":
                case "META":
                    modifiers |= EventMask.Meta;
                    break;
                default:
                    if (!Enum.TryParse($"Vc{part}", true, out key)) key = KeyCode.VcUndefined;
                    break;
            }

        return (key, modifiers);
    }

    #endregion

    #region Platform-Specific Launch At Login

#if WINDOWS
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private void SetLaunchAtLogin_Windows(bool isEnabled)
    {
        try
        {
            const string registryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryKeyPath, true)
                            ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(registryKeyPath);

            var appPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(appPath)) return;

            if (isEnabled)
                key.SetValue(Constants.AppName, $"\"{appPath}\"");
            else
                key.DeleteValue(Constants.AppName, false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to set startup registry key: {ex.Message}");
        }
    }
#endif


#if OSX
    private void SetLaunchAtLogin_MacOS(bool isEnabled)
    {
        try
        {
            var launchAgentsDir =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library",
                    "LaunchAgents");
            var plistFile = Path.Combine(launchAgentsDir, "com.proseflow.app.plist");

            Directory.CreateDirectory(launchAgentsDir);

            if (isEnabled)
            {
                var appPath = AppContext.BaseDirectory;
                // For .app bundles, the path points inside, we need the path to the bundle itself.
                var bundleIndex = appPath.IndexOf(".app/", StringComparison.OrdinalIgnoreCase);
                if (bundleIndex != -1) appPath = appPath[..(bundleIndex + 4)];

                var plistContent = $"""
                                    <?xml version="1.0" encoding="UTF-8"?>
                                    <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
                                    <plist version="1.0">
                                    <dict>
                                        <key>Label</key>
                                        <string>com.proseflow.app</string>
                                        <key>ProgramArguments</key>
                                        <array>
                                            <string>{appPath}</string>
                                        </array>
                                        <key>RunAtLoad</key>
                                        <true/>
                                    </dict>
                                    </plist>
                                    """;
                File.WriteAllText(plistFile, plistContent);
            }
            else
            {
                if (File.Exists(plistFile)) File.Delete(plistFile);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to set macOS launch agent: {ex.Message}");
        }
    }
#endif

#if LINUX
    private void SetLaunchAtLogin_Linux(bool isEnabled)
    {
        try
        {
            var autostartDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
            var desktopFile = Path.Combine(autostartDir, "proseflow.desktop");

            Directory.CreateDirectory(autostartDir);

            if (isEnabled)
            {
                var appPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(appPath)) return;

                var desktopContent = $"""
                                      [Desktop Entry]
                                      Type=Application
                                      Name={Constants.AppName}
                                      Exec={appPath}
                                      Icon=proseflow
                                      Comment=AI-Powered Writing Assistant
                                      X-GNOME-Autostart-enabled=true
                                      """;
                File.WriteAllText(desktopFile, desktopContent);
            }
            else
            {
                if (File.Exists(desktopFile)) File.Delete(desktopFile);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to set Linux autostart file: {ex.Message}");
        }
    }
#endif

    #endregion
}