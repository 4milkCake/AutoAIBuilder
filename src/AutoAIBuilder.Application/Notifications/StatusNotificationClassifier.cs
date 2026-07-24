namespace AutoAIBuilder.Application.Notifications;

public static class StatusNotificationClassifier
{
    private static readonly string[] CriticalErrorTerms =
    [
        "não foi possível",
        "não encontrado",
        "não está disponível",
        "inválid",
        "falha"
    ];

    private static readonly string[] ExplicitSuccessTerms =
    [
        "sem erro",
        "0 erro",
        "zero erro",
        "nenhum erro"
    ];

    private static readonly string[] WarningTerms =
    [
        "ausente",
        "bloquead",
        "cancelad",
        "selecione",
        "precisa",
        "alerta",
        "atenção",
        "pendente"
    ];

    private static readonly string[] SuccessTerms =
    [
        "salv",
        "criad",
        "atualiz",
        "restaurad",
        "exportad",
        "concluíd",
        "aprovad",
        "catalogad",
        "removid"
    ];

    public static NotificationTone Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return NotificationTone.Information;
        }

        if (ContainsAny(message, CriticalErrorTerms))
        {
            return NotificationTone.Error;
        }

        if (ContainsAny(message, ExplicitSuccessTerms))
        {
            return NotificationTone.Success;
        }

        if (message.Contains("erro", StringComparison.OrdinalIgnoreCase))
        {
            return NotificationTone.Error;
        }

        if (ContainsAny(message, WarningTerms))
        {
            return NotificationTone.Warning;
        }

        return ContainsAny(message, SuccessTerms)
            ? NotificationTone.Success
            : NotificationTone.Information;
    }

    private static bool ContainsAny(string message, IEnumerable<string> terms) =>
        terms.Any(term => message.Contains(term, StringComparison.OrdinalIgnoreCase));
}
