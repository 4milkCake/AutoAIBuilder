using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AutoAIBuilder.Application.Automation.Bridge;
using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Infrastructure.Automation;

public sealed partial class LegacyAutomationBridgeService
    : ILegacyAutomationBridgeService
{
    private const long MaximumRoutineSizeBytes = 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, RoutineProfile> Profiles =
        new Dictionary<string, RoutineProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["mascara_analisar_v01.lsp"] = new(
                "01", "Inventário inicial",
                ["DWG ativo", "entidades, camadas e blocos"],
                ["CSV de análise"],
                [], IdentificationContractId,
                LegacyRoutineSafety.ReportOutputOnly,
                "Somente lê o desenho e grava relatório CSV."),
            ["mascara_classificar_v02.lsp"] = new(
                "02", "Classificação preliminar",
                ["resultado v01", "tabela de blocos e camadas"],
                ["CSV de classificação"],
                ["mascara_analisar_v01.lsp"], IdentificationContractId,
                LegacyRoutineSafety.ReportOutputOnly,
                "Classifica entidades e grava relatório CSV."),
            ["mascara_previsualizar_v03.lsp"] = new(
                "03–03.5", "Legenda, contexto e pré-visualização",
                ["resultado v02", "legenda do projeto", "seleções do usuário"],
                ["classificação semântica", "relatórios de mapeamento"],
                ["mascara_classificar_v02.lsp"], IdentificationContractId,
                LegacyRoutineSafety.ReportOutputOnly,
                "Analisa símbolos e legenda; suas saídas externas são relatórios."),
            ["mascara_previsualizar_v04.lsp"] = new(
                "04–04.2", "Pontos e componentes associados",
                ["mapeamento v03", "blocos, textos e geometria próximos"],
                ["pontos elétricos", "pontos hidráulicos", "componentes associados"],
                ["mascara_previsualizar_v03.lsp"], IdentificationContractId,
                LegacyRoutineSafety.ReportOutputOnly,
                "Consolida a identificação e a associação sem alterar entidades."),
            ["mascara_camadas_v05.lsp"] = new(
                "05", "Aplicação de camadas semânticas",
                ["classificação v04", "entidades identificadas"],
                ["camadas semânticas", "entidades reorganizadas"],
                ["mascara_previsualizar_v04.lsp"], MaskContractId,
                LegacyRoutineSafety.DrawingMutationBlocked,
                "Contém entmod/entmake/vla-put e poderia alterar o DWG."),
            ["mascara_limpeza_v06.lsp"] = new(
                "06", "Limpeza e restauração da máscara",
                ["desenho organizado pela v05"],
                ["máscara limpa", "estado restaurável"],
                ["mascara_camadas_v05.lsp"], MaskContractId,
                LegacyRoutineSafety.DrawingMutationBlocked,
                "Contém entdel/entmod e poderia remover ou restaurar entidades."),
            ["mascara_exportar_v07.lsp"] = new(
                "07", "Exportação auditável",
                ["resultado semântico v05/v06"],
                ["CSV de pontos", "CSV de componentes", "CSV de auditoria"],
                ["mascara_camadas_v05.lsp", "mascara_limpeza_v06.lsp"],
                IdentificationContractId,
                LegacyRoutineSafety.ReportOutputOnly,
                "Lê o estado organizado e grava somente relatórios externos."),
            ["mascara_builder_piloto_v08.lsp"] = new(
                "08", "Direção e calibração Builder",
                ["relatórios v07", "geometria dos pontos"],
                ["CSV de direção e calibração"],
                ["mascara_exportar_v07.lsp"], IdentificationContractId,
                LegacyRoutineSafety.ReportOutputOnly,
                "Calcula direção e grava relatório; versão substituída pela 08.1."),
            ["mascara_builder_piloto_v081.lsp"] = new(
                "08.1", "Direção e calibração Builder",
                ["relatórios v07", "geometria dos pontos"],
                ["CSV de direção e calibração revisado"],
                ["mascara_exportar_v07.lsp"], IdentificationContractId,
                LegacyRoutineSafety.ReportOutputOnly,
                "Versão histórica mais recente da calibração; grava relatório.",
                true)
        };

    private const string IdentificationContractId =
        "autoaibuilder.identificacao-pontos";
    private const string MaskContractId =
        "autoaibuilder.criacao-mascara";

    public LegacyAutomationBridgeSnapshot Analyze(
        string sourceDirectory,
        SemanticWorkspaceSnapshot semanticSnapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentNullException.ThrowIfNull(semanticSnapshot);

        var fullDirectory = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException(
                $"A pasta AutoLISP não foi encontrada: {fullDirectory}");
        }

        var paths = Directory
            .EnumerateFiles(fullDirectory, "*.lsp", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
        {
            throw new InvalidOperationException(
                "A pasta selecionada não contém rotinas AutoLISP (.lsp).");
        }

        var before = paths.ToDictionary(
            path => path,
            CalculateSha256,
            StringComparer.OrdinalIgnoreCase);
        var routines = paths.Select(path => AnalyzeRoutine(path, before[path])).ToArray();
        var after = paths.ToDictionary(
            path => path,
            CalculateSha256,
            StringComparer.OrdinalIgnoreCase);
        var unchanged = before.All(
            pair => after.TryGetValue(pair.Key, out var hash)
                    && string.Equals(pair.Value, hash, StringComparison.Ordinal));

        return new LegacyAutomationBridgeSnapshot(
            fullDirectory,
            DateTimeOffset.Now,
            routines,
            BuildContractLinks(routines),
            BuildSimulation(semanticSnapshot),
            unchanged,
            "Análise estática: nenhum AutoLISP foi carregado ou executado; "
            + "nenhum DWG foi aberto, salvo ou alterado.");
    }

    private static LegacyAutomationRoutine AnalyzeRoutine(
        string path,
        string sha256)
    {
        var file = new FileInfo(path);
        if (file.Length > MaximumRoutineSizeBytes)
        {
            throw new InvalidOperationException(
                $"A rotina {file.Name} excede o limite de análise de 1 MiB.");
        }

        var text = File.ReadAllText(path);
        var commands = CommandDefinitionRegex()
            .Matches(text)
            .Select(match => match.Groups[1].Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var known = Profiles.TryGetValue(file.Name, out var profile);
        profile ??= BuildUnknownProfile(text);
        var detectedMutation = MutationRegex().IsMatch(text);
        var safety = detectedMutation
            ? LegacyRoutineSafety.DrawingMutationBlocked
            : profile.Safety;
        var reason = detectedMutation
            && profile.Safety != LegacyRoutineSafety.DrawingMutationBlocked
                ? "A análise estática detectou operações que podem alterar o desenho."
                : profile.SafetyReason;

        return new LegacyAutomationRoutine(
            file.Name,
            file.FullName,
            profile.Version,
            profile.Stage,
            file.Length,
            sha256,
            commands,
            profile.Inputs,
            profile.Outputs,
            profile.Dependencies,
            profile.ContractId,
            safety,
            reason,
            known && profile.IsPreferred);
    }

    private static RoutineProfile BuildUnknownProfile(string text)
    {
        var writesReports = ReportWriteRegex().IsMatch(text);
        return new RoutineProfile(
            "não identificada",
            "Rotina não catalogada",
            ["DWG ativo — requer revisão manual"],
            writesReports ? ["arquivo externo — requer revisão"] : ["não identificada"],
            [],
            IdentificationContractId,
            writesReports
                ? LegacyRoutineSafety.ReportOutputOnly
                : LegacyRoutineSafety.ReadOnlyAnalysis,
            "Rotina fora do catálogo histórico; permanece apenas em análise.");
    }

    private static IReadOnlyList<LegacyAutomationContractLink> BuildContractLinks(
        IReadOnlyList<LegacyAutomationRoutine> routines)
    {
        var identification = new AutomationMaskDefinition(
            "1.0",
            IdentificationContractId,
            "0.1.0",
            "Identificação supervisionada de pontos",
            "Elétrica e hidráulica",
            "Mapeia a cadeia v01–v04 e v07–v08.1 para uma simulação segura.",
            "0.1.0",
            [".dwg"],
            ["preservar-original", "rastrear-origem", "revisar-baixa-confianca"],
            [],
            [
                new("sourceDrawing", "file", "DWG de origem somente leitura", true),
                new("semanticBaseline", "dataset", "Base semântica já importada", true)
            ],
            [
                new("identified-points", "Pontos identificados e classificados",
                    "preview/identified-points.json"),
                new("audit", "Rastreabilidade da identificação",
                    "audit/identification-plan.json")
            ],
            [
                new("source-exists", "O DWG de origem deve existir."),
                new("source-hash", "A impressão digital do original deve ser registrada."),
                new("semantic-review", "Pendências semânticas devem permanecer visíveis.",
                    false)
            ],
            [
                new("source-unchanged", "O DWG original deve permanecer byte a byte intacto."),
                new("counts-reconciled", "As contagens devem fechar com a auditoria.")
            ]);

        var mask = new AutomationMaskDefinition(
            "1.0",
            MaskContractId,
            "0.1.0",
            "Criação supervisionada da máscara",
            "Elétrica e hidráulica",
            "Mapeia v05–v06 como proposta bloqueada até os marcos 11.6G/11.6H.",
            "0.1.0",
            [".dwg"],
            ["preservar-original", "executar-em-copia", "aprovar-proposta"],
            [new(IdentificationContractId, "0.1.0")],
            [
                new("identifiedPoints", "dataset", "Pontos aprovados para a máscara", true),
                new("technicalCopy", "file", "Cópia técnica, nunca o DWG original", true)
            ],
            [
                new("mask-plan", "Plano de camadas e elementos propostos",
                    "preview/mask-plan.json"),
                new("comparison", "Comparação antes/depois",
                    "audit/before-after.json")
            ],
            [
                new("identification-approved", "A identificação deve estar aprovada."),
                new("technical-copy", "A execução só poderá usar uma cópia técnica.")
            ],
            [
                new("original-unchanged", "O desenho original deve permanecer intacto."),
                new("result-audited", "Toda alteração proposta deve ter auditoria.")
            ]);

        return
        [
            CreateLink(
                identification,
                routines,
                "Identificação, associação, exportação e direção reutilizadas como "
                + "plano somente leitura."),
            CreateLink(
                mask,
                routines,
                "Aplicação de camadas e limpeza catalogadas, mas execução bloqueada.")
        ];
    }

    private static LegacyAutomationContractLink CreateLink(
        AutomationMaskDefinition contract,
        IReadOnlyList<LegacyAutomationRoutine> routines,
        string summary) =>
        new(
            contract,
            routines
                .Where(routine => routine.ContractId == contract.Id)
                .Select(routine => routine.FileName)
                .ToArray(),
            summary);

    private static LegacyAutomationSimulation BuildSimulation(
        SemanticWorkspaceSnapshot snapshot)
    {
        var dataset = snapshot.Dataset;
        var electrical = snapshot.Points.Count(
            point => point.Discipline == SemanticDiscipline.Electrical);
        var hydraulic = snapshot.Points.Count(
            point => point.Discipline == SemanticDiscipline.Hydraulic);
        var layerCount = snapshot.Points
            .Select(point => point.SemanticLayer)
            .Concat(snapshot.Components.Select(component => component.SemanticLayer))
            .Where(layer => !string.IsNullOrWhiteSpace(layer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var pending = snapshot.Points.Count(
                          point => point.ReviewStatus
                              == SemanticReviewStatus.NeedsReview)
                      + snapshot.Directions.Count(direction => direction.NeedsReview);
        var actions = new[]
        {
            new AutomationPlanAction(1, "CATALOGAR-ROTINAS",
                "Ler metadados, comandos e dependências dos arquivos AutoLISP.", false),
            new AutomationPlanAction(2, "IDENTIFICAR-ELETRICOS",
                $"Simular a identificação de {electrical} pontos elétricos.", false),
            new AutomationPlanAction(3, "IDENTIFICAR-HIDRAULICOS",
                $"Simular a identificação de {hydraulic} pontos hidráulicos.", false),
            new AutomationPlanAction(4, "ASSOCIAR-COMPONENTES",
                $"Simular a associação de {snapshot.Components.Count} componentes e textos.",
                false),
            new AutomationPlanAction(5, "PROPOR-CAMADAS",
                $"Preparar a proposta de {layerCount} camadas semânticas.", true),
            new AutomationPlanAction(6, "PROPOR-MASCARA",
                "Preparar o plano da máscara para aprovação futura no 11.6G.", true)
        };
        var warnings = new List<string>
        {
            "As ações que escreveriam camadas ou máscara estão somente descritas; "
            + "não existe comando de aplicar nesta ponte.",
            "As rotinas v05 e v06 permanecem bloqueadas por conterem mutações do desenho."
        };
        if (dataset is null)
        {
            warnings.Add(
                "Nenhuma base semântica está carregada para o projeto ativo; "
                + "a estrutura foi catalogada, mas as contagens estão vazias.");
        }

        return new LegacyAutomationSimulation(
            dataset is not null,
            snapshot.Points.Count,
            electrical,
            hydraulic,
            snapshot.Components.Count,
            layerCount,
            snapshot.Directions.Count,
            pending,
            actions,
            warnings,
            dataset is null
                ? "Ponte catalogada. Selecione um projeto com dados semânticos para "
                  + "simular as contagens reais."
                : $"{snapshot.Points.Count} pontos e {snapshot.Components.Count} "
                  + "componentes reaproveitados; nenhuma alteração foi executada.");
    }

    private static string CalculateSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    [GeneratedRegex(@"(?im)^\s*\(defun\s+c:([^\s()]+)")]
    private static partial Regex CommandDefinitionRegex();

    [GeneratedRegex(
        @"(?i)\b(entmod|entdel|entmake|entmakex|vla-put-|vla-delete)\b|\(command[^\r\n]*(erase|purge|wblock|save)")]
    private static partial Regex MutationRegex();

    [GeneratedRegex(@"(?i)\(open\s+[^\r\n]+\s+""[wa]""|write-line")]
    private static partial Regex ReportWriteRegex();

    private sealed record RoutineProfile(
        string Version,
        string Stage,
        IReadOnlyList<string> Inputs,
        IReadOnlyList<string> Outputs,
        IReadOnlyList<string> Dependencies,
        string ContractId,
        LegacyRoutineSafety Safety,
        string SafetyReason,
        bool IsPreferred = false);
}
