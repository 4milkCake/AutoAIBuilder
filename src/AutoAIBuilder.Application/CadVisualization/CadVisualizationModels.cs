namespace AutoAIBuilder.Application.CadVisualization;

public enum CadPrimitiveKind
{
    Line,
    Polyline,
    Circle,
    Arc,
    Text,
    Bounds
}

public enum CadTextAttachment
{
    BaselineLeft,
    BaselineCenter,
    BaselineRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    TopLeft,
    TopCenter,
    TopRight
}

public sealed record CadPoint2D(double X, double Y);

public sealed record CadDrawingBounds(
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY)
{
    public double Width => Math.Max(MaximumX - MinimumX, 1);

    public double Height => Math.Max(MaximumY - MinimumY, 1);
}

public sealed record CadLayerInfo(
    string Name,
    int ColorIndex,
    bool IsVisible);

public sealed record CadPrimitive(
    CadPrimitiveKind Kind,
    string Handle,
    string Layer,
    IReadOnlyList<CadPoint2D> Points,
    double Radius = 0,
    double StartAngleDegrees = 0,
    double EndAngleDegrees = 0,
    string? Text = null,
    double TextHeight = 0,
    double RotationDegrees = 0,
    CadTextAttachment TextAttachment = CadTextAttachment.BaselineLeft,
    double TextWidth = 0,
    string? TextStyle = null,
    string? SourceObjectType = null);

public sealed record CadVisualizationCoverage(
    int SourceEntityCount,
    int ExportedEntityCount,
    int RemainingBlockCount)
{
    public double Percentage => SourceEntityCount <= 0
        ? 0
        : Math.Min(100, ExportedEntityCount * 100d / SourceEntityCount);
}

public sealed record CadVisualizationSnapshot(
    Guid ProjectId,
    string SourceDwgPath,
    string SourceSha256,
    string EngineName,
    string EngineVersion,
    DateTimeOffset GeneratedAt,
    CadDrawingBounds Bounds,
    IReadOnlyList<CadLayerInfo> Layers,
    IReadOnlyList<CadPrimitive> Primitives,
    CadVisualizationCoverage Coverage,
    string ArtifactPath,
    bool LoadedFromCache);

public sealed record CadVisualizationEngineStatus(
    bool IsAvailable,
    string EngineName,
    string Version,
    string ExecutablePath,
    string Publisher,
    string Message);

public interface ICadVisualizationService
{
    CadVisualizationEngineStatus GetEngineStatus();

    CadVisualizationSnapshot? TryLoadCached(
        Guid projectId,
        string sourceDwgPath);

    Task<CadVisualizationSnapshot> GenerateAsync(
        Guid projectId,
        string sourceDwgPath,
        CancellationToken cancellationToken = default);
}
