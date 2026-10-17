using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>Validates detached physical, character and building identities.</summary>
internal sealed class DungeonPhysicalSaveReferenceValidation
{
    private readonly DungeonSaveReferenceRules references;
    private readonly IBuildingDefinitionLookup buildingDefinitions;
    private readonly ICombatEquipmentCatalog combatEquipmentDefinitions;

    internal DungeonPhysicalSaveReferenceValidation(DungeonSaveReferenceRules references, IBuildingDefinitionLookup buildingDefinitions, ICombatEquipmentCatalog combatEquipmentDefinitions)
    {
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.buildingDefinitions = buildingDefinitions ?? throw new ArgumentNullException(nameof(buildingDefinitions));
        this.combatEquipmentDefinitions = combatEquipmentDefinitions ?? throw new ArgumentNullException(nameof(combatEquipmentDefinitions));
    }

    internal DungeonSavePhysicalReferenceIndex ValidatePhysicalItems(
        DungeonPhysicalItemSaveData source,
        DungeonGameRestoreReport report)
    {
        DungeonSavePhysicalReferenceIndex result = new DungeonSavePhysicalReferenceIndex();
        foreach (WorldItemStackSaveData stack in source?.stacks
                     ?? new List<WorldItemStackSaveData>())
        {
            if (stack == null)
            {
                report.AddError("Physical item aggregate contains a null stack.");
                continue;
            }

            RequireUniqueId(stack.stackId, "item stack", result.StackIds, report);
            if (!string.IsNullOrWhiteSpace(stack.stackId))
                result.StacksById.TryAdd(stack.stackId, stack);
            references.RequireItemDefinition(stack.itemId, report);
            if (!string.IsNullOrWhiteSpace(stack.itemInstanceId))
            {
                RequireUniqueId(
                    stack.itemInstanceId,
                    "item instance",
                    result.ItemInstanceIds,
                    report);
            }

            if (stack.quantity <= 0)
            {
                report.AddError($"Item stack '{stack.stackId}' has non-positive quantity {stack.quantity}.");
            }

            CollectSeedLot(stack.components, stack.itemId, result, report);
            CollectProductionOutputCommit(stack, result, report);
        }

        foreach (UniqueItemInstanceSaveData unique in source?.uniqueItems
                     ?? new List<UniqueItemInstanceSaveData>())
        {
            if (unique == null)
            {
                report.AddError("Physical item aggregate contains a null unique item.");
                continue;
            }

            // A physical stack and its unique-item component payload are two
            // projections of the same item identity. Reject duplicates within
            // the unique-item collection, but merge that identity into the
            // aggregate reference index instead of treating the matching stack
            // projection as a second item.
            RequireUniqueId(
                unique.itemInstanceId,
                "unique item instance",
                result.UniqueItemInstanceIds,
                report);
            string uniqueItemInstanceId = unique.itemInstanceId?.Trim()
                ?? string.Empty;
            if (uniqueItemInstanceId.Length > 0)
            {
                result.ItemInstanceIds.Add(uniqueItemInstanceId);
                result.UniqueItems.TryAdd(
                    uniqueItemInstanceId,
                    unique);
            }
            references.RequireItemDefinition(unique.definitionId, report);
            CollectSeedLot(unique.components, unique.definitionId, result, report);
            ItemInstanceComponentSaveData equipmentComponent = unique.components?
                .FirstOrDefault(component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        ItemInstanceComponentIds.Equipment,
                        StringComparison.Ordinal));
            if (equipmentComponent != null
                && EquipmentItemStateCodec.TryDecode(
                    equipmentComponent,
                    out CombatEquipmentInstance equipment,
                    out _))
            {
                if (string.IsNullOrEmpty(equipment.instanceId)
                    || !string.Equals(
                        equipment.instanceId,
                        unique.itemInstanceId,
                        StringComparison.Ordinal)
                    || !result.EquipmentInstanceIds.Add(equipment.instanceId)
                    || !result.EquipmentDefinitionIds.TryAdd(
                        equipment.instanceId,
                        equipment.definitionId))
                {
                    report.AddError(
                        $"Physical equipment component '{equipment.instanceId}' does not uniquely match owning item instance '{unique.itemInstanceId}'.");
                }
                else if (!combatEquipmentDefinitions.TryGet(
                             equipment.definitionId,
                             out _))
                {
                    report.AddError(
                        $"Physical equipment '{equipment.instanceId}' references unknown equipment definition '{equipment.definitionId}'.");
                }
                else
                {
                    result.EquipmentInstances[equipment.instanceId] =
                        equipment.Clone();
                }
            }
        }

        foreach (ItemReservationIntentSaveData reservation in
                 source?.reservationIntents
                 ?? new List<ItemReservationIntentSaveData>())
        {
            if (reservation != null
                && reservation.hadActiveItemReservation
                && !string.IsNullOrWhiteSpace(reservation.ownerOperationId))
            {
                result.ReservationOwnerOperationIds.Add(
                    reservation.ownerOperationId);
            }
        }

