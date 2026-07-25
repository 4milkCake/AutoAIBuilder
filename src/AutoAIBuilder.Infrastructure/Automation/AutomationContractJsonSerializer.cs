using System.Text.Json;
using System.Text.Json.Serialization;
using AutoAIBuilder.Application.Automation.Contracts;

namespace AutoAIBuilder.Infrastructure.Automation;

public sealed class AutomationContractJsonSerializer :
    IAutomationContractSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public AutomationRuleCatalog DeserializeRuleCatalog(string json)
    {
        var catalog = Deserialize<AutomationRuleCatalog>(
            json,
            "catálogo de regras");
        if (catalog.Rules is null
            || catalog.Rules.Any(rule =>
                rule is null || rule.SupportedExtensions is null))
        {
            throw new InvalidDataException(
                "O catálogo de regras não contém todas as coleções obrigatórias.");
        }

        return catalog;
    }

    public AutomationMaskDefinition DeserializeMask(string json)
    {
        var mask = Deserialize<AutomationMaskDefinition>(json, "máscara");
        if (mask.AcceptedExtensions is null
            || mask.RequiredRuleIds is null
            || mask.Dependencies is null
            || mask.Parameters is null
            || mask.Outputs is null
            || mask.Preconditions is null
            || mask.Postconditions is null
            || mask.Dependencies.Any(dependency => dependency is null)
            || mask.Parameters.Any(parameter => parameter is null)
            || mask.Outputs.Any(output => output is null)
            || mask.Preconditions.Any(requirement => requirement is null)
            || mask.Postconditions.Any(requirement => requirement is null))
        {
            throw new InvalidDataException(
                "A máscara não contém todas as coleções obrigatórias.");
        }

        return mask;
    }

    public string Serialize(AutomationRuleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return JsonSerializer.Serialize(catalog, Options);
    }

    public string Serialize(AutomationMaskDefinition mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        return JsonSerializer.Serialize(mask, Options);
    }

    private static T Deserialize<T>(string json, string contractName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException(
                $"O JSON do {contractName} está vazio.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                   ?? throw new InvalidDataException(
                       $"O JSON do {contractName} não contém um documento.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"O JSON do {contractName} não segue o formato esperado.",
                exception);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true
        };
        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));
        return options;
    }
}
