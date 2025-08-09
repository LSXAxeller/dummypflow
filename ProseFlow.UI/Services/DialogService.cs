using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.DTOs.Models;
using ProseFlow.Application.Services;
using ProseFlow.UI.Models;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.ViewModels.Dialogs;
using ProseFlow.UI.ViewModels.Providers;
using ProseFlow.UI.Views.Actions;
using ProseFlow.UI.Views.Dialogs;
using ShadUI;
using Action = ProseFlow.Core.Models.Action;
using Window = Avalonia.Controls.Window;

namespace ProseFlow.UI.Services;

public class DialogService(IServiceProvider serviceProvider) : IDialogService
{
    private static Window? GetWindow(bool topLevel = true)
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? topLevel ? desktop.Windows.FirstOrDefault(x => x.IsActive && x != desktop.MainWindow) ?? desktop.MainWindow : desktop.MainWindow
            : null;
    }
    
    public async Task OpenUrlAsync(string url)
    {
        var mainWindow = GetWindow(false);
        if (mainWindow is null) return;
        
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            await mainWindow.Launcher.LaunchUriAsync(new Uri(url));
        }
        else
        {
            var storageItem = (IStorageItem?)await mainWindow.StorageProvider.TryGetFolderFromPathAsync(url) ?? 
                              await mainWindow.StorageProvider.TryGetFileFromPathAsync(url);
            
            if (storageItem != null) await mainWindow.Launcher.LaunchFileAsync(storageItem);
        }
    }
    
    public async Task<string?> ShowOpenFileDialogAsync(string title, string filterName, params string[] filterExtensions)
    {
        var mainWindow = GetWindow(false);
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
        var mainWindow = GetWindow(false);
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
        var mainWindow = GetWindow(false);
        if (mainWindow is null) return false;
        
        var actionService = serviceProvider.GetRequiredService<ActionManagementService>();

        var editorViewModel = new ActionEditorViewModel(action, actionService);
        var editorWindow = new ActionEditorView { DataContext = editorViewModel };

        return await editorWindow.ShowDialog<bool>(mainWindow);
    }

    public void ShowConfirmationDialog(string title, string message, System.Action? onConfirm = null, System.Action? onCancel = null)
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
    
    public Task<InputDialogResult> ShowInputDialogAsync(string title, string message, string confirmButtonText, string? initialValue = null)
    {
        var tcs = new TaskCompletionSource<InputDialogResult>();
        
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        var inputViewModel = serviceProvider.GetRequiredService<InputDialogViewModel>();
        inputViewModel.Initialize(title, message, confirmButtonText, initialValue);

        dialogManager.CreateDialog(inputViewModel)
            .Dismissible()
            .WithSuccessCallback(vm => tcs.SetResult(new InputDialogResult(true, vm.InputText)))
            .WithCancelCallback(() => tcs.SetResult(new InputDialogResult(false, null)))
            .Show();

        return tcs.Task;
    }
    
    public Task ShowModelLibraryDialogAsync()
    {
        var tcs = new TaskCompletionSource();
        
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        var modelLibraryViewModel = serviceProvider.GetRequiredService<ModelLibraryViewModel>();
        dialogManager
            .CreateDialog(modelLibraryViewModel)
            .Dismissible()
            .WithMinWidth(900).WithMaxWidth(900)
            .WithSuccessCallback(vm =>
            {
                vm.OnClosing();
                tcs.SetResult();
            })
            .WithCancelCallback(() => tcs.SetResult())
            .Show();
        
        return tcs.Task;
    }
    
    public async Task<CustomModelImportData?> ShowImportModelDialogAsync()
    {
        var mainWindow = GetWindow();
        if (mainWindow is null) return null;

        var importViewModel = serviceProvider.GetRequiredService<CustomModelImportViewModel>();
        var importWindow = new CustomModelImportView { DataContext = importViewModel };
        
        _ = importWindow.ShowDialog(mainWindow);
        return await importViewModel.CompletionSource.Task;
    }
}