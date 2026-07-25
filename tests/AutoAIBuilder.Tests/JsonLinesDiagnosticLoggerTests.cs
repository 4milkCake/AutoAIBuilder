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

    [TestMethod]
    public void Write_RedactsCredentialsAndLimitsOversizedMessages()
    {
        var options = new DiagnosticLogRetentionOptions
        {
            MaximumFileSizeBytes = 4_096,
            MaximumArchiveFiles = 2,
            MaximumEntrySizeBytes = 2_048,
            MaximumMessageLength = 128,
            MaximumStackTraceLength = 512,
            MaximumPropertyCount = 8,
            MaximumPropertyValueLength = 64
        };
        var logger = new JsonLinesDiagnosticLogger(_filePath, options);
        var sensitiveMessage =
            "Falha token=abc123 password=supersecreta "
            + new string('x', 500);

        logger.Write(
            DiagnosticLevel.Error,
            "SecurityTest",
            sensitiveMessage,
            new InvalidOperationException("senha=minha-senha"),
            new Dictionary<string, string>
            {
                ["apiKey"] = "chave-real",
                ["detail"] = "authorization=BearerToken"
            });

        var entry = logger.GetRecent().Single();
        var rawContent = File.ReadAllText(_filePath);

        Assert.IsTrue(entry.Message.Length <= options.MaximumMessageLength);
        StringAssert.Contains(entry.Message, "token=[REMOVIDO]");
        Assert.AreEqual("[REMOVIDO]", entry.Properties["apiKey"]);
        StringAssert.Contains(entry.Properties["detail"], "[REMOVIDO]");
        StringAssert.Contains(entry.ExceptionMessage, "[REMOVIDO]");
        Assert.IsFalse(rawContent.Contains("abc123", StringComparison.Ordinal));
        Assert.IsFalse(rawContent.Contains("supersecreta", StringComparison.Ordinal));
        Assert.IsFalse(rawContent.Contains("chave-real", StringComparison.Ordinal));
        Assert.IsFalse(rawContent.Contains("minha-senha", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Write_RotatesAndRetainsOnlyConfiguredArchiveCount()
    {
        var options = new DiagnosticLogRetentionOptions
        {
            MaximumFileSizeBytes = 1_200,
            MaximumArchiveFiles = 2,
            MaximumEntrySizeBytes = 600,
            MaximumMessageLength = 200,
            MaximumStackTraceLength = 256,
            MaximumPropertyCount = 4,
            MaximumPropertyValueLength = 64
        };
        var logger = new JsonLinesDiagnosticLogger(_filePath, options);

        for (var index = 0; index < 30; index++)
        {
            logger.Write(
                DiagnosticLevel.Information,
                "Rotation",
                $"Evento {index:00} {new string('x', 120)}");
        }

        var recent = logger.GetRecent(100);
        var firstArchive = Path.Combine(_directory, "diagnostics.1.jsonl");
        var secondArchive = Path.Combine(_directory, "diagnostics.2.jsonl");
        var discardedArchive = Path.Combine(_directory, "diagnostics.3.jsonl");

        Assert.AreEqual(2, logger.ArchiveCount);
        Assert.IsTrue(File.Exists(_filePath));
        Assert.IsTrue(File.Exists(firstArchive));
        Assert.IsTrue(File.Exists(secondArchive));
        Assert.IsFalse(File.Exists(discardedArchive));
        StringAssert.StartsWith(recent[0].Message, "Evento 29");
        Assert.IsTrue(recent.Count < 30);
        Assert.IsTrue(
            new[] { _filePath, firstArchive, secondArchive }
                .All(path =>
                    new FileInfo(path).Length <=
                    options.MaximumFileSizeBytes));
    }
}
