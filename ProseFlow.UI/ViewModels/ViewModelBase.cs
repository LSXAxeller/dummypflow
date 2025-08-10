using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using ProseFlow.UI.Utils;
using Microsoft.Extensions.Logging;

namespace ProseFlow.UI.ViewModels;

public interface IPageViewModel : INotifyPropertyChanged
{
    string Title { get; }
    LucideIconKind Icon { get; }
    bool IsSelected { get; set; }

    Task OnNavigatedToAsync();
}

public abstract partial class ViewModelBase : ObservableObject, IPageViewModel
{
    public virtual string Title { get; set; } = string.Empty;
    public virtual LucideIconKind Icon => LucideIconKind.Atom;
    
    protected ILogger Logger { get; } = Ioc.Default.GetRequiredService<ILogger<ViewModelBase>>();

    [ObservableProperty]
    private bool _isSelected;
    
    public virtual Task OnNavigatedToAsync()
    {
        return Task.CompletedTask;
    }
}