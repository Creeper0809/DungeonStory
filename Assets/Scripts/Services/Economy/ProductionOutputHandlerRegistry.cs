using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Deterministic registry for automatic production handlers and explicitly
/// declared domain-output capabilities. A domain capability may describe and
/// validate frozen output provenance without participating in generic recipe
/// auto-selection.
/// </summary>
public sealed class ProductionOutputHandlerRegistry :
    IProductionOutputCapabilityRegistry
{
    public const string Schema = "production-output-handler-registry@4";

    private readonly IProductionOutputCapability[] capabilities;
    private readonly IProductionOutputHandler[] handlers;
    private readonly Dictionary<string, IProductionOutputCapability> byCapabilityId;
    private readonly IProductionOutputCapability standardCapability;

    public ProductionOutputHandlerRegistry(
        IEnumerable<IProductionOutputCapability> capabilities)
    {
        IProductionOutputCapability[] source = (capabilities
                ?? throw new ArgumentNullException(nameof(capabilities)))
            .ToArray();
        if (source.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "Production output handler registry contains a null handler.");
        }

        foreach (IProductionOutputCapability capability in source)
        {
            if (!IsCanonicalCapabilityId(capability.CapabilityId))
            {
                throw new InvalidOperationException(
                    "Production output capability has a noncanonical ID: "
                    + (capability.CapabilityId ?? "<null>"));
            }
            if (capability.ContractVersion <= 0
                || !IsCanonicalCapabilityId(capability.ComponentCodecId)
                || capability.ComponentCodecVersion <= 0)
            {
                throw new InvalidOperationException(
                    "Production output capability has invalid contract metadata: "
                    + capability.CapabilityId);
            }
            if (capability is IProductionOutputHandler handler
                && handler is not IIdempotentProductionOutputHandler)
            {
                throw new InvalidOperationException(
                    "Production output capability is not exact-once: "
                    + handler.CapabilityId);
            }
            if (capability is IProductionOutputHandler
                && capability is IProductionPreparedOutputParticipantCapability)
            {
                throw new InvalidOperationException(
                    "Prepared-output participant cannot also own per-line execution: "
                    + capability.CapabilityId);
            }
        }

        this.capabilities = source
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        for (int i = 1; i < this.capabilities.Length; i++)
        {
            if (string.Equals(
                    this.capabilities[i - 1].CapabilityId,
                    this.capabilities[i].CapabilityId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Duplicate production output capability ID: "
                    + this.capabilities[i].CapabilityId);
            }
        }

        this.handlers = this.capabilities
            .OfType<IProductionOutputHandler>()
            .ToArray();

        standardCapability = this.capabilities.SingleOrDefault(value =>
            string.Equals(
                value.CapabilityId,
                ProductionOutputCapabilityIds.StandardDefinition,
                StringComparison.Ordinal));
        if (standardCapability == null
            || standardCapability is IProductionOutputHandler
            || standardCapability.ContractVersion !=
                ProductionOutputCapabilityIds.StandardDefinitionVersion
            || !string.Equals(
                standardCapability.ComponentCodecId,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                StringComparison.Ordinal)
            || standardCapability.ComponentCodecVersion !=
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion)
        {
            throw new InvalidOperationException(
                "Production output registry requires a non-executable exact standard-definition capability.");
        }
        byCapabilityId = this.capabilities.ToDictionary(
            value => value.CapabilityId,
            value => value,
            StringComparer.Ordinal);

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

    public bool TryResolve(
        string itemId,
        out IProductionOutputHandler handler)
    {
        handler = null;
        foreach (IProductionOutputHandler candidate in handlers)
        {
            if (!candidate.SupportsAutomaticSelection)
                continue;
            if (!candidate.CanHandle(itemId))
                continue;
            if (handler != null)
            {
                throw new InvalidOperationException(
                    "Ambiguous production output capability for item '"
                    + (itemId ?? string.Empty)
                    + "': "
                    + handler.CapabilityId
                    + ", "
                    + candidate.CapabilityId);
            }
            handler = candidate;
        }
        return handler != null;
    }

    public ProductionOutputCapabilityDescriptor CaptureDescriptor(
        string outputLineId,
        string itemId)
    {
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || string.IsNullOrEmpty(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || !TryResolveAutomaticCapability(
                itemId,
                out IProductionOutputCapability capability))
        {
            throw new InvalidOperationException(
                "Production output line has no registered capability: "
                + (outputLineId ?? string.Empty)
                + "/"
                + (itemId ?? string.Empty));
        }
        return CreateDescriptor(outputLineId, itemId, capability);
    }

    public ProductionOutputCapabilityDescriptor CaptureDeclaredDescriptor(
        string outputLineId,
        string itemId,
        string capabilityId)
    {
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || string.IsNullOrEmpty(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || !byCapabilityId.TryGetValue(
                capabilityId ?? string.Empty,
                out IProductionOutputCapability capability)
            || !capability.CanHandle(itemId))
        {
            throw new InvalidOperationException(
                "Production output line has no matching declared capability: "
                + (outputLineId ?? string.Empty)
                + "/"
                + (itemId ?? string.Empty)
                + "/"
                + (capabilityId ?? string.Empty));
        }
        return CreateDescriptor(outputLineId, itemId, capability);
    }

    public bool TryValidateExact(
        ProductionOutputCapabilityDescriptor descriptor,
        out IProductionOutputCapability capability,
        out DomainFailure failure)
    {
        capability = null;
        failure = DomainFailure.None;
        if (!byCapabilityId.TryGetValue(
                descriptor.CapabilityId ?? string.Empty,
                out IProductionOutputCapability candidate)
            || candidate.ContractVersion != descriptor.CapabilityVersion
            || !string.Equals(
                candidate.ComponentCodecId,
                descriptor.ComponentCodecId,
                StringComparison.Ordinal)
            || candidate.ComponentCodecVersion != descriptor.ComponentCodecVersion
            || !candidate.CanHandle(descriptor.ItemId))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                descriptor.ItemId ?? string.Empty,
                "output-capability-missing-or-drifted");
            return false;
        }
        ProductionOutputCapabilityDescriptor expected;
        try
        {
            expected = CreateDescriptor(
                descriptor.OutputLineId,
                descriptor.ItemId,
                candidate);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                descriptor.ItemId ?? string.Empty,
                "output-capability-descriptor-invalid");
            return false;
        }
        if (!string.Equals(
                expected.Fingerprint,
                descriptor.Fingerprint,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                descriptor.ItemId ?? string.Empty,
                "output-capability-fingerprint-drift");
            return false;
        }
        capability = candidate;
        return true;
    }

    public bool TryResolveExact(
        ProductionOutputCapabilityDescriptor descriptor,
        out IProductionOutputHandler handler,
        out DomainFailure failure)
    {
        handler = null;
        failure = DomainFailure.None;
        if (!TryValidateExact(
                descriptor,
                out IProductionOutputCapability capability,
                out failure)
            || capability is not IProductionOutputHandler candidate)
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    descriptor.ItemId ?? string.Empty,
                    "output-capability-not-executable-by-production-bill");
            }
            return false;
        }
        handler = candidate;
        return true;
    }

    private bool TryResolveAutomaticCapability(
        string itemId,
        out IProductionOutputCapability capability)
    {
        capability = null;
        foreach (IProductionOutputCapability candidate in capabilities)
        {
            if (ReferenceEquals(candidate, standardCapability)
                || !candidate.SupportsAutomaticSelection
                || !candidate.CanHandle(itemId))
            {
                continue;
            }
            if (capability != null)
            {
                throw new InvalidOperationException(
                    "Ambiguous production output capability for item '"
                    + (itemId ?? string.Empty)
                    + "': "
                    + capability.CapabilityId
                    + ", "
                    + candidate.CapabilityId);
            }
            capability = candidate;
        }
        if (capability != null)
            return true;
        if (!standardCapability.CanHandle(itemId))
            return false;
        capability = standardCapability;
        return true;
    }

    private static ProductionOutputCapabilityDescriptor CreateDescriptor(
        string outputLineId,
        string itemId,
        IProductionOutputCapability capability)
    {
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || string.IsNullOrEmpty(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Production output capability descriptor source is invalid.");
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
        IReadOnlyList<IProductionOutputCapability> ordered)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Count);
        foreach (IProductionOutputCapability capability in ordered)
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

    private static bool IsCanonicalCapabilityId(string value)
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
                || character == ':'
                || character == '-'
                || character == '.';
            if (!valid)
                return false;
        }
        return true;
    }
}
