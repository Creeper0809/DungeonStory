using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class ProductionBillLifecycleContributor : IProductionOutputDestinationLifecycleContributor
{
    private readonly IProductionBillCoreQuery bills;

    public ProductionBillLifecycleContributor(IProductionBillCoreQuery bills) =>
        this.bills = bills ?? throw new ArgumentNullException(nameof(bills));

    public string ContributorId => "generic-production-bills";

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        ProductionFacilityBillLifecycleSnapshot snapshot = bills.CaptureFacilityLifecycle(facilityId);
        List<ProductionOutputLifecycleBlock> blocks = new();
        Add(blocks, ProductionOutputLifecycleBlockCode.GenericBill, snapshot.BillCount);
        Add(blocks, ProductionOutputLifecycleBlockCode.GenericWorkInProgress, snapshot.ActiveWipCount);
        Add(blocks, ProductionOutputLifecycleBlockCode.WaitingForOutputSpace, snapshot.WaitingForOutputSpaceCount);
        Add(blocks, ProductionOutputLifecycleBlockCode.PublicationPrepared, snapshot.PublicationPreparedCount);
        Add(blocks, ProductionOutputLifecycleBlockCode.PhysicalCommitPending, snapshot.PhysicalCommitPendingCount);
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            snapshot.BillCount > 0,
            snapshot.BillAuthorityRevision,
            snapshot.BillCount,
            0L,
            blocks,
            snapshot.SemanticFingerprint,
            snapshot.DurableSemanticFingerprint);
    }

    private static void Add(
        ICollection<ProductionOutputLifecycleBlock> blocks,
        ProductionOutputLifecycleBlockCode code,
        int count)
    {
        if (count > 0)
            blocks.Add(new ProductionOutputLifecycleBlock(code, count, 0L));
    }
}

