using Avalonia;
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

    public INotificationManager Manager => _notificationManager
        ?? throw new InvalidOperationException("NotificationService not initialized. Call Initialize first.");
}