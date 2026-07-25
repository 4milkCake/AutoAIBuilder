using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Infrastructure.Automation;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class AutomationContractTests
{
    [TestMethod]
    public void Validator_AcceptsVersionedCatalogAndMask()
    {
        var result = new AutomationContractValidator().Validate(
            AutomationTestData.CreateCatalog(),
            AutomationTestData.CreateMask());

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Issues.Count);
    }

    [TestMethod]
    public void Validator_RejectsUnsupportedSchemaDuplicateRuleAndUnsafeOutput()
    {
        var rule = AutomationTestData.CreateCatalog().Rules.Single();
        var catalog = AutomationTestData.CreateCatalog() with
        {
            SchemaVersion = "2.0",
            Rules = [rule, rule]
        };
        var mask = AutomationTestData.CreateMask() with
        {
            Outputs =
            [
                new AutomationMaskOutput(
                    "resultado",
                    "Resultado inválido",
                    @"..\original.txt")
            ],
            RequiredRuleIds = ["regra-ausente"]
        };

        var result = new AutomationContractValidator().Validate(catalog, mask);

        Assert.IsFalse(result.IsValid);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "contract.unsupported-schema",
                "catalog.duplicate-rule",
                "mask.unsafe-output-path",
                "mask.missing-rule"
            },
            result.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void JsonSerializer_RoundTripsCamelCaseAndRejectsUnknownProperties()
    {
        var serializer = new AutomationContractJsonSerializer();
        var json = serializer.Serialize(AutomationTestData.CreateMask());

        StringAssert.Contains(json, "\"schemaVersion\"");
        StringAssert.Contains(json, "\"supportsSimulation\"");
        var loaded = serializer.DeserializeMask(json);
        Assert.AreEqual("mascara-teste", loaded.Id);
        Assert.AreEqual("1.0.0", loaded.Version);
        Assert.AreEqual(".txt", loaded.AcceptedExtensions.Single());
        Assert.AreEqual("modo", loaded.Parameters.Single().Name);
        Assert.AreEqual("processed.txt", loaded.Outputs.Single().RelativePath);

        var withUnknownProperty = json.Replace(
            "\"schemaVersion\"",
            "\"unknown\": true,\n  \"schemaVersion\"",
            StringComparison.Ordinal);
        Assert.ThrowsException<InvalidDataException>(
            () => serializer.DeserializeMask(withUnknownProperty));
        Assert.ThrowsException<InvalidDataException>(
            () => serializer.DeserializeMask(
                """
                {
                  "schemaVersion": "1.0",
                  "id": "incompleta"
                }
                """));
    }
}

internal static class AutomationTestData
{
    public static AutomationRuleCatalog CreateCatalog() =>
        new(
            AutomationContractVersions.RuleCatalogSchema,
            "regras-teste",
            "1.0.0",
            [
                new AutomationRuleDefinition(
                    "arquivo-legivel",
                    "1.0.0",
                    "Arquivo legível",
                    "Geral",
                    AutomationRuleSeverity.Blocking,
                    "A entrada deve ser legível.",
                    "input.readable == true",
                    "AutoAIBuilder",
                    [".txt"])
            ]);

    public static AutomationMaskDefinition CreateMask() =>
        new(
            AutomationContractVersions.MaskSchema,
            "mascara-teste",
            "1.0.0",
            "Máscara de teste",
            "Geral",
            "Contrato seguro usado somente pelos testes.",
            "1.0.0",
            [".txt"],
            ["arquivo-legivel"],
            [],
            [
                new AutomationMaskParameter(
                    "modo",
                    "string",
                    "Modo da transformação.",
                    true,
                    "seguro",
                    ["seguro"])
            ],
            [
                new AutomationMaskOutput(
                    "resultado",
                    "Arquivo processado.",
                    "processed.txt")
            ],
            [
                new AutomationValidationRequirement(
                    "entrada-integra",
                    "Checksum da entrada confirmado.")
            ],
            [
                new AutomationValidationRequirement(
                    "saida-existe",
                    "Saída obrigatória existe.")
            ]);
}
