using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed class ProductionOutputCapacityDurableProjection
{
    public ProductionOutputCapacityDurableProjection(
        ProductionFacilityCapacitySubject subject,
        ProductionOutputBufferCapacitySourceSnapshot portfolio,
        FacilityBufferCapacityProfile profile,
        FacilityBufferPhysicalOccupancySnapshot occupancy,
        string fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint) || fingerprint.Length != 64)
            throw new ArgumentException("Capacity fingerprint must be SHA-256.", nameof(fingerprint));
        Subject = subject;
        Portfolio = portfolio;
        Profile = profile;
        Occupancy = occupancy;
        Fingerprint = fingerprint;
    }

    public ProductionFacilityCapacitySubject Subject { get; }
    public ProductionOutputBufferCapacitySourceSnapshot Portfolio { get; }
    public FacilityBufferCapacityProfile Profile { get; }
    public FacilityBufferPhysicalOccupancySnapshot Occupancy { get; }
    public string Fingerprint { get; }
}

internal sealed class ProductionExactDestinationCustodyProjection
{
    internal ProductionExactDestinationCustodyProjection(
        IReadOnlyList<WorldItemStackSaveData> directStacks,
        IReadOnlyList<HaulDeliveryIntentSaveData> intents,
        IReadOnlyList<WorldItemStackSaveData> carriedStacks,
        IReadOnlyList<CharacterCarriedItemSaveData> carriedItems)
    {
        DirectStacks = directStacks ?? Array.Empty<WorldItemStackSaveData>();
        Intents = intents ?? Array.Empty<HaulDeliveryIntentSaveData>();
        CarriedStacks = carriedStacks ?? Array.Empty<WorldItemStackSaveData>();
        CarriedItems = carriedItems
            ?? Array.Empty<CharacterCarriedItemSaveData>();
    }

    internal IReadOnlyList<WorldItemStackSaveData> DirectStacks { get; }
    internal IReadOnlyList<HaulDeliveryIntentSaveData> Intents { get; }
    internal IReadOnlyList<WorldItemStackSaveData> CarriedStacks { get; }
    internal IReadOnlyList<CharacterCarriedItemSaveData> CarriedItems { get; }
    internal bool HasAuthority => DirectStacks.Count > 0 || Intents.Count > 0;
}

/// <summary>
/// Detached, save-DTO-only projection of the durable lifecycle owned by a
/// production-capable facility. It must remain usable during aggregate
/// preflight, before any restore candidate is published to the live runtime.
/// </summary>
public static class ProductionOutputDestinationDurableSaveProjector
{
    public const string AggregateSchemaToken =
        "production-output-durable-lifecycle@2";
    public const string GenericBillsContributorId =
        ProductionFacilityDestructiveDrainParticipantIds
            .GenericProductionBills;
    public const string EquipmentContributorId =
        ProductionFacilityDestructiveDrainParticipantIds
            .CombatEquipmentCrafting;
    public const string ApparelContributorId =
        ProductionFacilityDestructiveDrainParticipantIds
            .ApparelWorkOrders;
    public const string CapacityRoutingContributorId =
        ProductionFacilityDestructiveDrainParticipantIds
            .CapacityRoutingOutbox;
    public const string PhysicalCustodyContributorId =
        ProductionFacilityDestructiveDrainParticipantIds
            .PhysicalCustodyCarryRecovery;
    public const string StockSensorContributorId =
        ProductionFacilityDestructiveDrainParticipantIds
            .StockSensorEmbeddedSalvage;

    private static readonly string[] RequiredAggregateContributorIds =
    {
        ApparelContributorId,
        CapacityRoutingContributorId,
        EquipmentContributorId,
        GenericBillsContributorId,
        PhysicalCustodyContributorId,
        StockSensorContributorId
    };

    public static string ProjectGenericBills(
        BuildingInstanceId facilityId,
        DungeonProductionBillSaveData payload)
    {
        RequireFacility(facilityId);
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (payload.bills == null)
            throw new InvalidOperationException(
                "Production bill save payload has no current-format bill collection.");

        StringBuilder canonical = new StringBuilder(128)
            .Append(ProductionBillRuntime.GenericBillLifecycleSchema)
            .Append('|')
            .Append(facilityId.Value).Append('|');
        AppendOrdered(
            canonical,
            payload.bills
                .Where(value => value != null
                    && string.Equals(
                        value.buildingInstanceId,
                        facilityId.Value,
                        StringComparison.Ordinal))
                .OrderBy(value => value.billId, StringComparer.Ordinal));
        return ProductionLifecycleFingerprint.Compute(canonical.ToString());
    }

    public static string ProjectStockSensor(
        BuildingInstanceId facilityId,
        DungeonProductionBillSaveData payload,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload)
    {
        RequireFacility(facilityId);
        if (payload?.installedStockSensorFacilityIds == null
            || payload.acknowledgedStockSensorFacilityIds == null
            || payload.pendingStockSensorInstalls == null
            || payload.installedStockSensors == null
            || payload.pendingStockSensorRemovals == null)
        {
            throw new InvalidOperationException(
                "Production stock-sensor save payload is not current-format.");
        }

        string id = facilityId.Value;
        StringBuilder canonical = new StringBuilder(192)
            .Append(StockSensorContributorId).Append('|')
            .Append(id).Append('|')
            .Append(payload.installedStockSensorFacilityIds.Count(value =>
                string.Equals(value, id, StringComparison.Ordinal)))
            .Append('|')
            .Append(payload.acknowledgedStockSensorFacilityIds.Count(value =>
                string.Equals(value, id, StringComparison.Ordinal)))
            .Append('|');
        AppendOrdered(
            canonical,
            payload.pendingStockSensorInstalls
                .Where(value => value != null
                    && string.Equals(
                        value.facilityId,
                        id,
                        StringComparison.Ordinal))
                .OrderBy(value => value.operationId, StringComparer.Ordinal));
        AppendOrdered(
            canonical,
            payload.installedStockSensors
                .Where(value => value != null
                    && string.Equals(
                        value.facilityId,
                        id,
                        StringComparison.Ordinal))
                .OrderBy(value => value.inputOperationId, StringComparer.Ordinal));
        AppendOrdered(
            canonical,
            payload.pendingStockSensorRemovals
                .Where(value => value != null
                    && string.Equals(
                        value.facilityId,
                        id,
                        StringComparison.Ordinal))
                .OrderBy(value => value.operationId, StringComparer.Ordinal));
        ProductionExactDestinationCustodyProjection custody =
            CaptureExactDestinationCustody(
                ProductionStockSensorRuntime.BuildDestinationId(id),
                itemPayload,
                characterPayload);
        canonical.Append("socket-custody@1|direct|")
            .Append(custody.DirectStacks.Count).Append('|');
        AppendOrdered(canonical, custody.DirectStacks);
        canonical.Append("|intents|")
            .Append(custody.Intents.Count).Append('|');
        AppendOrdered(canonical, custody.Intents);
        canonical.Append("|carried-stacks|")
            .Append(custody.CarriedStacks.Count).Append('|');
        AppendOrdered(canonical, custody.CarriedStacks);
        canonical.Append("|carried-items|")
            .Append(custody.CarriedItems.Count).Append('|');
        AppendOrdered(canonical, custody.CarriedItems);
        return ProductionLifecycleFingerprint.Compute(canonical.ToString());
    }

    public static string ComposeAggregate(
        BuildingInstanceId facilityId,
        IEnumerable<KeyValuePair<string, string>> contributorFingerprints)
    {
        return ComposeAggregateCore(
            facilityId,
            contributorFingerprints,
            requireCurrentFormatSchema: true);
    }

    internal static string ComposeAggregateFixture(
        BuildingInstanceId facilityId,
        IEnumerable<KeyValuePair<string, string>> contributorFingerprints)
    {
        return ComposeAggregateCore(
            facilityId,
            contributorFingerprints,
            requireCurrentFormatSchema: false);
    }

