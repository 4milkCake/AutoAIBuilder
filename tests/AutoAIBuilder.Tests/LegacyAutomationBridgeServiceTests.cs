using System.Security.Cryptography;
using AutoAIBuilder.Application.Automation.Bridge;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Infrastructure.Automation;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class LegacyAutomationBridgeServiceTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void Analyze_CatalogsCommandsDependenciesAndBlocksMutation()
    {
        File.WriteAllText(
            Path.Combine(_directory, "mascara_previsualizar_v04.lsp"),
            "(defun c:MASCARA_V04_PREPARAR () (princ))");
        File.WriteAllText(
            Path.Combine(_directory, "mascara_camadas_v05.lsp"),
            "(defun c:MASCARA_V05_APLICAR_CAMADAS () "
            + "(entmod (subst '(8 . \"NOVA\") (assoc 8 e) e)))");

        var result = new LegacyAutomationBridgeService().Analyze(
            _directory,
            SemanticWorkspaceSnapshot.Empty);

        Assert.AreEqual(2, result.Routines.Count);
        var preview = result.Routines.Single(
            routine => routine.FileName == "mascara_previsualizar_v04.lsp");
        CollectionAssert.Contains(
            preview.Commands.ToArray(),
            "MASCARA_V04_PREPARAR");
        Assert.AreEqual(
            LegacyRoutineSafety.ReportOutputOnly,
            preview.Safety);

        var layers = result.Routines.Single(
            routine => routine.FileName == "mascara_camadas_v05.lsp");
        Assert.AreEqual(
            LegacyRoutineSafety.DrawingMutationBlocked,
            layers.Safety);
        CollectionAssert.Contains(
            layers.Dependencies.ToArray(),
            "mascara_previsualizar_v04.lsp");
        Assert.IsFalse(
            result.ContractLinks.Single(
                link => link.Contract.Id
                    == "autoaibuilder.criacao-mascara").ApplyEnabled);
    }

    [TestMethod]
    public void Analyze_DoesNotModifySourceAndOnlyBuildsSimulation()
    {
        var path = Path.Combine(_directory, "mascara_exportar_v07.lsp");
        File.WriteAllText(
            path,
            "(defun c:MASCARA_V07_EXPORTAR () "
            + "(setq f (open \"resultado.csv\" \"w\")) "
            + "(write-line \"ok\" f) (close f))");
        var before = CalculateSha256(path);
        var beforeWriteTime = File.GetLastWriteTimeUtc(path);

        var result = new LegacyAutomationBridgeService().Analyze(
            _directory,
            SemanticWorkspaceSnapshot.Empty);

        Assert.IsTrue(result.SourceFilesUnchanged);
        Assert.AreEqual(before, CalculateSha256(path));
        Assert.AreEqual(beforeWriteTime, File.GetLastWriteTimeUtc(path));
        Assert.IsTrue(
            result.SafetyStatement.Contains(
                "nenhum AutoLISP foi carregado ou executado",
                StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(
            result.Simulation.Actions.Any(
                action => action.Code == "PROPOR-MASCARA"
                          && action.WritesDuringApply));
        Assert.IsTrue(
            result.Simulation.Warnings.Any(
                warning => warning.Contains(
                    "não aplicada",
                    StringComparison.OrdinalIgnoreCase)
                           || warning.Contains(
                               "não existe comando de aplicar",
                               StringComparison.OrdinalIgnoreCase)));
    }

    private static string CalculateSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
