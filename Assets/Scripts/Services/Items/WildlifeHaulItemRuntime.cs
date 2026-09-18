using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum WildlifeHaulPlanStatus
{
    Pending = 0,
    Reserved = 1,
    NoWork = 2,
    Blocked = 3
}

public interface IWildlifeHaulItemRuntime
{
    WildlifeHaulPlanStatus TryReserveNext(
        string wildlifeId,
        Vector2Int start,
        long maximumCargoMassGrams,
        out WildlifeHaulAssignmentSnapshot reservation,
        out string failureReason);

    bool TryPickup(
        WildlifeActor actor,
        WildlifeHaulAssignmentSnapshot reservation,
        out WildlifeHaulAssignmentSnapshot cargo,
        out string failureReason);

    bool TryMoveCargo(
        WildlifeHaulAssignmentSnapshot cargo,
        Vector2Int position,
        out string failureReason);

    bool TryDeliver(
        WildlifeHaulAssignmentSnapshot cargo,
        out string failureReason);

    void ReleaseUnpicked(WildlifeHaulAssignmentSnapshot reservation);

    bool RenewUnpicked(WildlifeHaulAssignmentSnapshot reservation);

    bool TryCommitRecoveryPending(
        WildlifeHaulAssignmentSnapshot cargo,
        Vector2Int interruptionPosition,
        WorldItemCarryInterruptionKind interruptionKind,
        out string failureReason);
}

public interface IWildlifeHaulLifecycleSink
{
    void OnWildlifeUnavailable(
        WildlifeActor actor,
        WorldItemCarryInterruptionKind interruptionKind);
}

internal enum WildlifeHaulCargoCustodyPhase
{
    CargoOwned = 1,
    RecoveryPending = 2
}

internal readonly struct WildlifeHaulCargoCustody
{
    internal WildlifeHaulCargoCustody(
        WildlifeHaulCargoCustodyPhase phase,
        string wildlifeId,
        string operationId,
        string sourceStackId,
        string itemId,
        string expectedStackSignature,
        int quantity,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition,
        Vector2Int interruptionPosition,
        WorldItemCarryInterruptionKind interruptionKind,
        double recoveryDeadlineGameTime)
    {
        Phase = phase;
        WildlifeId = wildlifeId ?? string.Empty;
        OperationId = operationId ?? string.Empty;
        SourceStackId = sourceStackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        ExpectedStackSignature = expectedStackSignature ?? string.Empty;
        Quantity = quantity;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        DeliveryPosition = deliveryPosition;
        DropPosition = dropPosition;
        InterruptionPosition = interruptionPosition;
        InterruptionKind = interruptionKind;
        RecoveryDeadlineGameTime = recoveryDeadlineGameTime;
    }

    internal WildlifeHaulCargoCustodyPhase Phase { get; }
    internal string WildlifeId { get; }
    internal string OperationId { get; }
    internal string SourceStackId { get; }
    internal string ItemId { get; }
    internal string ExpectedStackSignature { get; }
    internal int Quantity { get; }
    internal WorldItemHaulDestinationKind DestinationKind { get; }
    internal string DestinationId { get; }
    internal Vector2Int DeliveryPosition { get; }
    internal Vector2Int DropPosition { get; }
    internal Vector2Int InterruptionPosition { get; }
    internal WorldItemCarryInterruptionKind InterruptionKind { get; }
    internal double RecoveryDeadlineGameTime { get; }
}

internal static class WildlifeHaulCargoCustodyCodec
{
    internal const string ComponentTypeId =
        ItemInstanceComponentIds.WildlifeHaulCustody;
    internal const int SchemaVersion = 1;

    internal static bool HasAny(IEnumerable<ItemInstanceComponentSaveData> values) =>
        (values ?? Array.Empty<ItemInstanceComponentSaveData>()).Any(IsComponent);

    internal static bool IsComponent(ItemInstanceComponentSaveData value) =>
        value != null
        && string.Equals(value.componentTypeId, ComponentTypeId,
            StringComparison.Ordinal);

