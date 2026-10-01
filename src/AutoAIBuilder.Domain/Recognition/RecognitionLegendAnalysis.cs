namespace AutoAIBuilder.Domain.Recognition;

public sealed record RecognitionLegendEntry(
    string SymbolHandle,
    string DescriptionHandle,
    string BlockName,
    string Description,
    double SymbolX,
    double SymbolY,
    double DescriptionX,
    double DescriptionY,
    double PairDistance,
    string Evidence,
    int ExpansionDepth = 0,
    string RootHandle = "",
    string StablePath = "",
    string GeometrySignature = "",
    int PrimitiveCount = 0,
    int NestedInsertCount = 0,
    bool IsLooseGeometry = false);

public sealed record RecognitionLegendAnalysis(
    bool IsDetected,
    string Status,
    string DetectionMethod,
    int AnchorCount,
    int RegionEntityCount,
    int DescriptionCandidateCount,
    int UnpairedDescriptionCount,
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY,
    IReadOnlyList<RecognitionLegendEntry> Entries)
{
    public static RecognitionLegendAnalysis Empty { get; } =
        new(
            false,
            "Nenhuma legenda textual foi localizada.",
            "Nenhum título LEGENDA ou SIMBOLOGIA foi encontrado.",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            []);
}
