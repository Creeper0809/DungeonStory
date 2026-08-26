using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

public sealed class WorldItemStackRuntime :
    IWorldItemStackRuntime,
    IWorldItemQuantityLeaseRuntime,
    IWorldItemCarryRecoveryRuntime,
    IPhysicalItemRestoreStaging,
    IPhysicalItemRestoreCandidateQuery,
    IProductionInputDestinationCustodyDrainRestoreCandidateQuery,
    IPhysicalItemRestoreCandidateOutputQuery,
    IFacilityBufferPlannedOutputRestoreCandidateQuery,
    IFacilityOutputExactRouteRestoreCandidateQuery,
    IFacilityOutputExactRouteDeliveryRevisionRestoreCandidateQuery,
    IDungeonRestoreTransactionParticipant,
    IWorldItemMarkerDataSource,
    IHaulPlanBuilder,
    IWarehouseOverCapacityEvacuationQuery,
    IStartable,
    ITickable,
    IDisposable
{
    public const string FacilityInputDestinationPrefix = "facility-input:";
    public const string WarehouseStorageDestinationPrefix =
        WarehouseStorageIdentity.DestinationPrefix;
    public const string CombatLoadoutDestinationPrefix = "combat-loadout-pickup:";

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly IWorldDropZoneQuery worldDropZoneQuery;
    private readonly ICharacterSpawnerProvider characterSpawnerProvider;
    private readonly IItemMarkerPresenter itemMarkerPresenter;
    private readonly WorldItemRepository itemRepository;
    private readonly IItemReservationService reservationService;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly WorldItemQueryService itemQueryService;
    private readonly IWorldItemHaulPlanningService haulPlanningService;
    private readonly IItemTransferService itemTransferService;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly WorldItemTheftService theftService;
    private readonly WorldItemPersistenceService persistence;
    private readonly WorldItemWarehouseService warehouseService;
    private readonly IHaulDeliveryIntentQuery haulDeliveryIntentQuery;
    private readonly IHaulDeliveryIntentCommand haulDeliveryIntentCommands;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private int projectedRestoreRevision;
    private IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
        restoreCandidateDispositions;
    private Dictionary<string, PhysicalItemRestoreCandidateDispositionSnapshot>
        restoreCandidateDispositionsByOperation;
    private IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
        restoreCandidateInputDestinationDrains;
    private Dictionary<string, ProductionInputDestinationCustodyDrainSaveData>
        restoreCandidateInputDestinationDrainsByStep;
    private IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
        restoreCandidateOutputs;
    private Dictionary<string,
        IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>>
        restoreCandidateOutputsByCommit;
    private IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        restoreCandidatePlannedOutputBatches;
    private Dictionary<string, FacilityBufferPlannedOutputRestoreBatchSnapshot>
        restoreCandidatePlannedOutputBatchesByCommit;
    private IReadOnlyList<FacilityOutputExactRouteOutboxSaveData>
        restoreCandidateExactOutputRoutes;
    private Dictionary<string, FacilityOutputExactRouteOutboxSaveData>
        restoreCandidateExactOutputRoutesByOperation;
    private long restoreCandidateExactRouteCheckpointSequence;
    private string restoreCandidateExactRouteCheckpointDigest = string.Empty;
    private bool restoreCandidateLifetimeActive;
    private bool restoreCandidateLifetimePublished;

    private List<WorldItemStackRecord> stacks => itemRepository.Records;
    private Dictionary<string, WorldItemStackRecord> stacksById => itemRepository.RecordsById;
    private Dictionary<Vector2Int, List<WorldItemStackRecord>> stacksByPosition =>
        itemRepository.RecordsByPosition;
    public WorldItemStackRuntime(
        IGridSystemProvider gridSystemProvider,
        ICharacterIdRegistry characterIdRegistry,
        IWorldDropZoneQuery worldDropZoneQuery,
        ICharacterSpawnerProvider characterSpawnerProvider,
        WorldItemReadServices readServices,
        WorldItemMutationServices mutationServices,
        WorldItemPersistenceService persistence,
        WorldItemWarehouseService warehouseService,
        IPhysicalItemBatchDispositionService batchDispositions)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.characterIdRegistry = characterIdRegistry ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.worldDropZoneQuery = worldDropZoneQuery
            ?? throw new ArgumentNullException(nameof(worldDropZoneQuery));
        this.characterSpawnerProvider = characterSpawnerProvider
            ?? throw new ArgumentNullException(nameof(characterSpawnerProvider));
        WorldItemReadServices requiredRead = readServices
            ?? throw new ArgumentNullException(nameof(readServices));
        WorldItemMutationServices requiredMutations = mutationServices
            ?? throw new ArgumentNullException(nameof(mutationServices));
        catalogProvider = requiredRead.Catalog;
        massQuery = requiredRead.Mass;
        haulingSettingsProvider = requiredRead.HaulingSettings;
        itemQueryService = requiredRead.Queries;
        itemMarkerPresenter = requiredRead.Markers;
        performanceRecorder = requiredRead.Performance;
        debugRules = requiredRead.DebugRules;
        itemRepository = requiredMutations.Repository;
        aggregateRootStore = itemRepository.AggregateRootStore;
        reservationService = requiredMutations.Reservations;
        itemSpawner = requiredMutations.Spawner;
        haulPlanningService = requiredMutations.HaulPlanning;
        itemTransferService = requiredMutations.Transfers;
        theftService = requiredMutations.Theft;
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.warehouseService = warehouseService
            ?? throw new ArgumentNullException(nameof(warehouseService));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        haulDeliveryIntentQuery = itemRepository.HaulDeliveryIntents;
        haulDeliveryIntentCommands = itemRepository.HaulDeliveryIntents;
    }

    public bool TryCommitBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => batchDispositions.TryCommit(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

    public bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => batchDispositions.TryCommitPending(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

    public bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt) =>
        batchDispositions.TryGetPending(operationId, out receipt);

    public bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason) => batchDispositions.Acknowledge(
            commitId,
            out failureReason);

    public IDungeonItemCatalogProvider CatalogProvider => catalogProvider;
    public IPhysicalItemMassQuery MassQuery => massQuery;
    public IItemHaulingSettingsProvider HaulingSettingsProvider => haulingSettingsProvider;
    public bool StoredItemMarkersVisible =>
        itemQueryService.StoredItemMarkersVisible;
    public int ItemStackVersion => itemRepository.ItemStackVersion;
    public int HaulJobVersion => itemRepository.HaulJobVersion;
    public int Revision => itemRepository.WarehouseEvacuationRevision;

    public bool SpawnItemAtWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned)
    {
        spawned = itemSpawner.Spawn(
            itemId,
            amount,
            position,
            state,
            destinationId,
            components: components);
        return spawned == amount;
    }

    public bool TryRemoveInstanceComponent(
        string stackId,
        string componentTypeId)
    {
        string stack = stackId ?? string.Empty;
        string type = componentTypeId ?? string.Empty;
        if (!stacksById.TryGetValue(stack, out WorldItemStackRecord record)
            || record?.components == null
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            return false;
        }
        int removed = record.components.RemoveAll(component => component != null
            && string.Equals(
                component.componentTypeId,
                type,
                StringComparison.Ordinal));
        if (removed != 1)
        {
            return false;
        }
        itemRepository.MarkChanged();
        return true;
    }

    public IReadOnlyList<string> CapturePendingWarehouseIds() =>
        itemRepository.CapturePendingWarehouseEvacuationIds();

    public bool IsPending(string warehouseDestinationId)
    {
        string destinationId = warehouseDestinationId?.Trim() ?? string.Empty;
        return destinationId.Length > 0
            && itemRepository.CapturePendingWarehouseEvacuationIds()
                .Contains(destinationId, StringComparer.Ordinal);
    }
    public int GetCommittedHaulDeliveryQuantity(
        string destinationId,
        string itemId) =>
        haulDeliveryIntentQuery.GetCommittedQuantity(destinationId, itemId);

    public long GetCommittedHaulDeliveryMassGrams(string destinationId)
    {
        string destination = destinationId ?? string.Empty;
        if (destination.Length == 0
            || !string.Equals(
                destination,
                destination.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical haul destination ID is required.",
                nameof(destinationId));
        }

        long totalMassGrams = 0L;
        foreach (HaulDeliveryIntentSaveData intent in
                 haulDeliveryIntentQuery.CaptureCommitted()
                     .Where(value => value != null
                         && string.Equals(
                             value.destinationId,
                             destination,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.operationId, StringComparer.Ordinal))
        {
            foreach (HaulDeliveryItemCommitmentSaveData commitment in
                     (intent.commitments
                         ?? new List<HaulDeliveryItemCommitmentSaveData>())
                     .Where(value => value != null)
                     .OrderBy(value => value.carriedStackId, StringComparer.Ordinal))
            {
                if (!stacksById.TryGetValue(
                        commitment.carriedStackId,
                        out WorldItemStackRecord carried)
                    || carried == null
                    || carried.state != WorldItemStackState.Carried
                    || carried.quantity != commitment.quantity
                    || !string.Equals(
                        carried.itemId,
                        commitment.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ItemReservationSignature.Create(
                            carried.itemId,
                            carried.components),
                        commitment.expectedStackSignature,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Committed haul mass has no exact physical carried lot: "
                        + $"{intent.operationId}:{commitment.carriedStackId}.");
                }

                ItemDefinitionId itemId = (ItemDefinitionId)carried.itemId;
                PhysicalItemMassSubject subject =
                    PhysicalItemMassSubjectAdapter.Create(
                        massQuery,
                        itemId,
                        carried.itemInstanceId,
                        carried.components);
                totalMassGrams = checked(totalMassGrams
                    + massQuery.GetQuantityMass(
                        itemId,
                        subject,
                        carried.quantity).Value);
            }
        }

        return totalMassGrams;
    }

    public bool TryCommitHaulPickup(
        string ownerOperationId,
        CharacterCarryInventory inventory,
        out string failureReason)
    {
        failureReason = string.Empty;
        string operationId = ownerOperationId?.Trim() ?? string.Empty;
        if (inventory == null
            || reservationService is not ItemReservationService reservationAuthority
            || !haulDeliveryIntentQuery.TryCapture(
                operationId,
                out HaulDeliveryIntentSaveData intent)
            || !reservationAuthority.QuantityReservations.TryGetLeasesByOwner(
                operationId,
                out IReadOnlyList<ItemQuantityLease> leases))
        {
            failureReason = "haul-pickup-lease-authority-missing:" + operationId;
            return false;
        }

        string expectedCohort =
            $"haul:{intent.destinationKind}:{intent.destinationId}";
        CharacterCarriedItemSaveData[] carried = inventory.Items
            .Where(item => item != null
                && string.Equals(
                    item.ownerOperationId?.Trim(),
                    operationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (carried.Length == 0)
        {
            failureReason = "haul-pickup-physical-commitment-missing:" + operationId;
            return false;
        }

        HashSet<string> matchedLeaseIds = new(StringComparer.Ordinal);
        foreach (CharacterCarriedItemSaveData item in carried)
        {
            string signature = ItemReservationSignature.Create(
                item.itemId,
                item.components);
            ItemQuantityLease lease = leases.SingleOrDefault(candidate =>
                candidate != null
                && candidate.purpose == ItemReservationPurpose.Hauling
                && candidate.remainingQuantity == item.quantity
                && candidate.slices != null
                && candidate.slices.Count == 1
                && string.Equals(
                    candidate.ownerCharacterId,
                    intent.ownerCharacterId,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.aggregationCohortId,
                    expectedCohort,
                    StringComparison.Ordinal)
                && candidate.slices[0] != null
                && candidate.slices[0].quantity == item.quantity
                && string.Equals(
                    candidate.slices[0].stackId,
                    item.carriedStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.slices[0].expectedStackSignature,
                    signature,
                    StringComparison.Ordinal));
            DomainFailure failure = DomainFailure.None;
            if (lease == null
                || !matchedLeaseIds.Add(lease.leaseId)
                || !reservationAuthority.QuantityReservations.Revalidate(
                    lease.leaseId,
                    out ItemQuantityLease revalidated,
                    out failure)
                || revalidated.remainingQuantity != item.quantity)
            {
                failureReason =
                    $"haul-pickup-lease-mismatch:{operationId}:{item.carriedStackId}:{failure}";
                return false;
            }
        }

        return haulDeliveryIntentCommands.TryCommitPickup(
            operationId,
            inventory,
            out failureReason);
    }

#if UNITY_EDITOR
    [GameplayInternalOnly(
        "Registers a durable haul intent in isolated Editor fixtures without exposing a production planning bypass.",
        "Physical item focused Editor fixtures only")]
    public bool TryRegisterHaulDeliveryPlanForEditorTest(
        string operationId,
        string ownerCharacterId,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition,
        out string failureReason) =>
        TryRegisterHaulDeliveryPlanForEditorTest(
            operationId,
            ownerCharacterId,
            destinationKind,
            destinationId,
            deliveryPosition,
            dropPosition,
            Array.Empty<WarehouseHaulAdmissionSaveData>(),
            out failureReason);

    [GameplayInternalOnly(
        "Registers an exact warehouse-admission vector in isolated Editor fixtures without exposing a production planning bypass.",
        "Capacity-routing actor transition focused Editor fixture only")]
    public bool TryRegisterHaulDeliveryPlanForEditorTest(
        string operationId,
        string ownerCharacterId,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition,
        IReadOnlyList<WarehouseHaulAdmissionSaveData> warehouseAdmissions,
        out string failureReason) =>
        haulDeliveryIntentCommands.TryRegisterPlan(
            operationId,
            ownerCharacterId,
            destinationKind,
            destinationId,
            deliveryPosition,
            dropPosition,
            warehouseAdmissions
                ?? Array.Empty<WarehouseHaulAdmissionSaveData>(),
            out failureReason);
#endif

    public bool TryCaptureHaulDeliveryIntent(
        string ownerOperationId,
        out HaulDeliveryIntentSaveData intent) =>
        haulDeliveryIntentQuery.TryCapture(ownerOperationId, out intent);

    public IReadOnlyList<HaulDeliveryIntentSaveData>
        CaptureHaulDeliveryIntentsByDestination(string destinationId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
            return Array.Empty<HaulDeliveryIntentSaveData>();
        return haulDeliveryIntentCommands.CaptureRuntimeState()
            .Where(intent => intent != null
                && string.Equals(
                    intent.destinationId,
                    destination,
                    StringComparison.Ordinal))
            .OrderBy(intent => intent.operationId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool ReleaseHaulDeliveryIntent(string ownerOperationId)
    {
        if (haulDeliveryIntentQuery.TryCapture(
                ownerOperationId,
                out HaulDeliveryIntentSaveData intent))
        {
            warehouseService.ReleaseHaulAdmissions(
                intent,
                WarehouseMassAdmissionReleaseReason.CancelledBeforePickup);
        }
        return haulDeliveryIntentCommands.Remove(ownerOperationId);
    }

    public bool TryRenewWarehouseAdmissionsForHaul(
        string ownerOperationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!haulDeliveryIntentQuery.TryCapture(
                ownerOperationId,
                out HaulDeliveryIntentSaveData intent))
        {
            failureReason = "haul delivery intent missing";
            return false;
        }
        return warehouseService.TryRenewHaulAdmissions(intent, out failureReason);
    }
    public void Start()
    {
        itemMarkerPresenter.Initialize(this);
        projectedRestoreRevision = aggregateRootStore.PublishedRestoreRevision;
        RefreshAllMarkers();
    }

    public void Tick()
    {
        if (projectedRestoreRevision != aggregateRootStore.PublishedRestoreRevision)
        {
            projectedRestoreRevision = aggregateRootStore.PublishedRestoreRevision;
            ProjectRestoredWorldState();
        }
        warehouseService.TryScheduleNextOverCapacityEvacuation(
            itemRepository.CapturePendingWarehouseEvacuationIds());
    }

    public void Dispose()
    {
        ClearPhysicalRestoreCandidateIndex();
        restoreCandidateLifetimeActive = false;
        restoreCandidateLifetimePublished = false;
    }

    public string ParticipantId =>
        "999.world.physical-item-restore-candidate-lifetime";

    public void BeginRestoreCandidate()
    {
        if (restoreCandidateLifetimeActive)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime is already active.");
        }
        if (!IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime requires a staged candidate index.");
        }

        restoreCandidateLifetimeActive = true;
        restoreCandidateLifetimePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreCandidateLifetimeActive
            || restoreCandidateLifetimePublished
            || !IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime is not ready to publish.");
        }

        restoreCandidateLifetimePublished = true;
    }

    public void RollbackPublishedRestoreCandidate() =>
        ResetPhysicalRestoreCandidateLifetime();

    public void CompleteRestoreCandidate()
    {
        if (!restoreCandidateLifetimeActive
            || !restoreCandidateLifetimePublished)
        {
            throw new InvalidOperationException(
                "Physical-item restore candidate lifetime cannot complete.");
        }

        ResetPhysicalRestoreCandidateLifetime();
    }

    public void DiscardRestoreCandidate() =>
        ResetPhysicalRestoreCandidateLifetime();

    private void ResetPhysicalRestoreCandidateLifetime()
    {
        ClearPhysicalRestoreCandidateIndex();
        restoreCandidateLifetimeActive = false;
        restoreCandidateLifetimePublished = false;
    }

    public bool IsCandidateAvailable => restoreCandidateDispositions != null;

    public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
        PendingBatchDispositions => restoreCandidateDispositions
            ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

    public bool TryGetPendingBatchDisposition(
        string operationId,
        out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
    {
        disposition = null;
        return restoreCandidateDispositionsByOperation != null
            && restoreCandidateDispositionsByOperation.TryGetValue(
                operationId ?? string.Empty,
                out disposition);
    }

    IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery.Drains =>
        restoreCandidateInputDestinationDrains
        ?? Array.Empty<ProductionInputDestinationCustodyDrainSaveData>();

    bool IProductionInputDestinationCustodyDrainRestoreCandidateQuery.TryGetDrain(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData drain)
    {
        drain = null;
        if (restoreCandidateInputDestinationDrainsByStep == null
            || !restoreCandidateInputDestinationDrainsByStep.TryGetValue(
                stepOperationId ?? string.Empty,
                out ProductionInputDestinationCustodyDrainSaveData found))
        {
            return false;
        }
        drain = found.Clone();
        return true;
    }

    IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
        IPhysicalItemRestoreCandidateOutputQuery.CommittedOutputs =>
        restoreCandidateOutputs
        ?? Array.Empty<PhysicalItemRestoreCandidateOutputSnapshot>();

    bool IPhysicalItemRestoreCandidateOutputQuery.TryGetCommittedOutput(
        string commitId,
        out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs)
    {
        outputs = Array.Empty<PhysicalItemRestoreCandidateOutputSnapshot>();
        return restoreCandidateOutputsByCommit != null
            && restoreCandidateOutputsByCommit.TryGetValue(
                commitId ?? string.Empty,
                out outputs);
    }

    IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        IFacilityBufferPlannedOutputRestoreCandidateQuery.Batches =>
        restoreCandidatePlannedOutputBatches
        ?? Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>();

    bool IFacilityBufferPlannedOutputRestoreCandidateQuery.TryGetBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
    {
        batch = null;
        return restoreCandidatePlannedOutputBatchesByCommit != null
            && restoreCandidatePlannedOutputBatchesByCommit.TryGetValue(
                batchCommitId ?? string.Empty,
                out batch);
    }

    IReadOnlyList<FacilityOutputExactRouteOutboxSaveData>
        IFacilityOutputExactRouteRestoreCandidateQuery.Routes =>
        restoreCandidateExactOutputRoutes
        ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>();

    IReadOnlyList<FacilityOutputExactRouteDeliveryRevisionSnapshot>
        IFacilityOutputExactRouteDeliveryRevisionRestoreCandidateQuery
            .CurrentDeliveryRevisions =>
        (restoreCandidateExactOutputRoutes
                ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Select(CreateDeliveryRevisionSnapshot)
            .ToArray();

    long IFacilityOutputExactRouteRestoreCandidateQuery
        .LastConfirmedCheckpointSequence =>
        restoreCandidateExactRouteCheckpointSequence;

    string IFacilityOutputExactRouteRestoreCandidateQuery
        .LastConfirmedCheckpointDigest =>
        restoreCandidateExactRouteCheckpointDigest;

    bool IFacilityOutputExactRouteRestoreCandidateQuery.TryGetRoute(
        string routeOperationId,
        out FacilityOutputExactRouteOutboxSaveData route)
    {
        route = null;
        return restoreCandidateExactOutputRoutesByOperation != null
            && restoreCandidateExactOutputRoutesByOperation.TryGetValue(
                routeOperationId ?? string.Empty,
                out route);
    }

    bool IFacilityOutputExactRouteDeliveryRevisionRestoreCandidateQuery
        .TryGetCurrentDeliveryRevision(
            string routeOperationId,
            out FacilityOutputExactRouteDeliveryRevisionSnapshot revision)
    {
        revision = null;
        if (restoreCandidateExactOutputRoutesByOperation == null
            || !restoreCandidateExactOutputRoutesByOperation.TryGetValue(
                routeOperationId ?? string.Empty,
                out FacilityOutputExactRouteOutboxSaveData route))
        {
            return false;
        }
        revision = CreateDeliveryRevisionSnapshot(route);
        return true;
    }

    private static FacilityOutputExactRouteDeliveryRevisionSnapshot
        CreateDeliveryRevisionSnapshot(
            FacilityOutputExactRouteOutboxSaveData route)
    {
        if (route == null)
            throw new InvalidOperationException(
                "Exact-route delivery revision source is null.");
        return new FacilityOutputExactRouteDeliveryRevisionSnapshot(
            route.routeOperationId,
            route.physicalReceiptFingerprint,
            route.currentDeliveryRevision,
            route.currentDeliveryRevisionFingerprint,
            route.currentDeliveryRerouteOperationId,
            route.currentTargetDestinationId,
            route.currentTargetPositionX,
            route.currentTargetPositionY,
            route.currentTargetAuthorityFingerprint);
    }

    public DungeonPhysicalItemSaveData Capture()
    {
        return persistence.Capture();
    }

    public void SetStoredItemMarkersVisible(bool visible)
    {
        itemQueryService.SetStoredItemMarkersVisible(visible);
    }

    public void Restore(DungeonPhysicalItemSaveData snapshot)
    {
        IDungeonSaveRestoreStage stage = StageRestore(snapshot);
        stage.Commit(new DungeonGameRestoreReport());
    }

    public IDungeonSaveRestoreStage StageRestore(
        DungeonPhysicalItemSaveData snapshot)
    {
        WorldItemRestoreState staged = persistence.StageRestore(snapshot);
        return new DungeonDelegateSaveRestoreStage(
            PhysicalItemsSaveSection.Id,
            _ => CommitRestore(staged));
    }

    public IDungeonSaveRestoreStage StageTransactionalRestore(
        DungeonPhysicalItemSaveData snapshot,
        IRestoreWorldCandidateQuery restoreWorldCandidates)
    {
        IRestoreWorldCandidateQuery candidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        if (!candidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> candidateBuildings))
        {
            throw new InvalidOperationException(
                "Physical item transactional restore requires the detached facility-world candidate.");
        }

        WorldItemRestoreState staged = persistence.StageRestore(
            snapshot,
            candidateBuildings,
            massQuery);
        PublishPhysicalRestoreCandidateIndex(staged);
        return new PhysicalItemCandidateSaveRestoreStage(this, staged);
    }

    private void PublishPhysicalRestoreCandidateIndex(
        WorldItemRestoreState staged)
    {
        if (staged?.RepositoryState == null)
        {
            throw new ArgumentNullException(nameof(staged));
        }
        if (restoreCandidateDispositions != null
            || restoreCandidateDispositionsByOperation != null
            || restoreCandidateInputDestinationDrains != null
            || restoreCandidateInputDestinationDrainsByStep != null
            || restoreCandidateOutputs != null
            || restoreCandidateOutputsByCommit != null
            || restoreCandidatePlannedOutputBatches != null
            || restoreCandidatePlannedOutputBatchesByCommit != null
            || restoreCandidateExactOutputRoutes != null
            || restoreCandidateExactOutputRoutesByOperation != null)
        {
            throw new InvalidOperationException(
                "A physical-item restore candidate is already indexed.");
        }

        PhysicalItemRestoreCandidateDispositionSnapshot[] snapshots =
            staged.RepositoryState.PendingBatchDispositions.Values
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .Select(value =>
                    new PhysicalItemRestoreCandidateDispositionSnapshot(value))
                .ToArray();
        Dictionary<string, PhysicalItemRestoreCandidateDispositionSnapshot>
            byOperation = new(StringComparer.Ordinal);
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot snapshot in
                 snapshots)
        {
            if (!byOperation.TryAdd(snapshot.OperationId, snapshot))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical restore candidate operation '{snapshot.OperationId}'.");
            }
        }

        ProductionInputDestinationCustodyDrainSaveData[] inputDrains = staged
            .RepositoryState.PendingProductionInputDestinationDrains.Values
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
        Dictionary<string, ProductionInputDestinationCustodyDrainSaveData>
            inputDrainsByStep = new(StringComparer.Ordinal);
        foreach (ProductionInputDestinationCustodyDrainSaveData drain in inputDrains)
        {
            if (drain == null
                || !inputDrainsByStep.TryAdd(
                    drain.stepOperationId,
                    drain.Clone()))
            {
                throw new InvalidOperationException(
                    $"Duplicate production input-destination drain restore candidate '{drain?.stepOperationId}'.");
            }
        }

        PhysicalItemRestoreCandidateOutputSnapshot[] outputSnapshots =
            staged.RepositoryState.Records
                .Select(record => TryCreateCommittedOutputSnapshot(
                    record,
                    out PhysicalItemRestoreCandidateOutputSnapshot output)
                    ? output
                    : null)
                .Where(output => output != null)
                .OrderBy(output => output.CommitId, StringComparer.Ordinal)
                .ThenBy(output => output.StackId, StringComparer.Ordinal)
                .ToArray();
        Dictionary<string,
            IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>>
            outputsByCommit = outputSnapshots
                .GroupBy(output => output.CommitId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<
                        PhysicalItemRestoreCandidateOutputSnapshot>)
                        Array.AsReadOnly(group.ToArray()),
                    StringComparer.Ordinal);
        FacilityBufferPlannedOutputRestoreBatchSnapshot[] plannedBatches =
            FacilityBufferPlannedOutputRestoreCandidateFactory
            .CapturePendingBatches(
                staged.RepositoryState.Records,
                massQuery)
            .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, FacilityBufferPlannedOutputRestoreBatchSnapshot>
            plannedByCommit = new(StringComparer.Ordinal);
        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot planned in
                 plannedBatches)
        {
            if (!plannedByCommit.TryAdd(planned.BatchCommitId, planned))
            {
                throw new InvalidOperationException(
                    $"Duplicate planned-output restore batch '{planned.BatchCommitId}'.");
            }
        }
        FacilityOutputExactRouteOutboxSaveData[] exactRoutes =
            (staged.ExactRouteCandidate?.Routes
                ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
        long exactRouteCheckpointSequence = staged.ExactRouteCandidate?
            .CheckpointSequence
            ?? throw new InvalidOperationException(
                "Physical restore has no exact-route checkpoint candidate.");
        string exactRouteCheckpointDigest = staged.ExactRouteCandidate
            .CheckpointDigest;
        Dictionary<string, FacilityOutputExactRouteOutboxSaveData>
            exactRoutesByOperation = new(StringComparer.Ordinal);
        foreach (FacilityOutputExactRouteOutboxSaveData route in exactRoutes)
        {
            if (route == null
                || !exactRoutesByOperation.TryAdd(
                    route.routeOperationId,
                    route))
            {
                throw new InvalidOperationException(
                    $"Duplicate exact-output-route restore candidate '{route?.routeOperationId}'.");
            }
        }
        // Candidate publication must be a non-failing pointer/map swap. Keep
        // every potentially throwing projection and duplicate check above in
        // locals so a rejected candidate cannot leave a partially visible
        // cross-section restore index behind.
        restoreCandidateDispositions = Array.AsReadOnly(snapshots);
        restoreCandidateDispositionsByOperation = byOperation;
        restoreCandidateInputDestinationDrains = Array.AsReadOnly(inputDrains);
        restoreCandidateInputDestinationDrainsByStep = inputDrainsByStep;
        restoreCandidateOutputs = Array.AsReadOnly(outputSnapshots);
        restoreCandidateOutputsByCommit = outputsByCommit;
        restoreCandidatePlannedOutputBatches = Array.AsReadOnly(plannedBatches);
        restoreCandidatePlannedOutputBatchesByCommit = plannedByCommit;
        restoreCandidateExactOutputRoutes = Array.AsReadOnly(exactRoutes);
        restoreCandidateExactOutputRoutesByOperation = exactRoutesByOperation;
        restoreCandidateExactRouteCheckpointSequence =
            exactRouteCheckpointSequence;
        restoreCandidateExactRouteCheckpointDigest =
            exactRouteCheckpointDigest;
    }

    private bool TryCreateCommittedOutputSnapshot(
        WorldItemStackRecord record,
        out PhysicalItemRestoreCandidateOutputSnapshot output)
    {
        output = null;
        ItemInstanceComponentSaveData commitComponent = (record?.components
                ?? new List<ItemInstanceComponentSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.ProductionOutputCommit,
                    StringComparison.Ordinal));
        ItemStateValueSaveData commitField = commitComponent?.values
            ?.SingleOrDefault(value => value != null
                && string.Equals(value.key, "commit-id", StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String);
        string commitId = commitField?.stringValue ?? string.Empty;
        if (record == null
            || record.quantity <= 0
            || string.IsNullOrWhiteSpace(commitId))
        {
            return false;
        }

        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)record.itemId,
            record.itemInstanceId,
            record.components);
        long massGrams = massQuery.GetQuantityMass(
            (ItemDefinitionId)record.itemId,
            subject,
            record.quantity).Value;
        output = new PhysicalItemRestoreCandidateOutputSnapshot(
            commitId,
            record.stackId,
            record.itemId,
            record.quantity,
            massGrams,
            record.state,
            record.position,
            record.destinationId);
        return true;
    }

    private void ClearPhysicalRestoreCandidateIndex()
    {
        restoreCandidateDispositions = null;
        restoreCandidateDispositionsByOperation = null;
        restoreCandidateInputDestinationDrains = null;
        restoreCandidateInputDestinationDrainsByStep = null;
        restoreCandidateOutputs = null;
        restoreCandidateOutputsByCommit = null;
        restoreCandidatePlannedOutputBatches = null;
        restoreCandidatePlannedOutputBatchesByCommit = null;
        restoreCandidateExactOutputRoutes = null;
        restoreCandidateExactOutputRoutesByOperation = null;
        restoreCandidateExactRouteCheckpointSequence = 0L;
        restoreCandidateExactRouteCheckpointDigest = string.Empty;
    }

    private void CommitRestore(WorldItemRestoreState staged)
    {
        persistence.Commit(staged);
        if (!aggregateRootStore.IsRestoreStaging)
        {
            ProjectRestoredWorldState();
        }
    }

    private void ProjectRestoredWorldState()
    {
        warehouseService.NormalizeStorageIds();
        RefreshAllMarkers();
    }

    public bool SpawnItemAtDropoff(
        string itemId,
        int amount,
        string sourceLabel,
        out int spawned)
    {
        spawned = 0;
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        if (normalizedItemId.Length == 0
            || amount <= 0
            || !TryGetDropoffPosition(out Vector2Int dropoff)
            || !catalogProvider.TryGetDefinition(normalizedItemId, out DungeonItemDefinition definition)
            || definition.MaxStack <= 1)
        {
            return false;
        }

        spawned = Spawn(
            normalizedItemId,
            amount,
            dropoff,
            WorldItemStackState.Loose,
            string.Empty);
        return spawned == amount;
    }

    public bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, out int spawned)
    {
        return SpawnStockAtDropoff(
            category,
            amount,
            sourceLabel,
            WorldItemStackState.Loose,
            string.Empty,
            out spawned);
    }

    public bool SpawnStockAtDropoff(
        StockCategory category,
        int amount,
        string sourceLabel,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = 0;
        if (amount <= 0 || !TryGetDropoffPosition(out Vector2Int dropoff))
        {
            return false;
        }

        DungeonItemDefinition definition = catalogProvider.All
            .Where(candidate => candidate != null
                && candidate.StockCategory == category
                && candidate.MaxStack > 1)
            .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No authored stackable item belongs to stock category '{category}'. "
                + "Unique equipment must be created through its authoritative equipment runtime.");
        spawned = Spawn(definition.ItemId, amount, dropoff, state, destinationId ?? string.Empty);
        return spawned == amount;
    }

    public bool SpawnStockInWarehouse(
        IWarehouseFacility warehouse,
        StockCategory category,
        int amount,
        out int spawned)
    {
        return warehouseService.SpawnStock(
            warehouse,
            category,
            amount,
            out spawned);
    }

    public bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = 0;
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return false;
        }

        spawned = Spawn(itemId.Trim(), amount, position, state, destinationId ?? string.Empty);
        return spawned == amount;
    }

    public bool SpawnWasteAt(
        string itemId,
        int amount,
        Vector2Int position,
        WasteOriginKind origin,
        float contamination,
        out int spawned)
    {
        spawned = 0;
        if (string.IsNullOrWhiteSpace(itemId)
            || amount <= 0
            || origin == WasteOriginKind.Unknown)
        {
            return false;
        }

        spawned = Spawn(
            itemId.Trim(),
            amount,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            wasteOrigin: origin,
            contamination: contamination);
        return spawned == amount;
    }

    public bool SpawnUniqueItemAt(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId)
    {
        return itemSpawner.SpawnUnique(
            itemId,
            position,
            state,
            destinationId,
            out stackId);
    }

    public bool SpawnExistingUniqueItemAt(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId)
    {
        IReadOnlyList<ItemInstanceComponentSaveData> components =
            itemRepository.EquipmentInstances.TryGetValue(
                itemInstanceId.Value,
                out CombatEquipmentInstance equipment)
                ? new[]
                {
                    EquipmentItemStateCodec.Encode(
                        equipment,
                        (equipment.moduleSlots ?? new List<EquipmentModuleSlotState>())
                            .Where(slot => slot != null
                                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)
                                && itemRepository.EquipmentModules.ContainsKey(
                                    slot.moduleInstanceId))
                            .Select(slot =>
                                itemRepository.EquipmentModules[slot.moduleInstanceId]))
                }
                : Array.Empty<ItemInstanceComponentSaveData>();
        return itemSpawner.SpawnExistingUnique(
            itemId,
            itemInstanceId,
            position,
            state,
            destinationId,
            false,
            default,
            components,
            out stackId);
    }

    public bool SpawnUniqueItemAt(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string stackId)
    {
        return itemSpawner.SpawnUnique(
            itemId,
            position,
            state,
            destinationId,
            destinationPosition,
            out stackId);
    }

    public bool SpawnHumanoidCorpse(
        CharacterActor source,
        Vector2Int position,
        string deathReason,
        out string stackId)
    {
        stackId = string.Empty;
        if (source == null)
        {
            return false;
        }

        if (!CharacterPersistentIdentity.TryGet(source, out CharacterId characterId))
        {
            return false;
        }

        string persistentId = characterId.Value;

        int spawned = Spawn(
            DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
            1,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            sourceCharacterId: persistentId,
            sourceDisplayName: source.Identity?.DisplayName ?? source.name,
            sourceSpeciesTag: source.Identity?.SpeciesTag ?? string.Empty,
            sourceDeathReason: deathReason ?? string.Empty,
            emergencyButcheryAllowed: false);
        if (spawned <= 0)
        {
            return false;
        }

        stackId = stacks.LastOrDefault(record => record != null
            && record.itemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
            && record.sourceCharacterId == persistentId
            && record.position == position)?.stackId ?? string.Empty;
        return !string.IsNullOrWhiteSpace(stackId);
    }

    public bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component)
    {
        if (component == null
            || string.IsNullOrWhiteSpace(component.componentTypeId)
            || !stacksById.TryGetValue(
                stackId?.Trim() ?? string.Empty,
                out WorldItemStackRecord stack)
            || stack == null
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                stack.components))
        {
            return false;
        }

        stack.components ??= new List<ItemInstanceComponentSaveData>();
        stack.components.RemoveAll(existing => existing != null
            && string.Equals(
                existing.componentTypeId?.Trim(),
                component.componentTypeId.Trim(),
                StringComparison.Ordinal));
        stack.components.Add(component.Clone());
        MarkStacksChanged();
        return true;
    }

    public bool TryRequestFacilityDelivery(
        StockCategory category,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        return warehouseService.TryRequestCategoryDelivery(
            category,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        return warehouseService.TryRequestDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    public bool TryRequestStackDelivery(
        string stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        return warehouseService.TryRequestStackDelivery(
            stackId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    public bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile)
    {
        return itemQueryService.TryGetPileAt(position, out pile);
    }

    public bool TryGetPileTargetAt(
        Vector2Int position,
        out ItemPileInfoTarget target,
        out UnityEngine.Object markerObject)
    {
        return itemQueryService.TryGetPileTargetAt(
            position,
            out target,
            out markerObject);
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(Vector2Int position, bool includeStored = false)
    {
        return itemQueryService.GetStacksAt(position, includeStored);
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks()
    {
        return itemQueryService.GetAllStacks();
    }

    public bool TryFindNearestAvailableStock(
        Vector2Int origin,
        StockCategory category,
        bool preferStored,
        out WorldItemStackSnapshot stack)
    {
        return itemQueryService.TryFindNearestAvailableStock(
            origin,
            category,
            preferStored,
            out stack);
    }

    public void CopyAvailableStockCandidates(
        StockCategory category,
        List<WorldItemStockCandidate> destination)
    {
        itemQueryService.CopyAvailableStockCandidates(category, destination);
    }

    public bool TryFindBestAvailableStack(
        Vector2Int origin,
        Func<string, int> rankSelector,
        out WorldItemStackSnapshot stack)
    {
        return itemQueryService.TryFindBestAvailableStack(
            origin,
            rankSelector,
            out stack);
    }

    public bool HasAvailableHaulJob(CharacterActor actor)
    {
        return haulPlanningService.HasAvailablePlan(actor);
    }

    public bool TryReserveBestHaulPlan(
        CharacterActor actor,
        out WorldItemHaulPlan plan,
        out string failureReason)
    {
        long started = performanceRecorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
        try
        {
            return haulPlanningService.TryReserveBestPlan(actor, out plan, out failureReason);
        }
        finally
        {
            if (started != 0L)
            {
                performanceRecorder.Record(
                    AiPerformanceCategory.HaulPlanning,
                    (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
            }
        }
    }

    public bool TryReserveStoredItemForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason)
    {
        return warehouseService.TryReserveStoredForDirectPickup(
            actor,
            itemId,
            quantity,
            out reservation,
            out pickupStandPosition,
            out failureReason);
    }

    public bool TryReserveAvailableItemForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        ItemReservationPurpose purpose,
        string ownerOperationId,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason)
    {
        reservation = default;
        pickupStandPosition = default;
        failureReason = string.Empty;
        string operationId = ownerOperationId?.Trim() ?? string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(itemId)
            || quantity <= 0
            || operationId.Length == 0
            || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "items.pickup.invalid_request";
            return false;
        }

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        WorldItemStackRecord selected = stacks
            .Where(record => record != null
                && record.quantity > 0
                && record.state is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                && !record.forbidden
                && record.quantity - record.reservedQuantity >= quantity
                && string.Equals(record.itemId, itemId, StringComparison.Ordinal))
            .Select(record => new
            {
                Record = record,
                HasStand = TryResolveDirectPickupStandCell(
                    grid,
                    record.position,
                    out Vector2Int stand),
                Stand = stand
            })
            .Where(candidate => candidate.HasStand)
            .OrderBy(candidate => Manhattan(actor.GetNowXY(), candidate.Stand))
            .ThenBy(candidate => candidate.Record.state == WorldItemStackState.Loose ? 0 : 1)
            .ThenBy(candidate => candidate.Record.stackId, StringComparer.Ordinal)
            .FirstOrDefault()?.Record;
        if (selected == null
            || !TryResolveDirectPickupStandCell(
                grid,
                selected.position,
                out pickupStandPosition))
        {
            failureReason = "items.pickup.available_item_unavailable";
            return false;
        }

        if (!itemTransferService.TryReserveAvailableStackForDirectPickup(
                actorId,
                operationId,
                purpose,
                selected.stackId,
                quantity,
                out ItemQuantityLease lease,
                out DomainFailure failure))
        {
            failureReason = failure.ToString();
            return false;
        }

        reservation = new WorldItemReservedStackQuantity(
            selected.stackId,
            selected.itemId,
            quantity,
            selected.position,
            WorldItemHaulDestinationKind.Warehouse,
            selected.destinationId,
            lease.leaseId,
            operationId);
        return true;
    }

    public bool TryReserveBestHaulJob(
        CharacterActor actor,
        out WorldItemHaulJob job,
        out string failureReason)
    {
        return haulPlanningService.TryReserveBestJob(actor, out job, out failureReason);
    }

    public bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason)
    {
        return itemTransferService.TryPickupReservedStackQuantity(
            actor,
            inventory,
            reservation,
            out pickedUp,
            out failureReason);
    }

    public bool TryPickupReservedStack(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemHaulJob job,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null || !job.IsValid)
        {
            failureReason = "invalid haul job";
            return false;
        }

        if (!stacksById.TryGetValue(job.StackId, out WorldItemStackRecord record)
            || record.quantity <= 0)
        {
            failureReason = "stack disappeared";
            return false;
        }

        if (string.IsNullOrWhiteSpace(job.LeaseId)
            || string.IsNullOrWhiteSpace(job.OwnerOperationId))
        {
            failureReason = "haul job has no quantity lease";
            return false;
        }

        WorldItemReservedStackQuantity reservation = new WorldItemReservedStackQuantity(
            record.stackId,
            record.itemId,
            Mathf.Max(1, job.Quantity),
            record.position,
            job.DestinationKind,
            job.DestinationId,
            job.LeaseId,
            job.OwnerOperationId);
        return TryPickupReservedStackQuantity(
            actor,
            inventory,
            reservation,
            out _,
            out failureReason);
    }

    public bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason)
    {
        return itemTransferService.TryDepositCarriedItems(
            actor,
            inventory,
            warehouse,
            out failureReason);
    }

    public bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason)
    {
        return itemTransferService.TryDepositCarriedItems(
            actor,
            inventory,
            warehouse,
            ownerOperationIds,
            out failureReason);
    }

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason)
    {
        return itemTransferService.TryDepositCarriedItemsToFacility(
            actor,
            inventory,
            destinationPosition,
            destinationId,
            out failureReason);
    }

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason)
    {
        return itemTransferService.TryDepositCarriedItemsToFacility(
            actor,
            inventory,
            destinationPosition,
            destinationId,
            ownerOperationIds,
            out failureReason);
    }

    public bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason)
    {
        return itemTransferService.TryConsumeFacilityBuffer(
            destinationId,
            costs,
            out failureReason);
    }

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        return itemTransferService.TryConsumeFacilityItemBuffer(
            destinationId,
            costs,
            out failureReason);
    }

    public bool TryStealLooseItem(
        CharacterActor actor,
        int searchRadius,
        out WorldItemStackSnapshot stolenItem,
        out string failureReason)
    {
        return theftService.TryStealLooseItem(
            actor,
            searchRadius,
            out stolenItem,
            out failureReason);
    }

    public void ReleaseReservation(string stackId, string persistentId)
    {
        itemTransferService.ReleaseQuantityReservationsByOwner(
            $"haul:{persistentId?.Trim() ?? string.Empty}",
            ItemReservationReleaseReason.Cancelled);
        reservationService.Release(stackId, persistentId);
    }

    public bool TryRenewQuantityLease(
        string leaseId,
        double requestedUntilGameSeconds,
        out string failureReason)
    {
        bool renewed = itemTransferService.RenewQuantityReservation(
            leaseId,
            requestedUntilGameSeconds,
            out DomainFailure failure);
        failureReason = renewed ? string.Empty : failure.ToString();
        return renewed;
    }

    public bool TryRevalidateQuantityLease(
        string leaseId,
        out string failureReason)
    {
        if (reservationService is not ItemReservationService reservationAuthority)
        {
            failureReason = "quantity reservation authority unavailable";
            return false;
        }

        bool valid = reservationAuthority.QuantityReservations.Revalidate(
            leaseId,
            out _,
            out DomainFailure failure);
        failureReason = valid ? string.Empty : failure.ToString();
        return valid;
    }

    public bool ReleaseQuantityLease(
        string leaseId,
        ItemReservationReleaseReason reason) =>
        itemTransferService.ReleaseQuantityReservation(leaseId, reason);

    public bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        out string failureReason)
    {
        if (itemTransferService is ICarriedItemDropService dropService)
        {
            return dropService.TryDropCarriedItems(
                actor,
                inventory,
                out failureReason);
        }

        failureReason = "carried item drop service unavailable";
        return false;
    }

    private sealed class PhysicalItemCandidateSaveRestoreStage :
        IDungeonSaveRestoreStage,
        IDungeonDiscardableSaveRestoreStage
    {
        private WorldItemStackRuntime owner;
        private WorldItemRestoreState staged;
        private bool committed;

        internal PhysicalItemCandidateSaveRestoreStage(
            WorldItemStackRuntime owner,
            WorldItemRestoreState staged)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.staged = staged ?? throw new ArgumentNullException(nameof(staged));
        }

        public string SectionId => PhysicalItemsSaveSection.Id;

        public void Commit(DungeonGameRestoreReport report)
        {
            _ = report ?? throw new ArgumentNullException(nameof(report));
            WorldItemStackRuntime requiredOwner = owner
                ?? throw new InvalidOperationException(
                    "Physical-item restore stage was already consumed.");
            requiredOwner.CommitRestore(staged);
            committed = true;
            staged = null;
            owner = null;
        }

        public void Discard()
        {
            if (committed || owner == null)
            {
                return;
            }
            owner.ClearPhysicalRestoreCandidateIndex();
            staged = null;
            owner = null;
        }
    }

    public bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        HaulCarryDropContext context,
        out string failureReason)
    {
        if (itemTransferService is ICarriedItemDropService dropService)
        {
            return dropService.TryDropCarriedItems(
                actor,
                inventory,
                ownerOperationIds,
                context,
                out failureReason);
        }

        failureReason = "carried item drop service unavailable";
        return false;
    }

    public bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason)
    {
        if (itemTransferService is ICarriedItemDropService dropService)
        {
            return dropService.TryDropCarriedItems(
                actor,
                inventory,
                ownerOperationIds,
                out failureReason);
        }

        failureReason = "carried item drop service unavailable";
        return false;
    }

    public bool TryClearReservation(string stackId)
    {
        return reservationService.TryClear(stackId);
    }

    public bool SetForbidden(string stackId, bool forbidden)
    {
        return reservationService.SetForbidden(stackId, forbidden);
    }

    public bool PrioritizeHaul(string stackId)
    {
        return reservationService.PrioritizeHaul(stackId);
    }

    public bool TryRouteStackToDestination(
        string stackId,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        string canonicalDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stackId)
            || canonicalDestination.Length == 0
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.reservedQuantity > 0)
        {
            failureReason = "이동시킬 물품 스택을 찾을 수 없습니다.";
            return false;
        }

        if (canonicalDestination.StartsWith(
                ReservedTargetDestinationIdentity.PowerFuelPrefix,
                StringComparison.Ordinal))
        {
            failureReason =
                "items.delivery.facility_buffer_managed_route_required";
            return false;
        }
        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            failureReason =
                "items.delivery.prepared_output_route_protected";
            return false;
        }

        record.state = state;
        record.destinationId = canonicalDestination;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = true;
        record.destinationPosition = destinationPosition;
        record.reservedByPersistentId = string.Empty;
        MarkStacksChanged();
        RefreshMarkerAt(record.position);
        return true;
    }

    public bool DeleteStack(string stackId)
    {
        if (string.IsNullOrWhiteSpace(stackId)
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record)
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            return false;
        }

        Vector2Int position = record.position;
        if (!string.IsNullOrWhiteSpace(record.itemInstanceId))
        {
            itemRepository.TryMarkEquipmentLostBySourceStack(record.stackId);
            itemRepository.TryMarkModuleLostBySourceStack(record.stackId);
        }
        RemoveRecord(record);
        RefreshMarkerAt(position);
        return true;
    }

    public bool TryAbsorbUniqueItemStack(
        string stackId,
        ItemInstanceId expectedInstanceId)
    {
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        if (!expectedInstanceId.IsValid
            || !stacksById.TryGetValue(
                normalizedStackId,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity != 1
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components)
            || !string.Equals(
                record.itemInstanceId,
                expectedInstanceId.Value,
                StringComparison.Ordinal))
        {
            return false;
        }

        Vector2Int position = record.position;
        RemoveRecord(record);
        RefreshMarkerAt(position);
        return true;
    }

    public bool TryConsumeStackQuantity(string stackId, int quantity, out WorldItemStackSnapshot consumed)
    {
        ItemStackId typedStackId = new(stackId);
        if (!typedStackId.IsValid)
        {
            consumed = null;
            return false;
        }

        return itemTransferService.TryConsumeStackQuantity(
            typedStackId,
            quantity,
            out consumed,
            out _);
    }

    public bool SetEmergencyButcheryAllowed(string stackId, bool allowed)
    {
        if (string.IsNullOrWhiteSpace(stackId)
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record)
            || record == null
            || record.itemId != DarkSurvivalItemDefinitions.HumanoidCorpseItemId)
        {
            return false;
        }

        record.emergencyButcheryAllowed = allowed;
        MarkStacksChanged();
        RefreshMarkerAt(record.position);
        return true;
    }

    public int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId)
    {
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return 0;
        }

        WorldItemStackRecord[] targets = stacks
            .Where(stack => stack != null
                && stack.state == state
                && string.Equals(
                    stack.destinationId ?? string.Empty,
                    normalizedDestination,
                    StringComparison.Ordinal))
            .ToArray();
        if (targets.Any(target =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    target.components)))
        {
            throw new FacilityOutputExactRouteBypassException(
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
                nameof(RemoveStacksByStateAndDestination));
        }
        int removed = 0;
        foreach (WorldItemStackRecord target in targets)
        {
            Vector2Int position = target.position;
            removed += Mathf.Max(0, target.quantity);
            if (state == WorldItemStackState.Stored && IsOutboundStoredStack(target))
            {
                int quantity = target.quantity;
                string itemId = target.itemId;
                string sourceStorageDestinationId = target.sourceStorageDestinationId;
                RemoveRecord(target);
                Spawn(
                    itemId,
                    quantity,
                    position,
                    WorldItemStackState.Stored,
                    sourceStorageDestinationId);
            }
            else
            {
                RemoveRecord(target);
            }

            RefreshMarkerAt(position);
        }

        return removed;
    }

    public int ReleaseStacksByDestination(
        string destinationId,
        Vector2Int releasePosition)
    {
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return 0;
        }

        WorldItemStackRecord[] targets = stacks
            .Where(stack => stack != null
                && string.Equals(
                    stack.destinationId ?? string.Empty,
                    normalizedDestination,
                    StringComparison.Ordinal))
            .ToArray();
        if (targets.Any(target =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    target.components)))
        {
            throw new FacilityOutputExactRouteBypassException(
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
                nameof(ReleaseStacksByDestination));
        }
        int released = 0;
        foreach (WorldItemStackRecord target in targets)
        {
            int quantity = Mathf.Max(0, target.quantity);
            if (quantity <= 0)
            {
                continue;
            }

            released += quantity;
            Vector2Int oldPosition = target.position;
            string itemId = target.itemId;
            if (target.state == WorldItemStackState.Stored
                && IsOutboundStoredStack(target))
            {
                string sourceStorageDestinationId =
                    target.sourceStorageDestinationId ?? string.Empty;
                RemoveRecord(target);
                Spawn(
                    itemId,
                    quantity,
                    oldPosition,
                    WorldItemStackState.Stored,
                    sourceStorageDestinationId);
            }
            else
            {
                Vector2Int loosePosition =
                    target.state == WorldItemStackState.FacilityBuffer
                    || target.state == WorldItemStackState.FacilityOutputBuffer
                        ? releasePosition
                        : oldPosition;
                if (!string.IsNullOrWhiteSpace(target.itemInstanceId))
                {
                    target.state = WorldItemStackState.Loose;
                    target.destinationId = string.Empty;
                    target.sourceStorageDestinationId = string.Empty;
                    target.hasDestinationPosition = false;
                    target.destinationPosition = default;
                    target.reservedByPersistentId = string.Empty;
                    MarkStacksChanged();
                    loosePosition = oldPosition;
                }
                else
                {
                    RemoveRecord(target);
                    Spawn(
                        itemId,
                        quantity,
                        loosePosition,
                        WorldItemStackState.Loose,
                        string.Empty);
                }
                RefreshMarkerAt(loosePosition);
            }

            RefreshMarkerAt(oldPosition);
        }

        return released;
    }

    private int Spawn(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition = false,
        Vector2Int destinationPosition = default,
        string sourceCharacterId = "",
        string sourceDisplayName = "",
        string sourceSpeciesTag = "",
        string sourceDeathReason = "",
        bool emergencyButcheryAllowed = false,
        string sourceStorageDestinationId = "",
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f)
    {
        return itemSpawner.Spawn(
            itemId,
            amount,
            position,
            state,
            destinationId,
            hasDestinationPosition,
            destinationPosition,
            sourceCharacterId,
            sourceDisplayName,
            sourceSpeciesTag,
            sourceDeathReason,
            emergencyButcheryAllowed,
            sourceStorageDestinationId,
            wasteOrigin,
            contamination);
    }

    private static WasteOriginKind ResolveLegacyWasteOrigin(string itemId)
    {
        string id = itemId?.Trim() ?? string.Empty;
        if (string.Equals(id, "waste:plant-rot", StringComparison.Ordinal))
        {
            return WasteOriginKind.Plant;
        }

        if (string.Equals(id, "waste:animal-rot", StringComparison.Ordinal))
        {
            return WasteOriginKind.Animal;
        }

        if (string.Equals(id, "waste:forbidden-rot", StringComparison.Ordinal))
        {
            return WasteOriginKind.Forbidden;
        }

        return IsLegacyWasteItem(id)
            ? WasteOriginKind.Mixed
            : WasteOriginKind.Unknown;
    }

    private static bool IsLegacyWasteItem(string itemId)
    {
        string id = itemId?.Trim() ?? string.Empty;
        return id.StartsWith("waste:", StringComparison.Ordinal)
            || string.Equals(id, WildlifeItemDefinitions.RotItemId, StringComparison.Ordinal);
    }

    private static string GetStoredSourceDestinationId(WorldItemStackRecord stack)
    {
        if (stack == null || stack.state != WorldItemStackState.Stored)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId))
        {
            return stack.sourceStorageDestinationId.Trim();
        }

        string destinationId = stack.destinationId?.Trim() ?? string.Empty;
        return destinationId.StartsWith(WarehouseStorageDestinationPrefix, StringComparison.Ordinal)
            ? destinationId
            : string.Empty;
    }

    private static Vector2Int ResolveWarehouseStoragePosition(IWarehouseFacility warehouse)
    {
        return warehouse is BuildableObject building ? building.centerPos : Vector2Int.zero;
    }

    private static bool IsOutboundStoredStack(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.state == WorldItemStackState.Stored
            && stack.hasDestinationPosition
            && !string.IsNullOrWhiteSpace(stack.destinationId)
            && !string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
            && (IsFacilityInputDestination(stack.destinationId)
                || IsCombatLoadoutDestination(stack.destinationId));
    }

    private static bool IsFacilityInputDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && (destinationId.StartsWith(FacilityInputDestinationPrefix, StringComparison.Ordinal)
                || destinationId.StartsWith(WorkOrderRuntime.ConstructionDestinationPrefix, StringComparison.Ordinal));
    }

    private static bool IsCombatLoadoutDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(CombatLoadoutDestinationPrefix, StringComparison.Ordinal);
    }

    private bool TryGetDropoffPosition(out Vector2Int dropoff)
    {
        if (worldDropZoneQuery.TryGetDeliveryDropoff(out dropoff))
        {
            return true;
        }

        if (characterSpawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            && spawner.TryGetEntryGridPosition(out dropoff))
        {
            return true;
        }

        if (TryGetGrid(out Grid grid))
        {
            GridCell cell = grid.GetCells()
                .Where(candidate => candidate != null && grid.IsWalkable(candidate.Position))
                .OrderBy(candidate => candidate.Position.y)
                .ThenBy(candidate => candidate.Position.x)
                .FirstOrDefault();
            if (cell != null)
            {
                dropoff = cell.Position;
                return true;
            }
        }

        dropoff = default;
        return false;
    }

    private void RemoveRecord(WorldItemStackRecord record)
    {
        itemRepository.Remove(record);
    }

    private static bool TryResolveDirectPickupStandCell(
        Grid grid,
        Vector2Int itemPosition,
        out Vector2Int stand)
    {
        stand = default;
        if (grid != null
            && grid.IsValidGridPos(itemPosition)
            && grid.IsWalkable(itemPosition))
        {
            stand = itemPosition;
            return true;
        }
        return grid != null
            && grid.TryFindNearbyWalkablePositionOnSameFloor(
                itemPosition,
                out stand,
                maxDistance: 1);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private void MarkStacksChanged()
    {
        itemRepository.MarkChanged();
    }

    private WorldItemStackSnapshot ToSnapshot(WorldItemStackRecord stack)
    {
        return itemQueryService.CreateSnapshot(stack);
    }

    private void RefreshAllMarkers()
    {
        itemMarkerPresenter.RefreshAll(stacks
            .Where(stack => stack != null)
            .Select(stack => stack.position)
            .Distinct()
            .ToArray());
    }

    private void RefreshMarkerAt(Vector2Int position)
    {
        itemMarkerPresenter.RefreshAt(position);
    }

    private bool TryGetGrid(out Grid grid)
    {
        return gridSystemProvider.TryGetGrid(out grid);
    }

}