    private static string ComposeAggregateCore(
        BuildingInstanceId facilityId,
        IEnumerable<KeyValuePair<string, string>> contributorFingerprints,
        bool requireCurrentFormatSchema)
    {
        RequireFacility(facilityId);
        KeyValuePair<string, string>[] ordered = (contributorFingerprints
                ?? throw new ArgumentNullException(nameof(contributorFingerprints)))
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => string.IsNullOrEmpty(value.Key)
                || string.IsNullOrEmpty(value.Value)
                || value.Value.Length != 64))
        {
            throw new InvalidOperationException(
                "Durable lifecycle aggregate has invalid contributor fingerprints.");
        }
        for (int index = 1; index < ordered.Length; index++)
        {
            if (string.Equals(
                    ordered[index - 1].Key,
                    ordered[index].Key,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Durable lifecycle aggregate has duplicate contributor: "
                    + ordered[index].Key);
            }
        }
        if (requireCurrentFormatSchema
            && (ordered.Length != RequiredAggregateContributorIds.Length
                || !ordered.Select(value => value.Key).SequenceEqual(
                    RequiredAggregateContributorIds,
                    StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Durable lifecycle aggregate does not match the required current-format contributor schema.");
        }

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(AggregateSchemaToken);
        canonical.Append(facilityId.Value);
        canonical.Append(ProductionOutputDestinationId.FromFacility(facilityId).Value);
        canonical.Append(ordered.Length);
        foreach (KeyValuePair<string, string> contributor in ordered)
        {
            canonical.Append(contributor.Key);
            canonical.Append(contributor.Value);
        }
        return canonical.ComputeSha256();
    }

    public static string ProjectAggregateFromSave(
        BuildingInstanceId facilityId,
        ModularFacilityWorldSaveData worldPayload,
        DungeonProductionBillSaveData productionPayload,
        DungeonProductionGenericBillTerminalDrainSaveData
            genericTerminalPayload,
        DungeonCombatEquipmentSaveData equipmentPayload,
        CombatEquipmentMaintenanceSaveData maintenancePayload,
        DungeonCharacterEnvironmentSaveData apparelPayload,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload,
        ProductionPreparedOutputRoutingSaveData routingPayload,
        IBuildingDefinitionLookup buildingDefinitions,
        ProductionOutputBufferCapacityProjector capacityProjector,
        IPhysicalItemMassQuery massQuery)
    {
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> outbox =
            itemPayload?.pendingExactOutputRoutes
            ?? throw new ArgumentNullException(nameof(itemPayload));
        ProductionOutputCapacityDurableProjection capacity =
            ProjectCapacityRoutingFromSave(
                facilityId,
                worldPayload,
                productionPayload,
                genericTerminalPayload,
                itemPayload,
                characterPayload,
                routingPayload,
                outbox,
                buildingDefinitions,
                capacityProjector,
                massQuery);
        KeyValuePair<string, string>[] contributors =
        {
            new(ApparelContributorId, ProjectApparel(facilityId, apparelPayload)),
            new(CapacityRoutingContributorId, capacity.Fingerprint),
            new(EquipmentContributorId, ProjectEquipment(
                facilityId,
                equipmentPayload,
                maintenancePayload)),
            new(GenericBillsContributorId, ProjectGenericBills(facilityId, productionPayload)),
            new(PhysicalCustodyContributorId, ProjectPhysicalCustody(
                facilityId,
                itemPayload,
                characterPayload)),
            new(StockSensorContributorId, ProjectStockSensor(
                facilityId,
                productionPayload,
                itemPayload,
                characterPayload))
        };
        return ComposeAggregate(facilityId, contributors);
    }

    public static string ProjectAbsentFacilityAggregateFromSave(
        BuildingInstanceId facilityId,
        ModularFacilityWorldSaveData worldPayload,
        DungeonProductionBillSaveData productionPayload,
        DungeonCombatEquipmentSaveData equipmentPayload,
        CombatEquipmentMaintenanceSaveData maintenancePayload,
        DungeonCharacterEnvironmentSaveData apparelPayload,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload,
        ProductionPreparedOutputRoutingSaveData routingPayload)
    {
        RequireFacility(facilityId);
        if (worldPayload?.buildings == null)
            throw new ArgumentNullException(nameof(worldPayload));
        if (worldPayload.buildings.Count(value => value != null
                && string.Equals(
                    value.persistentInstanceId,
                    facilityId.Value,
                    StringComparison.Ordinal)) != 0)
        {
            throw new InvalidOperationException(
                "Absent production lifecycle still has a facility: "
                + facilityId.Value);
        }
        RequireAbsentLifecycleSources(
            facilityId,
            productionPayload,
            equipmentPayload,
            maintenancePayload,
            apparelPayload,
            itemPayload,
            characterPayload,
            routingPayload);

        FacilityBufferPhysicalOccupancySnapshot occupancy = new(0L, 0L);
        string capacity = ProjectCapacityRouting(
            facilityId,
            null,
            occupancy,
            routingPayload,
            itemPayload.pendingExactOutputRoutes);
        KeyValuePair<string, string>[] contributors =
        {
            new(ApparelContributorId, ProjectApparel(facilityId, apparelPayload)),
            new(CapacityRoutingContributorId, capacity),
            new(EquipmentContributorId, ProjectEquipment(
                facilityId,
                equipmentPayload,
                maintenancePayload)),
            new(GenericBillsContributorId, ProjectGenericBills(facilityId, productionPayload)),
            new(PhysicalCustodyContributorId, ProjectPhysicalCustody(
                facilityId,
                itemPayload,
                characterPayload)),
            new(StockSensorContributorId, ProjectStockSensor(
                facilityId,
                productionPayload,
                itemPayload,
                characterPayload))
        };
        return ComposeAggregate(facilityId, contributors);
    }

    private static void RequireAbsentLifecycleSources(
        BuildingInstanceId facilityId,
        DungeonProductionBillSaveData productionPayload,
        DungeonCombatEquipmentSaveData equipmentPayload,
        CombatEquipmentMaintenanceSaveData maintenancePayload,
        DungeonCharacterEnvironmentSaveData apparelPayload,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload,
        ProductionPreparedOutputRoutingSaveData routingPayload)
    {
        string destination = ProductionOutputDestinationId
            .FromFacility(facilityId).Value;
        if (productionPayload?.bills == null
            || productionPayload.installedStockSensorFacilityIds == null
            || productionPayload.acknowledgedStockSensorFacilityIds == null
            || productionPayload.pendingStockSensorInstalls == null
            || productionPayload.installedStockSensors == null
            || productionPayload.pendingStockSensorRemovals == null
            || equipmentPayload?.craftOrders == null
            || maintenancePayload?.orders == null
            || apparelPayload?.apparelWorkOrders == null
            || itemPayload?.stacks == null
            || itemPayload.pendingExactOutputRoutes == null
            || characterPayload?.actors == null
            || routingPayload?.batches == null)
        {
            throw new InvalidOperationException(
                "Absent production lifecycle requires all current-format source collections.");
        }
        bool hasBill = productionPayload.bills.Any(value => value != null
            && string.Equals(
                value.buildingInstanceId,
                facilityId.Value,
                StringComparison.Ordinal));
        bool hasEquipment = equipmentPayload.craftOrders.Any(value => value != null
            && string.Equals(
                value.facilityPersistentId,
                facilityId.Value,
                StringComparison.Ordinal));
        bool hasRepair = maintenancePayload.orders.Any(value => value != null
            && value.state is not CombatEquipmentRepairOrderState.Completed
                and not CombatEquipmentRepairOrderState.Cancelled
            && string.Equals(
                value.facilityBuildingId,
                facilityId.Value,
                StringComparison.Ordinal));
        bool hasApparel = apparelPayload.apparelWorkOrders.Any(value => value != null
            && value.state != ApparelWorkOrderState.Completed
            && string.Equals(
                value.facilityInstanceId,
                facilityId.Value,
                StringComparison.Ordinal));
        bool hasRouting = routingPayload.batches.Any(value => value != null
                && string.Equals(
                    value.ownerFacilityId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            || itemPayload.pendingExactOutputRoutes.Any(value => value != null
                && string.Equals(
                    value.sourceDestinationId,
                    destination,
                    StringComparison.Ordinal));
        bool hasPhysical = itemPayload.stacks.Any(stack =>
        {
            if (stack == null || stack.quantity <= 0)
                return false;
            if (string.Equals(
                    stack.destinationId,
                    destination,
                    StringComparison.Ordinal))
                return true;
            return FacilityOutputExactRouteCustodyCodec.TryRead(
                    stack.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                && string.Equals(
                    custody.OriginDestinationId,
                    destination,
                    StringComparison.Ordinal);
        });
        bool hasCarriedIntent = characterPayload.actors.Any(actor =>
            actor?.haulDeliveryIntent != null
            && !actor.haulDeliveryIntent.IsDefaultEmptyProjection
            && string.Equals(
                actor.haulDeliveryIntent.destinationId,
                destination,
                StringComparison.Ordinal));
        bool hasStockSensorPhysical = CaptureExactDestinationCustody(
            ProductionStockSensorRuntime.BuildDestinationId(facilityId.Value),
            itemPayload,
            characterPayload).HasAuthority;
        bool hasActiveStockSensor =
            productionPayload.installedStockSensorFacilityIds.Any(value =>
                string.Equals(value, facilityId.Value, StringComparison.Ordinal))
            || productionPayload.acknowledgedStockSensorFacilityIds.Any(value =>
                string.Equals(value, facilityId.Value, StringComparison.Ordinal))
            || productionPayload.pendingStockSensorInstalls.Any(value =>
                value != null
                && string.Equals(
                    value.facilityId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            || productionPayload.installedStockSensors.Any(value =>
                value != null
                && string.Equals(
                    value.facilityId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            || productionPayload.pendingStockSensorRemovals.Any(value =>
                value != null
                && value.phase != ProductionStockSensorRemovalPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                && string.Equals(
                    value.facilityId,
                    facilityId.Value,
                    StringComparison.Ordinal));
        if (hasBill || hasEquipment || hasRepair || hasApparel || hasRouting
            || hasPhysical || hasCarriedIntent || hasActiveStockSensor
            || hasStockSensorPhysical)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-absent-lifecycle-has-owner: "
                + facilityId.Value);
        }
    }

    public static string ProjectEquipment(
        BuildingInstanceId facilityId,
        DungeonCombatEquipmentSaveData payload,
        CombatEquipmentMaintenanceSaveData maintenancePayload)
    {
        RequireFacility(facilityId);
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (payload.craftOrders == null)
            throw new InvalidOperationException(
                "Combat equipment save payload has no current-format craft-order collection.");
        if (maintenancePayload?.orders == null)
        {
            throw new InvalidOperationException(
                "Equipment maintenance save payload has no current-format repair-order collection.");
        }

        CombatEquipmentCraftOrderSaveData[] craftOrders = payload.craftOrders
            .Where(value => value != null
                && string.Equals(
                    value.facilityPersistentId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        CombatEquipmentRepairOrder[] repairOrders = maintenancePayload.orders
            .Where(value => value != null
                && value.state is not CombatEquipmentRepairOrderState.Completed
                    and not CombatEquipmentRepairOrderState.Cancelled
                && string.Equals(
                    value.facilityBuildingId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        RequireUniqueOrderIds(
            craftOrders.Select(value => value.orderId),
            "combat equipment craft");
        RequireUniqueOrderIds(
            repairOrders.Select(value => value.orderId),
            "equipment maintenance repair");

        StringBuilder canonical = new StringBuilder(128)
            .Append(EquipmentContributorId).Append('|')
            .Append(facilityId.Value).Append('|');
        foreach (CombatEquipmentCraftOrderSaveData order in craftOrders)
        {
            canonical.Append("craft|");
            ProductionLifecycleFingerprint.AppendSaveRecord(canonical, order);
        }
        foreach (CombatEquipmentRepairOrder order in repairOrders)
        {
            canonical.Append("repair|");
            ProductionLifecycleFingerprint.AppendSaveRecord(canonical, order);
        }
        return ProductionLifecycleFingerprint.Compute(canonical.ToString());
    }

    private static void RequireUniqueOrderIds(
        IEnumerable<string> orderIds,
        string sourceKind)
    {
        string[] ordered = (orderIds
                ?? throw new ArgumentNullException(nameof(orderIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (string.Equals(
                    ordered[index - 1],
                    ordered[index],
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Duplicate " + sourceKind + " order identity: "
                    + ordered[index]);
            }
        }
    }

    public static string ProjectApparel(
        BuildingInstanceId facilityId,
        DungeonCharacterEnvironmentSaveData payload)
    {
        RequireFacility(facilityId);
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (payload.apparelWorkOrders == null)
            throw new InvalidOperationException(
                "Character environment save payload has no current-format apparel work-order collection.");

        StringBuilder canonical = new StringBuilder(128)
            .Append(ApparelContributorId).Append('|')
            .Append(facilityId.Value).Append('|');
        AppendOrdered(
            canonical,
            payload.apparelWorkOrders
                .Where(value => value != null
                    && value.state != ApparelWorkOrderState.Completed
                    && string.Equals(
                        value.facilityInstanceId,
                        facilityId.Value,
                        StringComparison.Ordinal))
                .OrderBy(value => value.orderId, StringComparer.Ordinal));
        return ProductionLifecycleFingerprint.Compute(canonical.ToString());
    }

    public static string ProjectPhysicalCustody(
        BuildingInstanceId facilityId,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload)
    {
        RequireFacility(facilityId);
        if (itemPayload == null)
            throw new ArgumentNullException(nameof(itemPayload));
        if (characterPayload == null)
            throw new ArgumentNullException(nameof(characterPayload));
        if (itemPayload.stacks == null || characterPayload.actors == null)
        {
            throw new InvalidOperationException(
                "Physical custody projection requires current-format stack and actor collections.");
        }

        string destination = ProductionOutputDestinationId
            .FromFacility(facilityId).Value;
        Dictionary<string, WorldItemStackSaveData> stackById = itemPayload.stacks
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToDictionary(value => value.stackId, StringComparer.Ordinal);
        HashSet<string> custodyStackIds = new(StringComparer.Ordinal);
        HashSet<string> committedStackOwners = new(StringComparer.Ordinal);
        StringBuilder canonical = new StringBuilder(192)
            .Append(PhysicalCustodyContributorId).Append('|')
            .Append(destination).Append('|');
        foreach (WorldItemStackSaveData stack in stackById.Values)
        {
            bool originBuffered = stack.state ==
                    WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    stack.destinationId,
                    destination,
                    StringComparison.Ordinal);
            bool hasCustody = FacilityOutputExactRouteCustodyCodec.TryRead(
                    stack.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                && string.Equals(
                    custody.OriginDestinationId,
                    destination,
                    StringComparison.Ordinal);
            if (!originBuffered && !hasCustody)
                continue;
            if (hasCustody)
                custodyStackIds.Add(stack.stackId);
            ProductionLifecycleFingerprint.AppendSaveRecord(
                canonical,
                CanonicalizePhysicalStack(stack));
        }

        DungeonCharacterSaveData[] actors = characterPayload.actors
            .Where(value => value?.haulDeliveryIntent != null
                && !value.haulDeliveryIntent.IsDefaultEmptyProjection)
            .OrderBy(value => value.haulDeliveryIntent.operationId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < actors.Length; index++)
        {
            DungeonCharacterSaveData actor = actors[index];
            HaulDeliveryIntentSaveData intent = actor.haulDeliveryIntent;
            if (!string.Equals(
                    actor.persistentId,
                    intent.ownerCharacterId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Saved haul intent owner does not match its character.");
            }
            if (intent.commitments == null)
            {
                throw new InvalidOperationException(
                    "Saved haul intent has no current-format commitment collection: "
                    + intent.operationId);
            }
            HaulDeliveryItemCommitmentSaveData[] matching =
                intent.commitments
                .Where(value => value != null
                    && custodyStackIds.Contains(
                        value.carriedStackId ?? string.Empty))
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .ToArray();
            if (matching.Length > 0)
            {
                ProductionLifecycleFingerprint.AppendSaveRecord(
                    canonical,
                    CanonicalizeHaulIntent(intent, matching));
                for (int carriedIndex = 0; carriedIndex < matching.Length; carriedIndex++)
                {
                    HaulDeliveryItemCommitmentSaveData commitment = matching[carriedIndex];
                    if (!committedStackOwners.Add(
                            commitment.carriedStackId ?? string.Empty))
                    {
                        throw new InvalidOperationException(
                            "A production-output custody stack is owned by more than one saved haul commitment: "
                            + commitment.carriedStackId);
                    }
                    if (!stackById.TryGetValue(
                            commitment.carriedStackId ?? string.Empty,
                            out WorldItemStackSaveData stack))
                    {
                        throw new InvalidOperationException(
                            "Production-output custody commitment has no physical stack: "
                            + commitment.carriedStackId);
                    }
                    CharacterCarriedItemSaveData carried =
                        RequireExactCarriedInventoryJoin(
                            actor,
                            intent,
                            commitment,
                            stack);
                    ProductionLifecycleFingerprint.AppendSaveRecord(
                        canonical,
                        CanonicalizeCarriedItem(carried));
                }
            }
        }
        return ProductionLifecycleFingerprint.Compute(canonical.ToString());
    }

    public static string ProjectRoutingOutbox(
        BuildingInstanceId facilityId,
        ProductionPreparedOutputRoutingSaveData routingPayload,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> outboxPayload)
    {
        RequireFacility(facilityId);
        if (routingPayload == null)
            throw new ArgumentNullException(nameof(routingPayload));
        if (routingPayload.batches == null)
        {
            throw new InvalidOperationException(
                "Prepared-output routing save payload has no current-format batch collection.");
        }

        string destination = ProductionOutputDestinationId
            .FromFacility(facilityId).Value;
        StringBuilder canonical = new StringBuilder(256)
            .Append(CapacityRoutingContributorId).Append('|')
            .Append(facilityId.Value).Append('|');

        ProductionPreparedOutputRoutingBatchSaveData[] batches = routingPayload
            .batches
            .Where(value => value != null
                && string.Equals(
                    value.ownerFacilityId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            .Select(CanonicalizeRoutingBatch)
            .OrderBy(value => value.batchCommitId, StringComparer.Ordinal)
            .ToArray();
        AppendOrdered(canonical, batches);

        FacilityOutputExactRouteOutboxSaveData[] routes = (outboxPayload
                ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.sourceDestinationId,
                    destination,
                    StringComparison.Ordinal))
            .Select(CanonicalizeOutboxRoute)
            .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
            .ToArray();
        AppendOrdered(canonical, routes);

        return ProductionLifecycleFingerprint.Compute(canonical.ToString());
    }

    public static string ProjectCapacityRouting(
        BuildingInstanceId facilityId,
        FacilityBufferCapacityProfile profile,
        FacilityBufferPhysicalOccupancySnapshot occupancy,
        ProductionPreparedOutputRoutingSaveData routingPayload,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> outboxPayload)
    {
        RequireFacility(facilityId);
        string destination = ProductionOutputDestinationId
            .FromFacility(facilityId).Value;
        if (profile != null
            && (!string.Equals(profile.DestinationId, destination, StringComparison.Ordinal)
                || !string.Equals(
                    profile.OwnerDomain,
                    ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                    StringComparison.Ordinal)
                || !string.Equals(profile.OwnerOperationId, destination, StringComparison.Ordinal)
                || !string.Equals(profile.OwnerFacilityId, facilityId.Value, StringComparison.Ordinal)
                || profile.CapacityRevision !=
                    ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision))
        {
            throw new InvalidOperationException(
                "Production output capacity profile has invalid durable semantics: "
                + destination);
        }
        if (profile == null && occupancy.TotalMassGrams > 0L)
        {
            throw new InvalidOperationException(
                "Production output occupancy has no capacity authority: " + destination);
        }
        if (profile != null && occupancy.TotalMassGrams > profile.MaxMassGrams)
        {
            throw new InvalidOperationException(
                "Production output occupancy exceeds durable capacity: " + destination);
        }

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(AggregateSchemaToken);
        canonical.Append(CapacityRoutingContributorId);
        canonical.Append(facilityId.Value);
        canonical.Append(profile != null);
        if (profile != null)
        {
            canonical.Append(profile.DestinationId);
            canonical.Append(profile.DropPosition.x);
            canonical.Append(profile.DropPosition.y);
            canonical.Append(profile.OwnerDomain);
            canonical.Append(profile.OwnerOperationId);
            canonical.Append(profile.OwnerFacilityId);
            canonical.Append(profile.MaxMassGrams);
            canonical.Append(profile.CapacityRevision);
        }
        canonical.Append(occupancy.NonCarriedMassGrams);
        canonical.Append(occupancy.CommittedCarriedMassGrams);
        canonical.Append(ProjectRoutingOutbox(
            facilityId,
            routingPayload,
            outboxPayload));
        return canonical.ComputeSha256();
    }

    public static ProductionOutputCapacityDurableProjection
        ProjectCapacityRoutingFromSave(
        BuildingInstanceId facilityId,
        ModularFacilityWorldSaveData worldPayload,
        DungeonProductionBillSaveData productionPayload,
        DungeonProductionGenericBillTerminalDrainSaveData
            genericTerminalPayload,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload,
        ProductionPreparedOutputRoutingSaveData routingPayload,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> outboxPayload,
        IBuildingDefinitionLookup buildingDefinitions,
        ProductionOutputBufferCapacityProjector capacityProjector,
        IPhysicalItemMassQuery massQuery)
    {
        RequireFacility(facilityId);
        if (worldPayload?.buildings == null)
            throw new ArgumentNullException(nameof(worldPayload));
        if (productionPayload?.bills == null)
            throw new ArgumentNullException(nameof(productionPayload));
        ProductionGenericBillTerminalDrainSaveData[] terminalSources =
            CaptureCurrentTerminalSources(genericTerminalPayload);
        if (itemPayload?.stacks == null)
            throw new ArgumentNullException(nameof(itemPayload));
        if (characterPayload?.actors == null)
            throw new ArgumentNullException(nameof(characterPayload));
        if (routingPayload == null)
            throw new ArgumentNullException(nameof(routingPayload));
        if (buildingDefinitions == null)
            throw new ArgumentNullException(nameof(buildingDefinitions));
        if (capacityProjector == null)
            throw new ArgumentNullException(nameof(capacityProjector));
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));

        ModularFacilityBuildingSaveData[] matches = worldPayload.buildings
            .Where(value => value != null
                && string.Equals(
                    value.persistentInstanceId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Production capacity save projection requires exactly one facility: "
                + facilityId.Value);
        }
        ProductionFacilityCapacitySubject subject =
            ProductionFacilityCapacitySubjectAdapter.FromSave(
                matches[0],
                buildingDefinitions);
        ProductionOutputBufferCapacitySourceSnapshot portfolio =
            capacityProjector.CapturePortfolioSource(subject);
        long maximumCapacity = portfolio.ProjectedPortfolioCapacityGrams;
        foreach (ProductionBillSaveData bill in productionPayload.bills
                     .Where(value => value != null
                         && string.Equals(
                             value.buildingInstanceId,
                             facilityId.Value,
                             StringComparison.Ordinal)
                         && value.preparedOutput != null
                         && value.preparedOutput.phase !=
                            ProductionPreparedOutputPhase.Unresolved)
                     .OrderBy(value => value.billId, StringComparer.Ordinal))
        {
            ProductionPreparedOutputBatchSaveData prepared =
                bill.preparedOutput;
            ProductionPreparedOutputLineSaveData ruinedWaste = prepared.lines?
                .SingleOrDefault(value => value != null
                    && value.role == ProductionOutputRole.RecoverableWaste
                    && string.Equals(
                        value.outputLineId,
                        ProductionRuinedBatchDispositionPlan
                            .RecoverableWasteOutputLineId,
                        StringComparison.Ordinal));
            ProductionOutputBufferCapacitySourceSnapshot current;
            if (ruinedWaste != null)
            {
                ProductionOutputCapabilityDescriptor descriptor = new(
                    ruinedWaste.outputLineId,
                    ruinedWaste.itemId,
                    ruinedWaste.outputCapabilityId,
                    ruinedWaste.outputCapabilityVersion,
                    ruinedWaste.outputComponentCodecId,
                    ruinedWaste.outputComponentCodecVersion,
                    ruinedWaste.outputCapabilityFingerprint);
                ProductionRuinedOutputCapacityClaim claim = capacityProjector
                    .CaptureRuinedClaim(bill, descriptor);
                if (!string.Equals(
                        prepared.maximumMassProofDigest,
                        claim.MaximumMassProof.SourceDigest,
                        StringComparison.Ordinal)
                    || prepared.maximumBatchMassGrams
                        != claim.MaximumMassProof.MaximumBatchMassGrams
                    || !string.Equals(
                        prepared.capacityClaimDigest,
                        claim.SourceDigest,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Detached production capacity bill '"
                        + bill.billId
                        + "' has a stale ruined-output capacity proof.");
                }
                current = capacityProjector.CaptureSource(subject, claim);
            }
            else
            {
                ProductionPreparedOutputCapacityClaim claim = capacityProjector
                    .CapturePreparedClaim(prepared);
                if (!string.Equals(
                        prepared.maximumMassProofDigest,
                        claim.MaximumMassProof.SourceDigest,
                        StringComparison.Ordinal)
                    || prepared.maximumBatchMassGrams
                        != claim.MaximumMassProof.MaximumBatchMassGrams
                    || !string.Equals(
                        prepared.capacityClaimDigest,
                        claim.SourceDigest,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Detached production capacity bill '"
                        + bill.billId
                        + "' has a stale maximum-mass proof.");
                }
                current = capacityProjector.CaptureSource(subject, claim);
            }
            ProductionOutputBufferCapacitySourceGuard.ValidateSaved(
                prepared,
                current,
                "Detached production capacity bill '" + bill.billId + "'");
            maximumCapacity = Math.Max(
                maximumCapacity,
                current.RequiredMinimumCapacityGrams);
        }
        // A destructive generic-bill terminalization may remove the bill while
        // its independently owned routing batch remains. An exact terminal
        // source is reprojected from its frozen bill; unrelated routing batches
        // keep their routing-owned durability contract.
        foreach (ProductionPreparedOutputRoutingBatchSaveData batch in
                 (routingPayload.batches
                      ?? new List<
                          ProductionPreparedOutputRoutingBatchSaveData>())
                 .Where(value => value != null
                     && string.Equals(
                         value.ownerFacilityId,
                         facilityId.Value,
                         StringComparison.Ordinal))
                 .OrderBy(value => value.batchCommitId, StringComparer.Ordinal))
        {
            ProductionGenericBillTerminalDrainSaveData[] sourceMatches =
                terminalSources
                    .Where(value => string.Equals(
                            value.billId,
                            batch.ownerBillId,
                            StringComparison.Ordinal)
                        && value.sourceBill.cycleSequence ==
                            batch.cycleSequence)
                    .ToArray();
            if (sourceMatches.Length > 1)
            {
                throw new InvalidOperationException(
                    "detached-terminal-routing-source-duplicate:"
                    + (batch.batchCommitId ?? string.Empty));
            }
            int liveBillCount = productionPayload.bills.Count(value =>
                value != null
                && string.Equals(
                    value.billId,
                    batch.ownerBillId,
                    StringComparison.Ordinal));
            if (liveBillCount > 1)
            {
                throw new InvalidOperationException(
                    "detached-terminal-routing-live-source-duplicate:"
                    + (batch.ownerBillId ?? string.Empty));
            }
            if (sourceMatches.Length == 1 && liveBillCount == 0)
            {
                ProductionGenericBillTerminalDrainSaveData terminal =
                    sourceMatches[0];
                if (terminal.phase <
                    ProductionGenericBillTerminalDrainPhase
                        .InputDestinationAcknowledgedAwaitingBillTerminal)
                {
                    throw new InvalidOperationException(
                        "detached-terminal-routing-source-ineligible:"
                        + (batch.batchCommitId ?? string.Empty));
                }
                ProductionOutputBufferCapacitySourceSnapshot terminalSource =
                    ReprojectTerminalRoutingSource(
                        subject,
                        batch,
                        terminal,
                        capacityProjector);
                maximumCapacity = Math.Max(
                    maximumCapacity,
                    terminalSource.RequiredMinimumCapacityGrams);
                continue;
            }

            long batchPhysicalMass = checked((batch.lines
                    ?? new List<ProductionPreparedOutputRoutingLineSaveData>())
                .Where(line => line != null
                    && ProductionOutputRoleRules.IsPhysical(line.role))
                .Sum(line => line.originalMassGrams));
            bool hasProof = IsSha256(batch.maximumMassProofDigest)
                && batch.maximumBatchMassGrams > 0L
                && IsSha256(batch.capacityClaimDigest);
            bool hasNoProof = batch.maximumMassProofDigest != null
                && batch.maximumMassProofDigest.Length == 0
                && batch.maximumBatchMassGrams == 0L
                && batch.capacityClaimDigest != null
                && batch.capacityClaimDigest.Length == 0;
            long capacityBatchMass = hasProof
                ? batch.maximumBatchMassGrams
                : batchPhysicalMass;
            if (!IsSha256(batch.capacitySourceDigest)
                || batch.outputBufferCycleCapacity is < 2 or > 4
                || batch.projectedPortfolioCapacityGrams <= 0L
                || !hasProof && !hasNoProof
                || hasProof && batchPhysicalMass > batch.maximumBatchMassGrams
                || batch.requiredMinimumCapacityGrams != Math.Max(
                    batch.projectedPortfolioCapacityGrams,
                    checked(
                        capacityBatchMass
                        * batch.outputBufferCycleCapacity)))
            {
                throw new InvalidOperationException(
                    "Detached terminal routing batch has invalid capacity authority: "
                    + (batch.batchCommitId ?? string.Empty));
            }
            maximumCapacity = Math.Max(
                maximumCapacity,
                batch.requiredMinimumCapacityGrams);
        }

        FacilityBufferPhysicalOccupancySnapshot occupancy =
            ProjectPhysicalOccupancy(
                ProductionOutputDestinationId.FromFacility(facilityId).Value,
                itemPayload,
                characterPayload,
                massQuery);
        FacilityBufferCapacityProfile profile = maximumCapacity == 0L
            ? null
            : new FacilityBufferCapacityProfile(
                ProductionOutputDestinationId.FromFacility(facilityId).Value,
                subject.Position,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                ProductionOutputDestinationId.FromFacility(facilityId).Value,
                facilityId.Value,
                new PhysicalMassGrams(maximumCapacity),
                ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);
        string fingerprint = ProjectCapacityRouting(
            facilityId,
            profile,
            occupancy,
            routingPayload,
            outboxPayload);
        return new ProductionOutputCapacityDurableProjection(
            subject,
            portfolio,
            profile,
            occupancy,
            fingerprint);
    }

    private static ProductionGenericBillTerminalDrainSaveData[]
        CaptureCurrentTerminalSources(
            DungeonProductionGenericBillTerminalDrainSaveData payload)
    {
        if (payload == null
            || payload.version !=
                DungeonProductionGenericBillTerminalDrainSaveData
                    .CurrentVersion
            || payload.entries == null)
        {
            throw new InvalidOperationException(
                "Detached capacity projection requires the exact current-format generic terminal payload.");
        }
        ProductionGenericBillTerminalDrainSaveData[] entries = payload.entries
            .OrderBy(value => value?.billId ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        if (entries.Length > 4096
            || entries.Any(value =>
                !ProductionGenericBillTerminalDrainCanonical.IsValidSave(value)))
        {
            throw new InvalidOperationException(
                "Detached capacity projection found an invalid generic terminal source.");
        }
        if (entries.GroupBy(value => value.billId, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Detached capacity projection found duplicate generic terminal bill authority.");
        }
        return entries;
    }

    private static ProductionOutputBufferCapacitySourceSnapshot
        ReprojectTerminalRoutingSource(
            ProductionFacilityCapacitySubject subject,
            ProductionPreparedOutputRoutingBatchSaveData batch,
            ProductionGenericBillTerminalDrainSaveData terminal,
            ProductionOutputBufferCapacityProjector capacityProjector)
    {
        ProductionBillSaveData source = terminal?.sourceBill;
        if (source == null
            || !string.Equals(
                terminal.sourceBillFingerprint,
                ProductionGenericBillTerminalDrainCanonical
                    .CreateSourceBillFingerprint(source),
                StringComparison.Ordinal)
            || !string.Equals(batch.ownerBillId, source.billId,
                StringComparison.Ordinal)
            || !string.Equals(batch.ownerRecipeId, source.recipeId,
                StringComparison.Ordinal)
            || !string.Equals(batch.ownerFacilityId,
                source.buildingInstanceId, StringComparison.Ordinal)
            || !string.Equals(batch.ownerFacilityId,
                subject.FacilityId.Value, StringComparison.Ordinal)
            || batch.cycleSequence != source.cycleSequence
            || !string.Equals(batch.destinationId,
                source.outputDestinationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "detached-terminal-routing-source-identity-drift:"
                + (batch?.batchCommitId ?? string.Empty));
        }

        ProductionPreparedOutputBatchSaveData prepared = source.preparedOutput;
        try
        {
            ProductionPreparedOutputContract.ValidateForBill(
                prepared,
                (ProductionBillId)source.billId,
                source.recipeId,
                source.cycleSequence,
                source.outputDestinationId);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "detached-terminal-routing-prepared-join-drift:"
                + batch.batchCommitId,
                exception);
        }
        if (prepared.phase != ProductionPreparedOutputPhase.Completed
            || !string.Equals(batch.batchCommitId,
                prepared.batchCommitId, StringComparison.Ordinal)
            || !string.Equals(batch.outcomeFingerprint,
                prepared.outcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(batch.destinationId,
                prepared.destinationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "detached-terminal-routing-prepared-join-drift:"
                + batch.batchCommitId);
        }
        ValidateTerminalRoutingLines(batch, prepared);

        ProductionPreparedOutputLineSaveData[] ruinedLines = prepared.lines
            .Where(value => value != null
                && value.role == ProductionOutputRole.RecoverableWaste
                && string.Equals(
                    value.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal))
            .ToArray();
        ProductionOutputBufferCapacitySourceSnapshot current;
        if (ruinedLines.Length == 1)
        {
            ProductionPreparedOutputLineSaveData ruined = ruinedLines[0];
            ProductionOutputCapabilityDescriptor descriptor = new(
                ruined.outputLineId,
                ruined.itemId,
                ruined.outputCapabilityId,
                ruined.outputCapabilityVersion,
                ruined.outputComponentCodecId,
                ruined.outputComponentCodecVersion,
                ruined.outputCapabilityFingerprint);
            ProductionRuinedOutputCapacityClaim claim = capacityProjector
                .CaptureRuinedClaim(source, descriptor);
            if (!string.Equals(prepared.maximumMassProofDigest,
                    claim.MaximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || prepared.maximumBatchMassGrams !=
                    claim.MaximumMassProof.MaximumBatchMassGrams
                || !string.Equals(prepared.capacityClaimDigest,
                    claim.SourceDigest, StringComparison.Ordinal)
                || prepared.totalPhysicalMassGrams !=
                    claim.Disposition.RecoverableWasteMassGrams
                || prepared.totalDeclaredLossMassGrams !=
                    claim.Disposition.DeclaredLossMassGrams)
            {
                throw new InvalidOperationException(
                    "detached-terminal-routing-proof-stale:"
                    + batch.batchCommitId);
            }
            current = capacityProjector.CaptureSource(subject, claim);
        }
        else if (ruinedLines.Length == 0)
        {
            ProductionPreparedOutputCapacityClaim claim = capacityProjector
                .CapturePreparedClaim(prepared);
            if (!string.Equals(prepared.maximumMassProofDigest,
                    claim.MaximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || prepared.maximumBatchMassGrams !=
                    claim.MaximumMassProof.MaximumBatchMassGrams
                || !string.Equals(prepared.capacityClaimDigest,
                    claim.SourceDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "detached-terminal-routing-proof-stale:"
                    + batch.batchCommitId);
            }
            current = capacityProjector.CaptureSource(subject, claim);
        }
        else
        {
            throw new InvalidOperationException(
                "detached-terminal-routing-prepared-join-drift:"
                + batch.batchCommitId);
        }

        if (!string.Equals(batch.maximumMassProofDigest,
                prepared.maximumMassProofDigest, StringComparison.Ordinal)
            || batch.maximumBatchMassGrams !=
                prepared.maximumBatchMassGrams
            || !string.Equals(batch.capacityClaimDigest,
                prepared.capacityClaimDigest, StringComparison.Ordinal)
            || !string.Equals(batch.capacitySourceDigest,
                current.SourceDigest, StringComparison.Ordinal)
            || batch.outputBufferCycleCapacity != current.CycleCapacity
            || batch.projectedPortfolioCapacityGrams !=
                current.ProjectedPortfolioCapacityGrams
            || batch.requiredMinimumCapacityGrams !=
                current.RequiredMinimumCapacityGrams)
        {
            throw new InvalidOperationException(
                "detached-terminal-routing-capacity-source-stale:"
                + batch.batchCommitId);
        }
        ProductionOutputBufferCapacitySourceGuard.ValidateSaved(
            prepared,
            current,
            "Detached terminal routing batch '" + batch.batchCommitId + "'");
        return current;
    }

    private static void ValidateTerminalRoutingLines(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputBatchSaveData prepared)
    {
        ProductionPreparedOutputLineSaveData[] nonPhysical = prepared.lines
            .Where(value => value != null
                && ProductionOutputRoleRules.IsNonPhysical(value.role))
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToArray();
        ProductionPreparedOutputNonPhysicalDispositionSaveData[] dispositions =
            (batch.nonPhysicalDispositions
                    ?? new List<
                        ProductionPreparedOutputNonPhysicalDispositionSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                .ToArray();
        if (batch.totalDeclaredLossMassGrams !=
                prepared.totalDeclaredLossMassGrams
            || batch.totalDeclaredExternalInputMassGrams !=
                prepared.totalDeclaredExternalInputMassGrams
            || nonPhysical.Length != dispositions.Length)
        {
            throw new InvalidOperationException(
                "detached-terminal-routing-nonphysical-join-drift:"
                + batch.batchCommitId);
        }
        for (int index = 0; index < nonPhysical.Length; index++)
        {
            ProductionPreparedOutputLineSaveData source = nonPhysical[index];
            ProductionPreparedOutputNonPhysicalDispositionSaveData disposition =
                dispositions[index];
            if (!string.Equals(disposition.batchCommitId,
                    batch.batchCommitId, StringComparison.Ordinal)
                || !string.Equals(disposition.lineCommitId,
                    source.lineCommitId, StringComparison.Ordinal)
                || !string.Equals(disposition.outputLineId,
                    source.outputLineId, StringComparison.Ordinal)
                || disposition.role != source.role
                || !string.Equals(disposition.canonicalPayload,
                    source.componentPayload, StringComparison.Ordinal)
                || !string.Equals(disposition.dispositionFingerprint,
                    source.componentFingerprint, StringComparison.Ordinal)
                || disposition.exactMassGrams != source.exactMassGrams)
            {
                throw new InvalidOperationException(
                    "detached-terminal-routing-nonphysical-join-drift:"
                    + batch.batchCommitId + ":" + source.outputLineId);
            }
        }

        ProductionPreparedOutputLineSaveData[] physical = prepared.lines
            .Where(value => value != null
                && ProductionOutputRoleRules.IsPhysical(value.role)
                && value.quantity > 0)
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToArray();
        ProductionPreparedOutputRoutingLineSaveData[] routed = (batch.lines
                ?? new List<ProductionPreparedOutputRoutingLineSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToArray();
        if (physical.Length != routed.Length)
        {
            throw new InvalidOperationException(
                "detached-terminal-routing-prepared-join-drift:"
                + batch.batchCommitId);
        }
        for (int index = 0; index < physical.Length; index++)
        {
            ProductionPreparedOutputLineSaveData source = physical[index];
            ProductionPreparedOutputRoutingLineSaveData route = routed[index];
            string expectedLineCommitId =
                ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batch.batchCommitId,
                    source.outputLineId);
            if (!string.Equals(route.batchCommitId,
                    batch.batchCommitId, StringComparison.Ordinal)
                || !string.Equals(route.lineCommitId,
                    expectedLineCommitId, StringComparison.Ordinal)
                || !string.Equals(route.outputLineId,
                    source.outputLineId, StringComparison.Ordinal)
                || route.role != source.role
                || !string.Equals(route.itemId, source.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(route.destinationId,
                    prepared.destinationId, StringComparison.Ordinal)
                || !string.Equals(route.componentFingerprint,
                    source.componentFingerprint, StringComparison.Ordinal)
                || !string.Equals(route.outputCapabilityId,
                    source.outputCapabilityId, StringComparison.Ordinal)
                || route.outputCapabilityVersion !=
                    source.outputCapabilityVersion
                || !string.Equals(route.outputComponentCodecId,
                    source.outputComponentCodecId, StringComparison.Ordinal)
                || route.outputComponentCodecVersion !=
                    source.outputComponentCodecVersion
                || !string.Equals(route.outputCapabilityFingerprint,
                    source.outputCapabilityFingerprint,
                    StringComparison.Ordinal)
                || route.originalQuantity != source.quantity
                || route.originalMassGrams != source.exactMassGrams)
            {
                throw new InvalidOperationException(
                    "detached-terminal-routing-prepared-join-drift:"
                    + batch.batchCommitId + ":" + source.outputLineId);
            }
        }
    }

    internal static ProductionExactDestinationCustodyProjection
        CaptureExactDestinationCustody(
            string destinationId,
            DungeonPhysicalItemSaveData itemPayload,
            DungeonCharacterWorldSaveData characterPayload)
    {
        if (string.IsNullOrWhiteSpace(destinationId)
            || !string.Equals(
                destinationId,
                destinationId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical destination is required.",
                nameof(destinationId));
        }
        if (itemPayload?.stacks == null)
            throw new ArgumentNullException(nameof(itemPayload));
        if (characterPayload?.actors == null)
            throw new ArgumentNullException(nameof(characterPayload));

        Dictionary<string, WorldItemStackSaveData> stacks = itemPayload.stacks
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToDictionary(value => value.stackId, StringComparer.Ordinal);
        WorldItemStackSaveData[] direct = stacks.Values
            .Where(value => value.state != WorldItemStackState.Carried
                && string.Equals(
                    value.destinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .Select(CanonicalizePhysicalStack)
            .ToArray();
        List<HaulDeliveryIntentSaveData> intents = new();
        List<WorldItemStackSaveData> carriedStacks = new();
        List<CharacterCarriedItemSaveData> carriedItems = new();
        HashSet<string> committedStackOwners = new(StringComparer.Ordinal);

        foreach (DungeonCharacterSaveData actor in characterPayload.actors
                     .Where(value => value?.haulDeliveryIntent != null
                         && !value.haulDeliveryIntent.IsDefaultEmptyProjection
                         && string.Equals(
                             value.haulDeliveryIntent.destinationId,
                             destinationId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.haulDeliveryIntent.operationId,
                         StringComparer.Ordinal))
        {
            HaulDeliveryIntentSaveData intent = actor.haulDeliveryIntent;
            if (!string.Equals(
                    actor.persistentId,
                    intent.ownerCharacterId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Saved haul intent owner does not match its character.");
            }
            if (intent.commitments == null)
            {
                throw new InvalidOperationException(
                    "Saved haul intent has no current-format commitment collection: "
                    + intent.operationId);
            }

            HaulDeliveryItemCommitmentSaveData[] matching = intent.commitments
                .Where(value => value != null)
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .ToArray();
            intents.Add(CanonicalizeHaulIntent(intent, matching));
            foreach (HaulDeliveryItemCommitmentSaveData commitment in matching)
            {
                if (!committedStackOwners.Add(
                        commitment.carriedStackId ?? string.Empty))
                {
                    throw new InvalidOperationException(
                        "A carried stack is owned by more than one saved haul commitment: "
                        + commitment.carriedStackId);
                }
                if (!stacks.TryGetValue(
                        commitment.carriedStackId ?? string.Empty,
                        out WorldItemStackSaveData stack))
                {
                    throw new InvalidOperationException(
                        "Saved haul commitment has no physical carried stack: "
                        + commitment.carriedStackId);
                }

                CharacterCarriedItemSaveData carried =
                    RequireExactCarriedInventoryJoin(
                        actor,
                        intent,
                        commitment,
                        stack);
                if (stack.state != WorldItemStackState.Carried)
                {
                    if (stack.state == WorldItemStackState.FacilityBuffer
                        && string.Equals(
                            stack.destinationId,
                            destinationId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    throw new InvalidOperationException(
                        "Committed carried lot has invalid saved state: "
                        + commitment.carriedStackId);
                }
                if (stack.quantity != commitment.quantity
                    || !string.Equals(
                        stack.destinationId,
                        intent.ownerCharacterId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        stack.itemId,
                        commitment.itemId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Committed carried lot conflicts with saved occupancy: "
                        + commitment.carriedStackId);
                }
                carriedStacks.Add(CanonicalizePhysicalStack(stack));
                carriedItems.Add(CanonicalizeCarriedItem(carried));
            }
        }

        return new ProductionExactDestinationCustodyProjection(
            Array.AsReadOnly(direct),
            Array.AsReadOnly(intents
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .ToArray()),
            Array.AsReadOnly(carriedStacks
                .OrderBy(value => value.stackId, StringComparer.Ordinal)
                .ToArray()),
            Array.AsReadOnly(carriedItems
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .ToArray()));
    }

    public static FacilityBufferPhysicalOccupancySnapshot ProjectPhysicalOccupancy(
        string destinationId,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload,
        IPhysicalItemMassQuery massQuery)
    {
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));
        ProductionExactDestinationCustodyProjection custody =
            CaptureExactDestinationCustody(
                destinationId,
                itemPayload,
                characterPayload);
        long nonCarried = 0L;
        foreach (WorldItemStackSaveData stack in custody.DirectStacks)
        {
            nonCarried = checked(nonCarried + GetStackMass(stack, stack.quantity, massQuery));
        }
        long carried = 0L;
        foreach (WorldItemStackSaveData stack in custody.CarriedStacks)
        {
            carried = checked(
                carried + GetStackMass(stack, stack.quantity, massQuery));
        }
        return new FacilityBufferPhysicalOccupancySnapshot(nonCarried, carried);
    }

    private static CharacterCarriedItemSaveData RequireExactCarriedInventoryJoin(
        DungeonCharacterSaveData actor,
        HaulDeliveryIntentSaveData intent,
        HaulDeliveryItemCommitmentSaveData commitment,
        WorldItemStackSaveData stack)
    {
        if (actor?.carryInventory?.items == null)
        {
            throw new InvalidOperationException(
                "Saved haul commitment has no current-format carry inventory: "
                + intent?.operationId);
        }
        CharacterCarriedItemSaveData[] matches = actor.carryInventory.items
            .Where(value => value != null
                && string.Equals(
                    value.carriedStackId,
                    commitment.carriedStackId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Saved haul commitment does not have exactly one carried-inventory row: "
                + commitment.carriedStackId);
        }

        CharacterCarriedItemSaveData carried = matches[0];
        string stackSignature = ItemReservationSignature.Create(
            stack.itemId,
            stack.components);
        if (!string.Equals(
                carried.ownerOperationId,
                intent.operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                carried.sourceStackId,
                commitment.sourceStackId,
                StringComparison.Ordinal)
            || !string.Equals(
                carried.itemId,
                commitment.itemId,
                StringComparison.Ordinal)
            || !string.Equals(
                carried.itemInstanceId,
                stack.itemInstanceId,
                StringComparison.Ordinal)
            || carried.quantity != commitment.quantity
            || !string.Equals(
                stackSignature,
                commitment.expectedStackSignature,
                StringComparison.Ordinal)
            || !string.Equals(
                ItemReservationSignature.Create(
                    carried.itemId,
                    carried.components),
                stackSignature,
                StringComparison.Ordinal)
            || !string.Equals(
                ItemStackSignature.Create(stack.itemId, stack.components),
                ItemStackSignature.Create(carried.itemId, carried.components),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Saved haul commitment conflicts with its physical and carried-inventory join: "
                + commitment.carriedStackId);
        }
        return carried;
    }

    private static long GetStackMass(
        WorldItemStackSaveData stack,
        int quantity,
        IPhysicalItemMassQuery massQuery)
    {
        ItemDefinitionId itemId = (ItemDefinitionId)(stack.itemId ?? string.Empty);
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            itemId,
            stack.itemInstanceId,
            stack.components);
        return massQuery.GetQuantityMass(itemId, subject, quantity).Value;
    }

    internal static FacilityOutputExactRouteOutboxSaveData ToSaveData(
        FacilityOutputExactRoutePendingSnapshot pending)
    {
        if (pending?.Receipt == null || pending.DeliveryRevision == null)
            throw new ArgumentException("An exact-route snapshot is required.", nameof(pending));

        FacilityOutputExactRouteReceipt receipt = pending.Receipt;
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery =
            pending.DeliveryRevision;
        return new FacilityOutputExactRouteOutboxSaveData
        {
            phase = pending.Phase,
            routeOperationId = receipt.RouteOperationId,
            requestFingerprint = receipt.RequestFingerprint,
            physicalReceiptFingerprint = receipt.PhysicalReceiptFingerprint,
            batchCommitId = receipt.BatchCommitId,
            sourceDestinationId = receipt.SourceDestinationId,
            targetDestinationId = receipt.TargetDestinationId,
            targetPositionX = receipt.TargetPosition.x,
            targetPositionY = receipt.TargetPosition.y,
            totalQuantity = receipt.TotalQuantity,
            totalMassGrams = receipt.TotalMassGrams,
            currentDeliveryRevision = delivery.Revision,
            currentDeliveryRevisionFingerprint = delivery.RevisionFingerprint,
            currentDeliveryRerouteOperationId = delivery.RerouteOperationId,
            currentTargetDestinationId = delivery.TargetDestinationId,
            currentTargetPositionX = delivery.TargetPositionX,
            currentTargetPositionY = delivery.TargetPositionY,
            currentTargetAuthorityFingerprint = delivery.TargetAuthorityFingerprint,
            slices = receipt.Slices.Select(value =>
                new FacilityOutputExactRouteSliceSaveData
                {
                    sourceStackId = value.SourceStackId,
                    routedStackId = value.RoutedStackId,
                    outputLineId = value.OutputLineId,
                    lineCommitId = value.LineCommitId,
                    itemId = value.ItemId,
                    sourceOffsetQuantity = value.SourceOffsetQuantity,
                    routedOffsetQuantity = value.RoutedOffsetQuantity,
                    routedQuantity = value.RoutedQuantity,
                    routedMassGrams = value.RoutedMassGrams,
                    componentFingerprint = value.ComponentFingerprint
                }).ToList()
        };
    }

    private static ProductionPreparedOutputRoutingBatchSaveData
        CanonicalizeRoutingBatch(ProductionPreparedOutputRoutingBatchSaveData source)
    {
        ProductionPreparedOutputRoutingBatchSaveData clone = source.Clone();
        clone.nonPhysicalDispositions = (clone.nonPhysicalDispositions
                ?? new List<
                    ProductionPreparedOutputNonPhysicalDispositionSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.lineCommitId, StringComparer.Ordinal)
            .ToList();
        clone.lines = (clone.lines
                ?? new List<ProductionPreparedOutputRoutingLineSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.lineCommitId, StringComparer.Ordinal)
            .ThenBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToList();
        foreach (ProductionPreparedOutputRoutingLineSaveData line in clone.lines)
        {
            line.routeOperations = (line.routeOperations
                    ?? new List<ProductionPreparedOutputRouteOperationSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
                .ToList();
            foreach (ProductionPreparedOutputRouteOperationSaveData operation in
                     line.routeOperations)
            {
                operation.physicalSlices = (operation.physicalSlices
                        ?? new List<ProductionPreparedOutputPhysicalRouteSliceSaveData>())
                    .Where(value => value != null)
                    .OrderBy(value => value.sourceOffsetQuantity)
                    .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
                    .ThenBy(value => value.routedStackId, StringComparer.Ordinal)
                    .ToList();
                operation.deliveryRevisions = (operation.deliveryRevisions
                        ?? new List<ProductionPreparedOutputDeliveryRevisionSaveData>())
                    .Where(value => value != null)
                    .OrderBy(value => value.revision)
                    .ThenBy(value => value.revisionFingerprint, StringComparer.Ordinal)
                    .ToList();
            }
        }
        return clone;
    }

    private static FacilityOutputExactRouteOutboxSaveData CanonicalizeOutboxRoute(
        FacilityOutputExactRouteOutboxSaveData source)
    {
        FacilityOutputExactRouteOutboxSaveData clone = source.Clone();
        clone.slices = (clone.slices
                ?? new List<FacilityOutputExactRouteSliceSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.sourceOffsetQuantity)
            .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value.routedStackId, StringComparer.Ordinal)
            .ToList();
        return clone;
    }

    private static WorldItemStackSaveData CanonicalizePhysicalStack(
        WorldItemStackSaveData source) => new()
    {
        stackId = source.stackId ?? string.Empty,
        itemInstanceId = source.itemInstanceId ?? string.Empty,
        itemId = source.itemId ?? string.Empty,
        quantity = source.quantity,
        state = source.state,
        gridX = source.gridX,
        gridY = source.gridY,
        reservedByPersistentId = string.Empty,
        destinationId = source.destinationId ?? string.Empty,
        aggregationCohortId = source.aggregationCohortId ?? string.Empty,
        sourceStorageDestinationId = source.sourceStorageDestinationId ?? string.Empty,
        hasDestinationPosition = source.hasDestinationPosition,
        destinationGridX = source.destinationGridX,
        destinationGridY = source.destinationGridY,
        forbidden = source.forbidden,
        sourceCharacterId = source.sourceCharacterId ?? string.Empty,
        sourceDisplayName = source.sourceDisplayName ?? string.Empty,
        sourceSpeciesTag = source.sourceSpeciesTag ?? string.Empty,
        sourceDeathReason = source.sourceDeathReason ?? string.Empty,
        emergencyButcheryAllowed = source.emergencyButcheryAllowed,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = CanonicalizeComponents(source.components),
        dropDisposition = source.dropDisposition,
        recoveryOwnerOperationId = source.recoveryOwnerOperationId ?? string.Empty,
        recoverySourceStackId = source.recoverySourceStackId ?? string.Empty,
        recoveryCarrierPersistentId = source.recoveryCarrierPersistentId ?? string.Empty,
        recoveryInterruptionKind = source.recoveryInterruptionKind,
        droppedAtGameTime = source.droppedAtGameTime,
        recoveryDeadlineGameTime = source.recoveryDeadlineGameTime
    };

    private static HaulDeliveryIntentSaveData CanonicalizeHaulIntent(
        HaulDeliveryIntentSaveData source,
        IReadOnlyList<HaulDeliveryItemCommitmentSaveData> matching) => new()
    {
        operationId = source.operationId ?? string.Empty,
        ownerCharacterId = source.ownerCharacterId ?? string.Empty,
        destinationKind = source.destinationKind,
        destinationId = source.destinationId ?? string.Empty,
        deliveryGridX = source.deliveryGridX,
        deliveryGridY = source.deliveryGridY,
        dropGridX = source.dropGridX,
        dropGridY = source.dropGridY,
        warehouseAdmissions = (source.warehouseAdmissions
                ?? new List<WarehouseHaulAdmissionSaveData>())
            .Where(value => value != null)
            .Select(value => new WarehouseHaulAdmissionSaveData
            {
                tokenId = string.Empty,
                ownerAdmissionOperationId = value.ownerAdmissionOperationId ?? string.Empty,
                warehouseId = value.warehouseId ?? string.Empty,
                sourceWarehouseId = value.sourceWarehouseId ?? string.Empty,
                sourceStackId = value.sourceStackId ?? string.Empty,
                itemId = value.itemId ?? string.Empty,
                itemInstanceId = value.itemInstanceId ?? string.Empty,
                lotFingerprint = value.lotFingerprint ?? string.Empty,
                quantity = value.quantity,
                reservedMassGrams = value.reservedMassGrams,
                catalogRevision = value.catalogRevision,
                sourceRevision = value.sourceRevision
            })
            .OrderBy(value => value.ownerAdmissionOperationId, StringComparer.Ordinal)
            .ThenBy(value => value.warehouseId, StringComparer.Ordinal)
            .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
            .ToList(),
        commitments = matching
            .Where(value => value != null)
            .Select(value => new HaulDeliveryItemCommitmentSaveData
            {
                carriedStackId = value.carriedStackId ?? string.Empty,
                sourceStackId = value.sourceStackId ?? string.Empty,
                itemId = value.itemId ?? string.Empty,
                expectedStackSignature = value.expectedStackSignature ?? string.Empty,
                quantity = value.quantity
            })
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToList()
    };

    private static CharacterCarriedItemSaveData CanonicalizeCarriedItem(
        CharacterCarriedItemSaveData source) => new()
    {
        carriedStackId = source.carriedStackId ?? string.Empty,
        sourceStackId = source.sourceStackId ?? string.Empty,
        ownerOperationId = source.ownerOperationId ?? string.Empty,
        itemInstanceId = source.itemInstanceId ?? string.Empty,
        itemId = source.itemId ?? string.Empty,
        quantity = source.quantity,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = CanonicalizeComponents(source.components)
    };

    private static List<ItemInstanceComponentSaveData> CanonicalizeComponents(
        IEnumerable<ItemInstanceComponentSaveData> source) => (source
            ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(value => value != null)
        .Select(value => new ItemInstanceComponentSaveData
        {
            componentTypeId = value.componentTypeId ?? string.Empty,
            schemaVersion = value.schemaVersion,
            affectsStacking = value.affectsStacking,
            values = (value.values ?? new List<ItemStateValueSaveData>())
                .Where(entry => entry != null)
                .Select(entry => new ItemStateValueSaveData
                {
                    key = entry.key ?? string.Empty,
                    kind = entry.kind,
                    stringValue = entry.stringValue ?? string.Empty,
                    integerValue = entry.integerValue,
                    decimalValue = entry.decimalValue,
                    booleanValue = entry.booleanValue
                })
                .OrderBy(entry => entry.key, StringComparer.Ordinal)
                .ThenBy(entry => entry.kind)
                .ToList()
        })
        .OrderBy(value => value.componentTypeId, StringComparer.Ordinal)
        .ThenBy(value => value.schemaVersion)
        .ToList();

    private static void AppendOrdered<T>(
        StringBuilder canonical,
        IEnumerable<T> records)
        where T : class
    {
        foreach (T record in records)
            ProductionLifecycleFingerprint.AppendSaveRecord(canonical, record);
    }

    private static void RequireFacility(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            throw new ArgumentException(
                "A valid production facility ID is required.",
                nameof(facilityId));
        }
    }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}
