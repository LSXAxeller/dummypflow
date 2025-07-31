using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Actions;

public partial class ActionEditorViewModel(Action action, ActionManagementService actionService) : ViewModelBase
{
    private readonly bool _isNewAction = action.Id == 0;

    [ObservableProperty]
    private Action _action = new() // Clone the action to avoid modifying the original until save
    {
        Id = action.Id,
        Name = action.Name,
        Prefix = action.Prefix,
        Instruction = action.Instruction,
        Icon = action.Icon,
        OpenInWindow = action.OpenInWindow,
        ExplainChanges = action.ExplainChanges,
        ApplicationContext = [..action.ApplicationContext],
        SortOrder = action.SortOrder
    };


    [RelayCommand]
    private async Task SaveAsync(Window window)
    {
        if (string.IsNullOrWhiteSpace(Action.Name))
        {
            AppEvents.RequestNotification("Please provide a name for the action.", NotificationType.Warning);
            return;
        }

        if (_isNewAction)
            await actionService.CreateActionAsync(Action);
        else
            await actionService.UpdateActionAsync(Action);

        window.Close(true);
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window.Close(false);
    }
}