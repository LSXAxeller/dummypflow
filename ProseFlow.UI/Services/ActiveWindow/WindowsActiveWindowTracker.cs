using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ProseFlow.Core.Interfaces;

namespace ProseFlow.UI.Services.ActiveWindow;

public class WindowsActiveWindowTracker : IActiveWindowTracker
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    public Task<string> GetActiveWindowProcessNameAsync()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            var process = Process.GetProcessById((int)pid);
            return Task.FromResult(process.MainModule?.ModuleName ?? process.ProcessName);
        }
        catch
        {
            return Task.FromResult("unknown.exe");
        }
    }
}