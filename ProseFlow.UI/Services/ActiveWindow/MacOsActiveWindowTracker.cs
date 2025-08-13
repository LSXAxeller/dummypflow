using System.Threading.Tasks;
using ProseFlow.Core.Interfaces;

namespace ProseFlow.UI.Services.ActiveWindow;

public class MacOsActiveWindowTracker : IActiveWindowTracker
{
    public Task<string> GetActiveWindowProcessNameAsync()
    {
        #if OSX
        var foregroundApp = NSWorkspace.SharedWorkspace.FrontmostApplication;
        return Task.FromResult(foregroundApp.LocalizedName);
#else
        return Task.FromResult("unknown");
#endif
    }
}