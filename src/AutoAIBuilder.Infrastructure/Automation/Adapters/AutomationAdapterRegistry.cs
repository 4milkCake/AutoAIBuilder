using System.Text.RegularExpressions;
using AutoAIBuilder.Application.Automation.Adapters;

namespace AutoAIBuilder.Infrastructure.Automation.Adapters;

public sealed partial class AutomationAdapterRegistry :
    IAutomationAdapterRegistry
{
    private readonly IReadOnlyList<IAutomationAdapter> _adapters;

    public AutomationAdapterRegistry(
        IEnumerable<IAutomationAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        var registered = adapters.ToArray();
        foreach (var adapter in registered)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            Validate(adapter.Descriptor);
        }

        var duplicateAdapter = registered
            .GroupBy(
                adapter => (
                    adapter.Descriptor.AdapterId,
                    adapter.Descriptor.AdapterVersion))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAdapter is not null)
        {
            throw new InvalidOperationException(
                "O registro contém mais de um adaptador com a identidade "
                + $"'{duplicateAdapter.Key.AdapterId}@"
                + $"{duplicateAdapter.Key.AdapterVersion}'.");
        }

        var duplicateContract = registered
            .GroupBy(
                adapter => (
                    adapter.Descriptor.MaskId,
                    adapter.Descriptor.MaskVersion))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateContract is not null)
        {
            throw new InvalidOperationException(
                "Mais de um adaptador reivindica o mesmo contrato homologado "
                + $"'{duplicateContract.Key.MaskId}@"
                + $"{duplicateContract.Key.MaskVersion}'.");
        }

        _adapters = registered;
    }

    public IReadOnlyList<AutomationAdapterDescriptor> GetAll() =>
        _adapters
            .Select(adapter => adapter.Descriptor)
            .OrderBy(descriptor => descriptor.DisplayName)
            .ThenBy(descriptor => descriptor.AdapterVersion)
            .ToArray();

    public AutomationAdapterResolution Resolve(
        string maskId,
        string maskVersion,
        string contractSha256)
    {
        if (string.IsNullOrWhiteSpace(maskId)
            || string.IsNullOrWhiteSpace(maskVersion)
            || !IsSha256(contractSha256))
        {
            return new AutomationAdapterResolution(
                AutomationAdapterResolutionStatus.NotRegistered,
                null,
                null,
                "A identidade ou a impressão digital do contrato é inválida.");
        }

        var candidates = _adapters.Where(
                adapter =>
                    string.Equals(
                        adapter.Descriptor.MaskId,
                        maskId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        adapter.Descriptor.MaskVersion,
                        maskVersion,
                        StringComparison.Ordinal))
            .ToArray();
        if (candidates.Length == 0)
        {
            return new AutomationAdapterResolution(
                AutomationAdapterResolutionStatus.NotRegistered,
                null,
                null,
                $"Nenhum adaptador interno foi registrado para "
                + $"'{maskId}@{maskVersion}'.");
        }

        var exact = candidates.SingleOrDefault(
            adapter => string.Equals(
                adapter.Descriptor.ContractSha256,
                contractSha256,
                StringComparison.OrdinalIgnoreCase));
        if (exact is null)
        {
            return new AutomationAdapterResolution(
                AutomationAdapterResolutionStatus.ContractMismatch,
                candidates[0].Descriptor,
                null,
                "Existe um adaptador para essa identidade, mas o SHA-256 do "
                + "contrato não corresponde à versão homologada no código.");
        }

        if (!exact.Descriptor.IsEnabled)
        {
            return new AutomationAdapterResolution(
                AutomationAdapterResolutionStatus.Disabled,
                exact.Descriptor,
                null,
                $"O adaptador '{exact.Descriptor.DisplayName}' está desabilitado.");
        }

        return new AutomationAdapterResolution(
            AutomationAdapterResolutionStatus.Ready,
            exact.Descriptor,
            exact,
            $"Adaptador interno '{exact.Descriptor.DisplayName}' resolvido "
            + "por identidade, versão e SHA-256 exatos.");
    }

    private static void Validate(AutomationAdapterDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!IdentifierPattern().IsMatch(descriptor.AdapterId)
            || !IdentifierPattern().IsMatch(descriptor.MaskId)
            || !StableVersionPattern().IsMatch(descriptor.AdapterVersion)
            || !StableVersionPattern().IsMatch(descriptor.MaskVersion)
            || string.IsNullOrWhiteSpace(descriptor.DisplayName)
            || descriptor.DisplayName.Length > 200
            || string.IsNullOrWhiteSpace(descriptor.Description)
            || descriptor.Description.Length > 2_000
            || string.IsNullOrWhiteSpace(descriptor.Provider)
            || descriptor.Provider.Length > 200
            || !IsSha256(descriptor.ContractSha256)
            || !Enum.IsDefined(descriptor.Origin)
            || (!descriptor.SupportsSimulation
                && !descriptor.SupportsApply)
            || (descriptor.CatalogExecutionEnabled
                && !descriptor.SupportsApply))
        {
            throw new InvalidDataException(
                "O descritor de adaptador não atende ao contrato seguro do "
                + "registro interno.");
        }
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9.-]{0,126}[a-z0-9])?$")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex StableVersionPattern();

}
