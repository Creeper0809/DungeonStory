using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

public sealed class ProductionResolvedOutputRestoreValidationContext
{
    public ProductionResolvedOutputRestoreValidationContext(
        ProductionBillSaveData bill,
        ProductionResolvedOutputSaveData output,
        ProductionOutputCapabilityDescriptor descriptor,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputDetachedFacilityCapacityProjection facilityCapacity,
        FacilityBufferPlannedOutputRestoreBatchSnapshot physical,
        bool isPendingPhysical,
        ProductionExactOutputPublicationSaveData envelope)
    {
        Bill = bill ?? throw new ArgumentNullException(nameof(bill));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Descriptor = descriptor;
        MaximumMassProof = maximumMassProof
            ?? throw new ArgumentNullException(nameof(maximumMassProof));
        FacilityCapacity = facilityCapacity;
        Physical = physical ?? throw new ArgumentNullException(nameof(physical));
        IsPendingPhysical = isPendingPhysical;
        Envelope = envelope ?? ProductionExactOutputPublicationSaveData.Empty();
    }

    public ProductionBillSaveData Bill { get; }
    public ProductionResolvedOutputSaveData Output { get; }
    public ProductionOutputCapabilityDescriptor Descriptor { get; }
    public ProductionOutputBatchMaximumMassProof MaximumMassProof { get; }
    public ProductionOutputDetachedFacilityCapacityProjection FacilityCapacity { get; }
    public FacilityBufferPlannedOutputRestoreBatchSnapshot Physical { get; }
    public bool IsPendingPhysical { get; }
    public ProductionExactOutputPublicationSaveData Envelope { get; }
}

public interface IProductionResolvedOutputRestoreCapabilityValidator
{
    string CapabilityId { get; }
    int ContractVersion { get; }
    string ComponentCodecId { get; }
    int ComponentCodecVersion { get; }

    void Validate(ProductionResolvedOutputRestoreValidationContext context);
}

public interface IProductionResolvedOutputRestoreCapabilityValidatorRegistry
{
    bool RequiresValidation(ProductionOutputCapabilityDescriptor descriptor);
    void Validate(ProductionResolvedOutputRestoreValidationContext context);
}

public sealed class ProductionResolvedOutputRestoreCapabilityValidatorRegistry :
    IProductionResolvedOutputRestoreCapabilityValidatorRegistry
{
    private readonly Dictionary<string,
        IProductionResolvedOutputRestoreCapabilityValidator> byCapabilityId;

    public ProductionResolvedOutputRestoreCapabilityValidatorRegistry(
        IEnumerable<IProductionResolvedOutputRestoreCapabilityValidator> validators)
    {
        IProductionResolvedOutputRestoreCapabilityValidator[] ordered =
            (validators ?? throw new ArgumentNullException(nameof(validators)))
            .OrderBy(value => value?.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null
            || !Canonical(value.CapabilityId)
            || value.ContractVersion <= 0
            || !Canonical(value.ComponentCodecId)
            || value.ComponentCodecVersion <= 0))
        {
            throw new InvalidOperationException(
                "Production restore capability validator metadata is invalid.");
        }
        byCapabilityId = new Dictionary<string,
            IProductionResolvedOutputRestoreCapabilityValidator>(
            StringComparer.Ordinal);
        foreach (IProductionResolvedOutputRestoreCapabilityValidator validator in
                 ordered)
        {
            if (!byCapabilityId.TryAdd(validator.CapabilityId, validator))
            {
                throw new InvalidOperationException(
                    "Duplicate production restore capability validator: "
                    + validator.CapabilityId);
            }
        }
    }

    [Inject]
    public ProductionResolvedOutputRestoreCapabilityValidatorRegistry(
        IEnumerable<IProductionResolvedOutputRestoreCapabilityValidator> validators,
        IEnumerable<IProductionOutputCapability> capabilities)
        : this(validators)
    {
        IProductionOutputCapability[] exactHandlers = (capabilities
                ?? throw new ArgumentNullException(nameof(capabilities)))
            .Where(value => value is IIdempotentProductionOutputHandler)
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        foreach (IProductionOutputCapability capability in exactHandlers)
        {
            if (!byCapabilityId.TryGetValue(
                    capability.CapabilityId,
                    out IProductionResolvedOutputRestoreCapabilityValidator
                        validator)
                || validator.ContractVersion != capability.ContractVersion
                || !string.Equals(
                    validator.ComponentCodecId,
                    capability.ComponentCodecId,
                    StringComparison.Ordinal)
                || validator.ComponentCodecVersion
                    != capability.ComponentCodecVersion)
            {
                throw new InvalidOperationException(
                    "Exact production output capability has no matching restore validator: "
                    + capability.CapabilityId);
            }
        }
        if (byCapabilityId.Keys.Any(id => exactHandlers.All(value =>
                !string.Equals(value.CapabilityId, id, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "Production restore validator has no exact output capability.");
        }
    }

    public bool RequiresValidation(
        ProductionOutputCapabilityDescriptor descriptor) =>
        byCapabilityId.ContainsKey(descriptor.CapabilityId ?? string.Empty);

    public void Validate(ProductionResolvedOutputRestoreValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        ProductionOutputCapabilityDescriptor descriptor = context.Descriptor;
        if (!byCapabilityId.TryGetValue(
                descriptor.CapabilityId ?? string.Empty,
                out IProductionResolvedOutputRestoreCapabilityValidator validator)
            || validator.ContractVersion != descriptor.CapabilityVersion
            || !string.Equals(
                validator.ComponentCodecId,
                descriptor.ComponentCodecId,
                StringComparison.Ordinal)
            || validator.ComponentCodecVersion != descriptor.ComponentCodecVersion)
        {
            throw new InvalidOperationException(
                "Production restore capability validator is unavailable or drifted: "
                + (descriptor.CapabilityId ?? string.Empty));
        }
        validator.Validate(context);
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class EmptyProductionResolvedOutputRestoreCapabilityValidatorRegistry :
    IProductionResolvedOutputRestoreCapabilityValidatorRegistry
{
    public static readonly
        EmptyProductionResolvedOutputRestoreCapabilityValidatorRegistry Instance =
            new();

    private EmptyProductionResolvedOutputRestoreCapabilityValidatorRegistry()
    {
    }

    public bool RequiresValidation(
        ProductionOutputCapabilityDescriptor descriptor) => false;

    public void Validate(ProductionResolvedOutputRestoreValidationContext context)
    {
    }
}
