using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.UI.Utils;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Settings;

public partial class SettingsViewModel(SettingsService settingsService, ActionManagementService actionService, IOsService osService) : ViewModelBase
{
    public override string Title => "Settings";
    public override LucideIconKind Icon => LucideIconKind.Settings;

    [ObservableProperty]
    private GeneralSettings? _settings;

    [ObservableProperty]
    private bool _hasHotkeyConflict;

    public ObservableCollection<Action> AvailableActions { get; } = [];
    public List<string> AvailableThemes => Enum.GetNames(typeof(ThemeType)).ToList();
    
    [ObservableProperty]
    private Action? _selectedSmartPasteAction;
    
    [ObservableProperty]
    private string _selectedTheme = nameof(ThemeType.System);

    partial void OnSettingsChanged(GeneralSettings? value)
    {
        ValidateHotkeys();
    }
    
    partial void OnSelectedSmartPasteActionChanged(Action? value)
    {
        if (Settings is null || value is null) return;
        Settings.SmartPasteActionId = value.Id;
    }
    
    partial void OnSelectedThemeChanged(string value)
    {
        if (Settings is null || Avalonia.Application.Current is null || value == Settings.Theme) return;
        
        Settings.Theme = value;
        Avalonia.Application.Current.RequestedThemeVariant = value switch
        {
            nameof(ThemeType.System) => ThemeVariant.Default,
            nameof(ThemeType.Light) => ThemeVariant.Light,
            nameof(ThemeType.Dark) => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
        
        _ = SaveAsync();
    }
    
    public override async Task OnNavigatedToAsync()
    {
        Settings = await settingsService.GetGeneralSettingsAsync();
        SelectedTheme = Settings.Theme;
        
        AvailableActions.Clear();
        var actions = await actionService.GetActionsAsync();
        foreach (var action in actions) AvailableActions.Add(action);
        SelectedSmartPasteAction = AvailableActions.FirstOrDefault(a => a.Id == Settings.SmartPasteActionId);
    }
    
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Settings is null) return;
        
        ValidateHotkeys();
        if (HasHotkeyConflict)
        {
            AppEvents.RequestNotification("Cannot save with conflicting hotkeys.", NotificationType.Error);
            return;
        }

        osService.SetLaunchAtLogin(Settings.LaunchAtLogin);
        osService.UpdateHotkeys(Settings.ActionMenuHotkey, Settings.SmartPasteHotkey);
        await settingsService.SaveGeneralSettingsAsync(Settings);
        AppEvents.RequestNotification("Settings saved successfully.", NotificationType.Success);
    }
    
    public void ValidateHotkeys()
    {
        if (Settings is null)
        {
            HasHotkeyConflict = false;
            return;
        }

        HasHotkeyConflict = !string.IsNullOrWhiteSpace(Settings.ActionMenuHotkey) &&
                            Settings.ActionMenuHotkey.Equals(Settings.SmartPasteHotkey, StringComparison.OrdinalIgnoreCase);
    }
}