    internal static ItemInstanceComponentSaveData Create(
        WildlifeHaulCargoCustody value) => new()
        {
            componentTypeId = ComponentTypeId,
            schemaVersion = SchemaVersion,
            affectsStacking = false,
            values = new List<ItemStateValueSaveData>
            {
                Integer("phase", (int)value.Phase),
                Text("wildlife-id", value.WildlifeId),
                Text("operation-id", value.OperationId),
                Text("source-stack-id", value.SourceStackId),
                Text("item-id", value.ItemId),
                Text("expected-stack-signature", value.ExpectedStackSignature),
                Integer("quantity", value.Quantity),
                Integer("destination-kind", (int)value.DestinationKind),
                Text("destination-id", value.DestinationId),
                Integer("delivery-x", value.DeliveryPosition.x),
                Integer("delivery-y", value.DeliveryPosition.y),
                Integer("drop-x", value.DropPosition.x),
                Integer("drop-y", value.DropPosition.y),
                Integer("interruption-x", value.InterruptionPosition.x),
                Integer("interruption-y", value.InterruptionPosition.y),
                Integer("interruption-kind", (int)value.InterruptionKind),
                Decimal("recovery-deadline", value.RecoveryDeadlineGameTime)
            }
        };

    internal static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> source,
        out WildlifeHaulCargoCustody value)
    {
        value = default;
        ItemInstanceComponentSaveData[] matches = (source
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(IsComponent)
            .ToArray();
        if (matches.Length != 1
            || matches[0].schemaVersion != SchemaVersion
            || matches[0].affectsStacking
            || matches[0].values == null
            || matches[0].values.Count != 17)
        {
            return false;
        }

        IReadOnlyList<ItemStateValueSaveData> fields = matches[0].values;
        if (!TryInt(fields, "phase", out int rawPhase)
            || !TryText(fields, "wildlife-id", true, out string wildlifeId)
            || !TryText(fields, "operation-id", true, out string operationId)
            || !TryText(fields, "source-stack-id", true, out string sourceStackId)
            || !TryText(fields, "item-id", true, out string itemId)
            || !TryText(fields, "expected-stack-signature", true,
                out string expectedSignature)
            || !TryInt(fields, "quantity", out int quantity)
            || !TryInt(fields, "destination-kind", out int rawDestinationKind)
            || !TryText(fields, "destination-id", true, out string destinationId)
            || !TryInt(fields, "delivery-x", out int deliveryX)
            || !TryInt(fields, "delivery-y", out int deliveryY)
            || !TryInt(fields, "drop-x", out int dropX)
            || !TryInt(fields, "drop-y", out int dropY)
            || !TryInt(fields, "interruption-x", out int interruptionX)
            || !TryInt(fields, "interruption-y", out int interruptionY)
            || !TryInt(fields, "interruption-kind", out int rawInterruption)
            || !TryDecimal(fields, "recovery-deadline", out double deadline)
            || !Enum.IsDefined(typeof(WildlifeHaulCargoCustodyPhase), rawPhase)
            || !Enum.IsDefined(typeof(WorldItemHaulDestinationKind),
                rawDestinationKind)
            || !Enum.IsDefined(typeof(WorldItemCarryInterruptionKind),
                rawInterruption)
            || quantity <= 0
            || double.IsNaN(deadline)
            || double.IsInfinity(deadline)
            || deadline < 0d)
        {
            return false;
        }

        WildlifeHaulCargoCustodyPhase phase =
            (WildlifeHaulCargoCustodyPhase)rawPhase;
        WorldItemCarryInterruptionKind interruption =
            (WorldItemCarryInterruptionKind)rawInterruption;
        bool phaseShape = phase == WildlifeHaulCargoCustodyPhase.CargoOwned
            ? interruption == WorldItemCarryInterruptionKind.None
                && interruptionX == 0
                && interruptionY == 0
                && deadline == 0d
            : interruption is WorldItemCarryInterruptionKind.Downed
                    or WorldItemCarryInterruptionKind.Dead
                    or WorldItemCarryInterruptionKind.Disabled
                && deadline > 0d;
        if (!phaseShape)
        {
            return false;
        }

        value = new WildlifeHaulCargoCustody(
            phase,
            wildlifeId,
            operationId,
            sourceStackId,
            itemId,
            expectedSignature,
            quantity,
            (WorldItemHaulDestinationKind)rawDestinationKind,
            destinationId,
            new Vector2Int(deliveryX, deliveryY),
            new Vector2Int(dropX, dropY),
            new Vector2Int(interruptionX, interruptionY),
            interruption,
            deadline);
        return true;
    }

    private static ItemStateValueSaveData Text(string key, string value) => new()
    {
        key = key,
        kind = ItemStateValueKind.String,
        stringValue = value ?? string.Empty
    };

    private static ItemStateValueSaveData Integer(string key, long value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Integer,
        integerValue = value
    };

    private static ItemStateValueSaveData Decimal(string key, double value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Decimal,
        decimalValue = value
    };

    private static bool TryText(
        IReadOnlyList<ItemStateValueSaveData> fields,
        string key,
        bool required,
        out string value)
    {
        value = string.Empty;
        ItemStateValueSaveData[] matches = fields.Where(field => field != null
            && string.Equals(field.key, key, StringComparison.Ordinal)
            && field.kind == ItemStateValueKind.String).ToArray();
        if (matches.Length != 1
            || matches[0].stringValue == null
            || !string.Equals(matches[0].stringValue,
                matches[0].stringValue.Trim(), StringComparison.Ordinal)
            || required && matches[0].stringValue.Length == 0)
        {
            return false;
        }
        value = matches[0].stringValue;
        return true;
    }

    private static bool TryInt(
        IReadOnlyList<ItemStateValueSaveData> fields,
        string key,
        out int value)
    {
        value = 0;
        ItemStateValueSaveData[] matches = fields.Where(field => field != null
            && string.Equals(field.key, key, StringComparison.Ordinal)
            && field.kind == ItemStateValueKind.Integer).ToArray();
        if (matches.Length != 1
            || matches[0].integerValue < int.MinValue
            || matches[0].integerValue > int.MaxValue)
        {
            return false;
        }
        value = (int)matches[0].integerValue;
        return true;
    }

    private static bool TryDecimal(
        IReadOnlyList<ItemStateValueSaveData> fields,
        string key,
        out double value)
    {
        value = 0d;
        ItemStateValueSaveData[] matches = fields.Where(field => field != null
            && string.Equals(field.key, key, StringComparison.Ordinal)
            && field.kind == ItemStateValueKind.Decimal).ToArray();
        if (matches.Length != 1)
        {
            return false;
        }
        value = matches[0].decimalValue;
        return true;
    }
}