public sealed class ProductionStockSensorLifecycleContributor :
    IProductionOutputDestinationLifecycleContributor
{
    private readonly IProductionStockSensorRuntime sensors;
    private readonly WorldItemRepository repository;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents;

    public ProductionStockSensorLifecycleContributor(
        IProductionStockSensorRuntime sensors,
        WorldItemRepository repository,
        ICharacterLifetimeQuery characterLifetime,
        IPhysicalItemMassQuery massQuery,
        DungeonRuntimeAggregateRootStore aggregateRootStore = null,
        IRestoreWorldCandidateQuery restoreWorldCandidates = null,
        IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents = null)
    {
        this.sensors = sensors
            ?? throw new ArgumentNullException(nameof(sensors));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.aggregateRootStore = aggregateRootStore;
        this.restoreWorldCandidates = restoreWorldCandidates;
        this.restoreHaulIntents = restoreHaulIntents;
    }

    public string ContributorId =>
        ProductionFacilityDestructiveDrainParticipantIds
            .StockSensorEmbeddedSalvage;

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        string id = facilityId.Value;
        ProductionStockSensorPhysicalCommitSaveData[] pendingInstalls = sensors
            .PendingInstallations
            .Where(value => value != null
                && string.Equals(value.facilityId, id, StringComparison.Ordinal))
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
        ProductionInstalledStockSensorSaveData[] installedRecords = sensors
            .InstalledSensors
            .Where(value => value != null
                && string.Equals(value.facilityId, id, StringComparison.Ordinal))
            .OrderBy(value => value.inputOperationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
        ProductionStockSensorRemovalSaveData[] removals = sensors
            .PendingRemovals
            .Where(value => value != null
                && string.Equals(value.facilityId, id, StringComparison.Ordinal))
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
        DungeonProductionBillSaveData projection = new()
        {
            installedStockSensorFacilityIds = sensors.InstalledFacilityIds
                .Where(value => string.Equals(value, id, StringComparison.Ordinal))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            acknowledgedStockSensorFacilityIds = sensors.AcknowledgedFacilityIds
                .Where(value => string.Equals(value, id, StringComparison.Ordinal))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            pendingStockSensorInstalls = pendingInstalls.ToList(),
            installedStockSensors = installedRecords.ToList(),
            pendingStockSensorRemovals = removals.ToList()
        };
        string sensorDestination = ProductionStockSensorRuntime
            .BuildDestinationId(id);
        WorldItemStackRecord[] physicalRecords = repository.Records
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        DungeonPhysicalItemSaveData physicalPayload = new()
        {
            stacks = physicalRecords
                .Select(WorldItemPersistenceService.CaptureDurableStack)
                .ToList()
        };
        HaulDeliveryIntentSaveData[] sensorIntents = HaulDeliveryIntentAuthorityView
            .Capture(
                aggregateRootStore,
                restoreHaulIntents,
                repository.HaulDeliveryIntents)
            .Where(value => value != null
                && string.Equals(value.destinationId,
                    sensorDestination, StringComparison.Ordinal))
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        List<DungeonCharacterSaveData> sensorActors = new();
        foreach (HaulDeliveryIntentSaveData intent in sensorIntents)
        {
            CharacterActor[] matches = ProductionOutputRestoreAuthorityView
                .CaptureCharacters(
                    aggregateRootStore,
                    restoreWorldCandidates,
                    characterLifetime)
                .Where(actor => actor != null
                    && string.Equals(
                        actor.BuildingCharacterId.Value,
                        intent.ownerCharacterId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 || matches[0].CarryInventory == null)
            {
                throw new InvalidOperationException(
                    "Live stock-sensor carry authority does not resolve to exactly one lifetime actor: "
                    + intent.ownerCharacterId);
            }
            sensorActors.Add(new DungeonCharacterSaveData
            {
                persistentId = intent.ownerCharacterId,
                haulDeliveryIntent = intent,
                carryInventory = matches[0].CarryInventory.Capture()
            });
        }
        DungeonCharacterWorldSaveData characterPayload = new()
        {
            actors = sensorActors
        };
        string durable = ProductionOutputDestinationDurableSaveProjector
            .ProjectStockSensor(
                facilityId,
                projection,
                physicalPayload,
                characterPayload);
        FacilityBufferPhysicalOccupancySnapshot physicalOccupancy =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectPhysicalOccupancy(
                    sensorDestination,
                    physicalPayload,
                    characterPayload,
                    massQuery);

        List<ProductionOutputLifecycleBlock> blocks = new();
        if (pendingInstalls.Length > 0)
        {
            blocks.Add(new ProductionOutputLifecycleBlock(
                ProductionOutputLifecycleBlockCode.StockSensorInstallPending,
                pendingInstalls.Length,
                pendingInstalls.Sum(value => value.inputMassGrams)));
        }
        ProductionStockSensorRemovalSaveData removal = removals.SingleOrDefault();
        bool installedActive = installedRecords.Length > 0
            && (removal == null
                || removal.phase == ProductionStockSensorRemovalPhase.Prepared);
        if (installedActive)
        {
            blocks.Add(new ProductionOutputLifecycleBlock(
                ProductionOutputLifecycleBlockCode.StockSensorEmbedded,
                installedRecords.Length,
                installedRecords.Sum(value => value.embeddedMassGrams)));
        }
        if (removal?.phase == ProductionStockSensorRemovalPhase.OutputPublished)
        {
            blocks.Add(new ProductionOutputLifecycleBlock(
                ProductionOutputLifecycleBlockCode
                    .StockSensorRemovalAwaitingAck,
                1,
                0L));
        }
        ProductionExactDestinationCustodyProjection physicalCustody =
            ProductionOutputDestinationDurableSaveProjector
                .CaptureExactDestinationCustody(
                    sensorDestination,
                    physicalPayload,
                    characterPayload);
        if (physicalCustody.DirectStacks.Count > 0)
        {
            blocks.Add(new ProductionOutputLifecycleBlock(
                ProductionOutputLifecycleBlockCode.BufferedPhysicalMass,
                physicalCustody.DirectStacks.Count,
                physicalOccupancy.NonCarriedMassGrams));
        }
        if (physicalCustody.Intents.Count > 0)
        {
            blocks.Add(new ProductionOutputLifecycleBlock(
                ProductionOutputLifecycleBlockCode.HaulIntent,
                physicalCustody.Intents.Count,
                0L));
        }
        if (physicalOccupancy.CommittedCarriedMassGrams > 0L)
        {
            blocks.Add(new ProductionOutputLifecycleBlock(
                ProductionOutputLifecycleBlockCode.CarriedPhysicalMass,
                physicalCustody.CarriedStacks.Count,
                physicalOccupancy.CommittedCarriedMassGrams));
        }

        long ownedMass = installedActive
            ? installedRecords.Sum(value => value.embeddedMassGrams)
            : installedRecords.Length == 0
                ? pendingInstalls.Sum(value => value.inputMassGrams)
                : 0L;
        ownedMass = checked(
            ownedMass
            + physicalOccupancy.NonCarriedMassGrams
            + physicalOccupancy.CommittedCarriedMassGrams);
        int activeRecords = blocks.Sum(value => value.Count);
        string semantic = ProductionLifecycleFingerprint.Compute(
            ContributorId + "|" + id + "|" + sensors.Version + "|"
            + repository.ItemStackVersion + "|" + durable);
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            blocks.Count > 0,
            checked(sensors.Version + repository.ItemStackVersion),
            activeRecords,
            ownedMass,
            blocks,
            semantic,
            durable);
    }
}

public sealed class CombatEquipmentCraftLifecycleContributor : IProductionOutputDestinationLifecycleContributor
{
    private readonly Func<ICombatEquipmentCraftQueueQuery> equipment;
    private readonly Func<ICombatEquipmentMaintenanceOrderQuery> maintenance;

