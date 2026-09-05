using System;
using System.Collections.Generic;
using System.Linq;

public static class ProductionOutputCapabilityIds
{
    public const string StandardDefinition =
        "production-output:standard-definition";
    public const int StandardDefinitionVersion = 1;
    public const string DefinitionOnlyCodec =
        "production-output-codec:definition-only";
    public const int DefinitionOnlyCodecVersion = 1;

    public const string PerishableFood =
        "production-output:perishable-food";
    public const int PerishableFoodVersion = 1;
    public const string PerishableFoodFreshnessCodec =
        "production-output-codec:perishable-food-freshness";
    public const int PerishableFoodFreshnessCodecVersion = 1;

    public const string ApparelWorkOrder =
        "production-output:apparel-work-order";
    public const int ApparelWorkOrderVersion = 1;
    public const string ApparelStateCodec =
        "production-output-codec:apparel-state";
    public const int ApparelStateCodecVersion = 3;

    public const string CombatEquipmentCraft =
        "production-output:combat-equipment-craft";
    public const int CombatEquipmentCraftVersion = 1;
    public const string CombatEquipmentStateCodec =
        "production-output-codec:combat-equipment-state";
    public const int CombatEquipmentStateCodecVersion = 3;

    public const string CombatAmmunitionCraft =
        "production-output:combat-ammunition-craft";
    public const int CombatAmmunitionCraftVersion = 2;
    public const string CombatAmmunitionStateCodec =
        "production-output-codec:combat-ammunition-state";
    public const int CombatAmmunitionStateCodecVersion = 1;

    public const string CertifiedSeed =
        "production-output:certified-seed";
    public const int CertifiedSeedVersion = 1;
    public const string CropHarvestSeedLot =
        "production-output:crop-harvest-seed-lot";
    public const int CropHarvestSeedLotVersion = 1;
    public const string SeedLotStateCodec =
        "production-output-codec:seed-lot-state";
    public const int SeedLotStateCodecVersion = 2;
}

[Serializable]
public sealed class ProductionOutputCapabilitySaveData
{
    public string outputLineId = string.Empty;
    public string itemId = string.Empty;
    public string capabilityId = string.Empty;
    public int capabilityVersion;
    public string componentCodecId = string.Empty;
    public int componentCodecVersion;
    public string fingerprint = string.Empty;

    public bool IsEmpty =>
        string.IsNullOrEmpty(outputLineId)
        && string.IsNullOrEmpty(itemId)
        && string.IsNullOrEmpty(capabilityId)
        && capabilityVersion == 0
        && string.IsNullOrEmpty(componentCodecId)
        && componentCodecVersion == 0
        && string.IsNullOrEmpty(fingerprint);

    public ProductionOutputCapabilityDescriptor ToDescriptor() => new(
        outputLineId,
        itemId,
        capabilityId,
        capabilityVersion,
        componentCodecId,
        componentCodecVersion,
        fingerprint);

    public ProductionOutputCapabilitySaveData Clone() => new()
    {
        outputLineId = outputLineId ?? string.Empty,
        itemId = itemId ?? string.Empty,
        capabilityId = capabilityId ?? string.Empty,
        capabilityVersion = capabilityVersion,
        componentCodecId = componentCodecId ?? string.Empty,
        componentCodecVersion = componentCodecVersion,
        fingerprint = fingerprint ?? string.Empty
    };

    public static ProductionOutputCapabilitySaveData Freeze(
        ProductionOutputCapabilityDescriptor descriptor) => new()
    {
        outputLineId = descriptor.OutputLineId,
        itemId = descriptor.ItemId,
        capabilityId = descriptor.CapabilityId,
        capabilityVersion = descriptor.CapabilityVersion,
        componentCodecId = descriptor.ComponentCodecId,
        componentCodecVersion = descriptor.ComponentCodecVersion,
        fingerprint = descriptor.Fingerprint
    };
}

