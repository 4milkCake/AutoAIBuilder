using System.Resources;
using AutoAIBuilder.Desktop;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class WpfResourceSmokeTests
{
    [TestMethod]
    public void DesktopAssembly_EmbedsRequiredWpfResources()
    {
        var assembly = typeof(App).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "AutoAIBuilder.Desktop.g.resources");

        Assert.IsNotNull(
            stream,
            "O assembly desktop precisa incorporar os recursos WPF.");

        using var reader = new ResourceReader(stream);
        var resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enumerator = reader.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Key is string resourceName)
            {
                resources.Add(resourceName);
            }
        }

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "app.baml",
                "mainwindow.baml",
                "views/automationpilotview.baml"
            },
            resources.ToArray(),
            "Os recursos WPF indispensáveis não foram incorporados.");
    }
}
