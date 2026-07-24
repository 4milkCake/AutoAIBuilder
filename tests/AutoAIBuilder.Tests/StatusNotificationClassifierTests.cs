using AutoAIBuilder.Application.Notifications;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class StatusNotificationClassifierTests
{
    [DataTestMethod]
    [DataRow("Projeto criado e salvo.", NotificationTone.Success)]
    [DataRow("Validação concluída sem erros.", NotificationTone.Success)]
    [DataRow("Selecione um projeto antes de continuar.", NotificationTone.Warning)]
    [DataRow("Painel carregado com dados locais.", NotificationTone.Information)]
    public void Classify_ReturnsExpectedTone(
        string message,
        NotificationTone expected)
    {
        Assert.AreEqual(expected, StatusNotificationClassifier.Classify(message));
    }

    [TestMethod]
    public void Classify_PrioritizesErrorOverSuccessTerms()
    {
        Assert.AreEqual(
            NotificationTone.Error,
            StatusNotificationClassifier.Classify(
                "Não foi possível salvar as configurações."));
    }
}
