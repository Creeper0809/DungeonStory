using System;
using System.Collections.Generic;
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
            if (hasPhysicalProducer
                || hasGenericProducer
                || hasCombatProducer
                || hasApparelProducer)
            {
                report.AddError(
                    "Production destructive-drain producer exists without its journal section.");
            }
            return;
        }

        try
        {
            ValidateCore(
                RequirePayload<ModularFacilityWorldSaveData>(
                    saveData,
                    ModularFacilityWorldSaveSection.Id),
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
            if (hasPhysicalProducer
                || hasGenericProducer
                || hasCombatProducer
                || hasApparelProducer)
            {
                report.AddError(
                    "Production destructive-drain producer exists without its registry journal section.");
            }
            return;
        }

        try
        {
            ValidateCore(
                RequirePayload<ModularFacilityWorldSaveData>(
                    envelopes,
                    ModularFacilityWorldSaveSection.Id),
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
                Parse<DungeonProductionFacilityDestructiveDrainSaveData>(
                    drainEnvelope,
                    ProductionFacilityDestructiveDrainSaveSection.Id));
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
    }

    private static bool HasAnyDestructiveDrainProducer(
        DungeonPhysicalItemSaveData items) =>
        items?.pendingProductionCustodyDrains?.Count > 0
        || items?.pendingCapacityRoutingDrains?.Count > 0;

    private static bool HasAnyGenericTerminalProducer(
        DungeonProductionGenericBillTerminalDrainSaveData payload) =>
        payload?.entries?.Count > 0;

    private static bool HasAnyCombatTerminalProducer(
        DungeonCombatEquipmentTerminalDrainSaveData payload) =>
        payload?.entries?.Count > 0;

    private static bool HasAnyApparelTerminalProducer(
        DungeonProductionApparelOrderTerminalDrainSaveData payload) =>
        payload?.entries?.Count > 0;

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
                || participant.contractVersion != 1
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