public readonly struct ProductionOutputCapabilityDescriptor
{
    public ProductionOutputCapabilityDescriptor(
        string outputLineId,
        string itemId,
        string capabilityId,
        int capabilityVersion,
        string componentCodecId,
        int componentCodecVersion,
        string fingerprint)
    {
        OutputLineId = outputLineId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        CapabilityId = capabilityId ?? string.Empty;
        CapabilityVersion = capabilityVersion;
        ComponentCodecId = componentCodecId ?? string.Empty;
        ComponentCodecVersion = componentCodecVersion;
        Fingerprint = fingerprint ?? string.Empty;
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public string CapabilityId { get; }
    public int CapabilityVersion { get; }
    public string ComponentCodecId { get; }
    public int ComponentCodecVersion { get; }
    public string Fingerprint { get; }
}

/// <summary>
/// Assembly-neutral shape used to prove that execution and pure maximum-mass
/// registries expose the same capability contract set. It intentionally lives
/// in Foundation so model assemblies never depend on the default composition
/// assembly.
/// </summary>
public readonly struct ProductionOutputCapabilityContractSnapshot :
    IEquatable<ProductionOutputCapabilityContractSnapshot>
{
    public ProductionOutputCapabilityContractSnapshot(
        string capabilityId,
        int contractVersion,
        string componentCodecId,
        int componentCodecVersion,
        bool supportsAutomaticSelection,
        bool participatesInPreparedOutput = false)
    {
        CapabilityId = capabilityId ?? string.Empty;
        ContractVersion = contractVersion;
        ComponentCodecId = componentCodecId ?? string.Empty;
        ComponentCodecVersion = componentCodecVersion;
        SupportsAutomaticSelection = supportsAutomaticSelection;
        ParticipatesInPreparedOutput = participatesInPreparedOutput;
    }

    public string CapabilityId { get; }
    public int ContractVersion { get; }
    public string ComponentCodecId { get; }
    public int ComponentCodecVersion { get; }
    public bool SupportsAutomaticSelection { get; }
    public bool ParticipatesInPreparedOutput { get; }

    public bool Equals(ProductionOutputCapabilityContractSnapshot other) =>
        string.Equals(CapabilityId, other.CapabilityId, StringComparison.Ordinal)
        && ContractVersion == other.ContractVersion
        && string.Equals(
            ComponentCodecId,
            other.ComponentCodecId,
            StringComparison.Ordinal)
        && ComponentCodecVersion == other.ComponentCodecVersion
        && SupportsAutomaticSelection == other.SupportsAutomaticSelection
        && ParticipatesInPreparedOutput == other.ParticipatesInPreparedOutput;

    public override bool Equals(object obj) =>
        obj is ProductionOutputCapabilityContractSnapshot other
        && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        CapabilityId,
        ContractVersion,
        ComponentCodecId,
        ComponentCodecVersion,
        SupportsAutomaticSelection,
        ParticipatesInPreparedOutput);
}

public static class ProductionOutputCapabilityDescriptorFingerprint
{
    public const string Schema =
        "production-output-capability-descriptor@1";

    public static string Capture(
        string outputLineId,
        string itemId,
        string capabilityId,
        int capabilityVersion,
        string componentCodecId,
        int componentCodecVersion)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(outputLineId);
        digest.Append(itemId);
        digest.Append(capabilityId);
        digest.Append(capabilityVersion);
        digest.Append(componentCodecId);
        digest.Append(componentCodecVersion);
        return digest.ComputeSha256();
    }
}

[Serializable]
public sealed class ProductionDomainPublishedStackSaveData
{
    public string outputLineId = string.Empty;
    public string itemId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string stackId = string.Empty;
    public int quantity;
    public long massGrams;

    public ProductionDomainPublishedStackSaveData Clone() => new()
    {
        outputLineId = outputLineId ?? string.Empty,
        itemId = itemId ?? string.Empty,
        itemInstanceId = itemInstanceId ?? string.Empty,
        stackId = stackId ?? string.Empty,
        quantity = quantity,
        massGrams = massGrams
    };
}

