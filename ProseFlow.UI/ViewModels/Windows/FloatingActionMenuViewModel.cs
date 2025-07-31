using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Core.Enums;
using ProseFlow.UI.ViewModels.Actions;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Windows;

public partial class FloatingActionMenuViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<ActionExecutionRequest?> _selectionTcs = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ActionItemViewModel? _selectedAction;

    [ObservableProperty]
    private string _currentProviderName = nameof(ProviderType.Cloud);
    
    public ObservableCollection<ActionItemViewModel> AllActions { get; } = [];
    public ObservableCollection<ActionItemViewModel> FilteredActions { get; } = [];

    public FloatingActionMenuViewModel(IEnumerable<Action> availableActions, string activeAppContext)
    {
        foreach (var action in availableActions)
        {
            AllActions.Add(new ActionItemViewModel(action));
        }
        
        FilterActions();
        SelectedAction = FilteredActions.FirstOrDefault();
    }

    public Task<ActionExecutionRequest?> WaitForSelectionAsync() => _selectionTcs.Task;
    
    partial void OnSearchTextChanged(string value) => FilterActions();

    private void FilterActions()
    {
        FilteredActions.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? AllActions
            : AllActions.Where(a => a.Action.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filtered)
        {
            FilteredActions.Add(item);
        }

        if (SelectedAction is null || !FilteredActions.Contains(SelectedAction))
        {
            SelectedAction = FilteredActions.FirstOrDefault();
        }
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
            ProviderOverride: CurrentProviderName
        );
        if (!_selectionTcs.Task.IsCompleted)
            _selectionTcs.SetResult(request);
    }
    
    [RelayCommand]
    private void CancelSelection() => _selectionTcs.SetResult(null);
    
    [RelayCommand]
    private void ToggleProvider()
    {
        CurrentProviderName = CurrentProviderName == nameof(ProviderType.Cloud)
            ? nameof(ProviderType.Local)
            : nameof(ProviderType.Cloud);
    }
    
    [RelayCommand]
    private void SelectAction(ActionItemViewModel? item)
    {
        Console.WriteLine($"1 - Selected action: {item?.Action.Name}");
        
        if (item is null) return;
        SelectedAction = item;
        ConfirmSelection();
        
        Console.WriteLine($"2 -Selected action: {item.Action.Name}");
    }

    [RelayCommand]
    private void SelectNextAction()
    {
        if (FilteredActions.Count == 0) return;
        var currentIndex = SelectedAction != null ? FilteredActions.IndexOf(SelectedAction) : -1;
        SelectedAction = FilteredActions[(currentIndex + 1) % FilteredActions.Count];
    }

    [RelayCommand]
    private void SelectPreviousAction()
    {
        if (FilteredActions.Count == 0) return;
        var currentIndex = SelectedAction != null ? FilteredActions.IndexOf(SelectedAction) : -1;
        var newIndex = currentIndex - 1 < 0 ? FilteredActions.Count - 1 : currentIndex - 1;
        SelectedAction = FilteredActions[newIndex];
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
        var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
            
        if (mainWindow?.DataContext is MainViewModel mainWindowViewModel)
        {
            mainWindow.Show();
            mainWindow.Activate();
            mainWindowViewModel.Navigate(mainWindowViewModel.PageViewModels.FirstOrDefault(x => x.Title == "Settings"));
        }
        
        CancelSelection();
    }
}