using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.Interfaces;
using ProseFlow.UI.Services;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.ViewModels.Dashboard;
using ProseFlow.UI.ViewModels.Downloads;
using ProseFlow.UI.ViewModels.History;
using ProseFlow.UI.ViewModels.Providers;
using ProseFlow.UI.ViewModels.Settings;
using ProseFlow.UI.Views.Downloads;
using ShadUI;

namespace ProseFlow.UI.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDownloadManager _downloadManager;
    
    [ObservableProperty]
    private DialogManager _dialogManager;
    [ObservableProperty]
    private ToastManager _toastManager;
    [ObservableProperty]
    private IPageViewModel? _currentPage;
    [ObservableProperty]
    private bool _hasActiveDownloads;
    [ObservableProperty]
    private int _activeDownloadCount;
    [ObservableProperty]
    private DownloadsPopupViewModel _downloadsPopup;

    public ObservableCollection<IPageViewModel> PageViewModels { get; } = [];

    public MainViewModel(IServiceProvider serviceProvider, DialogManager dialogManager, ToastManager toastManager, IDownloadManager downloadManager)
    {
        _serviceProvider = serviceProvider;
        _downloadManager = downloadManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _downloadsPopup = _serviceProvider.GetRequiredService<DownloadsPopupViewModel>();

        // Add instances of all page ViewModels
        PageViewModels.Add(serviceProvider.GetRequiredService<DashboardViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<ActionsViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<ProvidersViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<SettingsViewModel>());
        PageViewModels.Add(serviceProvider.GetRequiredService<HistoryViewModel>());

        // Set the initial page
        Navigate(PageViewModels.FirstOrDefault());

        // Subscribe to download events
        _downloadManager.DownloadsChanged += OnDownloadsChanged;
        OnDownloadsChanged(); // Set initial state
    }

    private void OnDownloadsChanged()
    {
        ActiveDownloadCount = _downloadManager.ActiveDownloadCount;
        HasActiveDownloads = ActiveDownloadCount > 0;
    }
    
    [RelayCommand]
    public void Navigate(IPageViewModel? page)
    {
        if (page is not null) CurrentPage = page;
    }

    [RelayCommand]
    public void ShowDownloadsPopup()
    {
        var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
        dialogService.ShowDownloadsDialog();
    }

    partial void OnCurrentPageChanged(IPageViewModel? value)
    {
        if (value is null) return;

        // Deselect all pages
        foreach (var page in PageViewModels) page.IsSelected = false;

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
        _downloadManager.DownloadsChanged -= OnDownloadsChanged;
        foreach (var page in PageViewModels)
            if (page is IDisposable disposablePage)
                disposablePage.Dispose();

        GC.SuppressFinalize(this);
    }
}