    [VContainer.Inject]
    public CombatEquipmentCraftLifecycleContributor(
        Func<ICombatEquipmentCraftQueueQuery> equipment,
        Func<ICombatEquipmentMaintenanceOrderQuery> maintenance)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.maintenance = maintenance
            ?? throw new ArgumentNullException(nameof(maintenance));
    }

    public string ContributorId => "combat-equipment-crafting";

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        ICombatEquipmentCraftQueueQuery equipmentQuery = equipment()
            ?? throw new InvalidOperationException(
                "Combat equipment lifecycle requires the craft-queue query.");
        ICombatEquipmentMaintenanceOrderQuery maintenanceQuery = maintenance()
            ?? throw new InvalidOperationException(
                "Combat equipment lifecycle requires the maintenance-order query.");
        CombatEquipmentCraftOrderSaveData[] craftOrders = equipmentQuery.CraftQueue
            .Where(value => value != null
                && string.Equals(value.facilityPersistentId, facilityId.Value, StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        CombatEquipmentRepairOrder[] repairOrders = maintenanceQuery.Orders
            .Where(value => value != null
                && value.state is not CombatEquipmentRepairOrderState.Completed
                    and not CombatEquipmentRepairOrderState.Cancelled
                && string.Equals(
                    value.facilityBuildingId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        StringBuilder canonical = new StringBuilder(
                64 + (craftOrders.Length + repairOrders.Length) * 96)
            .Append(ContributorId).Append('|').Append(facilityId.Value).Append('|');
        for (int i = 0; i < craftOrders.Length; i++)
        {
            canonical.Append("craft|");
            CombatEquipmentCraftOrderSaveData order = craftOrders[i];
            ProductionLifecycleFingerprint.AppendSaveRecord(canonical, order);
        }
        for (int i = 0; i < repairOrders.Length; i++)
        {
            canonical.Append("repair|");
            CombatEquipmentRepairOrder order = repairOrders[i];
            ProductionLifecycleFingerprint.AppendSaveRecord(canonical, order);
        }
        List<ProductionOutputLifecycleBlock> blocks = new();
        if (craftOrders.Length > 0)
        {
            blocks.Add(
                new ProductionOutputLifecycleBlock(
                    ProductionOutputLifecycleBlockCode.EquipmentCraftOrder,
                    craftOrders.Length,
                    0L));
        }
        if (repairOrders.Length > 0)
        {
            blocks.Add(
                new ProductionOutputLifecycleBlock(
                    ProductionOutputLifecycleBlockCode.EquipmentRepairOrder,
                    repairOrders.Length,
                    0L));
        }
        int ownerCount = craftOrders.Length + repairOrders.Length;
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            ownerCount > 0,
            0L,
            ownerCount,
            0L,
            blocks,
            ProductionLifecycleFingerprint.Compute(canonical.ToString()),
            ProductionOutputDestinationDurableSaveProjector.ProjectEquipment(
                facilityId,
                new DungeonCombatEquipmentSaveData
                {
                    craftOrders = craftOrders.ToList()
                },
                new CombatEquipmentMaintenanceSaveData
                {
                    orders = repairOrders.Select(value => value.Clone()).ToList()
                }));
    }
}

public sealed class ApparelWorkOrderLifecycleContributor : IProductionOutputDestinationLifecycleContributor
{
    private readonly IApparelWorkOrderQuery apparel;

    public ApparelWorkOrderLifecycleContributor(IApparelWorkOrderQuery apparel) =>
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));

    public string ContributorId => "apparel-work-orders";

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        ApparelWorkOrderSaveData[] owned = apparel.Orders
            .Where(value => value != null
                && value.state != ApparelWorkOrderState.Completed
                && string.Equals(value.facilityInstanceId, facilityId.Value, StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        StringBuilder canonical = new StringBuilder(64 + owned.Length * 96)
            .Append(ContributorId).Append('|').Append(facilityId.Value).Append('|')
            .Append(apparel.Version).Append('|');
        for (int i = 0; i < owned.Length; i++)
        {
            ApparelWorkOrderSaveData order = owned[i];
            ProductionLifecycleFingerprint.AppendSaveRecord(canonical, order);
        }
        ProductionOutputLifecycleBlock[] blocks = owned.Length == 0
            ? Array.Empty<ProductionOutputLifecycleBlock>()
            : new[]
            {
                new ProductionOutputLifecycleBlock(
                    ProductionOutputLifecycleBlockCode.ApparelWorkOrder,
                    owned.Length,
                    0L)
            };
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            owned.Length > 0,
            apparel.Version,
            owned.Length,
            0L,
            blocks,
            ProductionLifecycleFingerprint.Compute(canonical.ToString()),
            ProductionOutputDestinationDurableSaveProjector.ProjectApparel(
                facilityId,
                new DungeonCharacterEnvironmentSaveData
                {
                    apparelWorkOrders = owned,
                    apparelWorkOrderTerminalStates =
                        Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
                }));
    }

}

