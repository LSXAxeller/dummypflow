using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using System;
using Avalonia.Threading;
using NotificationType = ProseFlow.Application.Events.NotificationType;

namespace ProseFlow.UI.Services;

public class NotificationService
{
    private WindowNotificationManager? _notificationManager;

    public void SetHostWindow(TopLevel topLevel)
    {
        _notificationManager = new WindowNotificationManager(topLevel) { Position = NotificationPosition.BottomRight, MaxItems = 3 };
    }

    public void Show(string message, NotificationType type)
    {
        if (_notificationManager is null) return;

        var notificationType = type switch
        {
            NotificationType.Success => Avalonia.Controls.Notifications.NotificationType.Success,
            NotificationType.Warning => Avalonia.Controls.Notifications.NotificationType.Warning,
            NotificationType.Error => Avalonia.Controls.Notifications.NotificationType.Error,
            _ => Avalonia.Controls.Notifications.NotificationType.Information
        };
        
        Dispatcher.UIThread.Post(() => _notificationManager.Show(new Notification(
            type.ToString(), 
            message, 
            notificationType, 
            TimeSpan.FromSeconds(5))));
    }
}