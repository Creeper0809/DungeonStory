using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-facing production ports that still require legacy runtime actors.
/// Pure production value types and persistence contracts live in
/// DungeonStory.Production.
/// </summary>
public interface IProductionBillQuery
{
    int Version { get; }
    IReadOnlyList<ProductionBillSnapshot> GetBills(BuildableObject facility);
    bool HasStockSensor(BuildableObject facility);
}

public interface IProductionBillOrderCommand :
    IProductionDistributionPolicyCommand
{
    ProductionBillCommandResult AddBill(
        BuildableObject facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials);
    ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex);
    ProductionBillCommandResult SetSuspended(
        ProductionBillId billId,
        bool suspended);
    ProductionBillCommandResult SetStockPolicy(
        ProductionBillId billId,
        int minimumReserve,
        int targetStock);
    ProductionBillCommandResult SetOrderMode(
        ProductionBillId billId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult SetWorkerPolicy(
        ProductionBillId billId,
        WorkerSelectionPolicySaveData policy);
    ProductionBillCommandResult RequestStockSensorInstallation(
        BuildableObject facility);
    ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        BuildableObject facility);
    ProductionBillCommandResult RemoveStockSensor(
        BuildableObject facility);
}

public interface IProductionBillWorkExecution
{
    ProductionWorkAvailabilityResult CheckWorkAvailability(
        BuildableObject facility,
        WorkTypeId workTypeId);
    ProductionWorkBeginResult BeginWork(
        CharacterActor worker,
        BuildableObject facility,
        WorkTypeId workTypeId);
    ProductionWorkExecutionResult ExecuteWork(
        CharacterActor worker,
        BuildableObject facility,
        ProductionBillId billId,
        float amount);
    bool TrySetEmergencyProduction(
        CharacterActor worker,
        ProductionBillId billId,
        bool enabled,
        out string failureReason);
}

public readonly struct ProductionOutputContext
{
    public ProductionOutputContext(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        CharacterActor worker,
        string outputLineId,
        string itemId,
        int amount,
        string outputDestinationId,
        float qualityModifier = 0f,
        float workerQuality = 0.7f,
        string commitId = "")
    {
        Recipe = recipe;
        Facility = facility;
        Worker = worker;
        OutputLineId = outputLineId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        Amount = Mathf.Max(1, amount);
        OutputDestinationId = outputDestinationId ?? string.Empty;
        QualityModifier = qualityModifier;
        WorkerQuality = workerQuality;
        CommitId = commitId ?? string.Empty;
    }

    public ProductionRecipeSO Recipe { get; }
    public BuildableObject Facility { get; }
    public CharacterActor Worker { get; }
    public string OutputLineId { get; }
    public string ItemId { get; }
    public int Amount { get; }
    public string OutputDestinationId { get; }
    public float QualityModifier { get; }
    public float WorkerQuality { get; }
    public string CommitId { get; }
}

public interface IProductionOutputCapability
{
    /// <summary>
    /// Canonical capability identity used by the deterministic special-output
    /// registry. This identifies the implementation contract, not an item.
    /// </summary>
    string CapabilityId { get; }
    int ContractVersion { get; }
    string ComponentCodecId { get; }
    int ComponentCodecVersion { get; }

    /// <summary>
    /// True only when a generic recipe output may select this capability from
    /// its item definition. Domain-owned producers declare their capability
    /// explicitly and therefore remain false even when they emit the same item.
    /// </summary>
    bool SupportsAutomaticSelection { get; }

    bool CanHandle(string itemId);
}

/// <summary>
/// Marker for an output capability whose physical component materialization is
/// owned by the common prepared-output batch transaction. Capability metadata,
/// not a content ID or codec-name comparison, selects that route.
/// </summary>
public interface IProductionPreparedOutputParticipantCapability
{
}

public interface IProductionOutputHandler : IProductionOutputCapability
{
    bool TryProduce(
        ProductionOutputContext context,
        out string failureReason);
}

public interface IProductionOutputCapabilityRegistry
{
    IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
        CapabilityContracts { get; }

    ProductionOutputCapabilityDescriptor CaptureDeclaredDescriptor(
        string outputLineId,
        string itemId,
        string capabilityId);

    bool TryValidateExact(
        ProductionOutputCapabilityDescriptor descriptor,
        out IProductionOutputCapability capability,
        out DomainFailure failure);
}

/// <summary>
/// Pure, execution-free upper-bound contract for one production output
/// capability. Implementations may inspect authored catalogs, but must not
/// reserve, publish, spawn, mutate, or depend on the runtime output handler.
/// </summary>
public interface IProductionOutputMaximumMassCapability
{
    string CapabilityId { get; }
    int ContractVersion { get; }
    string ComponentCodecId { get; }
    int ComponentCodecVersion { get; }
    bool SupportsAutomaticSelection { get; }

    bool CanHandle(string itemId);

    ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery);
}

public interface IProductionOutputMaximumMassRegistry
{
    IReadOnlyList<string> CapabilityIds { get; }
    IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
        CapabilityContracts { get; }
    string RegistryFingerprint { get; }

