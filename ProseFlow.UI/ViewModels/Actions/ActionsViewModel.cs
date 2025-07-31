using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.UI.Services;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Actions;

public partial class ActionsViewModel(
    ActionManagementService actionService,
    IDialogService dialogService) : ViewModelBase
{
    public override string Title => "Actions";
    public override string Icon => "\uE35B";

    public ObservableCollection<Action> Actions { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        await LoadActionsAsync();
    }

    private async Task LoadActionsAsync()
    {
        Actions.Clear();
        var actions = await actionService.GetActionsAsync();
        foreach (var action in actions)
        {
            Actions.Add(action);
        }
    }

    [RelayCommand]
    private async Task AddActionAsync()
    {
        var result = await dialogService.ShowActionEditorDialogAsync(new Action { Name = "New Action" });
        if (result)
        {
            await LoadActionsAsync();
        }
    }

    [RelayCommand]
    private async Task EditActionAsync(Action? action)
    {
        if (action is null) return;

        var result = await dialogService.ShowActionEditorDialogAsync(action);
        if (result)
        {
            await LoadActionsAsync();
        }
    }

    [RelayCommand]
    private void DeleteAction(Action? action)
    {
        if (action is null) return;

        dialogService.ShowConfirmationDialogAsync(
            "Delete Action",
            $"Are you sure you want to delete the action '{action.Name}'?", async () =>
            {
                await actionService.DeleteActionAsync(action.Id);
                await LoadActionsAsync();
            });
    }

    [RelayCommand]
    private async Task ReorderActionAsync((object dragged, object target) items)
    {
        if (items.dragged is not Action draggedAction || items.target is not Action targetAction) return;

        var oldIndex = Actions.IndexOf(draggedAction);
        var newIndex = Actions.IndexOf(targetAction);

        if (oldIndex == -1 || newIndex == -1) return;

        Actions.Move(oldIndex, newIndex);

        // Persist the new order to the database.
        await actionService.UpdateActionOrderAsync(Actions.ToList());
    }

    [RelayCommand]
    private async Task ImportActionsAsync()
    {
        var filePath = await dialogService.ShowOpenFileDialogAsync("Import Actions", "JSON files", "*.json");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            await actionService.ImportActionsFromJsonAsync(filePath);
            await LoadActionsAsync();
            AppEvents.RequestNotification("Actions imported successfully.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            AppEvents.RequestNotification("Failed to import actions.", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task ExportActionsAsync()
    {
        var filePath =
            await dialogService.ShowSaveFileDialogAsync("Export Actions", "proseflow_actions.json", "JSON files",
                "*.json");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            await actionService.ExportActionsToJsonAsync(filePath);
            AppEvents.RequestNotification("Actions exported successfully.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            AppEvents.RequestNotification("Failed to export actions.", NotificationType.Error);
        }
    }
}