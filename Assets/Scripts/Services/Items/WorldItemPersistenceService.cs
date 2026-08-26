using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class WorldItemRestoreState
{
    public ItemHaulingSettingsSnapshot HaulingSettings { get; set; }
    public WorldItemRepositoryState RepositoryState { get; set; }
    public WarehousePhysicalRestoreAssessment WarehouseAssessment { get; set; } =
        WarehousePhysicalRestoreAssessment.Empty;
    public IReadOnlyList<ItemReservationIntentSaveData> ReservationIntents { get; set; } =
        Array.Empty<ItemReservationIntentSaveData>();
    public FacilityOutputExactRouteRestoreCandidate ExactRouteCandidate
        { get; set; }
}

/// <summary>
/// Serializes physical item state and builds a fully validated restore stage.
/// It never mutates the live repository while parsing or validating input.
/// </summary>
public sealed class WorldItemPersistenceService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettings;
    private readonly WorldItemRepository repository;
    private readonly IItemQuantityReservationPersistence reservationPersistence;
    private readonly IItemReservationMutationGate mutationGate;
    private readonly IFacilityOutputExactRouteOutboxPersistence
        exactRouteOutboxPersistence;
    private IHaulDeliveryIntentQuery HaulDeliveryIntents =>
        repository.HaulDeliveryIntents;

    public WorldItemPersistenceService(
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettings,
        WorldItemRepository repository,
        IFacilityOutputExactRouteOutboxPersistence exactRouteOutboxPersistence,
        IItemQuantityReservationPersistence reservationPersistence = null,
        IItemReservationMutationGate mutationGate = null)
    {
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.haulingSettings = haulingSettings
            ?? throw new ArgumentNullException(nameof(haulingSettings));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.reservationPersistence = reservationPersistence;
        this.mutationGate = mutationGate;
        this.exactRouteOutboxPersistence = exactRouteOutboxPersistence
            ?? throw new ArgumentNullException(nameof(exactRouteOutboxPersistence));
    }

    public DungeonPhysicalItemSaveData Capture()
    {
        using IDisposable captureBarrier = mutationGate?.EnterCaptureBarrier();
        DungeonPhysicalItemSaveData snapshot = new DungeonPhysicalItemSaveData
        {
            version = DungeonPhysicalItemSaveData.CurrentVersion,
            nextHaulOperationSequence = repository.NextHaulOperationSequence,
            haulingSettings = haulingSettings.Capture(),
            stacks = repository.Records
                .Where(stack => stack != null && stack.quantity > 0)
                .OrderBy(stack => stack.position.y)
                .ThenBy(stack => stack.position.x)
                .ThenBy(stack => stack.itemId, StringComparer.Ordinal)
                .ThenBy(stack => stack.stackId, StringComparer.Ordinal)
                .Select(CaptureDurableStack)
                .ToList(),
            uniqueItems = repository.EquipmentInstances.Values
                .Where(instance => instance != null)
                .Select(instance => new UniqueItemInstanceSaveData
                {
                    itemInstanceId = instance.instanceId,
                    definitionId = PhysicalItemIds.ForEquipment(instance.definitionId),
                    components = new List<ItemInstanceComponentSaveData>
                    {
                        EquipmentItemStateCodec.Encode(
                            instance,
                            (instance.moduleSlots
                                ?? new List<EquipmentModuleSlotState>())
                            .Where(slot => slot != null
                                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)
                                && repository.EquipmentModules.ContainsKey(
                                    slot.moduleInstanceId))
                            .Select(slot => repository.EquipmentModules[
                                slot.moduleInstanceId]))
                    }
                })
                .Concat(repository.EquipmentModules.Values
                    .Where(module => module != null
                        && !string.IsNullOrWhiteSpace(module.sourceStackId)
                        && string.IsNullOrWhiteSpace(
                            module.attachedEquipmentInstanceId))
                    .Select(module => new UniqueItemInstanceSaveData
                    {
                        itemInstanceId = module.instanceId,
                        definitionId = PhysicalItemIds.ForEquipmentModule(),
                        components = new List<ItemInstanceComponentSaveData>
                        {
                            EquipmentModuleItemStateCodec.Encode(module)
                        }
                    }))
                .OrderBy(item => item.itemInstanceId, StringComparer.Ordinal)
                .ToList(),
            reservationIntents = CaptureDurableReservationIntents(),
            pendingBatchDispositions = repository
                .CapturePendingBatchDispositions()
                .ToList(),
            pendingExactOutputRoutes = exactRouteOutboxPersistence
                .CaptureOutbox()
                .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList(),
            pendingProductionCustodyDrains = repository
                .CapturePendingProductionCustodyDrains()
                .ToList(),
            pendingProductionInputDestinationDrains = repository
                .CapturePendingProductionInputDestinationDrains()
                .ToList(),
            pendingCapacityRoutingDrains = repository
                .CapturePendingCapacityRoutingDrains()
                .ToList(),
            lastConfirmedExactRouteCheckpointSequence =
                exactRouteOutboxPersistence.LastConfirmedCheckpointSequence,
            lastConfirmedExactRouteCheckpointDigest =
                exactRouteOutboxPersistence.LastConfirmedCheckpointDigest
        };

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        PhysicalItemSaveValidation.Validate(snapshot, report, catalogProvider);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                $"Physical item capture produced a non-canonical V{DungeonPhysicalItemSaveData.CurrentVersion} payload: "
                + string.Join(" | ", report.Errors));
        }
        return snapshot;
    }

