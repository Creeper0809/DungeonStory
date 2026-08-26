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

/// <summary>
/// Detached, save-DTO-only projection of the durable lifecycle owned by a
/// production-capable facility. It must remain usable during aggregate
/// preflight, before any restore candidate is published to the live runtime.
/// </summary>
public static class ProductionOutputDestinationDurableSaveProjector
{
    public const string AggregateSchemaToken =
        "production-output-durable-lifecycle@1";
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

    private static readonly string[] RequiredAggregateContributorIds =
    {
        ApparelContributorId,
        CapacityRoutingContributorId,
        EquipmentContributorId,
        GenericBillsContributorId,
        PhysicalCustodyContributorId
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
            && string.Equals(
                actor.haulDeliveryIntent.destinationId,
                destination,
                StringComparison.Ordinal));
        if (hasBill || hasEquipment || hasRepair || hasApparel || hasRouting
            || hasPhysical || hasCarriedIntent)
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
            .Where(value => value?.haulDeliveryIntent != null)
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
            capacityProjector.CaptureSource(subject, 0L);
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
            ProductionOutputBufferCapacitySourceSnapshot current =
                capacityProjector.CaptureSource(
                    subject,
                    bill.preparedOutput.totalPhysicalMassGrams);
            ProductionOutputBufferCapacitySourceGuard.ValidateSaved(
                bill.preparedOutput,
                current,
                "Detached production capacity bill '" + bill.billId + "'");
            maximumCapacity = Math.Max(
                maximumCapacity,
                current.RequiredMinimumCapacityGrams);
        }
        // A destructive generic-bill terminalization may remove the bill while
        // its independently owned routing batch remains. Reconstruct the same
        // capacity floor from that batch so detached save projection does not
        // shrink authority merely because the producer owner has retired.
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
            long batchPhysicalMass = checked((batch.lines
                    ?? new List<ProductionPreparedOutputRoutingLineSaveData>())
                .Where(line => line != null
                    && line.role != ProductionOutputRole.DeclaredLoss)
                .Sum(line => line.originalMassGrams));
            maximumCapacity = Math.Max(maximumCapacity, batchPhysicalMass);
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

    public static FacilityBufferPhysicalOccupancySnapshot ProjectPhysicalOccupancy(
        string destinationId,
        DungeonPhysicalItemSaveData itemPayload,
        DungeonCharacterWorldSaveData characterPayload,
        IPhysicalItemMassQuery massQuery)
    {
        if (string.IsNullOrWhiteSpace(destinationId)
            || !string.Equals(destinationId, destinationId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A canonical destination is required.", nameof(destinationId));
        if (itemPayload?.stacks == null)
            throw new ArgumentNullException(nameof(itemPayload));
        if (characterPayload?.actors == null)
            throw new ArgumentNullException(nameof(characterPayload));
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));

        Dictionary<string, WorldItemStackSaveData> stacks = itemPayload.stacks
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToDictionary(value => value.stackId, StringComparer.Ordinal);
        long nonCarried = 0L;
        foreach (WorldItemStackSaveData stack in stacks.Values
                     .Where(value => value.state != WorldItemStackState.Carried
                         && string.Equals(
                             value.destinationId,
                             destinationId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            nonCarried = checked(nonCarried + GetStackMass(stack, stack.quantity, massQuery));
        }

        long carried = 0L;
        HashSet<string> committedStackOwners = new(StringComparer.Ordinal);
        foreach (DungeonCharacterSaveData actor in characterPayload.actors
                     .Where(value => value?.haulDeliveryIntent != null
                         && string.Equals(
                             value.haulDeliveryIntent.destinationId,
                             destinationId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.haulDeliveryIntent.operationId, StringComparer.Ordinal))
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
            foreach (HaulDeliveryItemCommitmentSaveData commitment in
                     intent.commitments
                     .Where(value => value != null)
                     .OrderBy(value => value.carriedStackId, StringComparer.Ordinal))
            {
                if (!committedStackOwners.Add(commitment.carriedStackId ?? string.Empty))
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
                RequireExactCarriedInventoryJoin(actor, intent, commitment, stack);
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
                carried = checked(
                    carried + GetStackMass(stack, commitment.quantity, massQuery));
            }
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
}
