using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProseFlow.Application.Services;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.Views.Actions;
using Microsoft.Extensions.DependencyInjection;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.Services;

public class DialogService(IServiceProvider serviceProvider) : IDialogService
{
    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
    
    public async Task<string?> ShowOpenFileDialogAsync(string title, string filterName, params string[] filterExtensions)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow is null) return null;

        var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = filterExtensions }]
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string filterName, params string[] filterExtensions)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow is null) return null;

        var result = await mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            DefaultExtension = filterExtensions.FirstOrDefault()?.TrimStart('*'),
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = filterExtensions }]
        });

        return result?.TryGetLocalPath();
    }

    public async Task<bool> ShowActionEditorDialogAsync(Action action)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow is null) return false;
        
        // We need to resolve the ActionManagementService from the DI container for the ViewModel
        var actionService = serviceProvider.GetRequiredService<ActionManagementService>();

        var editorViewModel = new ActionEditorViewModel(action, actionService);
        var editorWindow = new ActionEditorView { DataContext = editorViewModel };

        return await editorWindow.ShowDialog<bool>(mainWindow);
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        // TODO: Create a more complex one, maybe a dedicated View/ViewModel.
        var mainWindow = GetMainWindow();
        if (mainWindow is null) return false;

        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 20 });
        
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(25) };
        var okButton = new Button { Content = "OK", IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", IsCancel = true };
        
        okButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);
        
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);
        
        dialog.Content = panel;

        return await dialog.ShowDialog<bool>(mainWindow);
    }
}