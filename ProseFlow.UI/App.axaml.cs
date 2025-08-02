using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Interfaces;
using ProseFlow.Infrastructure.Data;
using ProseFlow.Infrastructure.Security;
using ProseFlow.Infrastructure.Services.AiProviders;
using ProseFlow.Infrastructure.Services.Os;
using ProseFlow.UI.Services;
using ProseFlow.UI.ViewModels;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.ViewModels.General;
using ProseFlow.UI.ViewModels.History;
using ProseFlow.UI.ViewModels.Providers;
using ProseFlow.UI.ViewModels.Windows;
using ProseFlow.UI.Views.Windows;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.DTOs;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Services.Database;
using ProseFlow.UI.Views;
using Serilog;
using ShadUI;

namespace ProseFlow.UI;

public class App : Avalonia.Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices();

        // Ensure database is created and migrated on startup
        await using (var scope = Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        // Initialize and start background services
        var orchestrationService = Services.GetRequiredService<ActionOrchestrationService>();
        orchestrationService.Initialize();

        // Subscribe UI handlers to application-layer events
        var osService = Services.GetRequiredService<IOsService>();
        var settingsService = Services.GetRequiredService<SettingsService>();
        var generalSettings = await settingsService.GetGeneralSettingsAsync();
        _ = osService.StartHook(generalSettings.ActionMenuHotkey,
            generalSettings.SmartPasteHotkey);

        SubscribeToAppEvents();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = await settingsService.GetGeneralSettingsAsync();
            RequestedThemeVariant = settings.Theme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SubscribeToAppEvents()
    {
        if (Services is null) return;
        var notificationService = Services.GetRequiredService<NotificationService>();

        AppEvents.ShowNotificationRequested += (message, type) => Dispatcher.UIThread.Post(() => notificationService.Show(message, type));
        
        AppEvents.ShowResultWindowAndAwaitRefinement += data =>
        {
            // This must be run on the UI thread.
            return Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new ResultViewModel(data);
                var window = new ResultWindow { DataContext = viewModel };
                window.Show();
                return await viewModel.CompletionSource.Task;
            });
        };

        AppEvents.ShowFloatingMenuRequested += async (actions, context) =>
        {
            var providerSettings = await Services.GetRequiredService<SettingsService>().GetProviderSettingsAsync();
            var viewModel = new FloatingActionMenuViewModel(actions, providerSettings, context);
            Dispatcher.UIThread.Post(() =>
            {
                var window = new FloatingActionMenuWindow
                {
                    DataContext = viewModel
                };
                window.Show();
            });

            return await viewModel.WaitForSelectionAsync();
        };
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Add Core/Infrastructure Services
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var proseFlowDataPath = Path.Combine(appDataPath, "ProseFlow");
        Directory.CreateDirectory(proseFlowDataPath);

        var logPath = Path.Combine(proseFlowDataPath, "logs", "proseflow-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
#if DEBUG
            .WriteTo.Debug()
            .WriteTo.Console()
#endif
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });
        
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(proseFlowDataPath, "keys")))
            .SetApplicationName("ProseFlow");

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(proseFlowDataPath, "proseflow.db")}"));

        services.AddSingleton<ApiKeyProtector>();
        services.AddSingleton<LocalModelManagerService>();
        services.AddSingleton<LocalSessionService>();
        services.AddSingleton<IAiProvider, CloudProvider>();
        services.AddSingleton<IAiProvider, LocalProvider>();
        services.AddSingleton<IOsService, OsService>();

        // Add Application Services
        services.AddSingleton<ActionOrchestrationService>();
        services.AddScoped<ActionManagementService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<HistoryService>();
        services.AddScoped<CloudProviderManagementService>();

        // Add UI Services
        services.AddSingleton<DialogManager>();
        services.AddSingleton<ToastManager>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Add ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ActionsViewModel>();
        services.AddTransient<ActionEditorViewModel>();
        services.AddTransient<ProvidersViewModel>();
        services.AddTransient<CloudProviderEditorViewModel>();
        services.AddTransient<GeneralViewModel>();
        services.AddTransient<HistoryViewModel>();

        return services.BuildServiceProvider();
    }
}