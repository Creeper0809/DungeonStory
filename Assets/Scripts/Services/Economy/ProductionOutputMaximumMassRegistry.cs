using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Deterministic, execution-free registry for production output upper bounds.
/// It deliberately owns no publication handler and performs no world mutation.
/// </summary>
public sealed class ProductionOutputMaximumMassRegistry :
    IProductionOutputMaximumMassRegistry,
    IProductionOutputMaximumMassCapabilitySelector
{
    public const string Schema =
        "production-output-maximum-mass-registry@2";

    private readonly IProductionOutputMaximumMassCapability[] capabilities;
    private readonly Dictionary<string, IProductionOutputMaximumMassCapability>
        byCapabilityId;
    private readonly IProductionOutputMaximumMassCapability standardCapability;
    private readonly IPhysicalItemMassQuery massQuery;

    public ProductionOutputMaximumMassRegistry(
        IEnumerable<IProductionOutputMaximumMassCapability> capabilities,
        IPhysicalItemMassQuery massQuery)
    {
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        IProductionOutputMaximumMassCapability[] source = (capabilities
                ?? throw new ArgumentNullException(nameof(capabilities)))
            .ToArray();
        if (source.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "Production output maximum-mass registry contains a null capability.");
        }

        foreach (IProductionOutputMaximumMassCapability capability in source)
        {
            if (!Canonical(capability.CapabilityId)
                || capability.ContractVersion <= 0
                || !Canonical(capability.ComponentCodecId)
                || capability.ComponentCodecVersion <= 0)
            {
                throw new InvalidOperationException(
                    "Production output maximum-mass capability metadata is invalid: "
                    + (capability.CapabilityId ?? string.Empty));
            }
        }

        this.capabilities = source
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 1; index < this.capabilities.Length; index++)
        {
            if (string.Equals(
                    this.capabilities[index - 1].CapabilityId,
                    this.capabilities[index].CapabilityId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Duplicate production output maximum-mass capability ID: "
                    + this.capabilities[index].CapabilityId);
            }
        }

        byCapabilityId = this.capabilities.ToDictionary(
            value => value.CapabilityId,
            value => value,
            StringComparer.Ordinal);
        standardCapability = this.capabilities.SingleOrDefault(value =>
            string.Equals(
                value.CapabilityId,
                ProductionOutputCapabilityIds.StandardDefinition,
                StringComparison.Ordinal));
        if (standardCapability == null
            || standardCapability.ContractVersion
                != ProductionOutputCapabilityIds.StandardDefinitionVersion
            || !string.Equals(
                standardCapability.ComponentCodecId,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                StringComparison.Ordinal)
            || standardCapability.ComponentCodecVersion
                != ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion)
        {
            throw new InvalidOperationException(
                "Production output maximum-mass registry requires the exact standard-definition capability.");
        }

        CapabilityIds = Array.AsReadOnly(
            this.capabilities.Select(value => value.CapabilityId).ToArray());
        CapabilityContracts = Array.AsReadOnly(this.capabilities
            .Select(value => new ProductionOutputCapabilityContractSnapshot(
                value.CapabilityId,
                value.ContractVersion,
                value.ComponentCodecId,
                value.ComponentCodecVersion,
                value.SupportsAutomaticSelection,
                value is IProductionPreparedOutputParticipantCapability))
            .ToArray());
        RegistryFingerprint = ComputeFingerprint(this.capabilities);
    }

    public IReadOnlyList<string> CapabilityIds { get; }

    public IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
        CapabilityContracts { get; }

    public string RegistryFingerprint { get; }

    public ProductionOutputMaximumMassProjection CaptureAutomatic(
        string outputLineId,
        string itemId,
        int maximumQuantity)
    {
        IProductionOutputMaximumMassCapability capability =
            ResolveAutomatic(itemId);
        ProductionOutputCapabilityDescriptor descriptor = CreateDescriptor(
            outputLineId,
            itemId,
            capability);
        return Capture(capability, descriptor, maximumQuantity);
    }

    public ProductionOutputMaximumMassProjection CaptureForCapability(
        string outputLineId,
        string itemId,
        string capabilityId,
        int maximumQuantity)
    {
        if (!Canonical(capabilityId)
            || !byCapabilityId.TryGetValue(
                capabilityId,
                out IProductionOutputMaximumMassCapability capability))
        {
            throw new InvalidOperationException(
                "Production output maximum-mass capability is missing: "
                + (capabilityId ?? string.Empty));
        }
        if (!capability.CanHandle(itemId))
        {
            throw new InvalidOperationException(
                "Production output maximum-mass capability cannot handle item '"
                + (itemId ?? string.Empty)
                + "': "
                + capabilityId);
        }
        ProductionOutputCapabilityDescriptor descriptor = CreateDescriptor(
            outputLineId,
            itemId,
            capability);
        return Capture(capability, descriptor, maximumQuantity);
    }

    public ProductionOutputMaximumMassProjection CaptureDeclared(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity)
    {
        if (!byCapabilityId.TryGetValue(
                descriptor.CapabilityId ?? string.Empty,
                out IProductionOutputMaximumMassCapability capability))
        {
            throw new InvalidOperationException(
                "Declared production output maximum-mass capability is missing: "
                + (descriptor.CapabilityId ?? string.Empty));
        }
        return Capture(capability, descriptor, maximumQuantity);
    }

    private ProductionOutputMaximumMassProjection Capture(
        IProductionOutputMaximumMassCapability capability,
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity)
    {
        if (maximumQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumQuantity));
        ProductionOutputMaximumMassProjection projection =
            capability.CaptureDefinitionMaximum(
                descriptor,
                maximumQuantity,
                massQuery);
        if (!string.Equals(
                projection.Descriptor.Fingerprint,
                descriptor.Fingerprint,
                StringComparison.Ordinal)
            || projection.MaximumQuantity != maximumQuantity
            || projection.MassAuthorityRevision != massQuery.AuthorityRevision)
        {
            throw new InvalidOperationException(
                "Production output maximum-mass capability returned a drifted projection.");
        }
        return projection;
    }

    private IProductionOutputMaximumMassCapability ResolveAutomatic(
        string itemId)
    {
        IProductionOutputMaximumMassCapability resolved = null;
        foreach (IProductionOutputMaximumMassCapability candidate in capabilities)
        {
            if (ReferenceEquals(candidate, standardCapability)
                || !candidate.SupportsAutomaticSelection
                || !candidate.CanHandle(itemId))
            {
                continue;
            }
            if (resolved != null)
            {
                throw new InvalidOperationException(
                    "Ambiguous automatic production output maximum-mass capability for item '"
                    + (itemId ?? string.Empty)
                    + "': "
                    + resolved.CapabilityId
                    + ", "
                    + candidate.CapabilityId);
            }
            resolved = candidate;
        }
        if (resolved != null)
            return resolved;
        if (standardCapability.CanHandle(itemId))
            return standardCapability;
        throw new InvalidOperationException(
            "Production output item has no automatic maximum-mass capability: "
            + (itemId ?? string.Empty));
    }

    private static ProductionOutputCapabilityDescriptor CreateDescriptor(
        string outputLineId,
        string itemId,
        IProductionOutputMaximumMassCapability capability)
    {
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || string.IsNullOrEmpty(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production output maximum-mass descriptor source is invalid.");
        }
        return new ProductionOutputCapabilityDescriptor(
            outputLineId,
            itemId,
            capability.CapabilityId,
            capability.ContractVersion,
            capability.ComponentCodecId,
            capability.ComponentCodecVersion,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                outputLineId,
                itemId,
                capability.CapabilityId,
                capability.ContractVersion,
                capability.ComponentCodecId,
                capability.ComponentCodecVersion));
    }

    private static string ComputeFingerprint(
        IReadOnlyList<IProductionOutputMaximumMassCapability> ordered)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Count);
        foreach (IProductionOutputMaximumMassCapability capability in ordered)
        {
            digest.Append(capability.CapabilityId);
            digest.Append(capability.ContractVersion);
            digest.Append(capability.ComponentCodecId);
            digest.Append(capability.ComponentCodecVersion);
            digest.Append(capability.SupportsAutomaticSelection);
            digest.Append(
                capability is IProductionPreparedOutputParticipantCapability);
            digest.Append(capability.GetType().FullName ?? string.Empty);
        }
        return digest.ComputeSha256();
    }

    private static bool Canonical(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }
        foreach (char character in value)
        {
            bool valid = character is >= 'a' and <= 'z'
                || character is >= '0' and <= '9'
                || character is ':' or '-' or '.';
            if (!valid)
                return false;
        }
        return true;
    }
}