public sealed class ProductionOutputCapacityRoutingLifecycleContributor :
    IProductionOutputDestinationLifecycleContributor
{
    private readonly IFacilityBufferDestinationClaimQuery claims;
    private readonly IFacilityBufferMassCapacityQuery capacity;
    private readonly IFacilityBufferPhysicalOccupancyQuery occupancy;
    private readonly IProductionPreparedOutputRoutingAuthority routing;
    private readonly IProductionPreparedOutputRoutingPersistence routingPersistence;
    private readonly IFacilityOutputExactRouteOutboxQuery outbox;

    public ProductionOutputCapacityRoutingLifecycleContributor(
        IFacilityBufferDestinationClaimQuery claims,
        IFacilityBufferMassCapacityQuery capacity,
        IFacilityBufferPhysicalOccupancyQuery occupancy,
        IProductionPreparedOutputRoutingAuthority routing,
        IFacilityOutputExactRouteOutboxQuery outbox)
    {
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
        this.occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
        this.routing = routing ?? throw new ArgumentNullException(nameof(routing));
        routingPersistence = routing as IProductionPreparedOutputRoutingPersistence
            ?? throw new ArgumentException(
                "Production lifecycle routing authority must expose its durable save projection.",
                nameof(routing));
        this.outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    public string ContributorId => "capacity-routing-outbox";

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        string destination = destinationId.Value;
        FacilityBufferDestinationClaim[] destinationClaims = claims
            .CaptureClaims()
            .Where(value => value != null
                && string.Equals(
                    value.DestinationId,
                    destination,
                    StringComparison.Ordinal))
            .OrderBy(value => value.OwnerDomain, StringComparer.Ordinal)
            .ThenBy(value => value.OwnerOperationId, StringComparer.Ordinal)
            .ToArray();
        if (destinationClaims.Length > 1)
        {
            throw new InvalidOperationException(
                "Duplicate production output destination claim authority: "
                + destination);
        }
        FacilityBufferCapacityProfile[] profiles = capacity.CaptureProfiles()
            .Where(value => value != null
                && string.Equals(value.DestinationId, destination, StringComparison.Ordinal))
            .OrderBy(value => value.OwnerDomain, StringComparer.Ordinal)
            .ThenBy(value => value.OwnerOperationId, StringComparer.Ordinal)
            .ToArray();
        if (profiles.Length > 1)
            throw new InvalidOperationException("Duplicate production output capacity authority: " + destination);

        if (destinationClaims.Length != profiles.Length)
        {
            throw new InvalidOperationException(
                "Partial production output destination authority: "
                + destination + ":claim=" + destinationClaims.Length
                + ":profile=" + profiles.Length);
        }

        FacilityBufferDestinationClaim claim = destinationClaims.SingleOrDefault();
        FacilityBufferCapacityProfile profile = profiles.SingleOrDefault();
        if (claim != null && !IsExactAuthorityPair(
                facilityId,
                destinationId,
                claim,
                profile))
        {
            throw new InvalidOperationException(
                "Conflicting production output destination authority: "
                + destination);
        }
        long reservedMass = 0L;
        if (profile != null)
        {
            if (!capacity.TryGetCapacity(
                    destination,
                    profile.DropPosition,
                    out FacilityBufferMassCapacitySnapshot capacitySnapshot))
            {
                throw new InvalidOperationException(
                    "Production output capacity profile cannot be queried: " + destination);
            }
            reservedMass = capacitySnapshot.ReservedMassGrams;
        }
        FacilityBufferPhysicalOccupancySnapshot occupied = occupancy.Capture(destination);

        ProductionPreparedOutputRoutingLineSnapshot[] lines = routing.CaptureAll()
            .Where(value => string.Equals(value.OwnerFacilityId, facilityId.Value, StringComparison.Ordinal)
                && (value.RemainingQuantity > 0 || value.RoutedQuantity > 0))
            .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ThenBy(value => value.LineCommitId, StringComparer.Ordinal)
            .ToArray();
        ProductionPreparedOutputRouteRequestSnapshot[] operations = routing
            .CaptureRouteOperations()
            .Where(value => string.Equals(value.SourceDestinationId, destination, StringComparison.Ordinal))
            .OrderBy(value => value.RouteOperationId, StringComparer.Ordinal)
            .ToArray();
        FacilityOutputExactRoutePendingSnapshot[] pending = outbox.CapturePendingRoutes()
            .Where(value => value?.Receipt != null
                && string.Equals(value.Receipt.SourceDestinationId, destination, StringComparison.Ordinal))
            .OrderBy(value => value.Receipt.RouteOperationId, StringComparer.Ordinal)
            .ToArray();

        long routingMass = 0L;
        for (int i = 0; i < lines.Length; i++)
            routingMass = checked(routingMass + lines[i].RemainingMassGrams + lines[i].RoutedMassGrams);
        long operationMass = 0L;
        for (int i = 0; i < operations.Length; i++)
            operationMass = checked(operationMass + operations[i].RoutedMassGrams);
        long outboxMass = 0L;
        for (int i = 0; i < pending.Length; i++)
            outboxMass = checked(outboxMass + pending[i].Receipt.TotalMassGrams);

        List<ProductionOutputLifecycleBlock> blocks = new();
        Add(blocks, ProductionOutputLifecycleBlockCode.ReservedCapacityMass, 0, reservedMass);
        Add(blocks, ProductionOutputLifecycleBlockCode.BufferedPhysicalMass, 0, occupied.TotalMassGrams);
        Add(blocks, ProductionOutputLifecycleBlockCode.RoutingLine, lines.Length, routingMass);
        Add(blocks, ProductionOutputLifecycleBlockCode.RouteOperation, operations.Length, operationMass);
        Add(blocks, ProductionOutputLifecycleBlockCode.ExactRouteOutbox, pending.Length, outboxMass);

        StringBuilder canonical = new StringBuilder(256)
            .Append(ContributorId).Append('|').Append(facilityId.Value).Append('|')
            .Append(claims.Revision).Append('|')
            .Append(capacity.Revision).Append('|')
            .Append(claim?.OwnerDomain ?? string.Empty).Append('|')
            .Append(claim?.OwnerOperationId ?? string.Empty).Append('|')
            .Append(claim?.OwnerFacilityId ?? string.Empty).Append('|')
            .Append((int)(claim?.AnchorKind
                ?? FacilityBufferDestinationAnchorKind.LiveFacility)).Append('|')
            .Append(profile?.MaxMassGrams ?? 0L).Append('|')
            .Append(reservedMass).Append('|').Append(occupied.TotalMassGrams).Append('|');
        string durableFingerprint =
            ProductionOutputDestinationDurableSaveProjector.ProjectCapacityRouting(
                facilityId,
                profile,
                occupied,
                routingPersistence.Capture(),
                pending.Select(
                        ProductionOutputDestinationDurableSaveProjector.ToSaveData)
                    .ToArray());
        for (int i = 0; i < lines.Length; i++)
            AppendRoutingLine(canonical, lines[i]);
        for (int i = 0; i < operations.Length; i++)
            AppendRouteOperation(canonical, operations[i]);
        for (int i = 0; i < pending.Length; i++)
            AppendPendingRoute(canonical, pending[i]);

        int records = checked(
            destinationClaims.Length
            + profiles.Length
            + lines.Length
            + operations.Length
            + pending.Length);
        long mass = checked(
            occupied.TotalMassGrams + reservedMass + routingMass + operationMass + outboxMass);
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            records > 0 || occupied.TotalMassGrams > 0L,
            checked(claims.Revision + capacity.Revision),
            records,
            mass,
            blocks,
            ProductionLifecycleFingerprint.Compute(canonical.ToString()),
            durableFingerprint);
    }

    private static bool IsExactAuthorityPair(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) =>
        claim != null
        && profile != null
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.DropPosition == profile.DropPosition
        && profile.CapacityRevision ==
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision
        && string.Equals(
            claim.DestinationId,
            destinationId.Value,
            StringComparison.Ordinal)
        && string.Equals(
            profile.DestinationId,
            destinationId.Value,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerDomain,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerDomain,
            claim.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerOperationId,
            destinationId.Value,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            claim.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerFacilityId,
            facilityId.Value,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            claim.OwnerFacilityId,
            StringComparison.Ordinal);

    private static void AppendRoutingLine(
        StringBuilder canonical,
        ProductionPreparedOutputRoutingLineSnapshot line)
    {
        canonical.Append(line.BatchCommitId).Append('|')
            .Append(line.LineCommitId).Append('|')
            .Append(line.RemainingQuantity).Append('|')
            .Append(line.RemainingMassGrams).Append('|')
            .Append(line.RoutedQuantity).Append('|')
            .Append(line.RoutedMassGrams).Append(';');
    }

    private static void AppendRouteOperation(
        StringBuilder canonical,
        ProductionPreparedOutputRouteRequestSnapshot operation)
    {
        canonical.Append(operation.RouteOperationId).Append('|')
            .Append((int)operation.Phase).Append('|')
            .Append(operation.RoutedQuantity).Append('|')
            .Append(operation.RoutedMassGrams).Append(';');
    }

    private static void AppendPendingRoute(
        StringBuilder canonical,
        FacilityOutputExactRoutePendingSnapshot pending)
    {
        canonical.Append(pending.Receipt.RouteOperationId).Append('|')
            .Append((int)pending.Phase).Append('|')
            .Append(pending.Receipt.TotalMassGrams).Append(';');
    }

    private static void Add(
        ICollection<ProductionOutputLifecycleBlock> blocks,
        ProductionOutputLifecycleBlockCode code,
        int count,
        long mass)
    {
        if (count > 0 || mass > 0L)
            blocks.Add(new ProductionOutputLifecycleBlock(code, count, mass));
    }
}

