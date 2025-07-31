using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ProseFlow.Application.Services;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.Views.Actions;
using Microsoft.Extensions.DependencyInjection;
using ShadUI;
using Window = Avalonia.Controls.Window;

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

    public async Task<bool> ShowActionEditorDialogAsync(Core.Models.Action action)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow is null) return false;
        
        // We need to resolve the ActionManagementService from the DI container for the ViewModel
        var actionService = serviceProvider.GetRequiredService<ActionManagementService>();

        var editorViewModel = new ActionEditorViewModel(action, actionService);
        var editorWindow = new ActionEditorView { DataContext = editorViewModel };

        return await editorWindow.ShowDialog<bool>(mainWindow);
    }

    public void ShowConfirmationDialog(string title, string message, Action? onConfirm = null, Action? onCancel = null)
    {
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        dialogManager
            .CreateDialog(title, message)
            .WithPrimaryButton("Confirm", onConfirm)
            .WithCancelButton("Cancel", onCancel!)
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
        
    }
    
    public void ShowConfirmationDialogAsync(string title, string message, Func<Task>? onConfirm = null, Func<Task>? onCancel = null)
    {
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        dialogManager
            .CreateDialog(title, message)
            .WithPrimaryButton("Confirm", onConfirm)
            .WithCancelButton("Cancel", onCancel)
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
        
    }
}