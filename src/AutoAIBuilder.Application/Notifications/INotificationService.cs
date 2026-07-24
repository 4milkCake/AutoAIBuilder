namespace AutoAIBuilder.Application.Notifications;

public interface INotificationService
{
    event EventHandler<AppNotification>? NotificationPublished;

    AppNotification? Current { get; }

    void Publish(string message);

    void Dismiss();
}