public sealed class ProductionOutputPhysicalLifecycleContributor :
    IProductionOutputDestinationLifecycleContributor
{
    private readonly WorldItemRepository repository;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents;

    public ProductionOutputPhysicalLifecycleContributor(
        WorldItemRepository repository,
        ICharacterLifetimeQuery characterLifetime,
        DungeonRuntimeAggregateRootStore aggregateRootStore = null,
        IRestoreWorldCandidateQuery restoreWorldCandidates = null,
        IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents = null)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
        this.aggregateRootStore = aggregateRootStore;
        this.restoreWorldCandidates = restoreWorldCandidates;
        this.restoreHaulIntents = restoreHaulIntents;
    }

    public string ContributorId => "physical-custody-carry-recovery";

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        string destination = destinationId.Value;
        int originCount = 0;
        int custodyCount = 0;
        long custodyMass = 0L;
        long carriedMass = 0L;
        int recoveryCount = 0;
        long recoveryMass = 0L;
        HashSet<string> custodyStackIds = new(StringComparer.Ordinal);
        StringBuilder canonical = new StringBuilder(192)
            .Append(ContributorId).Append('|').Append(destination).Append('|')
            .Append(repository.ItemStackVersion).Append('|');

        WorldItemStackRecord[] records = repository.Records
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        for (int i = 0; i < records.Length; i++)
        {
            WorldItemStackRecord record = records[i];
            bool originBuffered = record.state == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(record.destinationId, destination, StringComparison.Ordinal);
            bool hasCustody = FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                && string.Equals(custody.OriginDestinationId, destination, StringComparison.Ordinal);
            if (!originBuffered && !hasCustody)
                continue;

            if (originBuffered)
                originCount++;
            if (hasCustody)
            {
                custodyCount++;
                custodyMass = checked(custodyMass + custody.MassGrams);
                custodyStackIds.Add(record.stackId);
                if (record.state is WorldItemStackState.Carried or WorldItemStackState.InTransit)
                    carriedMass = checked(carriedMass + custody.MassGrams);
                if (record.dropDisposition == WorldItemDropDisposition.TransientCarryRecoveryDrop)
                {
                    recoveryCount++;
                    recoveryMass = checked(recoveryMass + custody.MassGrams);
                }
            }
            ProductionOutputPhysicalDurableCanonical.AppendRecord(
                canonical,
                record.stackId,
                record.state,
                record.quantity,
                record.reservationRevision,
                record.destinationId,
                record.dropDisposition,
                record.recoveryOwnerOperationId,
                hasCustody,
                custody,
                includeVolatileRevision: true);
        }

        int haulIntentCount = 0;
        HaulDeliveryIntentSaveData[] intents = HaulDeliveryIntentAuthorityView
            .Capture(
                aggregateRootStore,
                restoreHaulIntents,
                repository.HaulDeliveryIntents)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        for (int i = 0; i < intents.Length; i++)
        {
            HaulDeliveryIntentSaveData intent = intents[i];
            HaulDeliveryItemCommitmentSaveData[] matching = (intent.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Where(value => value != null
                    && custodyStackIds.Contains(value.carriedStackId ?? string.Empty))
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .ToArray();
            if (matching.Length == 0)
                continue;
            haulIntentCount++;
            ProductionOutputPhysicalDurableCanonical.AppendHaulIntent(
                canonical,
                intent,
                matching);
        }

        DungeonPhysicalItemSaveData physicalPayload = new()
        {
            stacks = records
                .Select(WorldItemPersistenceService.CaptureDurableStack)
                .ToList()
        };
        List<DungeonCharacterSaveData> physicalActors = new();
        foreach (HaulDeliveryIntentSaveData intent in intents)
        {
            bool ownsCustody = (intent.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Any(commitment => commitment != null
                    && custodyStackIds.Contains(
                        commitment.carriedStackId ?? string.Empty));
            if (!ownsCustody)
                continue;

            CharacterActor[] matches = ProductionOutputRestoreAuthorityView
                .CaptureCharacters(
                    aggregateRootStore,
                    restoreWorldCandidates,
                    characterLifetime)
                .Where(actor => actor != null
                    && string.Equals(
                        actor.BuildingCharacterId.Value,
                        intent.ownerCharacterId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 || matches[0].CarryInventory == null)
            {
                throw new InvalidOperationException(
                    "Live production-output carry authority does not resolve to exactly one lifetime actor: "
                    + intent.ownerCharacterId);
            }
            physicalActors.Add(new DungeonCharacterSaveData
            {
                persistentId = intent.ownerCharacterId,
                haulDeliveryIntent = intent,
                carryInventory = matches[0].CarryInventory.Capture()
            });
        }
        DungeonCharacterWorldSaveData characterPayload = new()
        {
            actors = physicalActors
        };
        string durableFingerprint =
            ProductionOutputDestinationDurableSaveProjector.ProjectPhysicalCustody(
                facilityId,
                physicalPayload,
                characterPayload);

        List<ProductionOutputLifecycleBlock> blocks = new();
        Add(blocks, ProductionOutputLifecycleBlockCode.OriginPhysicalStack, originCount, 0L);
        Add(blocks, ProductionOutputLifecycleBlockCode.CustodyPhysicalStack, custodyCount, custodyMass);
        Add(blocks, ProductionOutputLifecycleBlockCode.HaulIntent, haulIntentCount, 0L);
        Add(blocks, ProductionOutputLifecycleBlockCode.CarriedPhysicalMass, 0, carriedMass);
        Add(blocks, ProductionOutputLifecycleBlockCode.RecoveryPending, recoveryCount, recoveryMass);
        int active = checked(originCount + custodyCount + haulIntentCount + recoveryCount);
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            active > 0,
            repository.ItemStackVersion,
            active,
            custodyMass,
            blocks,
            ProductionLifecycleFingerprint.Compute(canonical.ToString()),
            durableFingerprint);
    }

    private static void Add(
        ICollection<ProductionOutputLifecycleBlock> blocks,
        ProductionOutputLifecycleBlockCode code,
        int count,
        long mass)
    {
        if (count > 0 || mass > 0L)
            blocks.Add(new ProductionOutputLifecycleBlock(code, count, mass));
    }
}

