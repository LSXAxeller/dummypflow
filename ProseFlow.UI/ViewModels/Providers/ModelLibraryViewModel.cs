using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.UI.Services;
using ProseFlow.UI.ViewModels.Downloads;

namespace ProseFlow.UI.ViewModels.Providers;

public partial class ModelLibraryViewModel(
    IModelCatalogService catalogService,
    ILocalModelManagementService localModelService,
    IDownloadManager downloadManager,
    SettingsService settingsService,
    IDialogService dialogService) : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private int _selectedTabIndex;
    
    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isOnboardingMode;

    [ObservableProperty]
    private LocalModelViewModel? _selectedModel;
    
    public ObservableCollection<AvailableModelViewModel> AvailableModels { get; } = [];
    public ObservableCollection<LocalModelViewModel> LocalModels { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        localModelService.ModelsChanged += OnModelsChanged;
        downloadManager.DownloadsChanged += OnDownloadsChanged;
        
        await LoadLocalModelsAsync();
        await LoadAvailableModelsAsync();
    }

    // Onboarding related event
    private void OnDownloadsChanged()
    {
        // When a download completes, the model library changes.
        var completedDownload = downloadManager.AllDownloads.FirstOrDefault(d => d.Status == Application.DTOs.Models.DownloadStatus.Completed);
        if (completedDownload is not null)
        {
            // This event will trigger OnModelsChanged, which handles the refresh.
        }
    }

    private async void OnModelsChanged()
    {
        await LoadLocalModelsAsync();
    }

    private async Task LoadAvailableModelsAsync()
    {
        IsLoading = true;
        AvailableModels.Clear();
        var catalog = await catalogService.GetAvailableModelsAsync();
        foreach (var entry in catalog) AvailableModels.Add(new AvailableModelViewModel(entry, downloadManager));
        IsLoading = false;
    }

    private async Task LoadLocalModelsAsync()
    {
        LocalModels.Clear();
        var localModels = await localModelService.GetModelsAsync();
        var settings = await settingsService.GetProviderSettingsAsync();
        
        foreach (var entry in localModels)
        {
            var vm = new LocalModelViewModel(entry, localModelService, settingsService, dialogService);
            LocalModels.Add(vm);
        }
        
        // Find the newly downloaded model if applicable
        var recentlyCompleted = downloadManager.AllDownloads
            .FirstOrDefault(d => d.Status == Application.DTOs.Models.DownloadStatus.Completed);

        var modelToSelect = LocalModels.FirstOrDefault(m => m.Model.FilePath == recentlyCompleted?.DestinationPath) 
                            ?? LocalModels.FirstOrDefault(m => m.Model.FilePath == settings.LocalModelPath);

        if (modelToSelect != null)
        {
            await SelectLocalModelAsync(modelToSelect);
            
            // If in onboarding, automatically switch to the "My Models" tab after a download.
            if (IsOnboardingMode && recentlyCompleted != null)
            {
                SelectedTabIndex = 1;
            }
        }
    }
    
    // Onboarding related event
    partial void OnSelectedModelChanged(LocalModelViewModel? value)
    {
        OnPropertyChanged(nameof(IsAModelSelected));
    }
    
    public bool IsAModelSelected => SelectedModel is not null;

    [RelayCommand]
    private async Task ImportToLibraryAsync()
    {
        var importData = await dialogService.ShowImportModelDialogAsync();
        if (importData is null) return;

        try
        {
            await localModelService.ImportManagedModelAsync(importData);
            AppEvents.RequestNotification("Model imported to library successfully.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            AppEvents.RequestNotification($"Failed to import model: {ex.Message}", NotificationType.Error);
        }
    }
    
    [RelayCommand]
    private async Task LinkExternalModelAsync()
    {
        var importData = await dialogService.ShowImportModelDialogAsync();
        if (importData is null) return;

        try
        {
            await localModelService.LinkExternalModelAsync(importData);
            AppEvents.RequestNotification("Model linked successfully.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            AppEvents.RequestNotification($"Failed to link model: {ex.Message}", NotificationType.Error);
        }
    }
    
    [RelayCommand]
    private async Task SelectLocalModelAsync(LocalModelViewModel model)
    {
        await model.Select();
        foreach (var otherModel in LocalModels) otherModel.IsSelected = otherModel == model;
        
        SelectedModel = model;
    }

    public void OnClosing()
    {
        localModelService.ModelsChanged -= OnModelsChanged;
        downloadManager.DownloadsChanged -= OnDownloadsChanged;
    }

    public void Dispose()
    {
        OnClosing();
        GC.SuppressFinalize(this);
    }
}