using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Pilots;

namespace AutoAIBuilder.Infrastructure.Automation.Pilots;

internal static class VerifiedCopyPilotContract
{
    private static readonly IReadOnlyList<string> AcceptedExtensions =
    [
        ".dwg",
        ".dxf",
        ".ifc",
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".csv",
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".tif",
        ".tiff"
    ];

    public static AutomationRuleCatalog CreateRuleCatalog() =>
        new(
            AutomationContractVersions.RuleCatalogSchema,
            VerifiedCopyPilotConstants.CatalogId,
            VerifiedCopyPilotConstants.CatalogVersion,
            [
                new AutomationRuleDefinition(
                    "arquivo-original-integro",
                    "1.0.0",
                    "Arquivo original íntegro",
                    "Geral",
                    AutomationRuleSeverity.Blocking,
                    "A origem deve permanecer acessível e conservar o SHA-256 "
                    + "capturado na simulação.",
                    "input.exists && input.sha256 == simulation.sha256",
                    "AutoAIBuilder",
                    AcceptedExtensions),
                new AutomationRuleDefinition(
                    "saida-isolada",
                    "1.0.0",
                    "Saída isolada",
                    "Geral",
                    AutomationRuleSeverity.Blocking,
                    "A cópia e o manifesto devem permanecer na área exclusiva "
                    + "de saída.",
                    "output.path within workspace.result",
                    "AutoAIBuilder",
                    AcceptedExtensions)
            ]);

    public static AutomationMaskDefinition CreateMask() =>
        new(
            AutomationContractVersions.MaskSchema,
            VerifiedCopyPilotConstants.MaskId,
            VerifiedCopyPilotConstants.MaskVersion,
            "Cópia técnica verificada",
            "Geral",
            "Piloto de baixo risco que replica uma entrada já catalogada e "
            + "gera um manifesto de integridade, sem interpretar ou editar "
            + "seu conteúdo.",
            AutomationContractVersions.CurrentApplicationVersion,
            AcceptedExtensions,
            ["arquivo-original-integro", "saida-isolada"],
            [],
            [
                new AutomationMaskParameter(
                    VerifiedCopyPilotConstants.SourceFileNameParameter,
                    "string",
                    "Nome seguro preservado na cópia publicada.",
                    true)
            ],
            [
                new AutomationMaskOutput(
                    "arquivos-verificados",
                    "Pasta contendo a cópia byte a byte.",
                    VerifiedCopyPilotConstants.VerifiedFilesDirectory),
                new AutomationMaskOutput(
                    "manifesto-integridade",
                    "Manifesto JSON com checksums e identidade da execução.",
                    VerifiedCopyPilotConstants.ManifestFileName)
            ],
            [
                new AutomationValidationRequirement(
                    "simulacao-auditada",
                    "Uma simulação válida deve existir antes da confirmação."),
                new AutomationValidationRequirement(
                    "checksum-original",
                    "O SHA-256 original deve coincidir com a simulação.")
            ],
            [
                new AutomationValidationRequirement(
                    "checksum-copia",
                    "A cópia deve ter o mesmo SHA-256 da entrada isolada."),
                new AutomationValidationRequirement(
                    "manifesto-consistente",
                    "O manifesto deve corresponder à execução e à cópia.")
            ]);
}
