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
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ProseFlow.Infrastructure.Services.AiProviders.Local;
using ProseFlow.UI.Views;
using Serilog;
using ShadUI;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.DependencyInjection;
using ProseFlow.Application.Interfaces;
using ProseFlow.UI.ViewModels.Dialogs;
using ProseFlow.UI.Views.Dialogs;

namespace ProseFlow.UI;

public class App : Avalonia.Application
{
    public IServiceProvider? Services { get; private set; }
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices();
        Ioc.Default.ConfigureServices(Services);


        // Ensure database is created and migrated on startup
        await using (var scope = Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        var usageTrackingService = Services.GetRequiredService<UsageTrackingService>();
        await usageTrackingService.InitializeAsync();

        var settingsService = Services.GetRequiredService<SettingsService>();

        // Check for local model on startup
        await using (var scope = Services.CreateAsyncScope())
        {
            var modelManager = scope.ServiceProvider.GetRequiredService<LocalModelManagerService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();

            try
            {
                var providerSettings = await settingsService.GetProviderSettingsAsync();
                if (providerSettings is { PrimaryServiceType: "Local", LocalModelLoadOnStartup: true })
                {
                    if (string.IsNullOrWhiteSpace(providerSettings.LocalModelPath) ||
                        !File.Exists(providerSettings.LocalModelPath))
                    {
                        logger.LogWarning(
                            "Auto-load skipped: Local model path is not configured or file does not exist.");
                    }
                    else
                    {
                        logger.LogInformation("Attempting to auto-load local model on startup...");
                        if (!Design.IsDesignMode) // Don't auto-load model in design mode
                            _ = modelManager.LoadModelAsync(providerSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during the local model auto-load check.");
            }
        }

        // Initialize and start background services
        var orchestrationService = Services.GetRequiredService<ActionOrchestrationService>();
        orchestrationService.Initialize();

        // Subscribe UI handlers to application-layer events
        var osService = Services.GetRequiredService<IOsService>();
        var generalSettings = await settingsService.GetGeneralSettingsAsync();
        _ = osService.StartHookAsync();
        osService.UpdateHotkeys(generalSettings.ActionMenuHotkey, generalSettings.SmartPasteHotkey);

        SubscribeToAppEvents();
        
        
        // Setup Dialogs
        var dialogManager = Services.GetRequiredService<DialogManager>();
        dialogManager.Register<InputDialogView, InputDialogViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Don't shut down the app when the main window is closed.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += OnApplicationExit;

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

            // Handle the closing event to hide the window instead of closing
            desktop.MainWindow.Closing += (_, e) =>
            {
                e.Cancel = true;
                desktop.MainWindow.Hide();
            };

            // Create and set up the system tray icon
            _trayIcon = CreateTrayIcon();
            if (_trayIcon is not null)
            {
                TrayIcon.SetIcons(this, [_trayIcon]);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private TrayIcon? CreateTrayIcon()
    {
        if (Services is null) return null;

        var trayVm = Services.GetRequiredService<TrayIconViewModel>();

        // Wire up the event to show the main window
        trayVm.ShowMainWindowRequested += () =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: not null } desktop)
            {
                // Ensure we're on the UI thread before showing the window
                Dispatcher.UIThread.Post(() =>
                {
                    desktop.MainWindow.Show();
                    desktop.MainWindow.Activate();
                });
            }
        };

        // Define a converter for the menu item header
        var modelStatusToHeaderConverter = new FuncValueConverter<bool, string>(isLoaded =>
            isLoaded ? "Unload Local Model" : "Load Local Model");
        
        var providerTypeToHeaderConverter = new FuncValueConverter<string, string>(providerType =>
            $"Set Primary Provider ({providerType})");

        // Build the context menu items
        var openItem = new NativeMenuItem
        {
            Header = "Open ProseFlow",
            Command = trayVm.OpenSettingsCommand
        };

        var toggleModelItem = new NativeMenuItem
        {
            Command = trayVm.ToggleLocalModelCommand
        };
        toggleModelItem.Bind(NativeMenuItem.HeaderProperty, new Avalonia.Data.Binding(nameof(trayVm.IsModelLoaded))
        {
            Source = trayVm,
            Converter = modelStatusToHeaderConverter
        });
        toggleModelItem.Bind(NativeMenuItem.IsEnabledProperty, new Avalonia.Data.Binding(nameof(trayVm.ManagerStatus))
        {
            Source = trayVm,
            Converter = new FuncValueConverter<ModelStatus, bool>(s => s != ModelStatus.Loading)
        });

        // Provider Type Sub-menu
        var cloudProviderItem = new NativeMenuItem
        {
            Header = "Cloud",
            Command = trayVm.SetProviderTypeCommand,
            CommandParameter = "Cloud"
        };
        cloudProviderItem.Bind(NativeMenuItem.IsCheckedProperty,
            new Avalonia.Data.Binding(nameof(trayVm.CurrentProviderType))
            {
                Source = trayVm,
                Converter = new FuncValueConverter<string, bool>(t => t == "Cloud")
            });

        var localProviderItem = new NativeMenuItem
        {
            Header = "Local",
            Command = trayVm.SetProviderTypeCommand,
            CommandParameter = "Local"
        };
        localProviderItem.Bind(NativeMenuItem.IsCheckedProperty,
            new Avalonia.Data.Binding(nameof(trayVm.CurrentProviderType))
            {
                Source = trayVm,
                Converter = new FuncValueConverter<string, bool>(t => t == "Local")
            });

        var setProviderSubMenu = new NativeMenuItem
        {
            Menu = new NativeMenu
            {
                Items = { cloudProviderItem, localProviderItem }
            }
        };
        setProviderSubMenu.Bind(NativeMenuItem.HeaderProperty, new Avalonia.Data.Binding(nameof(trayVm.CurrentProviderType))
        {
            Source = trayVm,
            Converter = providerTypeToHeaderConverter
        });

        var quitItem = new NativeMenuItem
        {
            Header = "Quit",
            Command = trayVm.QuitApplicationCommand
        };

        // Create the TrayIcon instance
        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ProseFlow.UI/Assets/avalonia-logo.ico"))),
            ToolTipText = "ProseFlow",
            Menu = new NativeMenu
            {
                Items =
                {
                    openItem,
                    new NativeMenuItemSeparator(),
                    toggleModelItem,
                    setProviderSubMenu,
                    new NativeMenuItemSeparator(),
                    quitItem
                }
            }
        };

        // Open settings on left-click
        trayIcon.Clicked += (_, _) => trayVm.OpenSettingsCommand.Execute(null);

        return trayIcon;
    }

    private void SubscribeToAppEvents()
    {
        if (Services is null) return;
        var notificationService = Services.GetRequiredService<NotificationService>();

        AppEvents.ShowNotificationRequested += (message, type) =>
            Dispatcher.UIThread.Post(() => notificationService.Show(message, type));

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
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command",
                Serilog.Events.LogEventLevel.Warning)
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

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(proseFlowDataPath, "proseflow.db")}"));
        
        // Add Infrastructure Services
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ApiKeyProtector>();
        services.AddSingleton<UsageTrackingService>();
        services.AddSingleton<LocalModelManagerService>();
        services.AddSingleton<ILocalSessionService, LocalSessionService>();
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
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<TrayIconViewModel>();
        services.AddTransient<ActionsViewModel>();
        services.AddTransient<ActionEditorViewModel>();
        services.AddTransient<ProvidersViewModel>();
        services.AddTransient<CloudProviderEditorViewModel>();
        services.AddTransient<GeneralViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<InputDialogViewModel>();

        return services.BuildServiceProvider();
    }
    
    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (Services is null) return;

        var logger = Services.GetService<ILogger<App>>();
        logger?.LogInformation("Application exit requested. Cleaning up resources...");

        // Dispose the OS service to stop the SharpHook thread.
        var osService = Services.GetService<IOsService>();
        osService?.Dispose();

        // Also, as a best practice, unload the local model to free GPU/RAM.
        var modelManager = Services.GetService<LocalModelManagerService>();
        modelManager?.UnloadModel();
    
        logger?.LogInformation("Cleanup complete. Application will now exit.");
    }
}