using AutoAIBuilder.Application.Notifications;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class NotificationServiceTests
{
    [TestMethod]
    public void Publish_ClassifiesAndExposesCurrentNotification()
    {
        var service = new NotificationService();
        AppNotification? received = null;
        service.NotificationPublished += (_, notification) => received = notification;

        service.Publish("Relatório exportado com sucesso.");

        Assert.IsNotNull(received);
        Assert.AreEqual(NotificationTone.Success, received.Tone);
        Assert.AreEqual(received, service.Current);
    }

    [TestMethod]
    public void Dismiss_ClearsCurrentAndPublishesEmptyNotification()
    {
        var service = new NotificationService();
        service.Publish("Informação carregada.");
        AppNotification? received = null;
        service.NotificationPublished += (_, notification) => received = notification;

        service.Dismiss();

        Assert.IsNull(service.Current);
        Assert.IsNotNull(received);
        Assert.AreEqual(string.Empty, received.Message);
    }
}
