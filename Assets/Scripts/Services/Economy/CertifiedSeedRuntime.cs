using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public interface ICertifiedSeedCommand
{
    bool TryPlan(
        string actionId,
        string cropId,
        string facilityInstanceId,
        out DomainFailure failure);

    int CompleteDeliveredPlans(int operatingDay);
}

public sealed class CertifiedSeedPlanExecutionReceipt
{
    internal CertifiedSeedPlanExecutionReceipt(
        CertifiedSeedOrderSaveData order)
        : this(
            order?.actionId,
            order?.orderId,
            order?.orderSequence ?? -1,
            order?.destinationId,
            order?.facilityInstanceId,
            order?.cropId)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));
    }

    private CertifiedSeedPlanExecutionReceipt(
        string actionId,
        string orderId,
        int orderSequence,
        string destinationId,
        string facilityInstanceId,
        string cropId)
    {
        RequireCanonical(actionId, nameof(actionId));
        RequireCanonical(orderId, nameof(orderId));
        RequireCanonical(destinationId, nameof(destinationId));
        RequireCanonical(facilityInstanceId, nameof(facilityInstanceId));
        RequireCanonical(cropId, nameof(cropId));
        if (orderSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(orderSequence));

        ActionId = actionId;
        OrderId = orderId;
        OrderSequence = orderSequence;
        DestinationId = destinationId;
        FacilityInstanceId = facilityInstanceId;
        CropId = cropId;
        InputOperationId =
            CropPhysicalTransactionOutbox.FormatCertifiedOperationId(OrderId);
        OutputOwnerId = OrderId;
        OutputBatchCommitId =
            CertifiedSeedRuntime.CertifiedOutputBatchCommitPrefix + OrderId;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("certified-seed-plan-execution-receipt@1");
        digest.Append(ActionId);
        digest.Append(OrderId);
        digest.Append(OrderSequence);
        digest.Append(DestinationId);
        digest.Append(FacilityInstanceId);
        digest.Append(CropId);
        digest.Append(InputOperationId);
        digest.Append(OutputOwnerId);
        digest.Append(OutputBatchCommitId);
        SourceDigest = digest.ComputeSha256();
    }

    public static CertifiedSeedPlanExecutionReceipt CaptureIdentifiers(
        string actionId,
        string orderId,
        int orderSequence,
        string destinationId,
        string facilityInstanceId,
        string cropId) => new(
        actionId,
        orderId,
        orderSequence,
        destinationId,
        facilityInstanceId,
        cropId);

    public string ActionId { get; }
    public string OrderId { get; }
    public int OrderSequence { get; }
    public string DestinationId { get; }
    public string FacilityInstanceId { get; }
    public string CropId { get; }
    public string InputOperationId { get; }
    public string OutputOwnerId { get; }
    public string OutputBatchCommitId { get; }
    public string SourceDigest { get; }

    private static void RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Certified-seed receipt requires canonical identifiers.",
                name);
        }
    }
}

public interface ICertifiedSeedExecutionReceiptQuery
{
    bool TryCapturePlanReceipt(
        string actionId,
        out CertifiedSeedPlanExecutionReceipt receipt);

    bool IsPlanReadyForCompletion(string actionId);
}

public static class CertifiedSeedOperatingDayGate
{
    public static bool TryAdvance(
        int lastProcessedOperatingDay,
        int requestedOperatingDay,
        out int nextProcessedOperatingDay)
    {
        if (lastProcessedOperatingDay < 0)
            throw new ArgumentOutOfRangeException(
                nameof(lastProcessedOperatingDay));
        if (requestedOperatingDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedOperatingDay));
        if (requestedOperatingDay <= lastProcessedOperatingDay)
        {
            nextProcessedOperatingDay = lastProcessedOperatingDay;
            return false;
        }
        nextProcessedOperatingDay = requestedOperatingDay;
        return true;
    }
}

internal sealed class CertifiedSeedRuntimeState
{
    internal Dictionary<string, CertifiedSeedOrderSaveData> Orders { get; } =
        new(StringComparer.Ordinal);
    internal int NextOrderSequence { get; set; }
    internal int LastProcessedOperatingDay { get; set; }
}