        foreach (PhysicalItemBatchDispositionSaveData pending in
                 source?.pendingBatchDispositions
                 ?? new List<PhysicalItemBatchDispositionSaveData>())
        {
            if (pending == null)
            {
                report.AddError(
                    "Physical item aggregate contains a null pending batch disposition.");
                continue;
            }
            if (!result.PendingBatchByOperation.TryAdd(
                    pending.operationId ?? string.Empty,
                    pending)
                || !result.PendingBatchCommitIds.Add(
                    pending.commitId ?? string.Empty))
            {
                report.AddError(
                    $"Physical pending batch disposition '{pending.operationId}' has duplicate operation or commit identity.");
            }
        }

        return result;
    }

    internal static void CollectProductionOutputCommit(
        WorldItemStackSaveData stack,
        DungeonSavePhysicalReferenceIndex index,
        DungeonGameRestoreReport report)
    {
        ItemInstanceComponentSaveData component = (stack?.components
                ?? new List<ItemInstanceComponentSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.ProductionOutputCommit,
                    StringComparison.Ordinal));
        if (component == null) return;
        ItemStateValueSaveData field = component.values?.SingleOrDefault(value =>
            value != null
            && string.Equals(value.key, "commit-id", StringComparison.Ordinal)
            && value.kind == ItemStateValueKind.String);
        string commitId = field?.stringValue ?? string.Empty;
        if (component.schemaVersion != 1
            || !string.Equals(commitId, commitId.Trim(), StringComparison.Ordinal)
            || commitId.Length == 0
            || !index.OutputByCommitId.TryAdd(commitId, stack))
            report.AddError(
                $"Physical production output commit '{commitId}' is invalid or duplicated.");
    }

    internal static void CollectSeedLot(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        string itemDefinitionId,
        DungeonSavePhysicalReferenceIndex index,
        DungeonGameRestoreReport report)
    {
        if (!(components ?? Array.Empty<ItemInstanceComponentSaveData>()).Any(
                component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        ItemInstanceComponentIds.SeedLot,
                        StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            SeedLotState seedLot = SeedLotItemStateCodec.Decode(components);
            index.SeedLots.Add((itemDefinitionId?.Trim() ?? string.Empty, seedLot));
        }
        catch (Exception exception)
        {
            report.AddError(
                $"Physical seed lot '{itemDefinitionId}' is invalid: {exception.Message}");
        }
    }

    internal DungeonSaveCharacterReferenceIndex ValidateCharacters(
        DungeonCharacterWorldSaveData source,
        DungeonGameRestoreReport report)
    {
        DungeonSaveCharacterReferenceIndex result = new();
        foreach (DungeonCharacterSaveData actor in source?.actors
                     ?? new List<DungeonCharacterSaveData>())
        {
            if (actor == null)
            {
                report.AddError("Character aggregate contains a null actor.");
                continue;
            }

            if (RequireUniqueCharacterId(
                actor.persistentId,
                "character",
                result.KnownCharacterIds,
                report,
                rejectDuplicate: true,
                out string actorId))
            {
                result.ActorIds.Add(actorId);
                if (actor.lifecycleState != CharacterLifecycleState.Despawned
                    && actor.currentHealth > 0f)
                {
                    result.LivingCharacterIds.Add(actorId);
                }
            }
            foreach (CharacterCarriedItemSaveData carried in actor.carryInventory?.items
                         ?? new List<CharacterCarriedItemSaveData>())
            {
                if (carried != null)
                {
                    references.RequireItemDefinition(carried.itemId, report);
                }
            }
        }

        foreach (WorldCharacterProfile profile in source?.populationProfiles
                     ?? new List<WorldCharacterProfile>())
        {
            if (RequireUniqueCharacterId(
                profile?.persistentId,
                "character population profile",
                result.KnownCharacterIds,
                report,
                rejectDuplicate: false,
                out string profileId)
                && profile.isAlive)
            {
                result.LivingCharacterIds.Add(profileId);
            }
        }

        return result;
    }

    internal DungeonSaveBuildingReferenceIndex ValidateBuildings(
        ModularFacilityWorldSaveData source,
        DungeonGameRestoreReport report)
    {
        DungeonSaveBuildingReferenceIndex result = new DungeonSaveBuildingReferenceIndex();
        foreach (ModularFacilityBuildingSaveData building in source?.buildings
                     ?? new List<ModularFacilityBuildingSaveData>())
        {
            if (building == null)
            {
                report.AddError("Building aggregate contains a null building.");
                continue;
            }

            RequireUniqueId(
                building.persistentInstanceId,
                "building",
                result.InstanceIds,
                report);
            try
            {
                BuildingSO definition =
                    buildingDefinitions.GetBuilding(building.buildingId);
                result.InstanceDefinitions[building.persistentInstanceId] =
                    definition;
            }
            catch (Exception)
            {
                report.AddError(
                    $"Building '{building.persistentInstanceId}' references unknown definition {building.buildingId}.");
            }
        }

        return result;
    }
}
