using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Interfaces;

#if MACOS
using AppKit;
#endif

namespace ProseFlow.UI.Services.ActiveWindow;

/// <summary>
/// Tracks the active window process on Apple macOS using the AppKit framework.
/// </summary>
public class MacOsActiveWindowTracker(ILogger<MacOsActiveWindowTracker> logger) : IActiveWindowTracker
{
    private const string UnknownProcess = "unknown.exe";

    /// <inheritdoc />
    public Task<string> GetActiveWindowProcessNameAsync()
    {
#if MACOS
        try
        {
            var frontmostApp = NSWorkspace.SharedWorkspace.FrontmostApplication;
            if (frontmostApp == null)
                return Task.FromResult(UnknownProcess);

            // Fall back to the BundleIdentifier (e.g., "com.microsoft.VSCode") if the URL is unavailable.
            var processName = Path.GetFileName(frontmostApp.ExecutableUrl?.Path)
                                 ?? frontmostApp.BundleIdentifier
                                 ?? UnknownProcess;

            return Task.FromResult(processName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while getting the active window process on macOS.");
            return Task.FromResult(UnknownProcess);
        }
#else
        return Task.FromResult(UnknownProcess);
#endif
    }
}