/// <summary>
/// Converts one authored physical seed lot and one certified-seed kit at the
/// cultivar greenhouse. The destination id is the persistent order record, so
/// pending hauling survives save/restore without a second shadow inventory.
/// </summary>
public sealed class CertifiedSeedRuntime :
    ICertifiedSeedCommand,
    ICertifiedSeedExecutionReceiptQuery,
    ICertifiedSeedPersistence,
    ICertifiedSeedInputOwnerDescriptorSource,
    IProductionDomainOutputRestoreOwnerSource,
    IProductionDomainOutputFacilityLifecycleQuery
{
    public const string CertifiedOutputBatchCommitPrefix =
        ProductionDomainOutputPublicationIdentity.BatchCommitPrefix
        + "certified-seed:";
    public const string OutputPublicationOperationPrefix =
        ProductionDomainOutputPublicationIdentity.PublicationOperationPrefix
        + "certified-seed:";
    private const string CertificationKitItemId =
        CertifiedSeedPhysicalTransformAuthority.CertificationKitItemId;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IResourceEconomyContentCatalog crops;
    private readonly IStockQuery stock;
    private readonly IProductionItemGateway items;
    private readonly IItemTransferService transfers;
    private readonly IPhysicalSeedLotGateway seedLots;
    private readonly IProductionOutputCapabilityRegistry outputCapabilities;
    private readonly IProductionDomainOutputPublicationService outputPublication;
    private readonly IProductionFacilityMutationEpochQuery facilityMutations;
    private readonly ICertifiedSeedInputOwnerRuntime inputOwners;
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private CertifiedSeedRuntimeState State => rootStore.GetOrCreate(
        () => new CertifiedSeedRuntimeState());
    private Dictionary<string, CertifiedSeedOrderSaveData> orders => State.Orders;
    private int nextOrderSequence
    {
        get => State.NextOrderSequence;
        set => State.NextOrderSequence = value;
    }
    private int lastProcessedOperatingDay
    {
        get => State.LastProcessedOperatingDay;
        set => State.LastProcessedOperatingDay = value;
    }

    internal IReadOnlyCollection<CertifiedSeedOrderSaveData> PhysicalOrders =>
        orders.Values;

    public bool TryCapturePlanReceipt(
        string actionId,
        out CertifiedSeedPlanExecutionReceipt receipt)
    {
        string canonicalAction = actionId ?? string.Empty;
        if (canonicalAction.Length == 0
            || !string.Equals(
                canonicalAction,
                canonicalAction.Trim(),
                StringComparison.Ordinal))
        {
            receipt = null;
            return false;
        }
        CertifiedSeedOrderSaveData[] matches = orders.Values
            .Where(value => value != null
                && string.Equals(
                    value.actionId,
                    canonicalAction,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
        {
            receipt = null;
            return false;
        }
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Certified-seed action ID resolved multiple live orders: "
                + canonicalAction);
        }
        receipt = new CertifiedSeedPlanExecutionReceipt(matches[0]);
        return true;
    }

    public bool IsPlanReadyForCompletion(string actionId)
    {
        if (!TryResolveUniqueActionOrder(actionId, out CertifiedSeedOrderSaveData order)
            || order.phase != CertifiedSeedOrderPhase.Planned
            || !crops.TryGetCrop(order.cropId, out CropDefinitionSO crop)
            || crop == null)
        {
            return false;
        }

        Dictionary<string, int> inputs = new(StringComparer.Ordinal)
        {
            [crop.SeedItemId] =
                CertifiedSeedPhysicalTransformAuthority.SeedInputQuantity,
            [CertificationKitItemId] = CertifiedSeedPhysicalTransformAuthority
                .CertificationKitInputQuantity
        };
        return HasDelivered(order.destinationId, inputs);
    }

    private bool TryResolveUniqueActionOrder(
        string actionId,
        out CertifiedSeedOrderSaveData order)
    {
        string canonicalAction = actionId ?? string.Empty;
        if (canonicalAction.Length == 0
            || !string.Equals(
                canonicalAction,
                canonicalAction.Trim(),
                StringComparison.Ordinal))
        {
            order = null;
            return false;
        }

        CertifiedSeedOrderSaveData[] matches = orders.Values
            .Where(value => value != null
                && string.Equals(
                    value.actionId,
                    canonicalAction,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
        {
            order = null;
            return false;
        }
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Certified-seed action ID resolved multiple live orders: "
                + canonicalAction);
        }
        order = matches[0];
        return true;
    }

    public string OutputOwnerDomainId => "economy.certified-seed";
    public string OutputBatchCommitPrefix =>
        CertifiedOutputBatchCommitPrefix;

    public IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
        CapturePendingOutputOwners() => orders.Values
        .Where(value => value != null
            && (value.phase == CertifiedSeedOrderPhase.OutputPublished
                    && value.outputPublication is { outputAcknowledged: false }
                || value.phase == CertifiedSeedOrderPhase
                        .OutputRestoredAwaitingInputAcknowledgement
                    && value.outputPublication is
                    {
                        outputAcknowledged: true,
                        restoredInCurrentTransaction: true
                    }))
        .OrderBy(value => value.orderId, StringComparer.Ordinal)
        .Select(value => new ProductionDomainOutputRestoreOwnerSnapshot(
            value.orderId,
            value.outputPublication,
            new[]
            {
                new ProductionDomainOutputMaximumMassClaim(
                    value.outputCapability.ToDescriptor(),
                    1)
            }))
        .ToArray();

    public IReadOnlyList<ProductionDomainOutputFacilityOwnerSnapshot>
        CaptureActiveOutputOwners(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A valid facility ID is required.",
                nameof(facilityId));
        return orders.Values
            .Where(value => value != null
                && value.phase is CertifiedSeedOrderPhase.OutputPublished
                    or CertifiedSeedOrderPhase
                        .OutputRestoredAwaitingInputAcknowledgement
                && string.Equals(
                    value.facilityInstanceId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .Select(value =>
            {
                if (!ProductionDomainOutputPublicationService
                        .TryValidateCommittedOwner(
                            value.outputPublication,
                            out string failureReason)
                    || (value.phase == CertifiedSeedOrderPhase.OutputPublished)
                        == value.outputPublication.outputAcknowledged
                    || value.pendingInput?.phase
                        != CropPhysicalCommitPhase.OutcomePublished
                    || !seedLots.TryGetPendingBatchPhysicalDisposition(
                        value.pendingInput.operationId,
                        out _))
                {
                    throw new InvalidOperationException(
                        "Certified-seed active output owner is invalid: "
                        + value.orderId + ":" + failureReason);
                }
                return new ProductionDomainOutputFacilityOwnerSnapshot(
                    OutputOwnerDomainId,
                    value.orderId,
                    facilityId,
                    CreateLifecycleFingerprint(value));
            })
            .ToArray();
    }

    private static string CreateLifecycleFingerprint(
        CertifiedSeedOrderSaveData order)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("certified-seed-facility-lifecycle@1");
        digest.Append(order.orderId);
        digest.Append(order.facilityInstanceId);
        digest.Append((int)order.phase);
        digest.Append(order.pendingInput?.commitId ?? string.Empty);
        digest.Append(order.outputCapability?.fingerprint ?? string.Empty);
        digest.Append(order.outputPublication?.batchCommitId ?? string.Empty);
        digest.Append(order.outputPublication?.outcomeFingerprint
            ?? string.Empty);
        return digest.ComputeSha256();
    }

    public CertifiedSeedRuntime(
        IFacilityCapabilityQuery facilities,
        IBuildingWorldQuery buildingWorld,
        IResourceEconomyContentCatalog crops,
        IStockQuery stock,
        IProductionItemGateway items,
        IItemTransferService transfers,
        IPhysicalSeedLotGateway seedLots,
        IProductionOutputCapabilityRegistry outputCapabilities,
        IProductionDomainOutputPublicationService outputPublication,
        DungeonRuntimeAggregateRootStore rootStore,
        IProductionFacilityMutationEpochQuery facilityMutations,
        ICertifiedSeedInputOwnerRuntime inputOwners)
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.crops = crops ?? throw new ArgumentNullException(nameof(crops));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.seedLots = seedLots
            ?? throw new ArgumentNullException(nameof(seedLots));
        this.outputCapabilities = outputCapabilities
            ?? throw new ArgumentNullException(nameof(outputCapabilities));
        this.outputPublication = outputPublication
            ?? throw new ArgumentNullException(nameof(outputPublication));
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        this.facilityMutations = facilityMutations
            ?? throw new ArgumentNullException(nameof(facilityMutations));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
    }

    public bool TryPlan(
        string actionId,
        string cropId,
        string facilityInstanceId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string normalizedAction = actionId?.Trim() ?? string.Empty;
        string normalizedCrop = cropId?.Trim() ?? string.Empty;
        string normalizedFacility = facilityInstanceId?.Trim() ?? string.Empty;
        if (normalizedAction.Length == 0
            || normalizedCrop.Length == 0
            || !crops.TryGetCrop(normalizedCrop, out CropDefinitionSO crop)
            || crop == null
            || string.IsNullOrWhiteSpace(crop.SeedItemId))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        BuildableObject facility = FindFacility(normalizedFacility);
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        string destinationId = CertifiedSeedInputOwnerAuthority.BuildDestinationId(
            facility.PersistentInstanceId.Value,
            normalizedCrop,
            nextOrderSequence);
        CertifiedSeedOrderSaveData existing = orders.Values
            .FirstOrDefault(value => string.Equals(
                    value.facilityInstanceId,
                    facility.PersistentInstanceId.Value,
                    StringComparison.Ordinal)
                && string.Equals(value.cropId, normalizedCrop, StringComparison.Ordinal));
        if (existing != null)
        {
            // A persistent domain order, rather than a transient destination,
            // is the sole duplicate-planning authority.
            return existing.phase != CertifiedSeedOrderPhase.Planned
                || TryEnsurePlannedInputDeliveries(
                    existing,
                    crop,
                    facility,
                    out failure);
        }
        if (!ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                facilityMutations,
                facility.RequirePersistentInstanceId(),
                out failure))
        {
            return false;
        }

        int orderSequence = nextOrderSequence;
        string orderId = $"certified-seed-order:{orderSequence:D8}";
        CertifiedSeedOrderSaveData order = new()
        {
            orderId = orderId,
            orderSequence = orderSequence,
            actionId = normalizedAction,
            facilityInstanceId = facility.PersistentInstanceId.Value,
            cropId = normalizedCrop,
            destinationId = destinationId,
            destinationX = facility.centerPos.x,
            destinationY = facility.centerPos.y,
            phase = CertifiedSeedOrderPhase.Planned
        };
        orders.Add(orderId, order);
        nextOrderSequence = checked(nextOrderSequence + 1);

        CertifiedSeedInputOwnerDescriptor inputOwner =
            CreateInputOwnerDescriptor(order, crop, facility);
        if (!inputOwners.TryEnsure(inputOwner, out string ownerFailure))
        {
            orders.Remove(orderId);
            nextOrderSequence = orderSequence;
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                orderId,
                ownerFailure);
            return false;
        }

        if (!TryEnsurePlannedInputDeliveries(
                order,
                crop,
                facility,
                out failure))
        {
            RetireAbortedInputOwnerOrThrow(order, inputOwner);
            orders.Remove(orderId);
            nextOrderSequence = orderSequence;
            return false;
        }
        return true;
    }

    public int CompleteDeliveredPlans(int operatingDay)
    {
        if (!CertifiedSeedOperatingDayGate.TryAdvance(
                lastProcessedOperatingDay,
                operatingDay,
                out int nextProcessedDay))
            return 0;

        // Commit the monotonic gate before attempting any order. A repeated
        // day event must not create another completion opportunity even when
        // no delivered order was ready during the first dispatch.
        lastProcessedOperatingDay = nextProcessedDay;
        int completed = 0;
        foreach (CertifiedSeedOrderSaveData order in orders.Values
                     .OrderBy(value => value.orderId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (TryComplete(order)) completed++;
        }
        return completed;
    }

    private bool TryComplete(CertifiedSeedOrderSaveData order)
    {
        if (order == null)
            return false;

        if (order.phase is CertifiedSeedOrderPhase.OutputPublished
                or CertifiedSeedOrderPhase
                    .OutputRestoredAwaitingInputAcknowledgement)
        {
            return TryFinalizePublishedOutput(order);
        }
        if (order.phase == CertifiedSeedOrderPhase.FacilityDestroyedLossPending)
        {
            return TryFinalizeDestroyedFacilityLoss(order);
        }
        if (!crops.TryGetCrop(order.cropId, out CropDefinitionSO crop)
            || crop == null)
        {
            return false;
        }

        BuildableObject anyFacility = FindAnyFacility(order.facilityInstanceId);
        if (anyFacility == null || anyFacility.IsBuildingDestroyed)
        {
            return TryResolveDestroyedFacility(order);
        }
        BuildableObject facility = FindFacility(order.facilityInstanceId);
        if (facility == null)
            return false;
        if (!ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                facilityMutations,
                facility.RequirePersistentInstanceId(),
                out _))
        {
            return false;
        }

        Dictionary<string, int> inputs = new(StringComparer.Ordinal)
        {
            [crop.SeedItemId] =
                CertifiedSeedPhysicalTransformAuthority.SeedInputQuantity,
            [CertificationKitItemId] = CertifiedSeedPhysicalTransformAuthority
                .CertificationKitInputQuantity
        };
        if (order.phase == CertifiedSeedOrderPhase.Planned)
        {
            if (!TryEnsurePlannedInputDeliveries(
                    order,
                    crop,
                    facility,
                    out _)
                || !HasDelivered(order.destinationId, inputs)
                || !CropPhysicalTransactionOutbox.TryCommitOrResume(
                    order.pendingInput,
                    CropPhysicalTransactionOutbox.FormatCertifiedOperationId(
                        order.orderId),
                    CropPhysicalTransactionOutbox.CertifiedReasonCode,
                    order.orderSequence,
                    order.destinationId,
                    inputs,
                    crop.SeedItemId,
                    order.cropId,
                    seedLots,
                    out SeedLotState source,
                    out _))
            {
                return false;
            }
            SeedLotState certified =
                CertifiedSeedPhysicalTransformAuthority.Project(source);
            ProductionOutputCapabilityDescriptor outputCapability =
                outputCapabilities.CaptureDeclaredDescriptor(
                    CertifiedSeedOutputCapability.OutputLineId,
                    crop.SeedItemId,
                    ProductionOutputCapabilityIds.CertifiedSeed);
            order.certifiedSeedLot = certified;
            order.outputCapability =
                ProductionOutputCapabilitySaveData.Freeze(outputCapability);
            order.phase = CertifiedSeedOrderPhase
                .InputCommittedAwaitingDestinationRetirement;
        }

        if (order.phase == CertifiedSeedOrderPhase
                .InputCommittedAwaitingDestinationRetirement)
        {
            CertifiedSeedInputOwnerDescriptor inputOwner =
                CreateInputOwnerDescriptor(order, crop, facility);
            if (!inputOwners.TryRetire(
                    inputOwner,
                    CertifiedSeedInputOwnerAuthority
                        .CompletionReleaseReasonCode,
                    out _))
            {
                return false;
            }
            order.phase = CertifiedSeedOrderPhase.InputCommitted;
        }

        if (order.phase == CertifiedSeedOrderPhase.InputCommitted)
        {
            if (!TryValidateOutputCapability(
                    order,
                    crop.SeedItemId,
                    out _))
            {
                return false;
            }
            ProductionDomainOutputPublicationPlan outputPlan =
                CreateOutputPlan(order, crop.SeedItemId, facility);
            ProductionDomainOutputPublicationResult publicationResult =
                outputPublication.EnsureCommitted(
                    order.outputPublication,
                    outputPlan);
            if (!publicationResult.IsCommitted)
            {
                return false;
            }
            order.phase = CertifiedSeedOrderPhase.OutputPublished;
            order.pendingInput.phase = CropPhysicalCommitPhase.OutcomePublished;
        }
        return TryFinalizePublishedOutput(order);
    }

    private bool TryFinalizePublishedOutput(CertifiedSeedOrderSaveData order)
    {
        if (order?.pendingInput == null
            || order.pendingInput.phase
                != CropPhysicalCommitPhase.OutcomePublished
            || !CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                order.pendingInput,
                seedLots,
                out _))
        {
            return false;
        }

        switch (order.phase)
        {
            case CertifiedSeedOrderPhase.OutputPublished:
                if (order.outputPublication?.outputAcknowledged == true
                    || !outputPublication.TryAcknowledge(
                        order.outputPublication,
                        out _))
                {
                    return false;
                }
                break;

            case CertifiedSeedOrderPhase
                    .OutputRestoredAwaitingInputAcknowledgement:
                if (order.outputPublication?.outputAcknowledged != true)
                    return false;
                break;

            default:
                return false;
        }
        return orders.Remove(order.orderId);
    }

    private bool TryResolveDestroyedFacility(
        CertifiedSeedOrderSaveData order)
    {
        Vector2Int destination = new(order.destinationX, order.destinationY);
        switch (order.phase)
        {
            case CertifiedSeedOrderPhase.Planned:
                if (!crops.TryGetCrop(
                        order.cropId,
                        out CropDefinitionSO plannedCrop)
                    || plannedCrop == null
                    || !inputOwners.TryRetire(
                        CreateInputOwnerDescriptor(
                            order,
                            plannedCrop,
                            null,
                            destination),
                        CertifiedSeedInputOwnerAuthority
                            .FacilityLostReleaseReasonCode,
                        out _))
                {
                    return false;
                }
                return orders.Remove(order.orderId);

            case CertifiedSeedOrderPhase
                    .InputCommittedAwaitingDestinationRetirement:
                if (!crops.TryGetCrop(
                        order.cropId,
                        out CropDefinitionSO committedCrop)
                    || committedCrop == null
                    || !inputOwners.TryRetire(
                        CreateInputOwnerDescriptor(
                            order,
                            committedCrop,
                            null,
                            destination),
                        CertifiedSeedInputOwnerAuthority
                            .FacilityLostReleaseReasonCode,
                        out _))
                {
                    return false;
                }
                order.phase = CertifiedSeedOrderPhase.InputCommitted;
                goto case CertifiedSeedOrderPhase.InputCommitted;

            case CertifiedSeedOrderPhase.InputCommitted:
                order.phase =
                    CertifiedSeedOrderPhase.FacilityDestroyedLossPending;
                return TryFinalizeDestroyedFacilityLoss(order);

            default:
                return false;
        }
    }

    private bool TryFinalizeDestroyedFacilityLoss(
        CertifiedSeedOrderSaveData order)
    {
        if (order?.pendingInput == null
            || !CropPhysicalTransactionOutbox
                .TryAcknowledgeDestroyedFacilityLoss(
                    order.pendingInput,
                    seedLots,
                    out _))
        {
            return false;
        }
        return orders.Remove(order.orderId);
    }

    private static ProductionDomainOutputPublicationPlan CreateOutputPlan(
        CertifiedSeedOrderSaveData order,
        string seedItemId,
        BuildableObject facility)
    {
        ItemInstanceComponentSaveData seedState =
            SeedLotItemStateCodec.Encode(order.certifiedSeedLot);
        string outcomeFingerprint = CreateOutputOutcomeFingerprint(
            order,
            seedItemId,
            seedState);
        return new ProductionDomainOutputPublicationPlan(
            OutputPublicationOperationPrefix,
            order.orderId,
            CertifiedOutputBatchCommitPrefix + order.orderId,
            outcomeFingerprint,
            facility,
            new[]
            {
                new ProductionDomainOutputLine(
                    CertifiedSeedOutputCapability.OutputLineId,
                    seedItemId,
                    CertifiedSeedPhysicalTransformAuthority.OutputQuantity,
                    string.Empty,
                    new[] { seedState },
                    order.outputCapability.ToDescriptor())
            });
    }

    public static string CreateOutputOutcomeFingerprint(
        CertifiedSeedOrderSaveData order,
        string seedItemId,
        ItemInstanceComponentSaveData seedState)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("certified-seed-domain-output@1");
        digest.Append(order.orderId);
        digest.Append(order.facilityInstanceId);
        digest.Append(order.cropId);
        digest.Append(CertifiedSeedOutputCapability.OutputLineId);
        digest.Append(seedItemId);
        digest.Append(1);
        digest.Append(order.outputCapability?.fingerprint ?? string.Empty);
        digest.Append(seedState?.ToCanonicalString() ?? string.Empty);
        return digest.ComputeSha256();
    }

    private bool HasDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> requirements) =>
        requirements.All(requirement => stock.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ItemId,
                    requirement.Key,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity) >= requirement.Value);

    private bool TryEnsurePlannedInputDeliveries(
        CertifiedSeedOrderSaveData order,
        CropDefinitionSO crop,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null || crop == null || facility == null)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "certified-seed-input-maintenance-invalid");
            return false;
        }
        if (!seedLots.TryReleaseUnreachableSeedDelivery(
                crop.SeedItemId,
                order.cropId,
                facility.centerPos,
                order.destinationId,
                out _,
                out failure))
        {
            return false;
        }

        bool requestedAny = false;
        int requiredSeed = CertifiedSeedPhysicalTransformAuthority
            .SeedInputQuantity;
        int pendingSeed = items.CountPending(
            crop.SeedItemId,
            order.destinationId);
        if (pendingSeed < requiredSeed)
        {
            int missingSeed = requiredSeed - pendingSeed;
            if (missingSeed != 1
                || !seedLots.RequestBestSeedLot(
                    crop.SeedItemId,
                    order.cropId,
                    facility.centerPos,
                    order.destinationId,
                    out int requestedSeed,
                    out failure)
                || requestedSeed != missingSeed)
            {
                return false;
            }
            requestedAny = true;
        }

        int requiredKit = CertifiedSeedPhysicalTransformAuthority
            .CertificationKitInputQuantity;
        int pendingKit = items.CountPending(
            CertificationKitItemId,
            order.destinationId);
        if (pendingKit < requiredKit)
        {
            int missingKit = requiredKit - pendingKit;
            if (!transfers.TryRequestItemDelivery(
                    CertificationKitItemId,
                    missingKit,
                    facility.centerPos,
                    order.destinationId,
                    out int requestedKit,
                    out failure)
                || requestedKit != missingKit)
            {
                return false;
            }
            requestedAny = true;
        }

        if (requestedAny)
            transfers.PrioritizeDestination(order.destinationId);
        return true;
    }

    public CertifiedSeedWorldSaveData Capture() => new()
    {
        nextOrderSequence = nextOrderSequence,
        lastProcessedOperatingDay = lastProcessedOperatingDay,
        orders = orders.Values
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .Select(value => value.DeepClone())
            .ToList()
    };

    public CertifiedSeedRestoreCandidate BuildRestore(
        CertifiedSeedWorldSaveData snapshot)
    {
        if (snapshot == null
            || snapshot.version != CertifiedSeedWorldSaveData.CurrentVersion
            || snapshot.nextOrderSequence < 0
            || snapshot.lastProcessedOperatingDay < 0
            || snapshot.orders == null
            || snapshot.orders.Count > 256)
            throw new InvalidOperationException(
                "Certified-seed payload is missing or invalid.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        List<CertifiedSeedOrderSaveData> restored = new();
        foreach (CertifiedSeedOrderSaveData source in snapshot.orders)
        {
            CertifiedSeedOrderSaveData order = source?.DeepClone()
                ?? throw new InvalidOperationException(
                    "Certified-seed payload contains a null order.");
            ValidateOrder(order);
            if (!ids.Add(order.orderId))
                throw new InvalidOperationException(
                    "Certified-seed order IDs are duplicated.");
            restored.Add(order);
        }
        if (restored.Any(value => value.orderSequence >= snapshot.nextOrderSequence))
            throw new InvalidOperationException(
                "Certified-seed next sequence does not dominate active orders.");
        return new CertifiedSeedRestoreCandidate(
            snapshot.nextOrderSequence,
            restored,
            lastProcessedOperatingDay:
                snapshot.lastProcessedOperatingDay);
    }

    public void Restore(CertifiedSeedRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        CertifiedSeedRuntimeState restored = new()
        {
            NextOrderSequence = candidate.NextOrderSequence,
            LastProcessedOperatingDay = candidate.LastProcessedOperatingDay
        };
        foreach (CertifiedSeedOrderSaveData order in candidate.Orders)
            restored.Orders.Add(order.orderId, order.DeepClone());
        rootStore.Replace(restored);
    }

    public IReadOnlyList<CertifiedSeedInputOwnerDescriptor>
        BuildInputOwnerDescriptors(
            IReadOnlyList<CertifiedSeedOrderSaveData> candidateOrders)
    {
        List<CertifiedSeedInputOwnerDescriptor> descriptors = new();
        foreach (CertifiedSeedOrderSaveData order in (candidateOrders
                     ?? Array.Empty<CertifiedSeedOrderSaveData>())
                 .Where(value => value != null
                     && CertifiedSeedInputOwnerAuthority
                         .RequiresDestinationAuthority(value.phase))
                 .OrderBy(value => value.destinationId, StringComparer.Ordinal))
        {
            if (!crops.TryGetCrop(order.cropId, out CropDefinitionSO crop)
                || crop == null)
            {
                throw new InvalidOperationException(
                    "Certified-seed input owner crop is missing: "
                    + order.cropId);
            }
            BuildableObject facility = FindAnyFacility(order.facilityInstanceId);
            if (!CertifiedSeedFacilityEligibility.IsEligible(facility)
                || facility.centerPos.x != order.destinationX
                || facility.centerPos.y != order.destinationY)
            {
                throw new InvalidOperationException(
                    "Certified-seed input owner facility is missing or stale: "
                    + order.facilityInstanceId);
            }
            descriptors.Add(CreateInputOwnerDescriptor(order, crop, facility));
        }
        return descriptors;
    }

    private void ValidateOrder(CertifiedSeedOrderSaveData order)
    {
        if (!IsCanonical(order.orderId)
            || order.orderSequence < 0
            || !IsCanonical(order.actionId)
            || !IsCanonical(order.facilityInstanceId)
            || !IsCanonical(order.cropId)
            || !IsCanonical(order.destinationId)
            || !TryParseDestination(
                order.destinationId,
                out string facilityId,
                out string cropId,
                out int sequence)
            || sequence != order.orderSequence
            || !string.Equals(facilityId, order.facilityInstanceId, StringComparison.Ordinal)
            || !string.Equals(cropId, order.cropId, StringComparison.Ordinal)
            || !crops.TryGetCrop(order.cropId, out CropDefinitionSO crop)
            || crop == null
            || order.pendingInput == null)
            throw new InvalidOperationException(
                "Certified-seed order provenance is invalid.");
        if (order.phase == CertifiedSeedOrderPhase.Planned)
        {
            if (order.pendingInput.phase != CropPhysicalCommitPhase.None
                || order.certifiedSeedLot != null
                || order.outputCapability is { IsEmpty: false }
                || order.outputPublication is { IsEmpty: false })
                throw new InvalidOperationException(
                    "Planned certified-seed order contains committed state.");
            return;
        }
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [crop.SeedItemId] =
                CertifiedSeedPhysicalTransformAuthority.SeedInputQuantity,
            [CertificationKitItemId] = CertifiedSeedPhysicalTransformAuthority
                .CertificationKitInputQuantity
        };
        bool facilityLoss = order.phase
            == CertifiedSeedOrderPhase.FacilityDestroyedLossPending;
        CropPhysicalCommitSaveData provenanceOwner = order.pendingInput;
        if (facilityLoss)
        {
            if (!CropPhysicalTransactionOutbox.ValidateDestroyedFacilityLoss(
                    order.pendingInput,
                    out string terminalFailure))
            {
                throw new InvalidOperationException(
                    "Certified-seed terminal loss owner is invalid: "
                    + terminalFailure);
            }
            provenanceOwner = order.pendingInput.DeepClone();
            provenanceOwner.phase = CropPhysicalCommitPhase.InputCommitted;
            provenanceOwner.terminalDisposition =
                CropWipTerminalDisposition.None;
            provenanceOwner.terminalOperationId = string.Empty;
            provenanceOwner.terminalReasonCode = string.Empty;
            provenanceOwner.terminalLossQuantity = 0;
            provenanceOwner.terminalLossMassGrams = 0L;
        }
        if (!CropPhysicalTransactionOutbox.ValidateProvenance(
                provenanceOwner,
                CropPhysicalTransactionOutbox.FormatCertifiedOperationId(
                    order.orderId),
                CropPhysicalTransactionOutbox.CertifiedReasonCode,
                order.orderSequence,
                order.destinationId,
                requirements,
                crop.SeedItemId,
                order.cropId,
                out string failureReason)
            || order.certifiedSeedLot == null)
            throw new InvalidOperationException(
                "Certified-seed physical owner is invalid: " + failureReason);
        if (!TryValidateOutputCapability(
                order,
                crop.SeedItemId,
                out DomainFailure outputFailure))
        {
            throw new InvalidOperationException(
                "Certified-seed output capability is invalid: "
                + outputFailure.ToString());
        }
        bool outputPublished = order.phase is
            CertifiedSeedOrderPhase.OutputPublished
            or CertifiedSeedOrderPhase
                .OutputRestoredAwaitingInputAcknowledgement;
        bool outputAcknowledged = order.phase == CertifiedSeedOrderPhase
            .OutputRestoredAwaitingInputAcknowledgement;
        if (!facilityLoss
            && order.phase is not CertifiedSeedOrderPhase.InputCommitted
                and not CertifiedSeedOrderPhase
                    .InputCommittedAwaitingDestinationRetirement
                and not CertifiedSeedOrderPhase.OutputPublished
                and not CertifiedSeedOrderPhase
                    .OutputRestoredAwaitingInputAcknowledgement)
        {
            throw new InvalidOperationException(
                "Certified-seed order phase is unsupported.");
        }
        if (outputPublished !=
            (order.pendingInput.phase == CropPhysicalCommitPhase.OutcomePublished))
        {
            throw new InvalidOperationException(
                "Certified-seed output state contradicts its input owner: "
                + "certified-seed-input-output-phase-mismatch");
        }
        if (!TryValidateOutputPublicationOwner(
                order,
                crop.SeedItemId,
                outputPublished,
                outputAcknowledged,
                out string outputOwnerFailure))
        {
            throw new InvalidOperationException(
                "Certified-seed output state contradicts its input owner: "
                + outputOwnerFailure);
        }
    }

    private static bool TryValidateOutputPublicationOwner(
        CertifiedSeedOrderSaveData order,
        string seedItemId,
        bool outputPublished,
        bool outputAcknowledged,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionDomainOutputPublicationSaveData owner =
            order?.outputPublication;
        if (!ProductionDomainOutputPublicationService.TryValidateRestorableOwner(
                owner,
                out bool committed,
                out failureReason))
        {
            return false;
        }
        if (outputPublished != committed)
        {
            failureReason =
                "certified-seed-output-owner-phase-mismatch";
            return false;
        }
        if ((owner?.outputAcknowledged ?? false) != outputAcknowledged)
        {
            failureReason =
                "certified-seed-output-acknowledgement-phase-mismatch";
            return false;
        }
        if (owner == null || owner.IsEmpty)
            return true;
        ItemInstanceComponentSaveData seedState =
            SeedLotItemStateCodec.Encode(order.certifiedSeedLot);
        bool matches = string.Equals(
                owner.batchCommitId,
                CertifiedOutputBatchCommitPrefix + order.orderId,
                StringComparison.Ordinal)
            && string.Equals(
                owner.outcomeFingerprint,
                CreateOutputOutcomeFingerprint(order, seedItemId, seedState),
                StringComparison.Ordinal)
            && string.Equals(
                owner.ownerFacilityId,
                order.facilityInstanceId,
                StringComparison.Ordinal)
            && (!committed
                || owner.stacks.Count == 1
                && string.Equals(
                    owner.stacks[0].outputLineId,
                    CertifiedSeedOutputCapability.OutputLineId,
                    StringComparison.Ordinal)
                && string.Equals(
                    owner.stacks[0].itemId,
                    seedItemId,
                    StringComparison.Ordinal)
                && owner.stacks[0].quantity == 1);
        failureReason = matches
            ? string.Empty
            : "certified-seed-output-owner-provenance-drift";
        return matches;
    }

    private bool TryValidateOutputCapability(
        CertifiedSeedOrderSaveData order,
        string expectedItemId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ProductionOutputCapabilitySaveData frozen = order?.outputCapability;
        if (frozen == null
            || frozen.IsEmpty
            || !string.Equals(
                frozen.outputLineId,
                CertifiedSeedOutputCapability.OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                frozen.itemId,
                expectedItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                frozen.capabilityId,
                ProductionOutputCapabilityIds.CertifiedSeed,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                expectedItemId ?? string.Empty,
                "certified-seed-output-capability-owner-mismatch");
            return false;
        }
        return outputCapabilities.TryValidateExact(
            frozen.ToDescriptor(),
            out _,
            out failure);
    }

    private BuildableObject FindFacility(string facilityInstanceId) =>
        CertifiedSeedFacilityEligibility.FindOperational(facilities)
            .FirstOrDefault(value => string.IsNullOrWhiteSpace(facilityInstanceId)
                || string.Equals(
                    value.PersistentInstanceId.Value,
                    facilityInstanceId,
                    StringComparison.Ordinal));

    private BuildableObject FindAnyFacility(string facilityInstanceId) =>
        (buildingWorld.Buildings ?? Array.Empty<BuildableObject>())
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    facilityInstanceId,
                    StringComparison.Ordinal));

    private static bool TryParseDestination(
        string destinationId,
        out string facilityId,
        out string cropId,
        out int sequence)
    {
        facilityId = string.Empty;
        cropId = string.Empty;
        sequence = -1;
        const string prefix = ReservedTargetDestinationIdentity
            .ExactFacilityInputPrefix + CertifiedSeedInputOwnerAuthority
            .OwnerDomain + ":";
        string value = destinationId ?? string.Empty;
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string[] parts = value.Substring(prefix.Length).Split(':');
        if (parts.Length != 3)
            return false;
        try
        {
            facilityId = Uri.UnescapeDataString(parts[0]);
            cropId = Uri.UnescapeDataString(parts[1]);
            return facilityId.Length > 0
                && cropId.Length > 0
                && int.TryParse(
                    parts[2],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out sequence)
                && sequence >= 0;
        }
        catch (UriFormatException)
        {
            facilityId = string.Empty;
            cropId = string.Empty;
            sequence = -1;
            return false;
        }
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static CertifiedSeedInputOwnerDescriptor
        CreateInputOwnerDescriptor(
            CertifiedSeedOrderSaveData order,
            CropDefinitionSO crop,
            BuildableObject facility,
            Vector2Int? detachedPosition = null) => new(
        order.orderId,
        order.facilityInstanceId,
        facility?.centerPos
            ?? detachedPosition
            ?? new Vector2Int(order.destinationX, order.destinationY),
        order.destinationId,
        crop.SeedItemId);

    private void RetireAbortedInputOwnerOrThrow(
        CertifiedSeedOrderSaveData order,
        CertifiedSeedInputOwnerDescriptor descriptor)
    {
        if (inputOwners.TryRetire(
                descriptor,
                CertifiedSeedInputOwnerAuthority.AbortedReleaseReasonCode,
                out string failureReason))
        {
            return;
        }
        throw new InvalidOperationException(
            "Certified-seed input owner abort failed: "
            + order.orderId + ":" + failureReason);
    }
}