internal static class ProductionOutputRestoreAuthorityView
{
    internal static IReadOnlyList<CharacterActor> CaptureCharacters(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        ICharacterLifetimeQuery liveAuthority)
    {
        if (aggregateRootStore?.IsRestoreStaging == true)
        {
            if (restoreWorldCandidates == null
                || !restoreWorldCandidates.TryGetCharacters(
                    out IReadOnlyList<CharacterActor> candidates))
            {
                throw new InvalidOperationException(
                    "Restore staging requires detached character authority for production-output projection.");
            }
            return candidates ?? Array.Empty<CharacterActor>();
        }

        return liveAuthority?.AllCharacters ?? Array.Empty<CharacterActor>();
    }
}

internal static class ProductionOutputPhysicalDurableCanonical
{
    internal static void AppendRecord(
        StringBuilder canonical,
        string stackId,
        WorldItemStackState state,
        int quantity,
        long reservationRevision,
        string destinationId,
        WorldItemDropDisposition dropDisposition,
        string recoveryOwnerOperationId,
        bool hasCustody,
        FacilityOutputExactRouteCustodyMetadata custody,
        bool includeVolatileRevision)
    {
        canonical.Append(stackId).Append('|')
            .Append((int)state).Append('|').Append(quantity).Append('|');
        if (includeVolatileRevision)
            canonical.Append(reservationRevision).Append('|');
        canonical.Append(destinationId).Append('|')
            .Append((int)dropDisposition).Append('|')
            .Append(recoveryOwnerOperationId).Append('|');
        if (hasCustody)
        {
            canonical.Append((int)custody.Phase).Append('|')
                .Append(custody.BatchCommitId).Append('|')
                .Append(custody.RouteOperationId).Append('|')
                .Append(custody.MassGrams);
        }
        canonical.Append(';');
    }

