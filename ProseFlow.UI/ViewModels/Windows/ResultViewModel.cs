using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.DTOs;
using TextCopy;

namespace ProseFlow.UI.ViewModels.Windows;

public partial class ResultViewModel(ResultWindowData data) : ViewModelBase
{
    [ObservableProperty]
    private string _actionName = data.ActionName;

    [ObservableProperty]
    private string _mainContent = data.MainContent;

    [ObservableProperty]
    private string? _explanationContent = data.ExplanationContent;

    [ObservableProperty]
    private bool _isExplanationVisible = !string.IsNullOrWhiteSpace(data.ExplanationContent);

    [RelayCommand]
    private async Task CopyAsync()
    {
        await ClipboardService.SetTextAsync(MainContent);
    }
}