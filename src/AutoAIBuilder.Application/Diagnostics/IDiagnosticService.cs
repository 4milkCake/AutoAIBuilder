namespace AutoAIBuilder.Application.Diagnostics;

public interface IDiagnosticService
{
    DiagnosticSnapshot Capture();
}
