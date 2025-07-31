using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Models;

namespace ProseFlow.UI.ViewModels.Providers;

public partial class ProvidersViewModel(SettingsService settingsService) : ViewModelBase
{
    public override string Title => "Providers";
    public override string Icon => "\uE157";
    
    [ObservableProperty]
    private ProviderSettings? _settings;

    public List<string> AvailableProviders => [nameof(ProviderType.Cloud), nameof(ProviderType.Local)];
    public List<string> AvailableFallbackProviders => Enum.GetNames(typeof(ProviderType)).ToList();

    public override async Task OnNavigatedToAsync()
    {
        Settings = await settingsService.GetProviderSettingsAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Settings is null) return;
        await settingsService.SaveProviderSettingsAsync(Settings);
        AppEvents.RequestNotification("Settings saved successfully.", NotificationType.Success);
    }
}