using System.Text;
using AutoAIBuilder.Infrastructure.Reports;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class SimplePdfReportRendererTests
{
    [TestMethod]
    public void Render_CreatesValidPdfStructure()
    {
        var bytes = new SimplePdfReportRenderer().Render(
            "RELATÓRIO DE PRONTIDÃO\nProjeto: Edifício Águas Claras\nStatus: APROVADO");
        var document = Encoding.ASCII.GetString(bytes);

        Assert.IsTrue(document.StartsWith("%PDF-1.4", StringComparison.Ordinal));
        StringAssert.Contains(document, "/Type /Catalog");
        StringAssert.Contains(document, "/Type /Page");
        StringAssert.Contains(document, "/Encoding /WinAnsiEncoding");
        StringAssert.Contains(document, "xref");
        StringAssert.Contains(document, "%%EOF");
        Assert.IsTrue(bytes.Length > 500);
    }

    [TestMethod]
    public void Render_LongReportCreatesMultiplePages()
    {
        var content = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 125).Select(index => $"Linha técnica número {index}."));

        var bytes = new SimplePdfReportRenderer().Render(content);
        var document = Encoding.ASCII.GetString(bytes);

        StringAssert.Contains(document, "/Count 3");
    }

    [TestMethod]
    public void Render_RejectsEmptyContent()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new SimplePdfReportRenderer().Render(" "));
    }
}
