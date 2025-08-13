using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Interfaces;

namespace ProseFlow.UI.Services.ActiveWindow;

public class LinuxActiveWindowTracker(ILogger<LinuxActiveWindowTracker> logger) : IActiveWindowTracker
{
    private static MethodInfo? _getWindowPropertyMethod;
    private static object? _platformService;

    public Task<string> GetActiveWindowProcessNameAsync()
    {
        if (!OperatingSystem.IsLinux())
            return Task.FromResult("unknown");

        try
        {
            // Get the platform service and verify it's the X11 platform.
            if (_platformService is null)
            {
                var platformServiceProperty = typeof(AvaloniaLocator).GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static);
                _platformService = platformServiceProperty?.GetValue(null);
            }

            if (_platformService?.GetType().Name != "AvaloniaX11Platform")
            {
                logger.LogCritical("Could not get AvaloniaX11Platform instance. Current platform is {PlatformName}",
                    _platformService?.GetType().Name ?? "null");
                return Task.FromResult("unknown");
            }

            dynamic platform = _platformService;

            var display = platform.Display;
            var root = platform.Info.RootWindow;
            var atoms = platform.Info.Atoms;

            // Get the reflected static methods
            if (!InitializeReflection(logger))
            {
                // Error is logged within the initialization method.
                return Task.FromResult("unknown");
            }

            // 4. Get active window XID using the reflected method and dynamic properties
            var activeWindowXidPtrs = (IntPtr[]?)_getWindowPropertyMethod!.Invoke(null,
                [display, root, atoms._NET_ACTIVE_WINDOW, atoms.XA_WINDOW]);

            if (activeWindowXidPtrs is null || activeWindowXidPtrs.Length == 0)
                return Task.FromResult("unknown");

            var activeWindowXid = activeWindowXidPtrs[0];

            // 5. Get PID using the reflected method
            var pidPtrs = (IntPtr[]?)_getWindowPropertyMethod.Invoke(null,
                [display, activeWindowXid, atoms._NET_WM_PID, atoms.XA_CARDINAL]);

            if (pidPtrs is null || pidPtrs.Length == 0)
                return Task.FromResult("unknown");

            var pid = pidPtrs[0].ToInt32();

            // 6. Get process name from PID
            if (pid > 0)
            {
                var commPath = $"/proc/{pid}/comm";
                if (File.Exists(commPath))
                {
                    var processName = File.ReadAllText(commPath).Trim();
                    return Task.FromResult(processName);
                }
            }
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
        {
            logger.LogError(ex, "Failed to access internal Avalonia members dynamically. The API may have changed.");
            return Task.FromResult("unknown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get active window process name on Linux.");
            return Task.FromResult("unknown");
        }

        return Task.FromResult("unknown");
    }

    /// <summary>
    /// Uses reflection to get the internal static method 'XGetWindowPropertyAsIntPtrArray' from 'Avalonia.X11.XLib'.
    /// The result is cached for performance.
    /// </summary>
    private static bool InitializeReflection(ILogger logger)
    {
        if (_getWindowPropertyMethod is not null) return true;

        try
        {
            var avaloniaX11Assembly = Assembly.Load("Avalonia.X11");
            var xlibType = avaloniaX11Assembly.GetType("Avalonia.X11.XLib");
            if (xlibType is null)
            {
                logger.LogError("Could not load internal type 'Avalonia.X11.XLib' via reflection.");
                return false;
            }

            var method = xlibType.GetMethod("XGetWindowPropertyAsIntPtrArray", BindingFlags.Static | BindingFlags.NonPublic);
            if (method is null)
            {
                logger.LogError("Could not find internal static method 'XGetWindowPropertyAsIntPtrArray' via reflection.");
                return false;
            }

            _getWindowPropertyMethod = method;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize reflection for Avalonia.X11.");
            return false;
        }
    }
}