public sealed class WildlifeHaulItemRuntime :
    IWildlifeHaulItemRuntime,
    ITickable
{
    private const double RecoveryWindowSeconds = 15d;

    private readonly WorldItemRepository repository;
    private readonly IItemTransferService transfers;
    private readonly IReservedItemTransferService reservedTransfers;
    private readonly IItemQuantityReservationService reservations;
    private readonly IPhysicalItemMassQuery mass;
    private readonly IGridSystemProvider gridProvider;
    private readonly IGridPathSearchBroker paths;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IGameClock clock;

    public WildlifeHaulItemRuntime(
        WorldItemRepository repository,
        IItemTransferService transfers,
        IReservedItemTransferService reservedTransfers,
        IItemQuantityReservationService reservations,
        IPhysicalItemMassQuery mass,
        IGridSystemProvider gridProvider,
        IGridPathSearchBroker paths,
        ICharacterAiWorldRegistry world,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        IDungeonItemCatalogProvider catalog,
        IGameClock clock)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.reservedTransfers = reservedTransfers
            ?? throw new ArgumentNullException(nameof(reservedTransfers));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public WildlifeHaulPlanStatus TryReserveNext(
        string wildlifeId,
        Vector2Int start,
        long maximumCargoMassGrams,
        out WildlifeHaulAssignmentSnapshot reservation,
        out string failureReason)
    {
        reservation = default;
        failureReason = string.Empty;
        if (!IsCanonicalRequired(wildlifeId)
            || maximumCargoMassGrams <= 0L
            || !gridProvider.TryGetGrid(out Grid grid)
            || grid == null)
        {
            failureReason = "독립 운반 계획 권위가 유효하지 않습니다.";
            return WildlifeHaulPlanStatus.Blocked;
        }
        if (!paths.TryGetSearch(
                grid,
                start,
                out GridPathSearchResult reachable,
                GridPathSearchPriority.Normal,
                GridTraversalContext.ForWildlife(wildlifeId)))
        {
            failureReason = "독립 운반 경로 계산 대기";
            return WildlifeHaulPlanStatus.Pending;
        }

        Candidate selected = repository.Records
            .Where(IsCandidateSource)
            .Select(record => TryBuildCandidate(
                grid,
                reachable,
                record,
                maximumCargoMassGrams,
                out Candidate candidate)
                    ? candidate
                    : null)
            .Where(candidate => candidate != null)
            .OrderBy(candidate => candidate.TotalCost)
            .ThenBy(candidate => candidate.Record.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected == null)
        {
            failureReason = "운반 가능한 물품과 목적지가 없습니다.";
            return WildlifeHaulPlanStatus.NoWork;
        }

        string operationId = repository.AllocateHaulDeliveryOperationId(wildlifeId);
        if (!transfers.TryReserveAvailableStackForDirectPickup(
                wildlifeId,
                operationId,
                ItemReservationPurpose.Hauling,
                selected.Record.stackId,
                selected.Quantity,
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure))
        {
            failureReason = reserveFailure.ToString();
            return WildlifeHaulPlanStatus.Blocked;
        }

        reservation = new WildlifeHaulAssignmentSnapshot(
            wildlifeId,
            CapturedWildlifeHaulPhase.Reserved,
            0f,
            operationId,
            lease.leaseId,
            selected.Record.stackId,
            string.Empty,
            selected.Record.itemId,
            ItemStackSignature.Create(
                selected.Record.itemId,
                selected.Record.components),
            selected.Quantity,
            selected.PickupStand,
            selected.Destination.Kind,
            selected.Destination.DestinationId,
            selected.Destination.DeliveryPosition,
            selected.Destination.DropPosition,
            default);
        return WildlifeHaulPlanStatus.Reserved;
    }

    public bool TryPickup(
        WildlifeActor actor,
        WildlifeHaulAssignmentSnapshot reservation,
        out WildlifeHaulAssignmentSnapshot cargo,
        out string failureReason)
    {
        cargo = default;
        failureReason = string.Empty;
        if (actor == null
            || !string.Equals(actor.WildlifeId, reservation.WildlifeId,
                StringComparison.Ordinal)
            || reservation.Phase != CapturedWildlifeHaulPhase.Reserved
            || Manhattan(actor.GridPosition, reservation.PickupStandPosition) > 0
            || !repository.RecordsById.TryGetValue(
                reservation.SourceStackId,
                out WorldItemStackRecord source)
            || !MatchesReservedSource(source, reservation))
        {
            failureReason = "독립 운반 픽업 원본이 변경되었거나 거리가 멉니다.";
            return false;
        }
        if (!TryRevalidateSourceOwner(source)
            || !TryRevalidateDestination(reservation, out failureReason))
        {
            if (failureReason.Length == 0)
            {
                failureReason = "독립 운반 원본 또는 목적지 소유권이 변경되었습니다.";
            }
            return false;
        }
        if (!reservations.Revalidate(
                reservation.LeaseId,
                out ItemQuantityLease lease,
                out DomainFailure leaseFailure)
            || !string.Equals(lease.ownerCharacterId, reservation.WildlifeId,
                StringComparison.Ordinal)
            || !string.Equals(lease.ownerOperationId, reservation.OperationId,
                StringComparison.Ordinal)
            || lease.remainingQuantity < reservation.Quantity)
        {
            failureReason = leaseFailure.IsFailure
                ? leaseFailure.ToString()
                : "독립 운반 수량 임대가 변경되었습니다.";
            return false;
        }

        if (!reservedTransfers.TryExtractReservedQuantity(
                reservation.LeaseId,
                reservation.Quantity,
                new ItemTransitDestination(
                    WorldItemStackState.InTransit,
                    actor.GridPosition,
                    reservation.OperationId),
                out ItemExtractionReceipt receipt,
                out DomainFailure extractionFailure))
        {
            failureReason = extractionFailure.ToString();
            return false;
        }
        if (!repository.RecordsById.TryGetValue(
                receipt.ExtractedStackId,
                out WorldItemStackRecord extracted)
            || extracted == null
            || extracted.state != WorldItemStackState.InTransit
            || extracted.quantity != reservation.Quantity
            || !string.Equals(extracted.itemId, reservation.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                ItemStackSignature.Create(extracted.itemId, extracted.components),
                reservation.ExpectedStackSignature,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Reserved wildlife haul extraction violated its atomic receipt contract: "
                + reservation.OperationId);
        }

        extracted.components.Add(WildlifeHaulCargoCustodyCodec.Create(
            ToCustody(reservation, WildlifeHaulCargoCustodyPhase.CargoOwned)));
        repository.MarkChanged();
        transfers.ReleaseQuantityReservation(
            reservation.LeaseId,
            ItemReservationReleaseReason.Completed);
        cargo = new WildlifeHaulAssignmentSnapshot(
            reservation.WildlifeId,
            CapturedWildlifeHaulPhase.CargoOwned,
            0f,
            reservation.OperationId,
            string.Empty,
            reservation.SourceStackId,
            extracted.stackId,
            reservation.ItemId,
            reservation.ExpectedStackSignature,
            reservation.Quantity,
            reservation.PickupStandPosition,
            reservation.DestinationKind,
            reservation.DestinationId,
            reservation.DeliveryPosition,
            reservation.DropPosition,
            reservation.PenPosition);
        return true;
    }

    public bool TryMoveCargo(
        WildlifeHaulAssignmentSnapshot cargo,
        Vector2Int position,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetCargoRecord(cargo, out WorldItemStackRecord record))
        {
            failureReason = "독립 운반 화물 소유권이 일치하지 않습니다.";
            return false;
        }
        repository.Relocate(record, position);
        repository.MarkChanged();
        return true;
    }

    public bool TryDeliver(
        WildlifeHaulAssignmentSnapshot cargo,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetCargoRecord(cargo, out WorldItemStackRecord record))
        {
            failureReason = "독립 운반 화물 소유권이 일치하지 않습니다.";
            return false;
        }
        if (record.position != cargo.DeliveryPosition)
        {
            failureReason = "독립 운반 화물이 확정된 배송 접근 칸에 도착하지 않았습니다.";
            return false;
        }
        ItemInstanceComponentSaveData component = record.components.Single(
            WildlifeHaulCargoCustodyCodec.IsComponent);
        record.components.Remove(component);
        repository.MarkChanged();

        bool delivered;
        DomainFailure failure = DomainFailure.None;
        if (cargo.DestinationKind == WorldItemHaulDestinationKind.Warehouse)
        {
            IWarehouseFacility[] warehouseMatches =
                (world.Warehouses ?? Array.Empty<IWarehouseFacility>())
                .Where(value => value != null
                    && value.PersistentInstanceId.IsValid
                    && string.Equals(
                        WarehouseStorageIdentity.RequireDestinationId(value),
                        cargo.DestinationId,
                        StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            IWarehouseFacility warehouse = warehouseMatches.Length == 1
                ? warehouseMatches[0]
                : null;
            delivered = warehouse != null
                && transfers.TryCompleteTransitToWarehouse(
                    new ItemStackId(cargo.CargoStackId),
                    cargo.OperationId,
                    warehouse,
                    out _,
                    out failure);
        }
        else
        {
            delivered = transfers.TryCompleteTransitToFacilityBuffer(
                new ItemStackId(cargo.CargoStackId),
                cargo.OperationId,
                cargo.DropPosition,
                cargo.DestinationId,
                out _,
                out failure);
        }
        if (delivered)
        {
            return true;
        }

        if (repository.RecordsById.TryGetValue(
                cargo.CargoStackId,
                out WorldItemStackRecord retained)
            && retained != null
            && !WildlifeHaulCargoCustodyCodec.HasAny(retained.components))
        {
            retained.components.Add(component);
            repository.MarkChanged();
        }
        failureReason = failure.IsFailure
            ? failure.ToString()
            : "독립 운반 목적지가 현재 화물을 받을 수 없습니다.";
        return false;
    }

    public void ReleaseUnpicked(WildlifeHaulAssignmentSnapshot reservation)
    {
        if (!string.IsNullOrEmpty(reservation.LeaseId))
        {
            transfers.ReleaseQuantityReservation(
                reservation.LeaseId,
                ItemReservationReleaseReason.Cancelled);
        }
        if (!string.IsNullOrEmpty(reservation.OperationId))
        {
            transfers.ReleaseQuantityReservationsByOwner(
                reservation.OperationId,
                ItemReservationReleaseReason.Cancelled);
        }
    }

    public bool RenewUnpicked(WildlifeHaulAssignmentSnapshot reservation) =>
        !string.IsNullOrEmpty(reservation.LeaseId)
        && transfers.RenewQuantityReservation(
            reservation.LeaseId,
            Math.Max(0d, clock.Time) + 5d,
            out _);

    public bool TryCommitRecoveryPending(
        WildlifeHaulAssignmentSnapshot cargo,
        Vector2Int interruptionPosition,
        WorldItemCarryInterruptionKind interruptionKind,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (interruptionKind is not (WorldItemCarryInterruptionKind.Downed
                or WorldItemCarryInterruptionKind.Dead
                or WorldItemCarryInterruptionKind.Disabled)
            || !TryGetCargoRecord(cargo, out WorldItemStackRecord record))
        {
            failureReason = "독립 운반 중단 화물 소유권이 일치하지 않습니다.";
            return false;
        }

        record.components.RemoveAll(WildlifeHaulCargoCustodyCodec.IsComponent);
        record.components.Add(WildlifeHaulCargoCustodyCodec.Create(
            new WildlifeHaulCargoCustody(
                WildlifeHaulCargoCustodyPhase.RecoveryPending,
                cargo.WildlifeId,
                cargo.OperationId,
                cargo.SourceStackId,
                cargo.ItemId,
                cargo.ExpectedStackSignature,
                cargo.Quantity,
                cargo.DestinationKind,
                cargo.DestinationId,
                cargo.DeliveryPosition,
                cargo.DropPosition,
                interruptionPosition,
                interruptionKind,
                Math.Max(0d, clock.Time) + RecoveryWindowSeconds)));
        repository.Relocate(record, interruptionPosition);
        repository.MarkChanged();
        return true;
    }

    public void Tick()
    {
        if (clock.IsPaused)
        {
            return;
        }
        WorldItemStackRecord[] pending = repository.Records
            .Where(record => record != null
                && record.state == WorldItemStackState.InTransit
                && WildlifeHaulCargoCustodyCodec.TryRead(
                    record.components,
                    out WildlifeHaulCargoCustody custody)
                && custody.Phase == WildlifeHaulCargoCustodyPhase.RecoveryPending)
            .ToArray();
        foreach (WorldItemStackRecord record in pending)
        {
            TryPublishRecoveryDrop(record);
        }
    }

    private void TryPublishRecoveryDrop(WorldItemStackRecord record)
    {
        if (!WildlifeHaulCargoCustodyCodec.TryRead(
                record.components,
                out WildlifeHaulCargoCustody custody)
            || !gridProvider.TryGetGrid(out Grid grid)
            || grid == null)
        {
            return;
        }
        Vector2Int cell = custody.InterruptionPosition;
        if (!grid.IsValidGridPos(cell)
            || !grid.IsWalkable(cell))
        {
            if (!grid.TryFindNearbyWalkablePositionOnSameFloor(
                    cell,
                    out cell,
                    maxDistance: 2))
            {
                return;
            }
        }

        ItemInstanceComponentSaveData component = record.components.Single(
            WildlifeHaulCargoCustodyCodec.IsComponent);
        record.components.Remove(component);
        repository.MarkChanged();
        if (!transfers.TryCompleteTransit(
                new ItemStackId(record.stackId),
                custody.OperationId,
                WorldItemStackState.Loose,
                cell,
                string.Empty,
                out _))
        {
            record.components.Add(component);
            repository.MarkChanged();
            return;
        }

        record.dropDisposition = WorldItemDropDisposition.TransientCarryRecoveryDrop;
        record.recoveryOwnerOperationId = custody.OperationId;
        record.recoverySourceStackId = custody.SourceStackId;
        record.recoveryCarrierPersistentId = custody.WildlifeId;
        record.recoveryInterruptionKind = custody.InterruptionKind;
        record.droppedAtGameTime = Math.Max(
            0d,
            custody.RecoveryDeadlineGameTime - RecoveryWindowSeconds);
        record.recoveryDeadlineGameTime = custody.RecoveryDeadlineGameTime;
        repository.MarkChanged();
    }

    private bool TryBuildCandidate(
        Grid grid,
        GridPathSearchResult reachable,
        WorldItemStackRecord record,
        long maximumMass,
        out Candidate candidate)
    {
        candidate = null;
        int available = reservations.GetAvailableQuantity(
            new ItemStackId(record.stackId));
        long unitMass;
        try
        {
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                mass,
                (ItemDefinitionId)record.itemId,
                record.itemInstanceId,
                record.components);
            unitMass = mass.GetStackUnitMass(
                (ItemDefinitionId)record.itemId,
                subject).Value;
        }
        catch
        {
            return false;
        }
        if (unitMass <= 0L)
        {
            return false;
        }
        int quantity = (int)Math.Min(
            available,
            Math.Min(int.MaxValue, maximumMass / unitMass));
        if (quantity <= 0
            || !TryPickupStand(grid, reachable, record.position,
                out Vector2Int pickup))
        {
            return false;
        }

        WorldItemHaulDestinationAuthority.Resolution destination;
        if (record.hasDestinationPosition
            && !string.IsNullOrEmpty(record.destinationId))
        {
            WorldItemHaulDestinationKind kind = record.destinationId.StartsWith(
                    WarehouseStorageIdentity.DestinationPrefix,
                    StringComparison.Ordinal)
                ? WorldItemHaulDestinationKind.Warehouse
                : WorldItemHaulDestinationKind.FacilityBuffer;
            if (!WorldItemHaulDestinationAuthority.TryResolve(
                    grid,
                    world,
                    destinationClaims,
                    kind,
                    record.destinationId,
                    record.destinationPosition,
                    out destination,
                    out _))
            {
                return false;
            }
        }
        else if (!TryFindWarehouse(
                     grid,
                     record,
                     quantity,
                     unitMass,
                     out destination))
        {
            return false;
        }
        if (reachable.GetMoveCostTo(destination.DeliveryPosition) == int.MaxValue)
        {
            return false;
        }

        if (destination.Kind == WorldItemHaulDestinationKind.Warehouse)
        {
            quantity = Math.Min(
                quantity,
                destination.Warehouse.Inventory.GetAcceptableQuantity(
                    record.itemId,
                    quantity));
            quantity = (int)Math.Min(
                quantity,
                destination.Warehouse.Inventory.RemainingMassGrams
                    / Math.Max(1L, unitMass));
        }
        if (quantity <= 0)
        {
            return false;
        }

        candidate = new Candidate(
            record,
            quantity,
            pickup,
            destination,
            reachable.GetMoveCostTo(pickup)
                + reachable.GetMoveCostTo(destination.DeliveryPosition));
        return true;
    }

    private bool TryFindWarehouse(
        Grid grid,
        WorldItemStackRecord record,
        int quantity,
        long unitMass,
        out WorldItemHaulDestinationAuthority.Resolution destination)
    {
        destination = default;
        if (!catalog.TryGetDefinition(
                record.itemId,
                out DungeonItemDefinition definition)
            || definition == null)
        {
            return false;
        }
        IWarehouseFacility warehouse = (world.Warehouses
                ?? Array.Empty<IWarehouseFacility>())
            .Where(value => value != null
                && value.PersistentInstanceId.IsValid
                && value.HasWarehouseInventory
                && value.Inventory != null
                && value.Inventory.Accepts(definition.StockCategory)
                && value.Inventory.GetAcceptableQuantity(record.itemId, quantity) > 0
                && value.Inventory.RemainingMassGrams >= unitMass
                && value is BuildableObject building
                && !building.isDestroy)
            .OrderBy(value => value is BuildableObject building
                ? Manhattan(record.position, building.centerPos)
                : int.MaxValue)
            .ThenBy(value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (warehouse == null)
        {
            return false;
        }
        string destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        Vector2Int requested = warehouse is BuildableObject owner
            ? owner.centerPos
            : default;
        return WorldItemHaulDestinationAuthority.TryResolve(
            grid,
            world,
            destinationClaims,
            WorldItemHaulDestinationKind.Warehouse,
            destinationId,
            requested,
            out destination,
            out _);
    }

    private static bool IsCandidateSource(WorldItemStackRecord record) =>
        record != null
        && record.quantity > 0
        && !record.forbidden
        && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(record.components)
        && !WildlifeHaulCargoCustodyCodec.HasAny(record.components)
        && (record.state == WorldItemStackState.Loose
            || record.state == WorldItemStackState.Stored
                && record.hasDestinationPosition
                && !string.IsNullOrEmpty(record.destinationId)
                && !string.IsNullOrEmpty(record.sourceStorageDestinationId));

    private static bool MatchesReservedSource(
        WorldItemStackRecord source,
        WildlifeHaulAssignmentSnapshot reservation) =>
        IsCandidateSource(source)
        && source.quantity >= reservation.Quantity
        && Manhattan(source.position, reservation.PickupStandPosition) <= 1
        && string.Equals(source.itemId, reservation.ItemId,
            StringComparison.Ordinal)
        && string.Equals(
            ItemStackSignature.Create(source.itemId, source.components),
            reservation.ExpectedStackSignature,
            StringComparison.Ordinal)
        && (!source.hasDestinationPosition
            || string.Equals(source.destinationId, reservation.DestinationId,
                StringComparison.Ordinal)
                && source.destinationPosition == reservation.DropPosition);

    private bool TryRevalidateSourceOwner(WorldItemStackRecord source)
    {
        if (source.state != WorldItemStackState.Stored)
        {
            return true;
        }
        return (world.Warehouses ?? Array.Empty<IWarehouseFacility>()).Count(value =>
            value != null
            && value.PersistentInstanceId.IsValid
            && value.HasWarehouseInventory
            && value.Inventory != null
            && value is BuildableObject building
            && !building.isDestroy
            && string.Equals(
                WarehouseStorageIdentity.RequireDestinationId(value),
                source.sourceStorageDestinationId,
                StringComparison.Ordinal)) == 1;
    }

    private bool TryRevalidateDestination(
        WildlifeHaulAssignmentSnapshot reservation,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!gridProvider.TryGetGrid(out Grid grid)
            || grid == null
            || !WorldItemHaulDestinationAuthority.TryResolve(
                grid,
                world,
                destinationClaims,
                reservation.DestinationKind,
                reservation.DestinationId,
                reservation.DropPosition,
                out WorldItemHaulDestinationAuthority.Resolution destination,
                out failureReason))
        {
            return false;
        }
        if (destination.Kind != reservation.DestinationKind
            || !string.Equals(destination.DestinationId,
                reservation.DestinationId, StringComparison.Ordinal)
            || destination.DeliveryPosition != reservation.DeliveryPosition
            || destination.DropPosition != reservation.DropPosition)
        {
            failureReason = "독립 운반 목적지 결속이 변경되었습니다.";
            return false;
        }
        return true;
    }

    private bool TryGetCargoRecord(
        WildlifeHaulAssignmentSnapshot cargo,
        out WorldItemStackRecord record)
    {
        record = null;
        return cargo.Phase is CapturedWildlifeHaulPhase.CargoOwned
                or CapturedWildlifeHaulPhase.ReleasePending
            && repository.RecordsById.TryGetValue(cargo.CargoStackId, out record)
            && record != null
            && record.state == WorldItemStackState.InTransit
            && string.Equals(record.destinationId, cargo.OperationId,
                StringComparison.Ordinal)
            && string.Equals(record.itemId, cargo.ItemId, StringComparison.Ordinal)
            && record.quantity == cargo.Quantity
            && string.Equals(
                ItemStackSignature.Create(record.itemId, record.components),
                cargo.ExpectedStackSignature,
                StringComparison.Ordinal)
            && WildlifeHaulCargoCustodyCodec.TryRead(
                record.components,
                out WildlifeHaulCargoCustody custody)
            && custody.Phase == WildlifeHaulCargoCustodyPhase.CargoOwned
            && Matches(cargo, custody);
    }

    private static WildlifeHaulCargoCustody ToCustody(
        WildlifeHaulAssignmentSnapshot value,
        WildlifeHaulCargoCustodyPhase phase) => new(
            phase,
            value.WildlifeId,
            value.OperationId,
            value.SourceStackId,
            value.ItemId,
            value.ExpectedStackSignature,
            value.Quantity,
            value.DestinationKind,
            value.DestinationId,
            value.DeliveryPosition,
            value.DropPosition,
            default,
            WorldItemCarryInterruptionKind.None,
            0d);

    internal static bool Matches(
        WildlifeHaulAssignmentSnapshot role,
        WildlifeHaulCargoCustody cargo) =>
        string.Equals(role.WildlifeId, cargo.WildlifeId, StringComparison.Ordinal)
        && string.Equals(role.OperationId, cargo.OperationId, StringComparison.Ordinal)
        && string.Equals(role.SourceStackId, cargo.SourceStackId, StringComparison.Ordinal)
        && string.Equals(role.ItemId, cargo.ItemId, StringComparison.Ordinal)
        && string.Equals(role.ExpectedStackSignature,
            cargo.ExpectedStackSignature, StringComparison.Ordinal)
        && role.Quantity == cargo.Quantity
        && role.DestinationKind == cargo.DestinationKind
        && string.Equals(role.DestinationId, cargo.DestinationId,
            StringComparison.Ordinal)
        && role.DeliveryPosition == cargo.DeliveryPosition
        && role.DropPosition == cargo.DropPosition;

    private static bool TryPickupStand(
        Grid grid,
        GridPathSearchResult reachable,
        Vector2Int itemPosition,
        out Vector2Int stand)
    {
        stand = default;
        int best = int.MaxValue;
        foreach (Vector2Int candidate in new[]
                 {
                     itemPosition,
                     itemPosition + Vector2Int.left,
                     itemPosition + Vector2Int.right
                 })
        {
            if (!grid.IsValidGridPos(candidate) || !grid.IsWalkable(candidate))
            {
                continue;
            }
            int cost = reachable.GetMoveCostTo(candidate);
            if (cost < best)
            {
                best = cost;
                stand = candidate;
            }
        }
        return best != int.MaxValue;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private sealed class Candidate
    {
        internal Candidate(
            WorldItemStackRecord record,
            int quantity,
            Vector2Int pickupStand,
            WorldItemHaulDestinationAuthority.Resolution destination,
            int totalCost)
        {
            Record = record;
            Quantity = quantity;
            PickupStand = pickupStand;
            Destination = destination;
            TotalCost = totalCost;
        }

        internal WorldItemStackRecord Record { get; }
        internal int Quantity { get; }
        internal Vector2Int PickupStand { get; }
        internal WorldItemHaulDestinationAuthority.Resolution Destination { get; }
        internal int TotalCost { get; }
    }
}
