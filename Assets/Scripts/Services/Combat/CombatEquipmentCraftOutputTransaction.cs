using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// Combat-owned adapter over the shared gram/capacity/publication transaction.
/// It contains no item-definition switch: the frozen output capability and
/// common publication line are the extension boundary for future ammunition
/// and equipment definitions.
/// </summary>
public sealed class CombatEquipmentCraftOutputTransaction :
    IProductionDomainOutputRestoreOwnerSource,
    IProductionDomainOutputFacilityLifecycleQuery
{
    public const string OwnerDomainId = "combat.equipment-craft";
    public const string BatchCommitPrefix =
        "domain-output-batch:combat-craft:";
    public const string PublicationOperationPrefix =
        "domain-output-publication:combat-craft:";

    private readonly CombatEquipmentRuntimeStateStore stateStore;
    private readonly IBuildingWorldQuery buildings;
    private readonly IProductionDomainOutputPublicationService publication;
    private readonly IQualityRejectedSaleDestinationAuthority
        rejectedSaleDestination;
    private readonly IEquipmentPhysicalItemGateway physicalItems;

    public CombatEquipmentCraftOutputTransaction(
        CombatEquipmentRuntimeStateStore stateStore,
        IBuildingWorldQuery buildings,
        IProductionDomainOutputPublicationService publication,
        IQualityRejectedSaleDestinationAuthority rejectedSaleDestination,
        IEquipmentPhysicalItemGateway physicalItems)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
        this.rejectedSaleDestination = rejectedSaleDestination
            ?? throw new ArgumentNullException(nameof(rejectedSaleDestination));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
    }

    public string OutputOwnerDomainId => OwnerDomainId;
    public string OutputBatchCommitPrefix => BatchCommitPrefix;

    public ProductionDomainOutputPublicationResult EnsureCommitted(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null
            || order.outputPhase != CombatEquipmentCraftOutputPhase
                .ResolvedWaitingForPublication)
        {
            return Conflict("combat-output-owner-phase-invalid");
        }
        BuildableObject facility = FindFacility(order.facilityPersistentId);
        if (facility == null || facility.IsBuildingDestroyed)
            return Conflict("combat-output-facility-missing");

        string outcomeFingerprint = CaptureOutcomeFingerprint(order);
        bool ammunition = CombatEquipmentCraftingRuntime.IsAmmunitionRecipe(
            order.definitionId);
        IReadOnlyList<ItemInstanceComponentSaveData> components = ammunition
            ? Array.Empty<ItemInstanceComponentSaveData>()
            : order.outputPreparedComponent == null
                ? null
                : new[] { order.outputPreparedComponent };
        if (!ammunition
            && (string.IsNullOrEmpty(order.outputInstanceId)
                || components == null))
        {
            return Conflict("combat-equipment-output-prepared-state-missing");
        }
        bool markForSale = (int)order.resolvedQuality
                < (int)order.minimumQuality
            && order.rejectedDisposition ==
                RejectedOutputDisposition.MarkForSale;
        FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget = default;
        if (markForSale
            && !rejectedSaleDestination.TryEnsureTarget(
                out releaseTarget,
                out string saleTargetFailure))
        {
            return Pending("combat-output-market-target:" + saleTargetFailure);
        }
        ProductionDomainOutputPublicationPlan plan = new(
            PublicationOperationPrefix,
            OwnerStableId(order),
            BatchCommitId(order),
            outcomeFingerprint,
            facility,
            new[]
            {
                new ProductionDomainOutputLine(
                    ammunition
                        ? CombatAmmunitionCraftOutputCapability.OutputLineId
                        : CombatEquipmentCraftOutputCapability.OutputLineId,
                    order.outputItemId,
                    order.outputQuantity,
                    ammunition ? string.Empty : order.outputInstanceId,
                    components,
                    order.outputCapability.ToDescriptor())
            },
            releaseTarget);
        ProductionDomainOutputPublicationResult result = publication
            .EnsureCommitted(order.outputPublication, plan);
        if (result.IsCommitted)
        {
            order.outputPhase = CombatEquipmentCraftOutputPhase
                .PublishedAwaitingInputAcknowledgement;
            // Transitional compatibility fields remain derived until the
            // unique-equipment path joins this same envelope.
            order.outputPublished = true;
            order.outputCommitId = order.outputPublication.batchCommitId;
            order.outputStackId = ammunition
                ? string.Empty
                : order.outputPublication.stacks.Single().stackId;
        }
        return result;
    }

    public bool TryAcknowledgeAndRoute(
        CombatEquipmentCraftOrderSaveData order,
        bool markForSale,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || order.outputPublication == null
            || order.outputPhase is not (
                CombatEquipmentCraftOutputPhase
                    .PublishedAwaitingInputAcknowledgement
                or CombatEquipmentCraftOutputPhase
                    .RestoredOutputAwaitingInputAcknowledgement))
        {
            failureReason = "combat-output-acknowledgement-phase-invalid";
            return false;
        }

        bool frozenForMarket = order.outputPublication.releaseHasDestination
            && string.Equals(
                order.outputPublication.releaseDestinationId,
                QualityRejectedOutputRules.MarketDestinationId,
                StringComparison.Ordinal);
        if (markForSale != frozenForMarket)
        {
            failureReason = "combat-output-market-disposition-drift";
            return false;
        }

        if (order.outputPhase == CombatEquipmentCraftOutputPhase
                .PublishedAwaitingInputAcknowledgement)
        {
            if (!publication.TryAcknowledge(
                    order.outputPublication,
                    out failureReason))
            {
                return false;
            }
        }
        else if (!order.outputPublication.outputAcknowledged)
        {
            failureReason = "combat-output-restored-owner-not-acknowledged";
            return false;
        }

        order.outputMarketRouted = markForSale;
        return true;
    }

    public IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
        CapturePendingOutputOwners() => stateStore.Current.CraftOrders
        .Where(order => order != null
            && order.outputPublication != null
            && (order.outputPhase == CombatEquipmentCraftOutputPhase
                    .PublishedAwaitingInputAcknowledgement
                    && !order.outputPublication.outputAcknowledged
                || order.outputPhase == CombatEquipmentCraftOutputPhase
                    .RestoredOutputAwaitingInputAcknowledgement
                    && order.outputPublication.outputAcknowledged
                    && order.outputPublication.restoredInCurrentTransaction))
        .OrderBy(OwnerStableId, StringComparer.Ordinal)
        .Select(order => new ProductionDomainOutputRestoreOwnerSnapshot(
            OwnerStableId(order),
            order.outputPublication,
            new[]
            {
                new ProductionDomainOutputMaximumMassClaim(
                    order.outputCapability.ToDescriptor(),
                    order.outputQuantity)
            }))
        .ToArray();

    public IReadOnlyList<ProductionDomainOutputFacilityOwnerSnapshot>
        CaptureActiveOutputOwners(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "Combat output lifecycle requires a valid facility ID.",
                nameof(facilityId));
        return stateStore.Current.CraftOrders
            .Where(order => IsActiveOwnerAt(order, facilityId))
            .OrderBy(OwnerStableId, StringComparer.Ordinal)
            .Select(order => new ProductionDomainOutputFacilityOwnerSnapshot(
                OwnerDomainId,
                OwnerStableId(order),
                facilityId,
                CaptureLifecycleFingerprint(order)))
            .ToArray();
    }

    public static string BatchCommitId(
        CombatEquipmentCraftOrderSaveData order) => BatchCommitPrefix
        + OwnerStableId(order);

    public static string OwnerStableId(
        CombatEquipmentCraftOrderSaveData order) => string.Join(
        ":",
        order?.orderId ?? string.Empty,
        Math.Max(0, order?.qualityAttemptIndex ?? 0).ToString(
            "D4",
            CultureInfo.InvariantCulture));

    public static string CaptureOutcomeFingerprint(
        CombatEquipmentCraftOrderSaveData order)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-craft-domain-output@1");
        digest.Append(OwnerStableId(order));
        digest.Append(order?.facilityPersistentId ?? string.Empty);
        digest.Append(order?.definitionId ?? string.Empty);
        digest.Append(order?.outputItemId ?? string.Empty);
        digest.Append(Math.Max(0, order?.outputQuantity ?? 0));
        digest.Append(order?.outputCapability?.fingerprint ?? string.Empty);
        digest.Append(order?.outputInstanceId ?? string.Empty);
        digest.Append(order?.outputPreparedComponent?.ToCanonicalString()
            ?? string.Empty);
        digest.Append((int)(order?.resolvedQuality
            ?? CombatEquipmentQuality.Normal));
        digest.Append(order?.resolvedMakerCharacterId ?? string.Empty);
        return digest.ComputeSha256();
    }

    private bool IsActiveOwnerAt(
        CombatEquipmentCraftOrderSaveData order,
        BuildingInstanceId facilityId)
    {
        if (order == null
            || order.outputPublication == null
            || !string.Equals(
                order.facilityPersistentId,
                facilityId.Value,
                StringComparison.Ordinal)
            || !ProductionDomainOutputPublicationService
                .TryValidateCommittedOwner(
                    order.outputPublication,
                    out _)
            || order.materialTransferAcknowledged)
        {
            return false;
        }
        if (string.IsNullOrEmpty(order.materialTransferOperationId))
            return true;
        return physicalItems.TryGetPendingBatchPhysicalDisposition(
            order.materialTransferOperationId,
            out _);
    }

    private BuildableObject FindFacility(string persistentId) =>
        (buildings.Buildings ?? Array.Empty<BuildableObject>())
        .FirstOrDefault(value => value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                persistentId,
                StringComparison.Ordinal));

    private static string CaptureLifecycleFingerprint(
        CombatEquipmentCraftOrderSaveData order)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-craft-output-lifecycle@1");
        digest.Append(OwnerStableId(order));
        digest.Append((int)order.outputPhase);
        digest.Append(order.materialTransferOperationId);
        digest.Append(order.materialTransferCommitId);
        digest.Append(order.materialTransferAcknowledged);
        digest.Append(order.outputPublication.batchCommitId);
        digest.Append(order.outputPublication.outputAcknowledged);
        return digest.ComputeSha256();
    }

    private static ProductionDomainOutputPublicationResult Conflict(
        string reason) => new(
        ProductionDomainOutputPublicationStatus.Conflict,
        reason);

    private static ProductionDomainOutputPublicationResult Pending(
        string reason) => new(
        ProductionDomainOutputPublicationStatus.Pending,
        reason);
}
