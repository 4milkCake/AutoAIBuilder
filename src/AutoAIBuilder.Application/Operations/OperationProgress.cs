namespace AutoAIBuilder.Application.Operations;

public sealed record OperationProgress(int Percentage, string Message)
{
    public static OperationProgress Create(int percentage, string message)
    {
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentage),
                "O progresso deve estar entre 0 e 100.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "A etapa atual é obrigatória.",
                nameof(message));
        }

        return new OperationProgress(percentage, message.Trim());
    }
}