#if UNITY_EDITOR
    public void RestoreForEditorTest(DungeonPhysicalItemSaveData snapshot) =>
        Commit(StageRestore(snapshot));
#endif

    private List<ItemReservationIntentSaveData> CaptureDurableReservationIntents()
    {
        List<ItemReservationIntentSaveData> captured = CloneReservationIntents(
            reservationPersistence?.CaptureReservationIntents()
                ?? Array.Empty<ItemReservationIntentSaveData>());
        List<HaulDeliveryIntentSaveData> committed =
            HaulDeliveryIntents.CaptureCommitted().ToList();
        foreach (ItemReservationIntentSaveData intent in captured.ToArray())
        {
            bool hasHauling = intent.reservationHints.Any(hint =>
                hint != null && hint.purpose == ItemReservationPurpose.Hauling);
            if (!hasHauling)
                continue;

            intent.reservationHints = intent.reservationHints
                .Where(hint => hint != null
                    && (hint.purpose != ItemReservationPurpose.Hauling
                        || HaulDeliveryIntents.MatchesCommittedReservation(
                            intent.ownerOperationId,
                            hint.preferredPhysicalStackId,
                            hint.expectedStackSignature,
                            hint.quantity)))
                .OrderBy(hint => hint.preferredPhysicalStackId, StringComparer.Ordinal)
                .ThenBy(hint => hint.claimOrdinal)
                .ToList();
            for (int index = 0; index < intent.reservationHints.Count; index++)
            {
                intent.reservationHints[index].claimOrdinal = index;
                intent.reservationHints[index].claimHintId =
                    $"claim:{intent.ownerOperationId}:{index}";
            }
            if (intent.reservationHints.Count == 0)
                captured.Remove(intent);
        }

        foreach (HaulDeliveryIntentSaveData intent in committed)
        {
            ItemReservationIntentSaveData savedIntent = captured.SingleOrDefault(saved =>
                string.Equals(
                    saved.ownerOperationId,
                    intent.operationId,
                    StringComparison.Ordinal));
            if (savedIntent == null)
            {
                savedIntent = new ItemReservationIntentSaveData
                {
                    ownerOperationId = intent.operationId,
                    ownerCharacterId = intent.ownerCharacterId,
                    hadActiveItemReservation = true,
                    reservationHints = new List<ItemReservationClaimHintSaveData>()
                };
                captured.Add(savedIntent);
            }

            savedIntent.reservationHints ??=
                new List<ItemReservationClaimHintSaveData>();
            foreach (HaulDeliveryItemCommitmentSaveData commitment in
                     intent.commitments.Where(value => value != null))
            {
                bool alreadyProjected = savedIntent.reservationHints.Any(hint =>
                    hint != null
                    && hint.purpose == ItemReservationPurpose.Hauling
                    && hint.quantity == commitment.quantity
                    && string.Equals(
                        hint.preferredPhysicalStackId,
                        commitment.carriedStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        hint.expectedStackSignature,
                        commitment.expectedStackSignature,
                        StringComparison.Ordinal));
                if (alreadyProjected)
                {
                    continue;
                }

                // The committed carried lot is the physical ownership
                // authority. Its scheduling TTL may expire while the carrier
                // waits to replan, but save/restore must preserve that exact
                // destination ownership instead of orphaning the cargo.
                savedIntent.reservationHints.Add(
                    new ItemReservationClaimHintSaveData
                    {
                        originStackId = string.IsNullOrWhiteSpace(
                                commitment.sourceStackId)
                            ? commitment.carriedStackId
                            : commitment.sourceStackId,
                        preferredPhysicalStackId = commitment.carriedStackId,
                        itemId = commitment.itemId,
                        expectedStackSignature =
                            commitment.expectedStackSignature,
                        quantity = commitment.quantity,
                        purpose = ItemReservationPurpose.Hauling,
                        aggregationCohortId =
                            $"haul:{intent.destinationKind}:{intent.destinationId}"
                    });
            }
            savedIntent.reservationHints = savedIntent.reservationHints
                .Where(hint => hint != null)
                .OrderBy(hint => hint.preferredPhysicalStackId,
                    StringComparer.Ordinal)
                .ThenBy(hint => hint.expectedStackSignature,
                    StringComparer.Ordinal)
                .ThenBy(hint => hint.quantity)
                .ToList();
            for (int index = 0;
                 index < savedIntent.reservationHints.Count;
                 index++)
            {
                savedIntent.reservationHints[index].claimOrdinal = index;
                savedIntent.reservationHints[index].claimHintId =
                    $"claim:{savedIntent.ownerOperationId}:{index}";
            }
            ItemReservationClaimHintSaveData[] haulingHints = savedIntent?
                .reservationHints?
                .Where(hint => hint != null
                    && hint.purpose == ItemReservationPurpose.Hauling)
                .ToArray() ?? Array.Empty<ItemReservationClaimHintSaveData>();
            bool exact = haulingHints.Length == intent.commitments.Count
                && intent.commitments.All(commitment => commitment != null
                    && haulingHints.Count(hint =>
                        hint.quantity == commitment.quantity
                        && string.Equals(
                            hint.preferredPhysicalStackId,
                            commitment.carriedStackId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            hint.expectedStackSignature,
                            commitment.expectedStackSignature,
                            StringComparison.Ordinal)) == 1);
            if (!exact)
            {
                string commitmentProjection = string.Join(
                    ",",
                    (intent.commitments
                        ?? new List<HaulDeliveryItemCommitmentSaveData>())
                    .Where(commitment => commitment != null)
                    .Select(commitment =>
                        commitment.carriedStackId + ":"
                        + commitment.expectedStackSignature + ":"
                        + commitment.quantity));
                string hintProjection = string.Join(
                    ",",
                    haulingHints.Select(hint =>
                        hint.preferredPhysicalStackId + ":"
                        + hint.expectedStackSignature + ":"
                        + hint.quantity));
                throw new InvalidOperationException(
                    $"Committed haul delivery '{intent.operationId}' does not have a one-to-one saved quantity lease projection. "
                    + $"commitments=[{commitmentProjection}]; hints=[{hintProjection}]");
            }
        }
        return captured
            .OrderBy(intent => intent.ownerOperationId, StringComparer.Ordinal)
            .ToList();
    }

    internal WorldItemRestoreState StageRestore(DungeonPhysicalItemSaveData snapshot)
    {
        return StageRestore(snapshot, null, null);
    }

    internal WorldItemRestoreState StageRestore(
        DungeonPhysicalItemSaveData snapshot,
        IReadOnlyList<BuildableObject> candidateBuildings,
        IPhysicalItemMassQuery massQuery)
    {
        DungeonGameRestoreReport validation = new DungeonGameRestoreReport();
        PhysicalItemSaveValidation.Validate(
            snapshot,
            validation,
            catalogProvider);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                "Physical item restore validation failed: "
                + string.Join(" | ", validation.Errors));
        }

        Dictionary<string, CombatEquipmentInstance> equipment =
            new(StringComparer.Ordinal);
        Dictionary<string, EquipmentModuleInstance> modules =
            new(StringComparer.Ordinal);
        DecodeUniqueItems(snapshot.uniqueItems, equipment, modules);

        List<WorldItemStackRecord> records = new();
        HashSet<string> stackIds = new(StringComparer.Ordinal);
        HashSet<string> itemInstanceIds = new(StringComparer.Ordinal);
        foreach (WorldItemStackSaveData entry in snapshot.stacks)
        {
            ItemStackId stackId = (ItemStackId)entry.stackId;
            if (!stackId.IsValid || !stackIds.Add(stackId.Value))
            {
                throw new InvalidOperationException(
                    $"Physical item stack has invalid or duplicate persistent ID '{entry.stackId}'.");
            }

            DungeonItemDefinition definition =
                catalogProvider.GetDefinition(entry.itemId);
            ItemInstanceId itemInstanceId = (ItemInstanceId)entry.itemInstanceId;
            if (definition.MaxStack == 1 && !itemInstanceId.IsValid)
            {
                throw new InvalidOperationException(
                    $"Unique item stack '{stackId.Value}' has no valid item-instance ID.");
            }
            if (itemInstanceId.IsValid
                && !itemInstanceIds.Add(itemInstanceId.Value))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical item-instance ID '{itemInstanceId.Value}'.");
            }
            if (PhysicalItemIds.TryGetEquipmentDefinitionId(
                    entry.itemId,
                    out string equipmentDefinitionId)
                && (!equipment.TryGetValue(
                        itemInstanceId.Value,
                        out CombatEquipmentInstance equipmentInstance)
                    || !string.Equals(
                        equipmentInstance.definitionId,
                        equipmentDefinitionId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        equipmentInstance.sourceStackId,
                        stackId.Value,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Equipment stack '{stackId.Value}' does not reference its authoritative item instance.");
            }
            if (PhysicalItemIds.IsEquipmentModule(entry.itemId)
                && (!modules.TryGetValue(
                        itemInstanceId.Value,
                        out EquipmentModuleInstance moduleInstance)
                    || !string.Equals(
                        moduleInstance.sourceStackId,
                        stackId.Value,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Equipment-module stack '{stackId.Value}' does not reference its authoritative item instance.");
            }

            WorldItemStackRecord record = new WorldItemStackRecord
            {
                stackId = stackId.Value,
                itemInstanceId = itemInstanceId.IsValid
                    ? itemInstanceId.Value
                    : string.Empty,
                itemId = entry.itemId,
                quantity = entry.quantity,
                state = entry.state,
                position = new Vector2Int(entry.gridX, entry.gridY),
                reservedByPersistentId = string.Empty,
                destinationId = entry.destinationId,
                aggregationCohortId = entry.aggregationCohortId,
                sourceStorageDestinationId = entry.sourceStorageDestinationId,
                hasDestinationPosition = entry.hasDestinationPosition,
                destinationPosition = new Vector2Int(
                    entry.destinationGridX,
                    entry.destinationGridY),
                forbidden = entry.forbidden,
                sourceCharacterId = entry.sourceCharacterId,
                sourceDisplayName = entry.sourceDisplayName,
                sourceSpeciesTag = entry.sourceSpeciesTag,
                sourceDeathReason = entry.sourceDeathReason,
                emergencyButcheryAllowed = entry.emergencyButcheryAllowed,
                wasteOrigin = entry.wasteOrigin,
                contamination = entry.contamination,
                components = CloneComponents(entry.components),
                dropDisposition = entry.dropDisposition,
                recoveryOwnerOperationId = entry.recoveryOwnerOperationId,
                recoverySourceStackId = entry.recoverySourceStackId,
                recoveryCarrierPersistentId = entry.recoveryCarrierPersistentId,
                recoveryInterruptionKind = entry.recoveryInterruptionKind,
                droppedAtGameTime = entry.droppedAtGameTime,
                recoveryDeadlineGameTime = entry.recoveryDeadlineGameTime
            };
            if (record.state == WorldItemStackState.Stored)
            {
                ValidateWarehouseStorageKey(record.destinationId);
                ValidateWarehouseStorageKey(record.sourceStorageDestinationId);
            }
            records.Add(record);
        }

        HashSet<string> stackedUniqueIds = records
            .Where(record => !string.IsNullOrWhiteSpace(record.itemInstanceId))
            .Select(record => record.itemInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CombatEquipmentInstance instance in equipment.Values)
        {
            bool expectsStack = instance.worldState is CombatEquipmentWorldState.Stored
                or CombatEquipmentWorldState.Loose
                or CombatEquipmentWorldState.Carried
                or CombatEquipmentWorldState.MaintenanceBuffer;
            if (expectsStack
                && (!stackedUniqueIds.Contains(instance.instanceId)
                    || string.IsNullOrWhiteSpace(instance.sourceStackId)))
            {
                throw new InvalidOperationException(
                    $"Physical equipment '{instance.instanceId}' is missing its stack reference.");
            }
        }
        foreach (EquipmentModuleInstance module in modules.Values)
        {
            if (module == null
                || !string.IsNullOrWhiteSpace(
                    module.attachedEquipmentInstanceId))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(module.sourceStackId)
                || !stackedUniqueIds.Contains(module.instanceId)
                || !records.Any(record => record != null
                    && string.Equals(
                        record.stackId,
                        module.sourceStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        record.itemInstanceId,
                        module.instanceId,
                        StringComparison.Ordinal)
                    && PhysicalItemIds.IsEquipmentModule(record.itemId)))
            {
                throw new InvalidOperationException(
                    $"Physical equipment module '{module.instanceId}' is missing its authoritative stack reference.");
            }
        }

        WarehousePhysicalRestoreAssessment warehouseAssessment =
            WarehousePhysicalRestoreAssessment.Empty;
        if (candidateBuildings != null)
        {
            warehouseAssessment = WarehousePhysicalRestoreValidation.Validate(
                records,
                candidateBuildings,
                catalogProvider,
                massQuery ?? throw new ArgumentNullException(nameof(massQuery)));
        }

        return new WorldItemRestoreState
        {
            HaulingSettings = new ItemHaulingSettingsSnapshot
            {
                maxCarryMultiplier = snapshot.haulingSettings.maxCarryMultiplier
            },
            RepositoryState = repository.CreateDetachedState(
                records,
                equipment,
                modules,
                snapshot.nextHaulOperationSequence,
                warehouseAssessment.OverCapacityWarehouseIds,
                snapshot.pendingBatchDispositions,
                snapshot.pendingProductionCustodyDrains,
                snapshot.pendingProductionInputDestinationDrains,
                snapshot.pendingCapacityRoutingDrains),
            WarehouseAssessment = warehouseAssessment,
            ReservationIntents = CloneReservationIntents(
                snapshot.reservationIntents
                    ?? new List<ItemReservationIntentSaveData>()),
            ExactRouteCandidate = exactRouteOutboxPersistence
                .BuildRestoreCandidate(
                    snapshot.pendingExactOutputRoutes,
                    snapshot.stacks,
                    snapshot.lastConfirmedExactRouteCheckpointSequence,
                    snapshot.lastConfirmedExactRouteCheckpointDigest)
        };
    }

    internal void Commit(WorldItemRestoreState staged)
    {
        using IDisposable restoreBarrier = mutationGate?.EnterRestoreBarrier();
        WorldItemRestoreState required = staged
            ?? throw new ArgumentNullException(nameof(staged));
        haulingSettings.Restore(required.HaulingSettings);
        repository.ReplaceState(required.RepositoryState);
        exactRouteOutboxPersistence.RestoreCandidate(
            required.ExactRouteCandidate
            ?? throw new InvalidOperationException(
                "Physical restore has no exact-output-route candidate."));
        if (reservationPersistence != null)
        {
            if (!reservationPersistence.TryRestoreGrandfathered(
                    required.ReservationIntents,
                    out DomainFailure failure))
            {
                throw new InvalidOperationException(
                    $"Reservation grandfather restore failed: {failure}");
            }
        }
        else if (required.ReservationIntents.Count > 0)
        {
            throw new InvalidOperationException(
                "Reservation grandfather restore service is unavailable.");
        }
    }

    private static void DecodeUniqueItems(
        IEnumerable<UniqueItemInstanceSaveData> savedItems,
        IDictionary<string, CombatEquipmentInstance> equipment,
        IDictionary<string, EquipmentModuleInstance> modules)
    {
        foreach (UniqueItemInstanceSaveData unique in savedItems
                     ?? Array.Empty<UniqueItemInstanceSaveData>())
        {
            if (unique != null
                && PhysicalItemIds.IsEquipmentModule(unique.definitionId))
            {
                string moduleDecodeError = "missing equipment-module state";
                ItemInstanceComponentSaveData moduleComponent =
                    unique.components?.FirstOrDefault(candidate =>
                        candidate != null
                        && candidate.componentTypeId
                            == ItemInstanceComponentIds.EquipmentModule);
                if (!((ItemInstanceId)unique.itemInstanceId).IsValid
                    || moduleComponent == null
                    || !EquipmentModuleItemStateCodec.TryDecode(
                        moduleComponent,
                        out EquipmentModuleInstance module,
                        out moduleDecodeError)
                    || !string.Equals(
                        module.instanceId,
                        unique.itemInstanceId,
                        StringComparison.Ordinal)
                    || modules.ContainsKey(unique.itemInstanceId))
                {
                    throw new InvalidOperationException(
                        $"Invalid physical equipment module '{unique?.itemInstanceId}': {moduleDecodeError}");
                }

                modules.Add(unique.itemInstanceId, module.Clone());
                continue;
            }

            string decodeError = "missing equipment state";
            EquipmentPhysicalStatePayload payload = null;
            ItemInstanceComponentSaveData component = unique?.components?
                .FirstOrDefault(candidate => candidate != null
                    && candidate.componentTypeId
                        == ItemInstanceComponentIds.Equipment);
            if (unique == null
                || !((ItemInstanceId)unique.itemInstanceId).IsValid
                || component == null
                || !EquipmentItemStateCodec.TryDecodeFull(
                    component,
                    out payload,
                    out decodeError)
                || !string.Equals(
                    payload.equipment.instanceId,
                    unique.itemInstanceId,
                    StringComparison.Ordinal)
                || equipment.ContainsKey(unique.itemInstanceId))
            {
                throw new InvalidOperationException(
                    $"Invalid physical unique item '{unique?.itemInstanceId}': {decodeError}");
            }
            equipment.Add(unique.itemInstanceId, payload.equipment.Clone());
            foreach (EquipmentModuleInstance module in payload.attachedModules)
            {
                if (module == null
                    || string.IsNullOrWhiteSpace(module.instanceId)
                    || modules.ContainsKey(module.instanceId))
                {
                    throw new InvalidOperationException(
                        $"Invalid or duplicate physical equipment module '{module?.instanceId}'.");
                }
                modules.Add(module.instanceId, module.Clone());
            }
        }
    }

    internal static WorldItemStackSaveData CaptureDurableStack(
        WorldItemStackRecord stack)
    {
        bool directPickup = !string.IsNullOrWhiteSpace(
                stack.reservedByPersistentId)
            && IsCombatLoadoutDestination(stack.destinationId);
        string sourceStorage = stack.sourceStorageDestinationId?.Trim()
            ?? string.Empty;
        WorldItemStackState durableState = directPickup
            ? sourceStorage.Length > 0
                ? WorldItemStackState.Stored
                : WorldItemStackState.Loose
            : stack.state;
        string durableDestination = directPickup
            ? sourceStorage
            : stack.destinationId?.Trim() ?? string.Empty;
        bool hasDestinationPosition = !directPickup
            && stack.hasDestinationPosition;
        Vector2Int destinationPosition = hasDestinationPosition
            ? stack.destinationPosition
            : default;
        return new WorldItemStackSaveData
        {
            stackId = stack.stackId,
            itemInstanceId = stack.itemInstanceId,
            itemId = stack.itemId,
            quantity = stack.quantity,
            state = durableState,
            gridX = stack.position.x,
            gridY = stack.position.y,
            reservedByPersistentId = string.Empty,
            destinationId = durableDestination,
            aggregationCohortId = stack.aggregationCohortId?.Trim() ?? string.Empty,
            sourceStorageDestinationId = directPickup
                ? string.Empty
                : stack.sourceStorageDestinationId?.Trim() ?? string.Empty,
            hasDestinationPosition = hasDestinationPosition,
            destinationGridX = destinationPosition.x,
            destinationGridY = destinationPosition.y,
            forbidden = stack.forbidden,
            sourceCharacterId = stack.sourceCharacterId?.Trim() ?? string.Empty,
            sourceDisplayName = stack.sourceDisplayName?.Trim() ?? string.Empty,
            sourceSpeciesTag = stack.sourceSpeciesTag?.Trim() ?? string.Empty,
            sourceDeathReason = stack.sourceDeathReason?.Trim() ?? string.Empty,
            emergencyButcheryAllowed = stack.emergencyButcheryAllowed,
            wasteOrigin = stack.wasteOrigin,
            contamination = stack.contamination,
            components = CloneComponents(stack.components),
            dropDisposition = stack.dropDisposition,
            recoveryOwnerOperationId = stack.recoveryOwnerOperationId?.Trim()
                ?? string.Empty,
            recoverySourceStackId = stack.recoverySourceStackId?.Trim()
                ?? string.Empty,
            recoveryCarrierPersistentId = stack.recoveryCarrierPersistentId?.Trim()
                ?? string.Empty,
            recoveryInterruptionKind = stack.recoveryInterruptionKind,
            droppedAtGameTime = stack.droppedAtGameTime,
            recoveryDeadlineGameTime = stack.recoveryDeadlineGameTime
        };
    }

    private static List<ItemInstanceComponentSaveData> CloneComponents(
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        return components
            .Select(component => new ItemInstanceComponentSaveData
            {
                componentTypeId = component.componentTypeId,
                schemaVersion = component.schemaVersion,
                affectsStacking = component.affectsStacking,
                values = component.values
                    .Select(value => new ItemStateValueSaveData
                    {
                        key = value.key,
                        kind = value.kind,
                        stringValue = value.stringValue,
                        integerValue = value.integerValue,
                        decimalValue = value.decimalValue,
                        booleanValue = value.booleanValue
                    })
                    .ToList()
            })
            .ToList();
    }

    private static List<ItemReservationIntentSaveData> CloneReservationIntents(
        IEnumerable<ItemReservationIntentSaveData> intents)
    {
        return (intents ?? Array.Empty<ItemReservationIntentSaveData>())
            .Where(intent => intent != null)
            .OrderBy(intent => intent.ownerOperationId, StringComparer.Ordinal)
            .Select(intent => new ItemReservationIntentSaveData
            {
                ownerOperationId = intent.ownerOperationId?.Trim() ?? string.Empty,
                ownerCharacterId = intent.ownerCharacterId?.Trim() ?? string.Empty,
                hadActiveItemReservation = intent.hadActiveItemReservation,
                reservationHints = (intent.reservationHints
                        ?? new List<ItemReservationClaimHintSaveData>())
                    .Where(hint => hint != null)
                    .OrderBy(hint => hint.claimOrdinal)
                    .Select(hint => new ItemReservationClaimHintSaveData
                    {
                        claimHintId = hint.claimHintId?.Trim() ?? string.Empty,
                        originStackId = hint.originStackId?.Trim() ?? string.Empty,
                        preferredPhysicalStackId =
                            hint.preferredPhysicalStackId?.Trim() ?? string.Empty,
                        itemId = hint.itemId?.Trim() ?? string.Empty,
                        expectedStackSignature =
                            hint.expectedStackSignature?.Trim() ?? string.Empty,
                        quantity = hint.quantity,
                        purpose = hint.purpose,
                        aggregationCohortId =
                            hint.aggregationCohortId?.Trim() ?? string.Empty,
                        claimOrdinal = hint.claimOrdinal
                    })
                    .ToList()
            })
            .ToList();
    }

    private static bool IsCombatLoadoutDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                StringComparison.Ordinal);
    }

    private static void ValidateWarehouseStorageKey(string destinationId)
    {
        string normalized = destinationId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal))
        {
            return;
        }
        string suffix = normalized.Substring(
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix.Length);
        if (!suffix.StartsWith("building:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Legacy warehouse storage key '{normalized}' cannot be restored in V18.");
        }
    }

}
