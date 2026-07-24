using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Infrastructure.Diagnostics;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class JsonLinesDiagnosticLoggerTests
{
    private string _directory = null!;
    private string _filePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "diagnostics.jsonl");
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
    public void Write_RoundTripsStructuredExceptionAndProperties()
    {
        var logger = new JsonLinesDiagnosticLogger(_filePath);

        logger.Write(
            DiagnosticLevel.Error,
            "Validation",
            "Falha controlada.",
            new InvalidOperationException("Detalhe interno"),
            new Dictionary<string, string> { ["projectId"] = "123" });

        var entry = logger.GetRecent().Single();

        Assert.AreEqual(DiagnosticLevel.Error, entry.Level);
        Assert.AreEqual("Validation", entry.Source);
        Assert.AreEqual("Falha controlada.", entry.Message);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, entry.ExceptionType);
        Assert.AreEqual("Detalhe interno", entry.ExceptionMessage);
        Assert.AreEqual("123", entry.Properties["projectId"]);
    }

    [TestMethod]
    public void GetRecent_IgnoresCorruptedLineAndKeepsValidEvents()
    {
        var logger = new JsonLinesDiagnosticLogger(_filePath);
        logger.Write(DiagnosticLevel.Information, "Application", "Evento válido.");
        File.AppendAllText(_filePath, "{json inválido}" + Environment.NewLine);

        var entries = logger.GetRecent();

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("Evento válido.", entries[0].Message);
    }

    [TestMethod]
    public void GetRecent_RejectsUnsafeLimits()
    {
        var logger = new JsonLinesDiagnosticLogger(_filePath);

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => logger.GetRecent(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => logger.GetRecent(1001));
    }
}
