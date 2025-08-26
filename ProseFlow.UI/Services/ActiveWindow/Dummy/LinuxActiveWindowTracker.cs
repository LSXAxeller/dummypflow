using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Interfaces;

namespace ProseFlow.UI.Services.ActiveWindow.Dummy;

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
            if (!InitializeAvaloniaReflection(logger))
            {
                return Task.FromResult("unknown");
            }

            if (_platformService?.GetType().Name != "AvaloniaX11Platform")
            {
                logger.LogCritical("Could not get AvaloniaX11Platform instance. Current platform is {PlatformName}",
                    _platformService?.GetType().Name ?? "null");
                return Task.FromResult("unknown");
            }
            
            // 1. Get the X11Info object from the platform service
            var x11Info = GetPropertyValue<object>(_platformService, "Info");
            if (x11Info is null)
            {
                logger.LogError("Failed to get 'Info' property from AvaloniaX11Platform.");
                return Task.FromResult("unknown");
            }

            // 2. Get Display, RootWindow, and the Atoms object from the X11Info object
            var display = GetPropertyValue<IntPtr>(x11Info, "Display");
            var root = GetPropertyValue<IntPtr>(x11Info, "RootWindow");
            var atoms = GetPropertyValue<object>(x11Info, "Atoms");

            if (display == IntPtr.Zero || root == IntPtr.Zero || atoms is null)
            {
                logger.LogError("Failed to get one or more required properties (Display, RootWindow, Atoms) from X11Info.");
                return Task.FromResult("unknown");
            }

            // 3. Find the method used to look up an "Atom". An Atom is basically an integer ID for a string property name in X11.
            var getAtomMethod = atoms.GetType().GetMethod("GetAtom", [typeof(string)]);
            if (getAtomMethod is null)
            {
                logger.LogError("Failed to get 'GetAtom(string)' method from Atoms object via reflection.");
                return Task.FromResult("unknown");
            }

            // 4. Get the atom values
            
            // Use GetAtom method for _NET_ atoms (which are interned at runtime)
            var netActiveWindowAtom = (IntPtr)getAtomMethod.Invoke(atoms, ["_NET_ACTIVE_WINDOW"])!;
            var netWmPidAtom = (IntPtr)getAtomMethod.Invoke(atoms, ["_NET_WM_PID"])!;

            // Use GetPropertyValue for XA_ atoms (which are predefined constants exposed as fields)
            var xaWindowAtom = GetPropertyValue<IntPtr>(atoms, "XA_WINDOW");
            var xaCardinalAtom = GetPropertyValue<IntPtr>(atoms, "XA_CARDINAL");
            

            // Get active window XID
            var activeWindowXidPtrs = (IntPtr[]?)_getWindowPropertyMethod!.Invoke(null,
                [display, root, netActiveWindowAtom, xaWindowAtom]);

            if (activeWindowXidPtrs is null || activeWindowXidPtrs.Length == 0)
                return Task.FromResult("unknown");

            var activeWindowXid = activeWindowXidPtrs[0];

            // Get PID
            var pidPtrs = (IntPtr[]?)_getWindowPropertyMethod.Invoke(null,
                [display, activeWindowXid, netWmPidAtom, xaCardinalAtom]);

            if (pidPtrs is null || pidPtrs.Length == 0)
                return Task.FromResult("unknown");

            var pid = pidPtrs[0].ToInt32();

            // Get process name from PID
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
            // This can happen if Avalonia's internal API changes in an update.
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
    /// Helper method to get a property value via reflection.
    /// </summary>
    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        // Check for property first, then fall back to field for things like XA_WINDOW
        var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is not null)
            return (T?)property.GetValue(obj);

        var field = obj.GetType().GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null)
            return (T?)field.GetValue(obj);

        return default;
    }
    
    /// <summary>
    /// Performs the initial, one-time reflection to find and cache the internal Avalonia
    /// services and methods needed to query X11. This is the fragile part that might
    /// break with future Avalonia updates.
    /// </summary>
    private static bool InitializeAvaloniaReflection(ILogger logger)
    {
        // If both are already cached, we are done.
        if (_platformService is not null && _getWindowPropertyMethod is not null) return true;

        try
        {
            // Get and cache the Platform Service
            if (_platformService is null)
            {
                var locatorType = typeof(AvaloniaLocator);
                var currentProperty = locatorType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                var locatorInstance = currentProperty?.GetValue(null);
                if (locatorInstance is null)
                {
                    logger.LogCritical("Could not get AvaloniaLocator.Current instance.");
                    return false;
                }

                var windowingPlatformInterfaceType = typeof(IWindowingPlatform);
                
                var getServiceMethod = locatorInstance.GetType().GetMethod("GetService", [typeof(Type)]);
                if (getServiceMethod is null)
                {
                    logger.LogCritical("Could not find GetService(Type) method on AvaloniaLocator.Current.");
                    return false;
                }

                _platformService = getServiceMethod.Invoke(locatorInstance, [windowingPlatformInterfaceType]);
                if (_platformService is null)
                {
                    logger.LogCritical("GetService(IWindowingPlatform) returned null.");
                    return false;
                }
            }

            // Now, find the specific X11 P/Invoke method we need to call.
            if (_getWindowPropertyMethod is null)
            {
                var avaloniaX11Assembly = Assembly.Load("Avalonia.X11");
                var xlibType = avaloniaX11Assembly.GetType("Avalonia.X11.XLib");
                if (xlibType is null)
                {
                    logger.LogError("Could not load internal type 'Avalonia.X11.XLib' via reflection.");
                    return false;
                }

                var method = xlibType.GetMethod("XGetWindowPropertyAsIntPtrArray", BindingFlags.Static | BindingFlags.Public);
                if (method is null)
                {
                    logger.LogError("Could not find internal static method 'XGetWindowPropertyAsIntPtrArray' via reflection.");
                    return false;
                }
                _getWindowPropertyMethod = method;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize reflection for Avalonia platform services.");
            _platformService = null;
            _getWindowPropertyMethod = null;
            return false;
        }
    }
}