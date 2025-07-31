using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Services.Database;
using ProseFlow.UI.Services;
using ProseFlow.UI.Views.Providers;

namespace ProseFlow.UI.ViewModels.Providers;

public partial class ProvidersViewModel(
    SettingsService settingsService,
    CloudProviderManagementService providerService,
    IDialogService dialogService) : ViewModelBase
{
    public override string Title => "Providers";
    public override string Icon => "\uE157";
    
    [ObservableProperty]
    private ProviderSettings? _settings;

    public ObservableCollection<CloudProviderConfiguration> CloudProviders { get; } = [];
    
    public List<string> AvailableServiceTypes => ["Cloud", "Local"];
    public List<string> AvailableFallbackServiceTypes => ["Cloud", "Local", "None"];

    public override async Task OnNavigatedToAsync()
    {
        Settings = await settingsService.GetProviderSettingsAsync();
        await LoadCloudProvidersAsync();
    }
    
    private async Task LoadCloudProvidersAsync()
    {
        CloudProviders.Clear();
        var providers = await providerService.GetConfigurationsAsync();
        foreach (var provider in providers)
        {
            CloudProviders.Add(provider);
        }
    }

    [RelayCommand]
    private async Task AddProviderAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) return;

        var newConfig = new CloudProviderConfiguration { Name = "New Provider", Model = "gpt-4o" };
        var editorViewModel = new CloudProviderEditorViewModel(newConfig, providerService);
        
        // TODO:This is a temporary way to show the dialog until IDialogService is updated for this new view
        var editorWindow = new CloudProviderEditorView { DataContext = editorViewModel };
        var result = await editorWindow.ShowDialog<bool>(desktop.MainWindow);

        if (result) await LoadCloudProvidersAsync();
    }

    [RelayCommand]
    private async Task EditProviderAsync(CloudProviderConfiguration? config)
    {
        if (config is null || Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) return;
        
        var editorViewModel = new CloudProviderEditorViewModel(config, providerService);
        var editorWindow = new CloudProviderEditorView { DataContext = editorViewModel };
        
        var result = await editorWindow.ShowDialog<bool>(desktop.MainWindow);
        if (result) await LoadCloudProvidersAsync();
    }
    
    [RelayCommand]
    private void DeleteProvider(CloudProviderConfiguration? config)
    {
        if (config is null) return;

        dialogService.ShowConfirmationDialogAsync("Delete Provider",
            $"Are you sure you want to delete '{config.Name}'?",
            async () =>
            {
                await providerService.DeleteConfigurationAsync(config.Id);
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
        await providerService.UpdateConfigurationOrderAsync(CloudProviders.ToList());
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Settings is null) return;
        await providerService.UpdateConfigurationOrderAsync(CloudProviders.ToList());
        await settingsService.SaveProviderSettingsAsync(Settings);
        AppEvents.RequestNotification("Provider settings saved successfully.", NotificationType.Success);
    }
}