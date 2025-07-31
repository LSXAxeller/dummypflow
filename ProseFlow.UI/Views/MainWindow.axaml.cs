using System;
using ShadUI;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.UI.Services;

namespace ProseFlow.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Initialize the notification manager with this window as the host
        var app = (App)Avalonia.Application.Current!;
        var notificationService = app.Services?.GetRequiredService<NotificationService>();
        notificationService?.SetHostWindow(this);
    }
}