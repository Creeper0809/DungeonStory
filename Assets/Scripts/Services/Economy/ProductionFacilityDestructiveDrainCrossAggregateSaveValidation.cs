using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
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
            bool hasFireDamageOwner = DungeonSaveSectionPayload.TryRead(
                    saveData,
                    EnvironmentalFireSaveSection.Id,
                    out DungeonEnvironmentalFireSaveData orphanFire)
                && HasAnyFireDamageDestructiveDrainOwner(orphanFire);
            if (hasPhysicalProducer
                || hasGenericProducer
                || hasCombatProducer
                || hasApparelProducer
                || hasWorkOrderOwner
                || hasFireDamageOwner)
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
                RequirePayload<DungeonEnvironmentalFireSaveData>(
                    saveData,
                    EnvironmentalFireSaveSection.Id),
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
            ProductionFacilityDestructiveDrainOwnerJoinValidation
                .ValidateWorkOrderDestructiveDrainJoins(
                    workOrders,
                    world,
                    drain,
                    buildingDefinitions);
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
            bool hasFireDamageOwner = envelopes.TryGetValue(
                    EnvironmentalFireSaveSection.Id,
                    out DungeonSaveSectionEnvelope fireEnvelope)
                && HasAnyFireDamageDestructiveDrainOwner(
                    Parse<DungeonEnvironmentalFireSaveData>(
                        fireEnvelope,
                        EnvironmentalFireSaveSection.Id));
            if (hasPhysicalProducer
                || hasGenericProducer
                || hasCombatProducer
                || hasApparelProducer
                || hasWorkOrderOwner
                || hasFireDamageOwner)
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
                RequirePayload<DungeonEnvironmentalFireSaveData>(
                    envelopes,
                    EnvironmentalFireSaveSection.Id),
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
            ProductionFacilityDestructiveDrainOwnerJoinValidation
                .ValidateWorkOrderDestructiveDrainJoins(
                    workOrders,
                    world,
                    drain,
                    buildingDefinitions);
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
            bundle.Fire,
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
        DungeonEnvironmentalFireSaveData fire,
        DungeonProductionGenericBillTerminalDrainSaveData genericTerminalDrains,
        DungeonCombatEquipmentTerminalDrainSaveData combatTerminalDrains,
        DungeonProductionApparelOrderTerminalDrainSaveData apparelTerminalDrains,
        DungeonProductionFacilityDestructiveDrainSaveData drain)
    {
        drain = ProductionFacilityDestructiveDrainJournal
            .MigrateLegacyPayload(drain);
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
        ProductionFacilityDestructiveDrainPhysicalProducerJoinValidation
            .ValidateCapacityRoutingActorAuthorityDisjoint(
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
        HashSet<string> joinedFireDamageOperations =
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
                ? ProductionFacilityDestructiveDrainParticipantSaveValidation
                    .ProjectAbsentContributors(
                        facilityId,
                        world,
                        production,
                        combat,
                        maintenance,
                        environment,
                        items,
                        characters,
                        routing,
                        fire)
                : ProductionFacilityDestructiveDrainParticipantSaveValidation
                    .ProjectPresentContributors(
                        facilityId,
                        world,
                        production,
                        genericTerminalDrains,
                        combat,
                        maintenance,
                        environment,
                        items,
                        characters,
                        routing,
                        fire,
                        buildingDefinitions,
                        capacityProjector,
                        massQuery);
            ProductionFacilityDestructiveDrainParticipantSaveValidation
                .ValidateParticipants(entry, contributors);
            ProductionFacilityDestructiveDrainPhysicalProducerJoinValidation
                .ValidatePhysicalCustodyProducerJoin(
                    entry,
                    items.pendingProductionCustodyDrains,
                    joinedPhysicalProducerSteps);
            ProductionFacilityDestructiveDrainPhysicalProducerJoinValidation
                .ValidateCapacityRoutingProducerJoin(
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
            ProductionFacilityDestructiveDrainOwnerJoinValidation
                .ValidateFireDamageOutcomeJoin(
                    entry,
                    fire?.damageOperations,
                    joinedFireDamageOperations);
            ProductionFacilityDestructiveDrainOwnerJoinValidation
                .ValidateBuildingDemolitionOutcomeJoin(entry);
            if (entry.phase == ProductionFacilityDestructiveDrainPhase.Prepared)
            {
                ProductionFacilityDestructiveDrainParticipantSaveValidation
                    .ValidatePreparedOwnerBijection(
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
                            routing,
                            fire));
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
                        routing,
                        fire);
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
                && ProductionFacilityDestructiveDrainStockSensorProducerJoinValidation
                    .IsStockSensorChild(value)
                && !joinedStockSensorChildSteps.Contains(
                    value.stepOperationId ?? string.Empty));
        if (orphanStockSensorChild != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-orphan: "
                + orphanStockSensorChild.stepOperationId);
        }
        EnvironmentalFireDamageRecord orphanFireDamage =
            (fire?.damageOperations
                ?? new List<EnvironmentalFireDamageRecord>())
            .FirstOrDefault(value => value != null
                && value.phase ==
                    EnvironmentalFireDamagePhase.AwaitingWorldRemoval
                && !joinedFireDamageOperations.Contains(
                    value.operationId ?? string.Empty));
        if (orphanFireDamage != null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-fire-damage-owner-orphan: "
                + orphanFireDamage.operationId);
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

    private static bool HasAnyFireDamageDestructiveDrainOwner(
        DungeonEnvironmentalFireSaveData payload) =>
        (payload?.damageOperations
                ?? new List<EnvironmentalFireDamageRecord>())
            .Any(value => value != null
                && value.phase ==
                    EnvironmentalFireDamagePhase.AwaitingWorldRemoval);

    public static void ValidateGenericTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainTerminalProducerJoinValidation
            .ValidateGenericTerminalProducerJoin(
                entry,
                producers,
                joinedSteps);
    }

    public static void ValidateCombatTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<CombatEquipmentTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainTerminalProducerJoinValidation
            .ValidateCombatTerminalProducerJoin(
                entry,
                producers,
                joinedSteps);
    }

    public static void ValidateApparelTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainTerminalProducerJoinValidation
            .ValidateApparelTerminalProducerJoin(
                entry,
                producers,
                joinedSteps);
    }

    public static void ValidateStockSensorCompositeProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        DungeonProductionBillSaveData production,
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> children,
        ISet<string> joinedChildSteps)
    {
        ProductionFacilityDestructiveDrainStockSensorProducerJoinValidation
            .ValidateStockSensorCompositeProducerJoin(
                entry,
                production,
                children,
                joinedChildSteps);
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
