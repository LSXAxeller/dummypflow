using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.UI.Utils;
using ProseFlow.Application.Events;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Services.AiProviders.Local;
using ProseFlow.UI.Services;
using ProseFlow.UI.Views.Providers;

namespace ProseFlow.UI.ViewModels.Providers;

public partial class ProvidersViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly CloudProviderManagementService _providerService;
    private readonly IDialogService _dialogService;
    private readonly LocalModelManagerService _modelManager;
    private readonly UsageTrackingService _usageService;
    private readonly ILocalModelManagementService _localModelService;

    public override string Title => "Providers";
    public override LucideIconKind Icon => LucideIconKind.Cloud;
    
    [ObservableProperty]
    private ProviderSettings? _settings;
    
    [ObservableProperty]
    private ModelStatus _managerStatus;
    [ObservableProperty]

    private string? _managerErrorMessage;
    [ObservableProperty]
    private bool _isManagerLoaded;

    public ObservableCollection<CloudProviderConfiguration> CloudProviders { get; } = [];
    public ObservableCollection<LocalModel> LocalModels { get; } = [];
    
    public List<string> AvailableServiceTypes => ["Cloud", "Local"];
    public List<string> AvailableFallbackServiceTypes => ["Cloud", "Local", "None"];

    [ObservableProperty]
    private float _localTemp;
    
    [ObservableProperty]
    private int _localContextSize;
    
    [ObservableProperty]
    private int _localMaxTokens;
    
    [ObservableProperty]
    private LocalModel? _selectedModel;
    
    [ObservableProperty]
    private long _promptTokens;
    [ObservableProperty]
    private long _completionTokens;
    [ObservableProperty]
    private long _totalTokens;

    public ProvidersViewModel(
        SettingsService settingsService,
        CloudProviderManagementService providerService,
        IDialogService dialogService,
        LocalModelManagerService modelManager,
        UsageTrackingService usageService,
        ILocalModelManagementService localModelService)
    {
        _settingsService = settingsService;
        _providerService = providerService;
        _dialogService = dialogService;
        _modelManager = modelManager;
        _usageService = usageService;
        _localModelService = localModelService;

        // Subscribe to events
        _modelManager.StateChanged += OnManagerStateChanged;
        _localModelService.ModelsChanged += OnLocalModelsChanged;

        // Set initial state
        OnManagerStateChanged();
    }
    
    private void OnManagerStateChanged()
    {
        // UI updates must be dispatched to the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            ManagerStatus = _modelManager.Status;
            ManagerErrorMessage = _modelManager.ErrorMessage;
            IsManagerLoaded = _modelManager.IsLoaded;
        });
    }

    private async void OnLocalModelsChanged()
    {
        await LoadLocalModelsAsync();
    }

    partial void OnLocalTempChanged(float value)
    {
        if (Settings is null) return;
        Settings.LocalModelTemperature = value;
    }
    
    partial void OnLocalContextSizeChanged(int value)
    {
        if (Settings is null) return;
        Settings.LocalModelContextSize = value;
    }
    
    partial void OnLocalMaxTokensChanged(int value)
    {
        if (Settings is null) return;
        Settings.LocalModelMaxTokens = value;
    }

    partial void OnSelectedModelChanged(LocalModel? value)
    {
        if (Settings is null || value is null) return;
        Settings.LocalModelPath = value.FilePath;
    }
    
    public override async Task OnNavigatedToAsync()
    {
        Settings = await _settingsService.GetProviderSettingsAsync();
        LocalTemp = Settings.LocalModelTemperature;
        LocalContextSize = Settings.LocalModelContextSize;
        LocalMaxTokens = Settings.LocalModelMaxTokens;

        await LoadCloudProvidersAsync();
        await LoadLocalModelsAsync();
        UpdateUsageDisplay();
    }
    
    private async Task LoadCloudProvidersAsync()
    {
        CloudProviders.Clear();
        var providers = await _providerService.GetConfigurationsAsync();
        foreach (var provider in providers) CloudProviders.Add(provider);
    }

    private async Task LoadLocalModelsAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            LocalModels.Clear();
            var models = await _localModelService.GetModelsAsync();
            foreach (var model in models) LocalModels.Add(model);

            // Reselect the current model if it exists in the new list
            if (Settings is not null && !string.IsNullOrWhiteSpace(Settings.LocalModelPath)) SelectedModel = LocalModels.FirstOrDefault(m => m.FilePath == Settings.LocalModelPath);
        });
    }
    
    private void UpdateUsageDisplay()
    {
        var usage = _usageService.GetCurrentUsage();
        PromptTokens = usage.PromptTokens;
        CompletionTokens = usage.CompletionTokens;
        TotalTokens = usage.PromptTokens + usage.CompletionTokens;
    }
    
    [RelayCommand]
    private async Task ManageModelsAsync()
    {
        await _dialogService.ShowModelLibraryDialogAsync();
        
        // After closing the library, refresh the list of local models
        await LoadLocalModelsAsync();
    }

    [RelayCommand]
    private async Task LoadLocalModelAsync()
    {
        if (Settings is null) return;

        if (string.IsNullOrWhiteSpace(Settings.LocalModelPath))
        {
            AppEvents.RequestNotification("No local model is selected.", NotificationType.Warning);
            return;
        }
        
        await _modelManager.LoadModelAsync(Settings);
    }
    
    [RelayCommand]
    private void UnloadLocalModel()
    {
        _modelManager.UnloadModel();
    }
    
    [RelayCommand]
    private async Task AddProviderAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) return;

        var newConfig = new CloudProviderConfiguration { Name = "New Provider", Model = "gpt-4o" };
        var editorViewModel = new CloudProviderEditorViewModel(newConfig, _providerService);
        
        var editorWindow = new CloudProviderEditorView { DataContext = editorViewModel };
        var result = await editorWindow.ShowDialog<bool>(desktop.MainWindow);

        if (result) await LoadCloudProvidersAsync();
    }

    [RelayCommand]
    private async Task EditProviderAsync(CloudProviderConfiguration? config)
    {
        if (config is null || Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) return;
        
        var editorViewModel = new CloudProviderEditorViewModel(config, _providerService);
        var editorWindow = new CloudProviderEditorView { DataContext = editorViewModel };
        
        var result = await editorWindow.ShowDialog<bool>(desktop.MainWindow);
        if (result) await LoadCloudProvidersAsync();
    }
    
    [RelayCommand]
    private void DeleteProvider(CloudProviderConfiguration? config)
    {
        if (config is null) return;

        _dialogService.ShowConfirmationDialogAsync("Delete Provider",
            $"Are you sure you want to delete '{config.Name}'?",
            async () =>
            {
                await _providerService.DeleteConfigurationAsync(config.Id);
                await LoadCloudProvidersAsync();
            });
    }
    
    [RelayCommand]
    private async Task ReorderProviderAsync((object dragged, object target) items)
    {
        if (items.dragged is not CloudProviderConfiguration draggedProvider || 
            items.target is not CloudProviderConfiguration targetProvider) return;

        var oldIndex = CloudProviders.IndexOf(draggedProvider);
        var newIndex = CloudProviders.IndexOf(targetProvider);
        
        if (oldIndex == -1 || newIndex == -1) return;

        CloudProviders.Move(oldIndex, newIndex);

        // Persist the new order to the database.
        await _providerService.UpdateConfigurationOrderAsync(CloudProviders.ToList());
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Settings is null) return;
        await _providerService.UpdateConfigurationOrderAsync(CloudProviders.ToList());
        await _settingsService.SaveProviderSettingsAsync(Settings);
        AppEvents.RequestNotification("Provider settings saved successfully.", NotificationType.Success);
    }
    
    [RelayCommand]
    private void ResetUsage()
    {
        _dialogService.ShowConfirmationDialogAsync(
            "Reset Usage Counter",
            "Are you sure you want to reset the token usage counter for this month? This cannot be undone.",
            async () =>
            {
                await _usageService.ResetUsageAsync();
                UpdateUsageDisplay();
                AppEvents.RequestNotification("Usage counter has been reset.", NotificationType.Success);
            });
    }

    public void Dispose()
    {
        _modelManager.StateChanged -= OnManagerStateChanged;
        _localModelService.ModelsChanged -= OnLocalModelsChanged;
        GC.SuppressFinalize(this);
    }
}