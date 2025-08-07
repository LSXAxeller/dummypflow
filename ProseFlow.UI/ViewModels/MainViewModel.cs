using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.ViewModels.Dashboard;
using ProseFlow.UI.ViewModels.Settings;
using ProseFlow.UI.ViewModels.History;
using ProseFlow.UI.ViewModels.Providers;
using ShadUI;

namespace ProseFlow.UI.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private DialogManager _dialogManager;
    [ObservableProperty]
    private ToastManager _toastManager;
    [ObservableProperty]
    private IPageViewModel? _currentPage;

    public ObservableCollection<IPageViewModel> PageViewModels { get; } = [];

    public MainViewModel(IServiceProvider serviceProvider, DialogManager dialogManager, ToastManager toastManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;

        // Add instances of all page ViewModels
        PageViewModels.Add(serviceProvider.GetRequiredService<DashboardViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<ActionsViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<ProvidersViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<SettingsViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<HistoryViewModel>());

        // Set the initial page
        Navigate(PageViewModels.FirstOrDefault());
    }

    [RelayCommand]
    public void Navigate(IPageViewModel? page)
    {
        if (page is not null)
        {
            CurrentPage = page;
        }
    }

    partial void OnCurrentPageChanged(IPageViewModel? value)
    {
        if (value is null) return;

        // Deselect all pages
        foreach (var page in PageViewModels)
        {
            page.IsSelected = false;
        }

        // Select the new current page
        value.IsSelected = true;

        // Load data for the new page
        value.OnNavigatedToAsync();
    }

    [RelayCommand]
    private void SwitchTheme()
    {
        if (Avalonia.Application.Current is null) return;

        var currentTheme = Avalonia.Application.Current.RequestedThemeVariant;
        Avalonia.Application.Current.RequestedThemeVariant = currentTheme == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }

    public void Dispose()
    {
        foreach (var page in PageViewModels)
        {
            if (page is IDisposable disposablePage)
            {
                disposablePage.Dispose();
            }
        }
        GC.SuppressFinalize(this);
    }
}