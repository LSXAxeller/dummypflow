using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Core.Models;
using ProseFlow.UI.ViewModels.Actions;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Windows;

public partial class FloatingActionMenuViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<ActionExecutionRequest?> _selectionTcs = new();

    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private ActionItemViewModel? _selectedAction;

    [ObservableProperty] private string _currentServiceTypeName;

    public ObservableCollection<ActionItemViewModel> AllActions { get; } = [];

    public ObservableCollection<ActionGroupViewModel> ActionGroups { get; } = [];

    public FloatingActionMenuViewModel(IEnumerable<Action> availableActions, ProviderSettings providerSettings,
        string activeAppContext)
    {
        foreach (var action in availableActions)
        {
            var isContextual = action.ApplicationContext.Count > 0 &&
                               action.ApplicationContext.Contains(activeAppContext, StringComparer.OrdinalIgnoreCase);
            AllActions.Add(new ActionItemViewModel(action)
            {
                IsContextual = isContextual
            });
        }


        FilterAndGroupActions(); // Initial population
        CurrentServiceTypeName = providerSettings.PrimaryServiceType;
    }

    public Task<ActionExecutionRequest?> WaitForSelectionAsync() => _selectionTcs.Task;

    partial void OnSearchTextChanged(string value) => FilterAndGroupActions();

    partial void OnSelectedActionChanged(ActionItemViewModel? oldValue, ActionItemViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    private void FilterAndGroupActions()
    {
        // Filter the flat list based on search text
        var filteredItems = string.IsNullOrWhiteSpace(SearchText)
            ? AllActions
            : AllActions.Where(a => a.Action.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        // Group the filtered results in memory
        var groups = filteredItems
            .GroupBy(item => item.IsContextual)
            .OrderByDescending(g => g.Key);

        // Rebuild the public ActionGroups collection
        ActionGroups.Clear();
        foreach (var group in groups)
        {
            var groupName = group.Key ? "Contextual Actions" : "General Actions";
            var actionGroupVm = new ActionGroupViewModel(groupName);

            foreach (var item in group.OrderBy(i => i.Action.SortOrder))
            {
                actionGroupVm.Actions.Add(item);
            }

            ActionGroups.Add(actionGroupVm);
        }

        // Set the default selected item
        SelectedAction = ActionGroups.SelectMany(g => g.Actions).FirstOrDefault();
    }

    private List<ActionItemViewModel> GetFlatListOfVisibleActions()
    {
        return ActionGroups.SelectMany(g => g.Actions).ToList();
    }

    [RelayCommand]
    private void ConfirmSelection()
    {
        if (SelectedAction is null)
        {
            _selectionTcs.SetResult(null);
            return;
        }

        var request = new ActionExecutionRequest(
            ActionToExecute: SelectedAction.Action,
            ForceOpenInWindow: SelectedAction.IsForcedOpenInWindow,
            ProviderOverride: CurrentServiceTypeName
        );

        if (!_selectionTcs.Task.IsCompleted)
            _selectionTcs.SetResult(request);
    }

    [RelayCommand]
    private void CancelSelection()
    {
        if (!_selectionTcs.Task.IsCompleted)
            _selectionTcs.SetResult(null);
    }

    [RelayCommand]
    private void ToggleServiceType()
    {
        CurrentServiceTypeName = CurrentServiceTypeName == "Cloud"
            ? "Local"
            : "Cloud";
    }

    [RelayCommand]
    private void SelectAction(ActionItemViewModel? item)
    {
        if (item is null) return;
        SelectedAction = item;
        ConfirmSelection();
    }

    [RelayCommand]
    private void SelectNextAction()
    {
        var flatList = GetFlatListOfVisibleActions();
        if (flatList.Count == 0) return;
        var currentIndex = SelectedAction != null ? flatList.IndexOf(SelectedAction) : -1;
        SelectedAction = flatList[(currentIndex + 1) % flatList.Count];
    }

    [RelayCommand]
    private void SelectPreviousAction()
    {
        var flatList = GetFlatListOfVisibleActions();
        if (flatList.Count == 0) return;
        var currentIndex = SelectedAction != null ? flatList.IndexOf(SelectedAction) : -1;
        var newIndex = currentIndex - 1 < 0 ? flatList.Count - 1 : currentIndex - 1;
        SelectedAction = flatList[newIndex];
    }

    [RelayCommand]
    private void ToggleForceOpenInWindow(ActionItemViewModel? item)
    {
        if (item is null) return;
        item.IsForcedOpenInWindow = !item.IsForcedOpenInWindow;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var mainWindow =
            Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

        if (mainWindow?.DataContext is MainViewModel mainWindowViewModel)
        {
            mainWindow.Show();
            mainWindow.Activate();
            mainWindowViewModel.Navigate(
                mainWindowViewModel.PageViewModels.FirstOrDefault(x => x.Title == "Providers"));
        }

        CancelSelection();
    }
}