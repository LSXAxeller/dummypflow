using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Models;
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
        SortOrder = action.SortOrder,
        ActionGroupId = action.ActionGroupId
    };

    [ObservableProperty]
    private ActionGroup? _selectedActionGroup;
    
    public ObservableCollection<ActionGroup> AvailableGroups { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        AvailableGroups.Clear();
        var groups = await actionService.GetActionGroupsAsync();
        foreach (var group in groups)
        {
            AvailableGroups.Add(group);
        }

        SelectedActionGroup = AvailableGroups.FirstOrDefault(g => g.Id == Action.ActionGroupId);
        if (SelectedActionGroup is null && AvailableGroups.Count > 0)
        {
            SelectedActionGroup = AvailableGroups.FirstOrDefault(g => g.Id == 1) ?? AvailableGroups[0];
        }
    }
    
    partial void OnSelectedActionGroupChanged(ActionGroup? value)
    {
        if (value is null) return;
        Action.ActionGroupId = value.Id;
    }

    [RelayCommand]
    private async Task SaveAsync(Window window)
    {
        if (string.IsNullOrWhiteSpace(Action.Name))
        {
            AppEvents.RequestNotification("Please provide a name for the action.", NotificationType.Warning);
            return;
        }

        if (Action.ActionGroupId == 0)
        {
            AppEvents.RequestNotification("Please select a group for the action.", NotificationType.Warning);
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