/// <summary>
/// Completes hauled certification orders and exposes the player action through
/// the existing persistent event-alert UI. Two alerts are used because the
/// alert model intentionally caps one card at four choices.
/// </summary>
public sealed class CertifiedSeedApplicationAdapter : IStartable, IDisposable
{
    private readonly ICertifiedSeedCommand commands;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IResourceEconomyContentCatalog crops;
    private readonly IGameEventBus events;
    private IDisposable daySubscription;

    public CertifiedSeedApplicationAdapter(
        ICertifiedSeedCommand commands,
        IFacilityCapabilityQuery facilities,
        IResourceEconomyContentCatalog crops,
        IGameEventBus events)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.crops = crops ?? throw new ArgumentNullException(nameof(crops));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => daySubscription ??=
        events.Subscribe<OperatingDayStartedEvent>(OnDayStarted);

    public void Dispose()
    {
        daySubscription?.Dispose();
        daySubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        commands.CompleteDeliveredPlans(started.day);
        BuildableObject facility = CertifiedSeedFacilityEligibility
            .FindOperational(facilities)
            .FirstOrDefault();
        if (facility == null) return;

        CropDefinitionSO[] authored = crops.Crops
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.SeedItemId))
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .ToArray();
        for (int offset = 0; offset < authored.Length; offset += 4)
        {
            EventAlertChoice[] choices = authored.Skip(offset).Take(4)
                .Select(crop => new EventAlertChoice(
                    crop.DisplayName,
                    "기존 종자 로트와 인증 꾸러미를 운반해 품질을 높이고 병원체 부하를 낮춥니다.",
                    V21ContentAlertActionIds.CertifiedSeed(
                        crop.CropId,
                        facility.PersistentInstanceId.Value)))
                .ToArray();
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "인증 품종 종자 생산",
                "육종 온실에서 인증할 작물을 선택하십시오. 재료가 실제로 도착한 뒤 종자 로트가 배출됩니다.",
                EventAlertImportance.Medium,
                "V21 농업",
                choices,
                $"certified-seed:{facility.PersistentInstanceId.Value}:{offset / 4}")));
        }
    }
}