public static class ProductionDomainOutputPublicationIdentity
{
    public const string BatchCommitPrefix = "domain-output-batch:";
    public const string PublicationOperationPrefix =
        "domain-output-publication:";
}

public enum ProductionDomainOutputAcknowledgementDisposition
{
    ReleaseLooseOrDestination = 0,
    RetainFacilityOutputBuffer = 1
}

/// <summary>
/// Domain-neutral durable owner for one atomic FacilityBuffer output batch.
/// The domain still owns its outcome and input receipt; this envelope owns only
/// reserve/publication/admission provenance and never substitutes for either.
/// </summary>
[Serializable]
public sealed class ProductionDomainOutputPublicationSaveData
{
    public const int CurrentSchemaVersion = 6;

    public int schemaVersion = CurrentSchemaVersion;
    public int publicationAttempt;
    public string publicationOperationId = string.Empty;
    public string batchCommitId = string.Empty;
    public string outcomeFingerprint = string.Empty;
    public string maximumMassProofDigest = string.Empty;
    public long maximumBatchMassGrams;
    public string capacitySourceDigest = string.Empty;
    public long requiredMinimumCapacityGrams;
    public long outputMassGrams;
    public string admissionTokenId = string.Empty;
    public string plannedOutputFingerprint = string.Empty;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public bool releaseHasDestination;
    public string releaseDestinationId = string.Empty;
    public int releaseDestinationX;
    public int releaseDestinationY;
    public ProductionDomainOutputAcknowledgementDisposition
        acknowledgementDisposition;
    public string ownerDomain = string.Empty;
    public string ownerOperationId = string.Empty;
    public string ownerFacilityId = string.Empty;
    public long capacityRevision;
    public bool outputPublished;
    public bool admissionCommitted;
    public bool outputAcknowledged;
    [NonSerialized]
    public bool restoredInCurrentTransaction;
    public List<ProductionDomainPublishedStackSaveData> stacks = new();

    public bool IsEmpty =>
        publicationAttempt == 0
        && string.IsNullOrEmpty(publicationOperationId)
        && string.IsNullOrEmpty(batchCommitId)
        && string.IsNullOrEmpty(outcomeFingerprint)
        && string.IsNullOrEmpty(maximumMassProofDigest)
        && maximumBatchMassGrams == 0L
        && string.IsNullOrEmpty(capacitySourceDigest)
        && requiredMinimumCapacityGrams == 0L
        && outputMassGrams == 0L
        && string.IsNullOrEmpty(admissionTokenId)
        && string.IsNullOrEmpty(plannedOutputFingerprint)
        && string.IsNullOrEmpty(destinationId)
        && destinationX == 0
        && destinationY == 0
        && !releaseHasDestination
        && string.IsNullOrEmpty(releaseDestinationId)
        && releaseDestinationX == 0
        && releaseDestinationY == 0
        && acknowledgementDisposition ==
            ProductionDomainOutputAcknowledgementDisposition
                .ReleaseLooseOrDestination
        && string.IsNullOrEmpty(ownerDomain)
        && string.IsNullOrEmpty(ownerOperationId)
        && string.IsNullOrEmpty(ownerFacilityId)
        && capacityRevision == 0L
        && !outputPublished
        && !admissionCommitted
        && !outputAcknowledged
        && (stacks == null || stacks.Count == 0);

    public ProductionDomainOutputPublicationSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        publicationAttempt = publicationAttempt,
        publicationOperationId = publicationOperationId ?? string.Empty,
        batchCommitId = batchCommitId ?? string.Empty,
        outcomeFingerprint = outcomeFingerprint ?? string.Empty,
        maximumMassProofDigest = maximumMassProofDigest ?? string.Empty,
        maximumBatchMassGrams = maximumBatchMassGrams,
        capacitySourceDigest = capacitySourceDigest ?? string.Empty,
        requiredMinimumCapacityGrams = requiredMinimumCapacityGrams,
        outputMassGrams = outputMassGrams,
        admissionTokenId = admissionTokenId ?? string.Empty,
        plannedOutputFingerprint = plannedOutputFingerprint ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        destinationX = destinationX,
        destinationY = destinationY,
        releaseHasDestination = releaseHasDestination,
        releaseDestinationId = releaseDestinationId ?? string.Empty,
        releaseDestinationX = releaseDestinationX,
        releaseDestinationY = releaseDestinationY,
        acknowledgementDisposition = acknowledgementDisposition,
        ownerDomain = ownerDomain ?? string.Empty,
        ownerOperationId = ownerOperationId ?? string.Empty,
        ownerFacilityId = ownerFacilityId ?? string.Empty,
        capacityRevision = capacityRevision,
        outputPublished = outputPublished,
        admissionCommitted = admissionCommitted,
        outputAcknowledged = outputAcknowledged,
        restoredInCurrentTransaction = restoredInCurrentTransaction,
        stacks = stacks?
            .Where(value => value != null)
            .Select(value => value.Clone())
            .ToList()
            ?? new List<ProductionDomainPublishedStackSaveData>()
    };
}

public sealed class ProductionDomainOutputRestoreOwnerSnapshot
{
    public ProductionDomainOutputRestoreOwnerSnapshot(
        string ownerStableId,
        ProductionDomainOutputPublicationSaveData publication,
        IReadOnlyList<ProductionDomainOutputMaximumMassClaim>
            maximumMassClaims)
    {
        OwnerStableId = ownerStableId ?? string.Empty;
        Publication = publication?.Clone()
            ?? throw new ArgumentNullException(nameof(publication));
        MaximumMassClaims = Array.AsReadOnly((maximumMassClaims
                ?? throw new ArgumentNullException(nameof(maximumMassClaims)))
            .Select(value => value
                ?? throw new ArgumentException(
                    "Domain output restore contains a null maximum-mass claim.",
                    nameof(maximumMassClaims)))
            .ToArray());
    }

    public string OwnerStableId { get; }
    public ProductionDomainOutputPublicationSaveData Publication { get; }
    public IReadOnlyList<ProductionDomainOutputMaximumMassClaim>
        MaximumMassClaims { get; }
}

public sealed class ProductionDomainOutputMaximumMassClaim
{
    public ProductionDomainOutputMaximumMassClaim(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity)
    {
        Descriptor = descriptor;
        MaximumQuantity = maximumQuantity;
    }

    public ProductionOutputCapabilityDescriptor Descriptor { get; }
    public int MaximumQuantity { get; }
}

/// <summary>
/// Registration boundary for custom domain producers that use the shared gram
/// publication transaction. Adding another producer requires registering an
/// owner source; the common restore guard then performs the bidirectional join.
/// </summary>
public interface IProductionDomainOutputRestoreOwnerSource
{
    string OutputOwnerDomainId { get; }
    string OutputBatchCommitPrefix { get; }
    IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
        CapturePendingOutputOwners();
}

public sealed class ProductionDomainOutputFacilityOwnerSnapshot
{
    public ProductionDomainOutputFacilityOwnerSnapshot(
        string ownerDomainId,
        string ownerStableId,
        BuildingInstanceId facilityId,
        string stateFingerprint)
    {
        OwnerDomainId = ownerDomainId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        FacilityId = facilityId;
        StateFingerprint = stateFingerprint ?? string.Empty;
    }

    public string OwnerDomainId { get; }
    public string OwnerStableId { get; }
    public BuildingInstanceId FacilityId { get; }
    public string StateFingerprint { get; }
}

public interface IProductionDomainOutputFacilityLifecycleQuery
{
    IReadOnlyList<ProductionDomainOutputFacilityOwnerSnapshot>
        CaptureActiveOutputOwners(BuildingInstanceId facilityId);
}
public static class ProductionRuinedOutputProtocol
{
    public const string RecoverableWasteOutputLineId =
        "output:ruin:recoverable-waste";
    public const string DeclaredLossOutputLineId =
        "output:ruin:declared-loss";
}
