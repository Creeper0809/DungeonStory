using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;

internal static class ProductionFacilityDestructiveDrainParticipantSaveValidation
{
    internal static IReadOnlyDictionary<string, string> ProjectPresentContributors(
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
        ProductionPreparedOutputRoutingSaveData routing,
        DungeonEnvironmentalFireSaveData fire,
        IBuildingDefinitionLookup buildingDefinitions,
        ProductionOutputBufferCapacityProjector capacityProjector,
        IPhysicalItemMassQuery massQuery)
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
            fire,
            capacity.Fingerprint);
    }

    internal static IReadOnlyDictionary<string, string>
        ProjectAbsentContributors(
            BuildingInstanceId facilityId,
            ModularFacilityWorldSaveData world,
            DungeonProductionBillSaveData production,
            DungeonCombatEquipmentSaveData combat,
            CombatEquipmentMaintenanceSaveData maintenance,
            DungeonCharacterEnvironmentSaveData environment,
        DungeonPhysicalItemSaveData items,
        DungeonCharacterWorldSaveData characters,
        ProductionPreparedOutputRoutingSaveData routing,
        DungeonEnvironmentalFireSaveData fire)
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
                routing,
                fire);
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
            fire,
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
        DungeonEnvironmentalFireSaveData fire,
        string capacityFingerprint) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProductionOutputDestinationDurableSaveProjector
                .ApparelContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectApparel(
                    facilityId,
                    environment),
            [ProductionOutputDestinationDurableSaveProjector
                .DemolitionOutcomeContributorId] =
                BuildingDemolitionOutcomeLifecycle.ProjectContribution(facilityId),
            [ProductionOutputDestinationDurableSaveProjector
                .CapacityRoutingContributorId] = capacityFingerprint,
            [ProductionOutputDestinationDurableSaveProjector
                .EquipmentContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectEquipment(
                    facilityId,
                    combat,
                    maintenance),
            [ProductionOutputDestinationDurableSaveProjector
                .FireDamageContributorId] =
                EnvironmentalFireDamageAuthority
                    .ProjectFacilityContribution(
                        facilityId,
                        fire?.damageOperations),
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

    internal static void ValidateParticipants(
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

    internal static void ValidatePreparedOwnerBijection(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceOwners)
    {
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in entry.participants)
        {
            IReadOnlyList<string> expected;
            if (string.Equals(participant.participantId,
                    ProductionFacilityDestructiveDrainParticipantIds.BuildingDemolitionOutcome,
                    StringComparison.Ordinal))
            {
                expected = ProductionFacilityDestructiveDrainOwnerJoinValidation
                    .ProjectPreparedDemolitionOwners(entry);
            }
            else if (!sourceOwners.TryGetValue(
                    participant.participantId,
                    out expected))
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
}
