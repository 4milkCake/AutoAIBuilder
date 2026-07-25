namespace AutoAIBuilder.Application.Operations;

public sealed record OperationRequest(
    Guid Id,
    string OperationType,
    string DisplayName,
    string ResourceKey,
    Guid? ProjectId,
    TimeSpan Timeout)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromHours(24);

    public static OperationRequest Create(
        string operationType,
        string displayName,
        string resourceKey,
        Guid? projectId = null,
        TimeSpan? timeout = null,
        Guid? executionId = null)
    {
        var normalizedType = NormalizeRequired(operationType, nameof(operationType));
        var normalizedName = NormalizeRequired(displayName, nameof(displayName));
        var normalizedResource = NormalizeRequired(resourceKey, nameof(resourceKey));
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var id = executionId ?? Guid.NewGuid();

        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da execução é obrigatório.",
                nameof(executionId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do projeto, quando informado, deve ser válido.",
                nameof(projectId));
        }

        if (effectiveTimeout <= TimeSpan.Zero || effectiveTimeout > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "O timeout deve ser maior que zero e não pode exceder 24 horas.");
        }

        return new OperationRequest(
            id,
            normalizedType,
            normalizedName,
            normalizedResource,
            projectId,
            effectiveTimeout);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "O valor é obrigatório.",
                parameterName);
        }

        return value.Trim();
    }
}
