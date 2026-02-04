using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using System;

namespace CodeSnip.Services;

public class NotificationService
{
    private static NotificationService? _instance;
    public static NotificationService Instance => _instance ??= new NotificationService();

    private INotificationManager? _notificationManager;
    private readonly object _lock = new();
    private TopLevel? _topLevel;

    private NotificationService() { }

    public void Initialize(TopLevel topLevel)
    {
        lock (_lock)
        {
            _topLevel = topLevel;
            _notificationManager = new WindowNotificationManager(topLevel)
            {
                Position = NotificationPosition.TopRight,
                MaxItems = 5
            };
        }
    }

    public void Show(string title = "", string message = "", NotificationType type = NotificationType.Information, long expirationSeconds = 5)
    {
        if (_notificationManager == null)
            return;

        Manager.Show(new Notification
        {
            Title = title,
            Message = message,
            Type = type,
            Expiration= TimeSpan.FromSeconds(expirationSeconds)
        });
    }

    public INotificationManager Manager => _notificationManager
        ?? throw new InvalidOperationException("NotificationService not initialized. Call Initialize first.");
}