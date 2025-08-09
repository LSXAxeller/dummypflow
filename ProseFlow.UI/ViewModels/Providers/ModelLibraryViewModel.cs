using System;
using System.Collections.ObjectModel;
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
    IDialogService dialogService) : ViewModelBase
{
    [ObservableProperty]
    private int _selectedTabIndex;
    
    [ObservableProperty]
    private bool _isLoading = true;
    
    public ObservableCollection<AvailableModelViewModel> AvailableModels { get; } = [];
    public ObservableCollection<LocalModelViewModel> LocalModels { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        localModelService.ModelsChanged += OnModelsChanged;
        await LoadLocalModelsAsync();
        await LoadAvailableModelsAsync();
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
            var vm = new LocalModelViewModel(entry, localModelService, settingsService, dialogService)
            {
                IsSelected = entry.FilePath == settings.LocalModelPath
            };
            LocalModels.Add(vm);
        }
    }

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
        foreach (var otherModel in LocalModels)
            if (otherModel != model) otherModel.IsSelected = false;
    }

    public void OnClosing()
    {
        localModelService.ModelsChanged -= OnModelsChanged;
    }
}