    ProductionOutputMaximumMassProjection CaptureAutomatic(
        string outputLineId,
        string itemId,
        int maximumQuantity);

    ProductionOutputMaximumMassProjection CaptureDeclared(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity);
}

/// <summary>
/// Pure explicit-capability projection used by definition-only facility
/// contributors. It is separate from the replay registry contract so restore
/// fixtures do not need to pretend they support new authored selection.
/// </summary>
public interface IProductionOutputMaximumMassCapabilitySelector
{
    ProductionOutputMaximumMassProjection CaptureForCapability(
        string outputLineId,
        string itemId,
        string capabilityId,
        int maximumQuantity);
}

public readonly struct ProductionOutputMaximumMassProjection
{
    public ProductionOutputMaximumMassProjection(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        long definitionUnitMassGrams,
        long maximumMassGrams,
        long massAuthorityRevision,
        string sourceDigest)
    {
        if (maximumQuantity <= 0
            || definitionUnitMassGrams <= 0L
            || maximumMassGrams != checked(
                definitionUnitMassGrams * maximumQuantity)
            || massAuthorityRevision < 0L
            || string.IsNullOrEmpty(descriptor.Fingerprint)
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Production output maximum-mass projection is invalid.");
        }

        Descriptor = descriptor;
        MaximumQuantity = maximumQuantity;
        DefinitionUnitMassGrams = definitionUnitMassGrams;
        MaximumMassGrams = maximumMassGrams;
        MassAuthorityRevision = massAuthorityRevision;
        SourceDigest = sourceDigest;
    }

    public ProductionOutputCapabilityDescriptor Descriptor { get; }
    public int MaximumQuantity { get; }
    public long DefinitionUnitMassGrams { get; }
    public long MaximumMassGrams { get; }
    public long MassAuthorityRevision { get; }
    public string SourceDigest { get; }
}

public static class ProductionOutputDefinitionMaximumMassProjection
{
    public const string Schema =
        "production-output-definition-maximum-mass@1";

    public static ProductionOutputMaximumMassProjection Capture(
        IProductionOutputMaximumMassCapability capability,
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery)
    {
        if (capability == null)
            throw new ArgumentNullException(nameof(capability));
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));
        if (maximumQuantity <= 0
            || !capability.CanHandle(descriptor.ItemId)
            || !string.Equals(
                capability.CapabilityId,
                descriptor.CapabilityId,
                StringComparison.Ordinal)
            || capability.ContractVersion != descriptor.CapabilityVersion
            || !string.Equals(
                capability.ComponentCodecId,
                descriptor.ComponentCodecId,
                StringComparison.Ordinal)
            || capability.ComponentCodecVersion
                != descriptor.ComponentCodecVersion)
        {
            throw new InvalidOperationException(
                "Production output maximum-mass descriptor does not match its capability.");
        }

        string expectedFingerprint =
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                descriptor.OutputLineId,
                descriptor.ItemId,
                descriptor.CapabilityId,
                descriptor.CapabilityVersion,
                descriptor.ComponentCodecId,
                descriptor.ComponentCodecVersion);
        if (!string.Equals(
                expectedFingerprint,
                descriptor.Fingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production output maximum-mass descriptor fingerprint drifted.");
        }

        long unitMassGrams = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)descriptor.ItemId).Value;
        long maximumMassGrams = checked(unitMassGrams * maximumQuantity);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(descriptor.OutputLineId);
        digest.Append(descriptor.ItemId);
        digest.Append(descriptor.CapabilityId);
        digest.Append(descriptor.CapabilityVersion);
        digest.Append(descriptor.ComponentCodecId);
        digest.Append(descriptor.ComponentCodecVersion);
        digest.Append(descriptor.Fingerprint);
        digest.Append(maximumQuantity);
        digest.Append(massQuery.AuthorityRevision);
        digest.Append(unitMassGrams);
        digest.Append(maximumMassGrams);
        return new ProductionOutputMaximumMassProjection(
            descriptor,
            maximumQuantity,
            unitMassGrams,
            maximumMassGrams,
            massQuery.AuthorityRevision,
            digest.ComputeSha256());
    }
}

/// <summary>
/// Localization-neutral production output boundary for handlers that expose
/// stable domain failures. Legacy handlers can continue implementing
/// <see cref="IProductionOutputHandler"/> until their own domain is migrated.
/// </summary>
public interface IDomainFailureProductionOutputHandler
{
    bool TryProduce(
        ProductionOutputContext context,
        out DomainFailure failure);
}

public interface IIdempotentProductionOutputHandler
{
    bool TryProduceIdempotent(
        ProductionOutputContext context,
        out DomainFailure failure);
    bool TryAcknowledge(
        string commitId,
        out DomainFailure failure);
    bool TryCaptureCommittedOutput(
        ProductionOutputContext context,
        out ProductionCommittedOutputSnapshot snapshot,
        out DomainFailure failure);
}
