namespace AutoAIBuilder.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<AppNotification>? NotificationPublished;

    public AppNotification? Current { get; private set; }

    public void Publish(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Dismiss();
            return;
        }

        Current = new AppNotification(
            message.Trim(),
            StatusNotificationClassifier.Classify(message));
        NotificationPublished?.Invoke(this, Current);
    }

    public void Dismiss()
    {
        if (Current is null)
        {
            return;
        }

        Current = null;
        NotificationPublished?.Invoke(
            this,
            new AppNotification(string.Empty, NotificationTone.Information));
    }
}
