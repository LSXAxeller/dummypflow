using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Infrastructure.Services.AiProviders.Local;

namespace ProseFlow.UI.ViewModels;

/// <summary>
/// ViewModel to manage the state and commands for the System Tray Icon.
/// </summary>
public partial class TrayIconViewModel : ViewModelBase, IDisposable
{
    private readonly LocalModelManagerService _modelManager;
    private readonly SettingsService _settingsService;

    // Event to signal the UI layer (App.axaml.cs) to show the main window.
    public event Action? ShowMainWindowRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModelLoaded))]
    [NotifyPropertyChangedFor(nameof(ModelStatusText))]
    private ModelStatus _managerStatus;
    
    [ObservableProperty]
    private string _currentProviderType = "Cloud";

    public bool IsModelLoaded => ManagerStatus == ModelStatus.Loaded;

    public string ModelStatusText => ManagerStatus switch
    {
        ModelStatus.NotLoaded => "Local model is not loaded.",
        ModelStatus.Loading => "Loading local model...",
        ModelStatus.Loaded => "Local model is loaded.",
        ModelStatus.Error => $"Error: {_modelManager.ErrorMessage}",
        _ => "Unknown status."
    };

    public TrayIconViewModel(LocalModelManagerService modelManager, SettingsService settingsService)
    {
        _modelManager = modelManager;
        _settingsService = settingsService;
        
        // Subscribe to state changes from the infrastructure service
        _modelManager.StateChanged += OnManagerStateChanged;
        
        // Load initial state
        Dispatcher.UIThread.Post(async void () => await LoadInitialStateAsync());
        OnManagerStateChanged();
    }
    
    private async Task LoadInitialStateAsync()
    {
        var settings = await _settingsService.GetProviderSettingsAsync();
        CurrentProviderType = settings.PrimaryServiceType;
    }

    private void OnManagerStateChanged()
    {
        // UI updates must be dispatched to the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            ManagerStatus = _modelManager.Status;
        });
    }

    [RelayCommand]
    private void OpenSettings()
    {
        ShowMainWindowRequested?.Invoke();
    }
    
    [RelayCommand(CanExecute = nameof(CanToggleModel))]
    private async Task ToggleLocalModel()
    {
        if (IsModelLoaded)
        {
            _modelManager.UnloadModel();
        }
        else
        {
            var settings = await _settingsService.GetProviderSettingsAsync();
            await _modelManager.LoadModelAsync(settings);
        }
    }
    
    private bool CanToggleModel() => ManagerStatus is not ModelStatus.Loading;

    [RelayCommand]
    private async Task SetProviderType(string type)
    {
        if (CurrentProviderType == type) return;

        var settings = await _settingsService.GetProviderSettingsAsync();
        settings.PrimaryServiceType = type;
        await _settingsService.SaveProviderSettingsAsync(settings);
        
        CurrentProviderType = type;
        AppEvents.RequestNotification($"Primary provider set to {type}.", NotificationType.Info);
    }
    
    [RelayCommand]
    private void QuitApplication()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    public void Dispose()
    {
        _modelManager.StateChanged -= OnManagerStateChanged;
        GC.SuppressFinalize(this);
    }
}