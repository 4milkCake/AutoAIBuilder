namespace AutoAIBuilder.Domain.Projects;

public sealed record ProjectRules
{
    public string MeasurementUnit { get; init; } = "Milímetros";

    public string DrawingScale { get; init; } = "1:50";

    public decimal DefaultFloorHeightMeters { get; init; } = 2.80m;

    public string NamingStandard { get; init; } = "DISCIPLINA-TIPO-NÍVEL";

    public bool RequireLayerStandard { get; init; } = true;

    public bool RequireFileIntegrity { get; init; } = true;

    public bool BlockAutomationOnValidationErrors { get; init; } = true;

    public static ProjectRules CreateDefault() => new();

    public ProjectRules ValidateAndNormalize()
    {
        if (string.IsNullOrWhiteSpace(MeasurementUnit))
        {
            throw new ArgumentException("Selecione a unidade de medida.", nameof(MeasurementUnit));
        }

        if (string.IsNullOrWhiteSpace(DrawingScale))
        {
            throw new ArgumentException("Informe a escala padrão.", nameof(DrawingScale));
        }

        if (DefaultFloorHeightMeters <= 0 || DefaultFloorHeightMeters > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DefaultFloorHeightMeters),
                "A altura padrão do pavimento deve ser maior que zero e menor ou igual a 100 metros.");
        }

        if (string.IsNullOrWhiteSpace(NamingStandard))
        {
            throw new ArgumentException("Informe o padrão de nomenclatura.", nameof(NamingStandard));
        }

        return this with
        {
            MeasurementUnit = MeasurementUnit.Trim(),
            DrawingScale = DrawingScale.Trim(),
            NamingStandard = NamingStandard.Trim()
        };
    }
}
