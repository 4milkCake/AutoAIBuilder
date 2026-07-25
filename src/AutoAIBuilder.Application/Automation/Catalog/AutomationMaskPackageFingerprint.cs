using System.Security.Cryptography;
using System.Text;

namespace AutoAIBuilder.Application.Automation.Catalog;

public static class AutomationMaskPackageFingerprint
{
    private const string DomainSeparator =
        "AUTOAIBUILDER-MASK-PACKAGE-1\nMASK\n";

    public static string Compute(
        string canonicalMaskJson,
        string canonicalRuleCatalogJson)
    {
        if (string.IsNullOrWhiteSpace(canonicalMaskJson))
        {
            throw new ArgumentException(
                "O contrato normalizado da máscara é obrigatório.",
                nameof(canonicalMaskJson));
        }

        if (string.IsNullOrWhiteSpace(canonicalRuleCatalogJson))
        {
            throw new ArgumentException(
                "O catálogo normalizado de regras é obrigatório.",
                nameof(canonicalRuleCatalogJson));
        }

        var content = Encoding.UTF8.GetBytes(
            DomainSeparator
            + canonicalMaskJson
            + "\nRULE-CATALOG\n"
            + canonicalRuleCatalogJson);
        return Convert.ToHexString(SHA256.HashData(content));
    }
}
