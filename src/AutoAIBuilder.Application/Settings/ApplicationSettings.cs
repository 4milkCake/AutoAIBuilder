namespace AutoAIBuilder.Application.Settings;

public sealed record ApplicationSettings
{
    public string DefaultProjectType { get; init; } = "Residencial";

    public int DefaultFloors { get; init; } = 1;

    public int DefaultUnits { get; init; } = 1;

    public bool ConfirmFileReferenceRemoval { get; init; } = true;

    public static ApplicationSettings CreateDefault() => new();

    public ApplicationSettings ValidateAndNormalize()
    {
        if (string.IsNullOrWhiteSpace(DefaultProjectType))
        {
            throw new ArgumentException("Informe o tipo padrão de projeto.", nameof(DefaultProjectType));
        }

        if (DefaultFloors < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DefaultFloors),
                "A quantidade padrão de pavimentos deve ser maior ou igual a 1.");
        }

        if (DefaultUnits < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DefaultUnits),
                "A quantidade padrão de unidades deve ser maior ou igual a 1.");
        }

        return this with { DefaultProjectType = DefaultProjectType.Trim() };
    }
}
