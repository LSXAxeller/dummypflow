using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ProseFlow.UI.ViewModels;

public interface IPageViewModel : INotifyPropertyChanged
{
    string Title { get; }
    string Icon { get; }
    bool IsSelected { get; set; }

    Task OnNavigatedToAsync();
}

public abstract partial class ViewModelBase : ObservableObject, IPageViewModel
{
    public virtual string Title => string.Concat(GetType().Name.Replace("ViewModel", "").Select(c => char.IsUpper(c) ? " " + c : c.ToString()));
    public virtual string Icon => "\uE115"; // Default icon
    
    protected ILogger Logger { get; } = Ioc.Default.GetRequiredService<ILogger<ViewModelBase>>();

    [ObservableProperty]
    private bool _isSelected;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}