namespace AutoAIBuilder.Application.Operations;

public sealed class OperationConflictException : InvalidOperationException
{
    public OperationConflictException(string resourceKey, string activeOperationName)
        : base(
            $"A operação “{activeOperationName}” já está usando o recurso "
            + $"“{resourceKey}”. Aguarde sua conclusão ou cancele-a.")
    {
        ResourceKey = resourceKey;
        ActiveOperationName = activeOperationName;
    }

    public string ResourceKey { get; }

    public string ActiveOperationName { get; }
}