    internal static void AppendHaulIntent(
        StringBuilder canonical,
        HaulDeliveryIntentSaveData intent,
        IReadOnlyList<HaulDeliveryItemCommitmentSaveData> matching)
    {
        canonical.Append("intent|").Append(intent.operationId).Append('|')
            .Append(intent.ownerCharacterId).Append('|')
            .Append((int)intent.destinationKind).Append('|')
            .Append(intent.destinationId).Append('|')
            .Append(intent.deliveryGridX).Append('|')
            .Append(intent.deliveryGridY).Append('|')
            .Append(intent.dropGridX).Append('|')
            .Append(intent.dropGridY).Append('|');
        for (int index = 0; index < matching.Count; index++)
        {
            HaulDeliveryItemCommitmentSaveData commitment = matching[index];
            canonical.Append(commitment.carriedStackId).Append(',')
                .Append(commitment.sourceStackId).Append(',')
                .Append(commitment.itemId).Append(',')
                .Append(commitment.expectedStackSignature).Append(',')
                .Append(commitment.quantity).Append(';');
        }
        canonical.Append('|');
    }
}

public sealed class ProductionOutputDestinationLifecycleQuery :
    IProductionOutputDestinationLifecycleQuery
{
    private readonly IProductionOutputDestinationLifecycleContributor[] contributors;
    private readonly bool requireCurrentFormatAggregateSchema;

    public ProductionOutputDestinationLifecycleQuery(
        ProductionBillLifecycleContributor bills,
        CombatEquipmentCraftLifecycleContributor equipment,
        ApparelWorkOrderLifecycleContributor apparel,
        ProductionOutputCapacityRoutingLifecycleContributor capacityRouting,
        ProductionOutputPhysicalLifecycleContributor physical,
        ProductionStockSensorLifecycleContributor stockSensor)
    {
        contributors = new IProductionOutputDestinationLifecycleContributor[]
        {
            bills ?? throw new ArgumentNullException(nameof(bills)),
            equipment ?? throw new ArgumentNullException(nameof(equipment)),
            apparel ?? throw new ArgumentNullException(nameof(apparel)),
            capacityRouting ?? throw new ArgumentNullException(nameof(capacityRouting)),
            physical ?? throw new ArgumentNullException(nameof(physical)),
            stockSensor ?? throw new ArgumentNullException(nameof(stockSensor))
        };
        requireCurrentFormatAggregateSchema = true;
        Array.Sort(contributors, (left, right) =>
            string.CompareOrdinal(left.ContributorId, right.ContributorId));
        for (int i = 1; i < contributors.Length; i++)
        {
            if (string.Equals(
                    contributors[i - 1].ContributorId,
                    contributors[i].ContributorId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Duplicate production lifecycle contributor: " + contributors[i].ContributorId);
            }
        }
    }

    internal ProductionOutputDestinationLifecycleQuery(
        IReadOnlyList<IProductionOutputDestinationLifecycleContributor> source)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("At least one lifecycle contributor is required.", nameof(source));
        contributors = new IProductionOutputDestinationLifecycleContributor[source.Count];
        requireCurrentFormatAggregateSchema = false;
        for (int i = 0; i < source.Count; i++)
        {
            contributors[i] = source[i]
                ?? throw new ArgumentException("Lifecycle contributors cannot contain null.", nameof(source));
        }
        Array.Sort(contributors, (left, right) =>
            string.CompareOrdinal(left.ContributorId, right.ContributorId));
        for (int i = 1; i < contributors.Length; i++)
        {
            if (string.Equals(
                    contributors[i - 1].ContributorId,
                    contributors[i].ContributorId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Duplicate production lifecycle contributor: " + contributors[i].ContributorId);
            }
        }
    }

    public ProductionOutputDestinationLifecycleSnapshot Capture(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid production facility ID is required.", nameof(facilityId));
        ProductionOutputDestinationId destinationId =
            ProductionOutputDestinationId.FromFacility(facilityId);
        ProductionOutputDestinationLifecycleContribution[] captured =
            new ProductionOutputDestinationLifecycleContribution[contributors.Length];
        StringBuilder canonical = new StringBuilder(256)
            .Append(facilityId.Value).Append('|').Append(destinationId.Value).Append('|');
        List<KeyValuePair<string, string>> durableContributors = new(
            contributors.Length);
        for (int i = 0; i < contributors.Length; i++)
        {
            captured[i] = contributors[i].Capture(facilityId, destinationId);
            if (!string.Equals(
                    captured[i].ContributorId,
                    contributors[i].ContributorId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Lifecycle contributor returned the wrong identity.");
            }
            canonical.Append(captured[i].ContributorId).Append('|')
                .Append(captured[i].AuthorityRevision.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(captured[i].SemanticFingerprint).Append(';');
            durableContributors.Add(new KeyValuePair<string, string>(
                captured[i].ContributorId,
                captured[i].DurableSemanticFingerprint));
        }
        return new ProductionOutputDestinationLifecycleSnapshot(
            facilityId,
            destinationId,
            captured,
            ProductionLifecycleFingerprint.Compute(canonical.ToString()),
            requireCurrentFormatAggregateSchema
                ? ProductionOutputDestinationDurableSaveProjector.ComposeAggregate(
                    facilityId,
                    durableContributors)
                : ProductionOutputDestinationDurableSaveProjector.ComposeAggregateFixture(
                    facilityId,
                    durableContributors));
    }
}

internal static class ProductionLifecycleFingerprint
{
    internal static string Compute(string canonical)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical ?? string.Empty));
        StringBuilder result = new StringBuilder(digest.Length * 2);
        for (int i = 0; i < digest.Length; i++)
            result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    internal static void AppendSaveRecord<T>(
        StringBuilder canonical,
        T record)
        where T : class
    {
        if (canonical == null)
            throw new ArgumentNullException(nameof(canonical));
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        string payload = JsonUtility.ToJson(record);
        canonical.Append(Encoding.UTF8.GetByteCount(payload)
                .ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(payload)
            .Append(';');
    }
}
