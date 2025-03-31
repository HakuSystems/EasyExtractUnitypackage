using Notification.Wpf;

namespace EasyExtract.Services;

public class NotificationService
{
    private static readonly SoundManager SoundManager = new();

    public void ShowNotification(string title, string message, NotificationType type)
    {
        var notificationManager = new NotificationManager();
        var notification = new NotificationContent
        {
            Title = title,
            Message = message,
            Type = type
        };

        notificationManager.Show(notification);
        var switchSound = type switch
        {
            NotificationType.Error => "pack://application:,,,/Resources/Sounds/notification.wav", // meh sounds
            NotificationType.Information => "pack://application:,,,/Resources/Sounds/notification.wav", // meh sounds
            NotificationType.Success => "pack://application:,,,/Resources/Sounds/notification.wav", // meh sounds
            NotificationType.Warning => "pack://application:,,,/Resources/Sounds/error.wav", // meh sounds
            _ => null
        };
        SoundManager.PlayAudio(switchSound);
    }
}