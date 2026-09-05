using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pure raw-save validation for the destructive-drain journal. It is shared
/// by the whole-game preflight and the registry preflight, and never reads or
/// publishes live aggregate state.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainCrossAggregateSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator,
    IProductionFacilityDestructiveDrainCandidateValidator
{
    private readonly IBuildingDefinitionLookup buildingDefinitions;
    private readonly ProductionOutputBufferCapacityProjector capacityProjector;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly ProductionGenericBillTerminalDrainSaveValidation
        genericTerminalValidation;
    private readonly CombatEquipmentTerminalDrainSaveValidation
        combatTerminalValidation;
    private readonly ProductionApparelOrderTerminalDrainSaveValidation
        apparelTerminalValidation;

    public ProductionFacilityDestructiveDrainCrossAggregateSaveValidation(
        IBuildingDefinitionLookup buildingDefinitions,
        ProductionOutputBufferCapacityProjector capacityProjector,
        IPhysicalItemMassQuery massQuery,
        ProductionGenericBillTerminalDrainSaveValidation
            genericTerminalValidation,
        CombatEquipmentTerminalDrainSaveValidation combatTerminalValidation,
        ProductionApparelOrderTerminalDrainSaveValidation
            apparelTerminalValidation)
    {
        this.buildingDefinitions = buildingDefinitions
            ?? throw new ArgumentNullException(nameof(buildingDefinitions));
        this.capacityProjector = capacityProjector
            ?? throw new ArgumentNullException(nameof(capacityProjector));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.genericTerminalValidation = genericTerminalValidation
            ?? throw new ArgumentNullException(
                nameof(genericTerminalValidation));
        this.combatTerminalValidation = combatTerminalValidation
            ?? throw new ArgumentNullException(
                nameof(combatTerminalValidation));
        this.apparelTerminalValidation = apparelTerminalValidation
            ?? throw new ArgumentNullException(
                nameof(apparelTerminalValidation));
    }

    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        if (!DungeonSaveSectionPayload.TryRead(
                saveData,
                ProductionFacilityDestructiveDrainSaveSection.Id,
                out DungeonProductionFacilityDestructiveDrainSaveData drain))
        {
            bool hasPhysicalProducer = DungeonSaveSectionPayload.TryRead(
                    saveData,
                    PhysicalItemsSaveSection.Id,
                    out DungeonPhysicalItemSaveData orphanItems)
                && HasAnyDestructiveDrainProducer(orphanItems);
            bool hasGenericProducer = DungeonSaveSectionPayload.TryRead(
                    saveData,
                    ProductionGenericBillTerminalDrainSaveSection.Id,
                    out DungeonProductionGenericBillTerminalDrainSaveData
                        orphanGeneric)
                && HasAnyGenericTerminalProducer(orphanGeneric);
            bool hasCombatProducer = DungeonSaveSectionPayload.TryRead(
                    saveData,
                    CombatEquipmentTerminalDrainSaveSection.Id,
                    out DungeonCombatEquipmentTerminalDrainSaveData
                        orphanCombat)
                && HasAnyCombatTerminalProducer(orphanCombat);
            bool hasApparelProducer = DungeonSaveSectionPayload.TryRead(
                    saveData,
                    ProductionApparelOrderTerminalDrainSaveSection.Id,
                    out DungeonProductionApparelOrderTerminalDrainSaveData
                        orphanApparel)
                && HasAnyApparelTerminalProducer(orphanApparel);
            bool hasWorkOrderOwner = DungeonSaveSectionPayload.TryRead(
                    saveData,
                    WorkOrdersSaveSection.Id,
                    out DungeonWorkOrderSaveData orphanWorkOrders)
                && HasAnyWorkOrderDestructiveDrainOwner(orphanWorkOrders);
            if (hasPhysicalProducer
                || hasGenericProducer
                || hasCombatProducer
                || hasApparelProducer
                || hasWorkOrderOwner)
            {
                report.AddError(
                    "Production destructive-drain producer exists without its journal section.");
            }
            return;
        }

        try
        {
            ModularFacilityWorldSaveData world =
                RequirePayload<ModularFacilityWorldSaveData>(
                    saveData,
                    ModularFacilityWorldSaveSection.Id);
            DungeonWorkOrderSaveData workOrders =
                RequirePayload<DungeonWorkOrderSaveData>(
                    saveData,
                    WorkOrdersSaveSection.Id);
            ValidateCore(
                world,
                RequirePayload<DungeonCharacterWorldSaveData>(
                    saveData,
                    CharacterWorldSaveSection.Id),
                RequirePayload<DungeonPhysicalItemSaveData>(
                    saveData,
                    PhysicalItemsSaveSection.Id),
                RequirePayload<DungeonProductionBillSaveData>(
                    saveData,
                    ProductionBillsSaveSection.Id),
                RequirePayload<ProductionPreparedOutputRoutingSaveData>(
                    saveData,
                    ProductionPreparedOutputRoutingSaveSection.Id),
                RequirePayload<DungeonCombatEquipmentSaveData>(
                    saveData,
                    CombatEquipmentSaveSection.Id),
                RequirePayload<CombatEquipmentMaintenanceSaveData>(
                    saveData,
                    EquipmentMaintenanceSaveSection.Id),
                RequirePayload<DungeonCharacterEnvironmentSaveData>(
                    saveData,
                    CharacterEnvironmentSaveSection.Id),
                RequirePayload<
                    DungeonProductionGenericBillTerminalDrainSaveData>(
                    saveData,
                    ProductionGenericBillTerminalDrainSaveSection.Id),
                RequirePayload<DungeonCombatEquipmentTerminalDrainSaveData>(
                    saveData,
                    CombatEquipmentTerminalDrainSaveSection.Id),
                RequirePayload<
                    DungeonProductionApparelOrderTerminalDrainSaveData>(
                    saveData,
                    ProductionApparelOrderTerminalDrainSaveSection.Id),
                drain);
            ValidateWorkOrderDestructiveDrainJoins(
                workOrders,
                world,
                drain);
        }
        catch (Exception exception)
        {
            report.AddError(
                "Production destructive-drain cross-aggregate preflight failed: "
                + exception.Message);
        }
    }

    public void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (envelopes == null)
            throw new ArgumentNullException(nameof(envelopes));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        if (!envelopes.TryGetValue(
                ProductionFacilityDestructiveDrainSaveSection.Id,
                out DungeonSaveSectionEnvelope drainEnvelope))
        {
            bool hasPhysicalProducer = envelopes.TryGetValue(
                    PhysicalItemsSaveSection.Id,
                    out DungeonSaveSectionEnvelope physicalEnvelope)
                && HasAnyDestructiveDrainProducer(
                    Parse<DungeonPhysicalItemSaveData>(
                        physicalEnvelope,
                        PhysicalItemsSaveSection.Id));
            bool hasGenericProducer = envelopes.TryGetValue(
                    ProductionGenericBillTerminalDrainSaveSection.Id,
                    out DungeonSaveSectionEnvelope genericEnvelope)
                && HasAnyGenericTerminalProducer(
                    Parse<DungeonProductionGenericBillTerminalDrainSaveData>(
                        genericEnvelope,
                        ProductionGenericBillTerminalDrainSaveSection.Id));
            bool hasCombatProducer = envelopes.TryGetValue(
                    CombatEquipmentTerminalDrainSaveSection.Id,
                    out DungeonSaveSectionEnvelope combatTerminalEnvelope)
                && HasAnyCombatTerminalProducer(
                    Parse<DungeonCombatEquipmentTerminalDrainSaveData>(
                        combatTerminalEnvelope,
                        CombatEquipmentTerminalDrainSaveSection.Id));
            bool hasApparelProducer = envelopes.TryGetValue(
                    ProductionApparelOrderTerminalDrainSaveSection.Id,
                    out DungeonSaveSectionEnvelope apparelTerminalEnvelope)
                && HasAnyApparelTerminalProducer(
                    Parse<DungeonProductionApparelOrderTerminalDrainSaveData>(
                        apparelTerminalEnvelope,
                        ProductionApparelOrderTerminalDrainSaveSection.Id));
            bool hasWorkOrderOwner = envelopes.TryGetValue(
                    WorkOrdersSaveSection.Id,
                    out DungeonSaveSectionEnvelope workOrdersEnvelope)
                && HasAnyWorkOrderDestructiveDrainOwner(
                    Parse<DungeonWorkOrderSaveData>(
                        workOrdersEnvelope,
                        WorkOrdersSaveSection.Id));
            if (hasPhysicalProducer
                || hasGenericProducer
                || hasCombatProducer
                || hasApparelProducer
                || hasWorkOrderOwner)
            {
                report.AddError(
                    "Production destructive-drain producer exists without its registry journal section.");
            }
            return;
        }

        try
        {
            ModularFacilityWorldSaveData world =
                RequirePayload<ModularFacilityWorldSaveData>(
                    envelopes,
                    ModularFacilityWorldSaveSection.Id);
            DungeonWorkOrderSaveData workOrders =
                RequirePayload<DungeonWorkOrderSaveData>(
                    envelopes,
                    WorkOrdersSaveSection.Id);
            DungeonProductionFacilityDestructiveDrainSaveData drain =
                Parse<DungeonProductionFacilityDestructiveDrainSaveData>(
                    drainEnvelope,
                    ProductionFacilityDestructiveDrainSaveSection.Id);
            ValidateCore(
                world,
                RequirePayload<DungeonCharacterWorldSaveData>(
                    envelopes,
                    CharacterWorldSaveSection.Id),
                RequirePayload<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id),
                RequirePayload<DungeonProductionBillSaveData>(
                    envelopes,
                    ProductionBillsSaveSection.Id),
                RequirePayload<ProductionPreparedOutputRoutingSaveData>(
                    envelopes,
                    ProductionPreparedOutputRoutingSaveSection.Id),
                RequirePayload<DungeonCombatEquipmentSaveData>(
                    envelopes,
                    CombatEquipmentSaveSection.Id),
                RequirePayload<CombatEquipmentMaintenanceSaveData>(
                    envelopes,
                    EquipmentMaintenanceSaveSection.Id),
                RequirePayload<DungeonCharacterEnvironmentSaveData>(
                    envelopes,
                    CharacterEnvironmentSaveSection.Id),
                RequirePayload<
                    DungeonProductionGenericBillTerminalDrainSaveData>(
                    envelopes,
                    ProductionGenericBillTerminalDrainSaveSection.Id),
                RequirePayload<DungeonCombatEquipmentTerminalDrainSaveData>(
                    envelopes,
                    CombatEquipmentTerminalDrainSaveSection.Id),
                RequirePayload<
                    DungeonProductionApparelOrderTerminalDrainSaveData>(
                    envelopes,
                    ProductionApparelOrderTerminalDrainSaveSection.Id),
                drain);
            ValidateWorkOrderDestructiveDrainJoins(
                workOrders,
                world,
                drain);
        }
        catch (Exception exception)
        {
            report.AddError(
                "Production destructive-drain registry preflight failed: "
                + exception.Message);
        }
    }

    public void Validate(
        ProductionOutputLifecycleRestoreCandidateBundle bundle,
        DungeonProductionGenericBillTerminalDrainSaveData genericTerminalDrains,
        DungeonCombatEquipmentTerminalDrainSaveData combatTerminalDrains,
        DungeonProductionApparelOrderTerminalDrainSaveData apparelTerminalDrains,
        DungeonProductionFacilityDestructiveDrainSaveData drain)
    {
        if (bundle == null)
            throw new ArgumentNullException(nameof(bundle));
        ValidateCore(
            bundle.World,
            bundle.Characters,
            bundle.PhysicalItems,
            bundle.Production,
            bundle.Routing,
            bundle.Combat,
            bundle.Maintenance,
            bundle.Environment,
            genericTerminalDrains ?? throw new InvalidOperationException(
                "Production destructive-drain restore requires the generic terminal producer candidate."),
            combatTerminalDrains ?? throw new InvalidOperationException(
                "Production destructive-drain restore requires the combat terminal producer candidate."),
            apparelTerminalDrains ?? throw new InvalidOperationException(
                "Production destructive-drain restore requires the apparel terminal producer candidate."),
            drain ?? throw new ArgumentNullException(nameof(drain)));
    }

    private void ValidateCore(
        ModularFacilityWorldSaveData world,
        DungeonCharacterWorldSaveData characters,
        DungeonPhysicalItemSaveData items,
        DungeonProductionBillSaveData production,
        ProductionPreparedOutputRoutingSaveData routing,
        DungeonCombatEquipmentSaveData combat,
        CombatEquipmentMaintenanceSaveData maintenance,
        DungeonCharacterEnvironmentSaveData environment,
        DungeonProductionGenericBillTerminalDrainSaveData genericTerminalDrains,
        DungeonCombatEquipmentTerminalDrainSaveData combatTerminalDrains,
        DungeonProductionApparelOrderTerminalDrainSaveData apparelTerminalDrains,
        DungeonProductionFacilityDestructiveDrainSaveData drain)
    {
        if (drain?.entries == null
            || drain.version !=
                DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion
            || !string.Equals(
                drain.registryFingerprint,
                ProductionFacilityDestructiveDrainParticipantRegistry
                    .ExpectedRegistryFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Destructive-drain payload header does not match the exact current-format participant registry.");
        }

        if (items.pendingProductionCustodyDrains == null)
        {
            throw new InvalidOperationException(
                "Destructive-drain validation requires the current physical custody-drain producer collection.");
        }
        if (items.pendingCapacityRoutingDrains == null)
        {
            throw new InvalidOperationException(
                "Destructive-drain validation requires the current capacity-routing producer collection.");
        }
        if (items.pendingProductionInputDestinationDrains == null)
        {
            throw new InvalidOperationException(
                "Destructive-drain validation requires the current input-destination custody producer collection.");
        }
        genericTerminalValidation.ValidateOwnPayload(genericTerminalDrains);
        combatTerminalValidation.ValidateOwnPayload(combatTerminalDrains);
        apparelTerminalValidation.ValidateOwnPayload(apparelTerminalDrains);
        ValidateCapacityRoutingActorAuthorityDisjoint(
            items.pendingCapacityRoutingDrains,
            characters);
        HashSet<string> joinedPhysicalProducerSteps =
            new(StringComparer.Ordinal);
        HashSet<string> joinedCapacityProducerSteps =
            new(StringComparer.Ordinal);
        HashSet<string> joinedGenericProducerSteps =
            new(StringComparer.Ordinal);
        HashSet<string> joinedCombatProducerSteps =
            new(StringComparer.Ordinal);
        HashSet<string> joinedApparelProducerSteps =
            new(StringComparer.Ordinal);
        HashSet<string> joinedStockSensorChildSteps =
            new(StringComparer.Ordinal);
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in
                 drain.entries
                     .Where(value => value != null)
                     .OrderBy(value => value.facilityId, StringComparer.Ordinal))
        {
            BuildingInstanceId facilityId =
                (BuildingInstanceId)(entry.facilityId ?? string.Empty);
            if (!facilityId.IsValid)
                throw new InvalidOperationException(
                    "Destructive-drain entry has an invalid facility ID.");
            if (entry.phase == ProductionFacilityDestructiveDrainPhase.None)
                throw new InvalidOperationException(
                    "Destructive-drain entry has no active phase: "
                    + facilityId.Value);
            bool worldRemoved = entry.phase ==
                ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc;
            IReadOnlyDictionary<string, string> contributors = worldRemoved
                ? ProjectAbsentContributors(
                    facilityId,
                    world,
                    production,
                    combat,
                    maintenance,
                    environment,
                    items,
                    characters,
                    routing)
                : ProjectPresentContributors(
                    facilityId,
                    world,
                    production,
                    genericTerminalDrains,
                    combat,
                    maintenance,
                    environment,
                    items,
                    characters,
                    routing);
            ValidateParticipants(entry, contributors);
            ValidatePhysicalCustodyProducerJoin(
                entry,
                items.pendingProductionCustodyDrains,
                joinedPhysicalProducerSteps);
            ValidateCapacityRoutingProducerJoin(
                entry,
                items.pendingCapacityRoutingDrains,
                routing,
                items.pendingExactOutputRoutes,
                joinedCapacityProducerSteps);
            ValidateGenericTerminalProducerJoin(
                entry,
                genericTerminalDrains.entries,
                joinedGenericProducerSteps);
            ValidateCombatTerminalProducerJoin(
                entry,
                combatTerminalDrains.entries,
                joinedCombatProducerSteps);
            ValidateApparelTerminalProducerJoin(
                entry,
                apparelTerminalDrains.entries,
                joinedApparelProducerSteps);
            ValidateStockSensorCompositeProducerJoin(
                entry,
                production,
                items.pendingProductionInputDestinationDrains,
                joinedStockSensorChildSteps);
            if (entry.phase == ProductionFacilityDestructiveDrainPhase.Prepared)
            {
                ValidatePreparedOwnerBijection(
                    entry,
                    ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection
                        .Project(
                            facilityId,
                            production,
                            combat,
                            maintenance,
                            environment,
                            items,
                            characters,
                            routing));
            }
            else if (worldRemoved
                && entry.participants.Any(participant =>
                    participant?.owners != null
                    && participant.owners.Any(owner => owner == null
                        || owner.phase !=
                            ProductionFacilityDestructiveDrainStepPhase
                                .OwnerAcknowledged)))
            {
                throw new InvalidOperationException(
                    "World-removed destructive drain contains an unacknowledged owner.");
            }

            string projected = ProductionOutputDestinationDurableSaveProjector
                .ComposeAggregate(facilityId, contributors);
            if (worldRemoved)
            {
                string absentProjection =
                    ProductionOutputDestinationDurableSaveProjector
                        .ProjectAbsentFacilityAggregateFromSave(
                        facilityId,
                        world,
                        production,
                        combat,
                        maintenance,
                        environment,
                        items,
                        characters,
                        routing);
                if (!string.Equals(
                        projected,
                        absentProjection,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Destructive-drain absent contributor projection drifted from the aggregate authority.");
                }
            }
            if (!string.Equals(
                    projected,
                    entry.expectedCurrentLifecycleFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-lifecycle-fingerprint-mismatch: "
                    + facilityId.Value);
            }
            if (entry.phase == ProductionFacilityDestructiveDrainPhase.Prepared
                && !string.Equals(
                    entry.preparedLifecycleFingerprint,
                    entry.expectedCurrentLifecycleFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Prepared destructive drain does not preserve the captured lifecycle: "
                    + facilityId.Value);
            }
        }
        ProductionPhysicalCustodyDrainSaveData orphanPhysicalProducer =
            items.pendingProductionCustodyDrains.FirstOrDefault(value =>
                value != null
                && !joinedPhysicalProducerSteps.Contains(
                    value.stepOperationId ?? string.Empty));
        if (orphanPhysicalProducer != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-orphan: "
                + orphanPhysicalProducer.stepOperationId);
        }
        ProductionCapacityRoutingDrainSaveData orphanCapacityProducer =
            items.pendingCapacityRoutingDrains.FirstOrDefault(value =>
                value != null
                && !joinedCapacityProducerSteps.Contains(
                    value.stepOperationId ?? string.Empty));
        if (orphanCapacityProducer != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-capacity-producer-orphan: "
                + orphanCapacityProducer.stepOperationId);
        }
        ProductionGenericBillTerminalDrainSaveData orphanGenericProducer =
            genericTerminalDrains.entries.FirstOrDefault(value =>
                value != null
                && !joinedGenericProducerSteps.Contains(
                    value.stepOperationId ?? string.Empty));
        if (orphanGenericProducer != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-generic-producer-orphan: "
                + orphanGenericProducer.stepOperationId);
        }
        CombatEquipmentTerminalDrainSaveData orphanCombatProducer =
            combatTerminalDrains.entries.FirstOrDefault(value =>
                value != null
                && !joinedCombatProducerSteps.Contains(
                    value.stepOperationId ?? string.Empty));
        if (orphanCombatProducer != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-combat-producer-orphan: "
                + orphanCombatProducer.stepOperationId);
        }
        ProductionApparelOrderTerminalDrainSaveData orphanApparelProducer =
            apparelTerminalDrains.entries.FirstOrDefault(value =>
                value != null
                && !joinedApparelProducerSteps.Contains(
                    value.stepOperationId ?? string.Empty));
        if (orphanApparelProducer != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-apparel-producer-orphan: "
                + orphanApparelProducer.stepOperationId);
        }
        ProductionInputDestinationCustodyDrainSaveData orphanStockSensorChild =
            items.pendingProductionInputDestinationDrains.FirstOrDefault(value =>
                value != null
                && IsStockSensorChild(value)
                && !joinedStockSensorChildSteps.Contains(
                    value.stepOperationId ?? string.Empty));
        if (orphanStockSensorChild != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-orphan: "
                + orphanStockSensorChild.stepOperationId);
        }
    }

    private static bool HasAnyDestructiveDrainProducer(
        DungeonPhysicalItemSaveData items) =>
        items?.pendingProductionCustodyDrains?.Count > 0
        || items?.pendingCapacityRoutingDrains?.Count > 0
        || items?.pendingProductionInputDestinationDrains?.Count > 0;

    private static bool HasAnyGenericTerminalProducer(
        DungeonProductionGenericBillTerminalDrainSaveData payload) =>
        payload?.entries?.Count > 0;

    private static bool HasAnyCombatTerminalProducer(
        DungeonCombatEquipmentTerminalDrainSaveData payload) =>
        payload?.entries?.Count > 0;

    private static bool HasAnyApparelTerminalProducer(
        DungeonProductionApparelOrderTerminalDrainSaveData payload) =>
        payload?.entries?.Count > 0;

    private static bool HasAnyWorkOrderDestructiveDrainOwner(
        DungeonWorkOrderSaveData payload) =>
        (payload?.orders ?? new List<WorkOrderSaveData>()).Any(order =>
            order != null
            && (!string.IsNullOrEmpty(order.destructiveDrainOperationId)
                || order.facilityRemovedForRetry
                || order.cancelRebuildAfterDestructiveDrain));

    private void ValidateWorkOrderDestructiveDrainJoins(
        DungeonWorkOrderSaveData workOrders,
        ModularFacilityWorldSaveData world,
        DungeonProductionFacilityDestructiveDrainSaveData drain)
    {
        if (workOrders == null
            || workOrders.version != DungeonWorkOrderSaveData.CurrentVersion
            || workOrders.orders == null
            || workOrders.qualityPipelines == null
            || world?.buildings == null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-work-order-payload-invalid");
        }

        HashSet<string> joinedOperations = new(StringComparer.Ordinal);
        foreach (WorkOrderSaveData order in workOrders.orders
                     .Where(value => value != null)
                     .OrderBy(value => value.workOrderId, StringComparer.Ordinal))
        {
            string operationValue = order.destructiveDrainOperationId
                ?? string.Empty;
            bool hasOwnerState = operationValue.Length > 0
                || order.facilityRemovedForRetry
                || order.cancelRebuildAfterDestructiveDrain;
            if (!hasOwnerState)
                continue;
            if (!string.Equals(
                    order.workTypeId,
                    BuiltInWorkTypeIds.Dismantle.Value,
                    StringComparison.Ordinal)
                || !ProductionFacilityDestructiveDrainOperationId.TryParse(
                    operationValue,
                    out ProductionFacilityDestructiveDrainOperationId operation))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-owner-invalid: "
                    + (order.workOrderId ?? string.Empty));
            }
            if (!joinedOperations.Add(operation.Value))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-owner-duplicate: "
                    + operation.Value);
            }

            QualityTargetPipelineSaveData[] pipelineMatches =
                workOrders.qualityPipelines
                    .Where(pipeline => pipeline != null
                        && string.Equals(
                            pipeline.pipelineId,
                            order.qualityPipelineId,
                            StringComparison.Ordinal))
                    .Take(2)
                    .ToArray();
            if (string.IsNullOrEmpty(order.qualityPipelineId)
                || pipelineMatches.Length != 1)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-pipeline-cardinality: "
                    + (order.workOrderId ?? string.Empty));
            }
            QualityTargetPipelineSaveData pipeline = pipelineMatches[0];
            BuildingSO definition =
                buildingDefinitions.GetBuilding(order.targetBuildingId);
            string numericDefinitionId = order.targetBuildingId.ToString(
                CultureInfo.InvariantCulture);
            bool definitionMatches = definition != null
                && (string.Equals(
                        pipeline.definitionId,
                        definition.ContentDefinitionId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        pipeline.definitionId,
                        numericDefinitionId,
                        StringComparison.Ordinal));
            if (!definitionMatches
                || !pipeline.facilityPipeline
                || pipeline.footprintX != order.gridX
                || pipeline.footprintY != order.gridY
                || pipeline.footprintWidth !=
                    Math.Max(1, definition.Placement.Width)
                || pipeline.footprintHeight !=
                    Math.Max(1, definition.Placement.Height))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-pipeline-identity-mismatch: "
                    + (order.workOrderId ?? string.Empty));
            }
            if (order.cancelRebuildAfterDestructiveDrain
                && (pipeline.stage != QualityTargetPipelineStage.Cancelled
                    || order.status != (order.facilityRemovedForRetry
                        ? WorkOrderStatus.WaitingForOutputSpace
                        : WorkOrderStatus.Blocked)))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-cancel-state-mismatch: "
                    + (order.workOrderId ?? string.Empty));
            }

            ProductionFacilityDestructiveDrainEntrySaveData[] matches =
                (drain?.entries
                    ?? new List<
                        ProductionFacilityDestructiveDrainEntrySaveData>())
                .Where(entry => entry != null
                    && string.Equals(
                        entry.operationId,
                        operation.Value,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-journal-cardinality: "
                    + operation.Value);
            }

            ProductionFacilityDestructiveDrainEntrySaveData entry = matches[0];
            BuildingInstanceId facilityId =
                (BuildingInstanceId)(entry.facilityId ?? string.Empty);
            if (!facilityId.IsValid
                || !string.Equals(
                    ProductionFacilityDestructiveDrainOperationId
                        .FromFacility(facilityId).Value,
                    operation.Value,
                    StringComparison.Ordinal)
                || entry.cause !=
                    ProductionFacilityDestructiveDrainCause.ExplicitDemolition)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-journal-identity-mismatch: "
                    + operation.Value);
            }
            if (order.facilityRemovedForRetry
                && entry.phase != ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-terminal-phase-mismatch: "
                    + operation.Value);
            }
            if (entry.phase == ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc)
            {
                continue;
            }

            ModularFacilityBuildingSaveData[] worldMatches = world.buildings
                .Where(building => building != null
                    && string.Equals(
                        building.persistentInstanceId,
                        facilityId.Value,
                        StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (worldMatches.Length != 1
                || worldMatches[0].buildingId != order.targetBuildingId
                || worldMatches[0].centerX != order.gridX
                || worldMatches[0].centerY != order.gridY)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-world-target-mismatch: "
                    + operation.Value);
            }
        }
    }

    private static void ValidateCapacityRoutingActorAuthorityDisjoint(
        IReadOnlyList<ProductionCapacityRoutingDrainSaveData> producers,
        DungeonCharacterWorldSaveData characters)
    {
        foreach (ProductionCapacityRoutingDrainSaveData producer in
                 producers ?? Array.Empty<ProductionCapacityRoutingDrainSaveData>())
        {
            if (producer == null)
                continue;
            if (producer.phase is ProductionCapacityRoutingDrainPhase
                    .QuiescingActors
                or ProductionCapacityRoutingDrainPhase
                    .ReleasingOperationAuthority)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-transient-save-phase: "
                    + producer.stepOperationId);
            }
            if (producer.phase < ProductionCapacityRoutingDrainPhase
                    .AwaitingStablePhysicalState)
            {
                continue;
            }
            HashSet<string> releasedOperations = producer
                .actorAuthorityReleases
                .Where(value => value != null && value.effectsCommitted)
                .SelectMany(value => value.operationIds ?? new List<string>())
                .ToHashSet(StringComparer.Ordinal);
            if (releasedOperations.Count == 0)
                continue;
            foreach (DungeonCharacterSaveData actor in characters?.actors
                         ?? new List<DungeonCharacterSaveData>())
            {
                string characterId = actor?.persistentId ?? string.Empty;
                if (actor?.haulDeliveryIntent != null
                    && releasedOperations.Contains(
                        actor.haulDeliveryIntent.operationId)
                    || actor?.carryInventory?.items?.Any(item => item != null
                        && item.quantity > 0
                        && releasedOperations.Contains(
                            item.ownerOperationId)) == true)
                {
                    throw new InvalidOperationException(
                        "production-destructive-drain-released-character-authority-live: "
                        + producer.stepOperationId + ":" + characterId);
                }
            }
        }
    }

    public static void ValidateGenericTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Generic destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
            {
                throw new InvalidOperationException(
                    "Generic destructive-drain owner is null.");
            }

            ProductionGenericBillTerminalDrainSaveData[] matches =
                (producers
                    ?? Array.Empty<
                        ProductionGenericBillTerminalDrainSaveData>())
                .Where(value => value != null
                    && string.Equals(
                        value.stepOperationId,
                        owner.stepOperationId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned)
                {
                    continue;
                }
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-duplicate: "
                    + owner.stepOperationId);
            }

            ProductionGenericBillTerminalDrainSaveData producer = matches[0];
            if (!string.Equals(
                    producer.parentOperationId,
                    entry.operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.ownerStableId,
                    owner.ownerStableId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.facilityId,
                    entry.facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.requestFingerprint,
                    owner.requestFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase !=
                        ProductionGenericBillTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is
                        ProductionGenericBillTerminalDrainPhase
                            .BillTerminalCommittedAwaitingOwnerAcknowledgement
                        or ProductionGenericBillTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase ==
                        ProductionGenericBillTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            if (!phaseMatches)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-phase-mismatch: "
                    + owner.stepOperationId);
            }
            if (owner.phase !=
                    ProductionFacilityDestructiveDrainStepPhase.Planned
                && (!string.Equals(
                        owner.commitId,
                        producer.commitId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        owner.receiptFingerprint,
                        producer.receiptFingerprint,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-receipt-mismatch: "
                    + owner.stepOperationId);
            }
        }
    }

    public static void ValidateCombatTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<CombatEquipmentTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Combat destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
                throw new InvalidOperationException(
                    "Combat destructive-drain owner is null.");
            CombatEquipmentTerminalDrainSaveData[] matches = (producers
                    ?? Array.Empty<CombatEquipmentTerminalDrainSaveData>())
                .Where(value => value != null
                    && string.Equals(value.stepOperationId,
                        owner.stepOperationId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned)
                    continue;
                throw new InvalidOperationException(
                    "production-destructive-drain-combat-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-combat-producer-duplicate: "
                    + owner.stepOperationId);
            }

            CombatEquipmentTerminalDrainSaveData producer = matches[0];
            if (!string.Equals(producer.parentOperationId,
                    entry.operationId, StringComparison.Ordinal)
                || !string.Equals(producer.source.ownerStableId,
                    owner.ownerStableId, StringComparison.Ordinal)
                || !string.Equals(producer.source.facilityId,
                    entry.facilityId, StringComparison.Ordinal)
                || !string.Equals(producer.requestFingerprint,
                    owner.requestFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-combat-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase != CombatEquipmentTerminalDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is CombatEquipmentTerminalDrainPhase
                            .TerminalEffectsCommittedAwaitingOwnerAcknowledgement
                        or CombatEquipmentTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase == CombatEquipmentTerminalDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            ValidateTerminalProducerPhaseAndReceipt(
                owner,
                producer.commitId,
                producer.receiptFingerprint,
                phaseMatches,
                "combat");
        }
    }

    public static void ValidateApparelTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Apparel destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
                throw new InvalidOperationException(
                    "Apparel destructive-drain owner is null.");
            ProductionApparelOrderTerminalDrainSaveData[] matches = (producers
                    ?? Array.Empty<
                        ProductionApparelOrderTerminalDrainSaveData>())
                .Where(value => value != null
                    && string.Equals(value.stepOperationId,
                        owner.stepOperationId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned)
                    continue;
                throw new InvalidOperationException(
                    "production-destructive-drain-apparel-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-apparel-producer-duplicate: "
                    + owner.stepOperationId);
            }

            ProductionApparelOrderTerminalDrainSaveData producer = matches[0];
            if (!string.Equals(producer.parentOperationId,
                    entry.operationId, StringComparison.Ordinal)
                || !string.Equals(producer.ownerStableId,
                    owner.ownerStableId, StringComparison.Ordinal)
                || !string.Equals(producer.facilityId,
                    entry.facilityId, StringComparison.Ordinal)
                || !string.Equals(producer.requestFingerprint,
                    owner.requestFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-apparel-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase !=
                        ProductionApparelOrderTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is
                        ProductionApparelOrderTerminalDrainPhase
                            .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement
                        or ProductionApparelOrderTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase ==
                        ProductionApparelOrderTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            ValidateTerminalProducerPhaseAndReceipt(
                owner,
                producer.commitId,
                producer.receiptFingerprint,
                phaseMatches,
                "apparel");
        }
    }

    private static void ValidateTerminalProducerPhaseAndReceipt(
        ProductionFacilityDestructiveDrainOwnerSaveData owner,
        string producerCommitId,
        string producerReceiptFingerprint,
        bool phaseMatches,
        string producerKind)
    {
        if (!phaseMatches)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-" + producerKind
                + "-producer-phase-mismatch: " + owner.stepOperationId);
        }
        if (owner.phase != ProductionFacilityDestructiveDrainStepPhase.Planned
            && (!string.Equals(owner.commitId,
                    producerCommitId, StringComparison.Ordinal)
                || !string.Equals(owner.receiptFingerprint,
                    producerReceiptFingerprint, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-" + producerKind
                + "-producer-receipt-mismatch: " + owner.stepOperationId);
        }
    }

    private static void ValidateCapacityRoutingProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionCapacityRoutingDrainSaveData> producers,
        ProductionPreparedOutputRoutingSaveData routing,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> physicalRoutes,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Capacity destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
                throw new InvalidOperationException(
                    "Capacity destructive-drain owner is null.");
            ProductionCapacityRoutingDrainSaveData[] matches = producers
                .Where(value => value != null
                    && string.Equals(
                        value.stepOperationId,
                        owner.stepOperationId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned)
                {
                    continue;
                }
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-duplicate: "
                    + owner.stepOperationId);
            }

            ProductionCapacityRoutingDrainSaveData producer = matches[0];
            if (producer.phase is ProductionCapacityRoutingDrainPhase
                    .QuiescingActors
                or ProductionCapacityRoutingDrainPhase
                    .ReleasingOperationAuthority)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-transient-save-phase: "
                    + producer.stepOperationId);
            }
            string expectedOwner =
                ProductionFacilityDestructiveDrainOwnerStableIds.RoutingBatch(
                    producer.batchCommitId);
            if (!string.Equals(
                    producer.ownerStableId,
                    owner.ownerStableId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.ownerStableId,
                    expectedOwner,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.facilityId,
                    entry.facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.sourceDestinationId,
                    entry.destinationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.requestFingerprint,
                    owner.requestFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.sourceOwnershipFingerprint,
                    participant.preparedContributionFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase != ProductionCapacityRoutingDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is ProductionCapacityRoutingDrainPhase
                            .EffectCommittedAwaitingOwnerAck
                        or ProductionCapacityRoutingDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase == ProductionCapacityRoutingDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            if (!phaseMatches)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-phase-mismatch: "
                    + owner.stepOperationId);
            }
            if (owner.phase != ProductionFacilityDestructiveDrainStepPhase.Planned
                && (!string.Equals(
                        owner.commitId,
                        producer.commitId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        owner.receiptFingerprint,
                        producer.receiptFingerprint,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-receipt-mismatch: "
                    + owner.stepOperationId);
            }
            ValidateCapacityRoutingProducerSource(
                producer,
                routing,
                physicalRoutes);
        }
    }

    private static void ValidateCapacityRoutingProducerSource(
        ProductionCapacityRoutingDrainSaveData producer,
        ProductionPreparedOutputRoutingSaveData routing,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> physicalRoutes)
    {
        ProductionPreparedOutputRoutingBatchSaveData[] batchMatches =
            (routing?.batches
                ?? new List<ProductionPreparedOutputRoutingBatchSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.batchCommitId,
                    producer.batchCommitId,
                    StringComparison.Ordinal))
            .ToArray();
        FacilityOutputExactRouteOutboxSaveData[] itemRoutes =
            (physicalRoutes ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.batchCommitId,
                    producer.batchCommitId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
            .ToArray();
        bool terminal = producer.phase is ProductionCapacityRoutingDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
        bool mayObserveCheckpointGap = producer.phase ==
            ProductionCapacityRoutingDrainPhase.AwaitingDurableCheckpointGc;
        if (batchMatches.Length == 0)
        {
            if ((!terminal && !mayObserveCheckpointGap)
                || itemRoutes.Length != 0)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-source-missing-one-sided: "
                    + producer.stepOperationId);
            }
            return;
        }
        if (batchMatches.Length != 1 || terminal)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-capacity-source-terminal-or-duplicate: "
                + producer.stepOperationId);
        }

        ProductionPreparedOutputRoutingBatchSaveData batch = batchMatches[0];
        if (!string.Equals(batch.ownerFacilityId, producer.facilityId,
                StringComparison.Ordinal)
            || !string.Equals(batch.destinationId, producer.sourceDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(batch.outcomeFingerprint,
                producer.sourceOutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(batch.routingFingerprint,
                producer.sourceRoutingFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-capacity-source-batch-mismatch: "
                + producer.stepOperationId);
        }

        Dictionary<string, ProductionPreparedOutputRoutingLineSaveData> lines =
            (batch.lines ?? new List<ProductionPreparedOutputRoutingLineSaveData>())
            .Where(value => value != null)
            .ToDictionary(value => value.lineCommitId, StringComparer.Ordinal);
        foreach (ProductionCapacityRoutingDrainLineSaveData frozen in
                 producer.sourceLines)
        {
            if (!lines.TryGetValue(
                    frozen.lineCommitId,
                    out ProductionPreparedOutputRoutingLineSaveData live)
                || !string.Equals(live.outputLineId, frozen.outputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(live.itemId, frozen.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(live.componentFingerprint,
                    frozen.componentFingerprint, StringComparison.Ordinal)
                || !string.Equals(live.outputCapabilityId,
                    frozen.outputCapabilityId, StringComparison.Ordinal)
                || live.outputCapabilityVersion !=
                    frozen.outputCapabilityVersion
                || !string.Equals(live.outputComponentCodecId,
                    frozen.outputComponentCodecId, StringComparison.Ordinal)
                || live.outputComponentCodecVersion !=
                    frozen.outputComponentCodecVersion
                || !string.Equals(live.outputCapabilityFingerprint,
                    frozen.outputCapabilityFingerprint,
                    StringComparison.Ordinal)
                || live.originalQuantity != frozen.originalQuantity
                || live.originalMassGrams != frozen.originalMassGrams
                || live.remainingQuantity > frozen.remainingQuantity
                || live.remainingMassGrams > frozen.remainingMassGrams
                || live.routedQuantity < frozen.routedQuantity
                || live.routedMassGrams < frozen.routedMassGrams)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-source-line-mismatch: "
                    + producer.stepOperationId + ":" + frozen.lineCommitId);
            }
        }

        Dictionary<string, ProductionPreparedOutputRouteOperationSaveData>
            operations = lines.Values
                .SelectMany(line => line.routeOperations
                    ?? new List<ProductionPreparedOutputRouteOperationSaveData>())
                .Where(value => value != null)
                .ToDictionary(value => value.routeOperationId,
                    StringComparer.Ordinal);
        foreach (ProductionCapacityRoutingDrainRouteSaveData frozen in
                 producer.sourceRoutes)
        {
            if (!operations.TryGetValue(
                    frozen.routeOperationId,
                    out ProductionPreparedOutputRouteOperationSaveData live)
                || !string.Equals(live.requestFingerprint,
                    frozen.requestFingerprint, StringComparison.Ordinal)
                || (int)live.phase < frozen.phase
                || !string.IsNullOrEmpty(frozen.physicalReceiptFingerprint)
                    && !string.Equals(live.physicalReceiptFingerprint,
                        frozen.physicalReceiptFingerprint,
                        StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-source-route-mismatch: "
                    + producer.stepOperationId + ":"
                    + frozen.routeOperationId);
            }
        }

        if (producer.finalRouteOperationIds.Count > 0)
        {
            string[] liveOperationIds = operations.Keys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] physicalOperationIds = itemRoutes
                .Select(value => value.routeOperationId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!producer.finalRouteOperationIds.SequenceEqual(
                    liveOperationIds,
                    StringComparer.Ordinal)
                || !producer.finalRouteOperationIds.SequenceEqual(
                    physicalOperationIds,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-terminal-route-set-mismatch: "
                    + producer.stepOperationId);
            }
        }
    }

    public static void ValidateStockSensorCompositeProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        DungeonProductionBillSaveData production,
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> children,
        ISet<string> joinedChildSteps)
    {
        if (entry == null
            || production == null
            || children == null
            || joinedChildSteps == null
            || !ProductionFacilityDestructiveDrainOperationId.TryParse(
                entry.operationId,
                out ProductionFacilityDestructiveDrainOperationId operationId))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-join-input-invalid");
        }

        BuildingInstanceId facilityId =
            (BuildingInstanceId)(entry.facilityId ?? string.Empty);
        if (!facilityId.IsValid)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-facility-invalid");
        }

        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            (entry.participants
                ?? new List<ProductionFacilityDestructiveDrainParticipantSaveData>())
            .Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
            ?? throw new InvalidOperationException(
                "Stock-sensor destructive-drain participant has no owner collection.");
        if (owners.Count > 1 || owners.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-owner-cardinality");
        }

        ProductionStockSensorPhysicalCommitSaveData pending =
            SingleStockSensorRow(
                production.pendingStockSensorInstalls,
                facilityId.Value,
                value => value?.facilityId,
                "pending-install");
        ProductionInstalledStockSensorSaveData installed =
            SingleStockSensorRow(
                production.installedStockSensors,
                facilityId.Value,
                value => value?.facilityId,
                "installed");
        ProductionStockSensorRemovalSaveData removal =
            SingleStockSensorRow(
                production.pendingStockSensorRemovals,
                facilityId.Value,
                value => value?.facilityId,
                "removal");
        if (!ProductionStockSensorDestructiveDrainCanonical.Provenance.TryCreate(
                facilityId,
                pending,
                installed,
                removal,
                out ProductionStockSensorDestructiveDrainCanonical.Provenance
                    provenance))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-provenance-conflict");
        }

        string expectedOwner =
            ProductionFacilityDestructiveDrainOwnerStableIds.StockSensor(
                facilityId.Value);
        string expectedUpperStep = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(
                operationId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                expectedOwner);
        string expectedChildStep =
            ProductionStockSensorDestructiveDrainCanonical
                .BuildChildStepOperationId(expectedUpperStep);
        string expectedDestination =
            ProductionStockSensorRuntime.BuildDestinationId(facilityId.Value);
        ProductionInputDestinationCustodyDrainSaveData[] childMatches = children
            .Where(value => value != null
                && string.Equals(value.stepOperationId,
                    expectedChildStep, StringComparison.Ordinal))
            .ToArray();

        if (owners.Count == 0)
        {
            if (provenance.Present || childMatches.Length != 0)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-stock-sensor-owner-missing");
            }
            return;
        }

        ProductionFacilityDestructiveDrainOwnerSaveData owner = owners[0];
        if (!string.Equals(owner.ownerStableId,
                expectedOwner, StringComparison.Ordinal)
            || !string.Equals(owner.stepOperationId,
                expectedUpperStep, StringComparison.Ordinal)
            || owner.disposition !=
                ProductionFacilityDestructiveDrainDisposition.Terminalize
            || !string.IsNullOrEmpty(owner.targetDestinationId))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-owner-identity-mismatch");
        }

        if (childMatches.Length == 0)
        {
            if (owner.phase ==
                ProductionFacilityDestructiveDrainStepPhase.Planned)
            {
                return;
            }
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-missing: "
                + expectedChildStep);
        }
        if (childMatches.Length != 1
            || !joinedChildSteps.Add(expectedChildStep))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-duplicate: "
                + expectedChildStep);
        }

        ProductionInputDestinationCustodyDrainSaveData child = childMatches[0];
        if (!ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
            || !string.Equals(child.parentOperationId,
                entry.operationId, StringComparison.Ordinal)
            || !string.Equals(child.ownerStableId,
                expectedOwner, StringComparison.Ordinal)
            || !string.Equals(child.billId,
                expectedDestination, StringComparison.Ordinal)
            || !string.Equals(child.facilityId,
                facilityId.Value, StringComparison.Ordinal)
            || !string.Equals(child.sourceDestinationId,
                expectedDestination, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-identity-mismatch: "
                + expectedChildStep);
        }

        string expectedRequest =
            ProductionStockSensorDestructiveDrainCanonical
                .BuildRequestFingerprint(child.requestFingerprint, provenance);
        if (!string.Equals(owner.requestFingerprint,
                expectedRequest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-request-mismatch: "
                + expectedUpperStep);
        }

        bool childPhaseMatches = owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                !ProductionStockSensorDestructiveDrainCanonical
                    .IsChildAcknowledged(child.phase),
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck =>
                ProductionStockSensorDestructiveDrainCanonical
                    .IsChildEffectCommitted(child.phase),
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                ProductionStockSensorDestructiveDrainCanonical
                    .IsChildAcknowledged(child.phase),
            _ => false
        };
        bool removalPhaseMatches = owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                removal == null
                || !ProductionStockSensorDestructiveDrainCanonical
                    .IsSensorAcknowledged(removal.phase),
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck =>
                provenance.Present
                    ? removal != null
                        && ProductionStockSensorDestructiveDrainCanonical
                            .IsSensorEffectCommitted(removal.phase)
                    : removal == null,
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                provenance.Present
                    ? removal != null
                        && ProductionStockSensorDestructiveDrainCanonical
                            .IsSensorAcknowledged(removal.phase)
                    : removal == null,
            _ => false
        };
        if (!childPhaseMatches || !removalPhaseMatches)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-phase-mismatch: "
                + expectedUpperStep);
        }

        if (owner.phase == ProductionFacilityDestructiveDrainStepPhase.Planned)
            return;
        if (!ProductionStockSensorDestructiveDrainCanonical
                .TryBuildCompositeTerminal(
                    owner.requestFingerprint,
                    child,
                    removal,
                    out string expectedCommit,
                    out string expectedReceipt)
            || !string.Equals(owner.commitId,
                expectedCommit, StringComparison.Ordinal)
            || !string.Equals(owner.receiptFingerprint,
                expectedReceipt, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-terminal-mismatch: "
                + expectedUpperStep);
        }
    }

    private static T SingleStockSensorRow<T>(
        IEnumerable<T> rows,
        string facilityId,
        Func<T, string> facilitySelector,
        string sourceKind)
        where T : class
    {
        T[] matches = (rows ?? Array.Empty<T>())
            .Where(value => value != null
                && string.Equals(facilitySelector(value),
                    facilityId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-source-duplicate: "
                + sourceKind + ":" + facilityId);
        }
        return matches.SingleOrDefault();
    }

    private static bool IsStockSensorChild(
        ProductionInputDestinationCustodyDrainSaveData child) =>
        child != null
        && ((child.ownerStableId ?? string.Empty).StartsWith(
                "stock-sensor:", StringComparison.Ordinal)
            || (child.billId ?? string.Empty).StartsWith(
                "production-sensor:", StringComparison.Ordinal)
            || (child.sourceDestinationId ?? string.Empty).StartsWith(
                "production-sensor:", StringComparison.Ordinal));

    private static void ValidatePhysicalCustodyProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionPhysicalCustodyDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Physical destructive-drain participant has no owner collection.");
        if (owners.Count > 1)
        {
            throw new InvalidOperationException(
                "Physical destructive-drain participant must use one atomic destination owner.");
        }
        if (owners.Count == 0)
            return;

        ProductionFacilityDestructiveDrainOwnerSaveData owner = owners[0]
            ?? throw new InvalidOperationException(
                "Physical destructive-drain owner is null.");
        ProductionPhysicalCustodyDrainSaveData[] matches = producers
            .Where(value => value != null
                && string.Equals(
                    value.stepOperationId,
                    owner.stepOperationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            if (owner.phase == ProductionFacilityDestructiveDrainStepPhase.Planned)
                return;
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-missing: "
                + owner.stepOperationId);
        }
        if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-duplicate: "
                + owner.stepOperationId);
        }

        ProductionPhysicalCustodyDrainSaveData producer = matches[0];
        if (!string.Equals(
                producer.ownerStableId,
                owner.ownerStableId,
                StringComparison.Ordinal)
            || !string.Equals(
                producer.sourceDestinationId,
                entry.destinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                producer.requestFingerprint,
                owner.requestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                producer.sourceOwnershipFingerprint,
                participant.preparedContributionFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-request-mismatch: "
                + owner.stepOperationId);
        }

        bool phaseMatches = owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                producer.phase != ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck =>
                producer.phase is ProductionPhysicalCustodyDrainPhase
                        .EffectCommittedAwaitingOwnerAck
                    or ProductionPhysicalCustodyDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                producer.phase == ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
            _ => false
        };
        if (!phaseMatches)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-phase-mismatch: "
                + owner.stepOperationId);
        }
        if (owner.phase != ProductionFacilityDestructiveDrainStepPhase.Planned
            && (!string.Equals(
                    owner.commitId,
                    producer.commitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.receiptFingerprint,
                    producer.receiptFingerprint,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-receipt-mismatch: "
                + owner.stepOperationId);
        }
    }

    private IReadOnlyDictionary<string, string> ProjectPresentContributors(
        BuildingInstanceId facilityId,
        ModularFacilityWorldSaveData world,
        DungeonProductionBillSaveData production,
        DungeonProductionGenericBillTerminalDrainSaveData
            genericTerminalDrains,
        DungeonCombatEquipmentSaveData combat,
        CombatEquipmentMaintenanceSaveData maintenance,
        DungeonCharacterEnvironmentSaveData environment,
        DungeonPhysicalItemSaveData items,
        DungeonCharacterWorldSaveData characters,
        ProductionPreparedOutputRoutingSaveData routing)
    {
        ProductionOutputCapacityDurableProjection capacity =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    facilityId,
                    world,
                    production,
                    genericTerminalDrains,
                    items,
                    characters,
                    routing,
                    items.pendingExactOutputRoutes,
                    buildingDefinitions,
                    capacityProjector,
                    massQuery);
        return CreateContributorMap(
            facilityId,
            production,
            combat,
            maintenance,
            environment,
            items,
            characters,
            capacity.Fingerprint);
    }

    private static IReadOnlyDictionary<string, string>
        ProjectAbsentContributors(
            BuildingInstanceId facilityId,
            ModularFacilityWorldSaveData world,
            DungeonProductionBillSaveData production,
            DungeonCombatEquipmentSaveData combat,
            CombatEquipmentMaintenanceSaveData maintenance,
            DungeonCharacterEnvironmentSaveData environment,
            DungeonPhysicalItemSaveData items,
            DungeonCharacterWorldSaveData characters,
            ProductionPreparedOutputRoutingSaveData routing)
    {
        ProductionOutputDestinationDurableSaveProjector
            .ProjectAbsentFacilityAggregateFromSave(
                facilityId,
                world,
                production,
                combat,
                maintenance,
                environment,
                items,
                characters,
                routing);
        string capacity = ProductionOutputDestinationDurableSaveProjector
            .ProjectCapacityRouting(
                facilityId,
                null,
                new FacilityBufferPhysicalOccupancySnapshot(0L, 0L),
                routing,
                items.pendingExactOutputRoutes);
        return CreateContributorMap(
            facilityId,
            production,
            combat,
            maintenance,
            environment,
            items,
            characters,
            capacity);
    }

    private static IReadOnlyDictionary<string, string> CreateContributorMap(
        BuildingInstanceId facilityId,
        DungeonProductionBillSaveData production,
        DungeonCombatEquipmentSaveData combat,
        CombatEquipmentMaintenanceSaveData maintenance,
        DungeonCharacterEnvironmentSaveData environment,
        DungeonPhysicalItemSaveData items,
        DungeonCharacterWorldSaveData characters,
        string capacityFingerprint) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProductionOutputDestinationDurableSaveProjector
                .ApparelContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectApparel(
                    facilityId,
                    environment),
            [ProductionOutputDestinationDurableSaveProjector
                .CapacityRoutingContributorId] = capacityFingerprint,
            [ProductionOutputDestinationDurableSaveProjector
                .EquipmentContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectEquipment(
                    facilityId,
                    combat,
                    maintenance),
            [ProductionOutputDestinationDurableSaveProjector
                .GenericBillsContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectGenericBills(
                    facilityId,
                    production),
            [ProductionOutputDestinationDurableSaveProjector
                .PhysicalCustodyContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectPhysicalCustody(
                    facilityId,
                    items,
                    characters),
            [ProductionOutputDestinationDurableSaveProjector
                .StockSensorContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectStockSensor(
                    facilityId,
                    production,
                    items,
                    characters)
        };

    private static void ValidateParticipants(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyDictionary<string, string> contributors)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in entry.participants
                     ?? new List<ProductionFacilityDestructiveDrainParticipantSaveData>())
        {
            if (participant == null
                || !ProductionFacilityDestructiveDrainParticipantRegistry
                    .TryGetRequiredContractVersion(
                        participant.participantId,
                        out int requiredContractVersion)
                || participant.contractVersion != requiredContractVersion
                || !contributors.TryGetValue(
                    participant.participantId ?? string.Empty,
                    out string currentFingerprint)
                || !seen.Add(participant.participantId)
                || !string.Equals(
                    participant.expectedCurrentContributionFingerprint,
                    currentFingerprint,
                    StringComparison.Ordinal)
                || entry.phase ==
                    ProductionFacilityDestructiveDrainPhase.Prepared
                    && !string.Equals(
                        participant.preparedContributionFingerprint,
                        currentFingerprint,
                        StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Destructive-drain entry has an unknown, duplicate, version-drifted or contribution-mismatched participant: "
                    + (participant?.participantId ?? string.Empty));
            }
        }
        if (seen.Count != contributors.Count)
        {
            throw new InvalidOperationException(
                "Destructive-drain entry is missing one or more required lifecycle participants.");
        }
    }

    private static void ValidatePreparedOwnerBijection(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceOwners)
    {
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in entry.participants)
        {
            if (!sourceOwners.TryGetValue(
                    participant.participantId,
                    out IReadOnlyList<string> expected))
            {
                throw new InvalidOperationException(
                    "Destructive-drain participant has no planned source-owner projection: "
                    + participant.participantId);
            }
            IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData>
                journalOwners = participant.owners
                    ?? throw new InvalidOperationException(
                        "Prepared destructive-drain participant has no owner collection: "
                        + participant.participantId);
            string[] actual = journalOwners
                .Where(value => value != null)
                .Select(value => value.ownerStableId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (actual.Length != journalOwners.Count
                || !actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-prepared-owner-source-bijection-mismatch: "
                    + participant.participantId);
            }
            if (journalOwners.Any(owner =>
                    owner.phase !=
                        ProductionFacilityDestructiveDrainStepPhase.Planned))
            {
                throw new InvalidOperationException(
                    "Prepared destructive-drain owner is not in the Planned phase: "
                    + participant.participantId);
            }
        }
    }

    private static TPayload RequirePayload<TPayload>(
        DungeonGameSaveData saveData,
        string sectionId)
        where TPayload : class, new()
    {
        if (!DungeonSaveSectionPayload.TryRead(
                saveData,
                sectionId,
                out TPayload payload))
        {
            throw new InvalidOperationException(
                "Destructive-drain validation requires section '" + sectionId + "'.");
        }
        return payload;
    }

    private static TPayload RequirePayload<TPayload>(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        string sectionId)
        where TPayload : class
    {
        if (!envelopes.TryGetValue(sectionId, out DungeonSaveSectionEnvelope envelope))
        {
            throw new InvalidOperationException(
                "Destructive-drain validation requires section '" + sectionId + "'.");
        }
        return Parse<TPayload>(envelope, sectionId);
    }

    private static TPayload Parse<TPayload>(
        DungeonSaveSectionEnvelope envelope,
        string sectionId)
        where TPayload : class
    {
        if (envelope == null || string.IsNullOrWhiteSpace(envelope.payloadJson))
            throw new InvalidOperationException(
                "Destructive-drain validation found an empty section '" + sectionId + "'.");
        return JsonUtility.FromJson<TPayload>(envelope.payloadJson)
            ?? throw new InvalidOperationException(
                "Destructive-drain validation could not parse section '" + sectionId + "'.");
    }
}
