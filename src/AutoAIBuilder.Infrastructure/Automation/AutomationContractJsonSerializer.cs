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
        ValidateRuleCatalogDocument(json);
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
        ValidateMaskDocument(json);
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

    private static void ValidateRuleCatalogDocument(string json)
    {
        using var document = ParseDocument(json, "catálogo de regras");
        var root = RequireObject(document.RootElement, "catálogo de regras");
        RequireProperties(
            root,
            "catálogo de regras",
            "schemaVersion",
            "catalogId",
            "version",
            "rules");
        foreach (var rule in RequireArray(root, "rules", "catálogo de regras"))
        {
            RequireProperties(
                RequireObject(rule, "regra"),
                "regra",
                "id",
                "version",
                "name",
                "discipline",
                "severity",
                "description",
                "condition",
                "source",
                "supportedExtensions",
                "isEnabled");
        }
    }

    private static void ValidateMaskDocument(string json)
    {
        using var document = ParseDocument(json, "máscara");
        var root = RequireObject(document.RootElement, "máscara");
        RequireProperties(
            root,
            "máscara",
            "schemaVersion",
            "id",
            "version",
            "name",
            "discipline",
            "description",
            "minimumApplicationVersion",
            "acceptedExtensions",
            "requiredRuleIds",
            "dependencies",
            "parameters",
            "outputs",
            "preconditions",
            "postconditions",
            "supportsSimulation",
            "isIdempotent");

        ValidateArrayObjects(
            root,
            "dependencies",
            "dependência",
            "id",
            "minimumVersion",
            "isRequired");
        ValidateArrayObjects(
            root,
            "parameters",
            "parâmetro",
            "name",
            "type",
            "description",
            "isRequired");
        ValidateArrayObjects(
            root,
            "outputs",
            "saída",
            "id",
            "description",
            "relativePath",
            "isRequired");
        ValidateArrayObjects(
            root,
            "preconditions",
            "pré-condição",
            "id",
            "description",
            "isBlocking");
        ValidateArrayObjects(
            root,
            "postconditions",
            "pós-condição",
            "id",
            "description",
            "isBlocking");
    }

    private static JsonDocument ParseDocument(string json, string contractName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException(
                $"O JSON do {contractName} está vazio.");
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"O JSON do {contractName} não segue o formato esperado.",
                exception);
        }
    }

    private static JsonElement RequireObject(
        JsonElement element,
        string owner)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"O documento de {owner} deve ser um objeto JSON.");
        }

        return element;
    }

    private static JsonElement.ArrayEnumerator RequireArray(
        JsonElement owner,
        string propertyName,
        string ownerName)
    {
        if (!owner.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"O campo '{propertyName}' de {ownerName} deve ser uma lista.");
        }

        return property.EnumerateArray();
    }

    private static void ValidateArrayObjects(
        JsonElement root,
        string propertyName,
        string itemName,
        params string[] requiredProperties)
    {
        foreach (var item in RequireArray(root, propertyName, "máscara"))
        {
            RequireProperties(
                RequireObject(item, itemName),
                itemName,
                requiredProperties);
        }
    }

    private static void RequireProperties(
        JsonElement element,
        string owner,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out _))
            {
                throw new InvalidDataException(
                    $"O campo obrigatório '{propertyName}' não foi informado "
                    + $"em {owner}.");
            }
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
