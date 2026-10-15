using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum HouseholdBirthAssignmentResolution
{
    None = 0,
    Assigned = 1,
    PendingParentHousehold = 2,
    PendingBedCapacity = 3
}

public readonly struct HouseholdBedCandidate
{
    public HouseholdBedCandidate(
        BuildingInstanceId roomId,
        BuildingInstanceId bedId,
        int slotIndex,
        float roomQuality)
    {
        if (!roomId.IsValid || !bedId.IsValid || slotIndex < 0)
            throw new ArgumentException("A household bed candidate requires canonical room, bed, and slot identity.");
        RoomId = roomId;
        BedId = bedId;
        SlotIndex = slotIndex;
        RoomQuality = Mathf.Clamp01(roomQuality);
    }

    public BuildingInstanceId RoomId { get; }
    public BuildingInstanceId BedId { get; }
    public int SlotIndex { get; }
    public float RoomQuality { get; }
}

public interface IHouseholdBedInventory
{
    IReadOnlyList<HouseholdBedCandidate> GetOperationalBedSlots();
}

public interface IHouseholdBirthAssignmentCommand
{
    bool TryResolveBirth(
        CharacterId childId,
        CharacterId firstParentId,
        CharacterId secondParentId,
        CharacterId carrierId,
        int absoluteDay,
        out HouseholdBirthAssignmentResolution resolution,
        out string failureReason);
}

public interface IHouseholdBirthAssignmentRecovery
{
    bool TryResumePending(out string failureReason);
}

/// <summary>
/// Enumerates only authored rest capacity in a usable room. Capacity remains
/// owned by the building/room policy; this adapter only projects stable slots.
/// </summary>
public sealed class HouseholdBedInventory : IHouseholdBedInventory
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IRoomLayoutCache rooms;
    private readonly IRoomFacilityPolicy roomPolicy;

    public HouseholdBedInventory(
        IBuildingWorldQuery buildings,
        IRoomLayoutCache rooms,
        IRoomFacilityPolicy roomPolicy)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        this.roomPolicy = roomPolicy
            ?? throw new ArgumentNullException(nameof(roomPolicy));
    }

    public IReadOnlyList<HouseholdBedCandidate> GetOperationalBedSlots()
    {
        var result = new List<HouseholdBedCandidate>();
        foreach (BuildableObject bed in buildings.Buildings
                     ?? Array.Empty<BuildableObject>())
        {
            if (bed == null
                || bed.isDestroy
                || bed.IsDamaged
                || !bed.PersistentInstanceId.IsValid
                || !bed.SupportsFacilityRole(FacilityRole.Rest)
                || !rooms.TryGetRoom(bed, out RoomInstance room)
                || room == null
                || !room.IsUsable
                || !roomPolicy.IsFacilityRoleAvailable(
                    bed,
                    FacilityRole.Rest,
                    out _))
                continue;

            int capacity = roomPolicy.GetEffectiveCapacity(bed);
            if (capacity <= 0)
                continue;
            BuildingInstanceId roomId = ResolveRoomAnchor(room, bed);
            for (int slot = 0; slot < capacity; slot++)
                result.Add(new HouseholdBedCandidate(
                    roomId,
                    bed.PersistentInstanceId,
                    slot,
                    room.GetQualityScore()));
        }

        return result
            .OrderByDescending(value => value.RoomQuality)
            .ThenBy(value => value.RoomId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.BedId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.SlotIndex)
            .ToArray();
    }

    private static BuildingInstanceId ResolveRoomAnchor(
        RoomInstance room,
        BuildableObject bed)
    {
        BuildingInstanceId anchor = room.Furniture
            .Where(value => value != null && value.PersistentInstanceId.IsValid)
            .Select(value => value.PersistentInstanceId)
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        return anchor.IsValid ? anchor : bed.RequirePersistentInstanceId();
    }
}

/// <summary>
/// A birth may join a parent's existing household, but this command never
/// invents a household. Missing ownership/capacity is saved as explicit
/// pending state and can be retried after a parent or bed becomes available.
/// </summary>
public sealed class HouseholdBirthAssignmentRuntime :
    IHouseholdBirthAssignmentCommand,
    IHouseholdBirthAssignmentRecovery
{
    public const string ParentHouseholdUnavailableReason =
        "household-parent-unassigned";
    public const string BedCapacityUnavailableReason =
        "household-bed-capacity-unavailable";

    private readonly IHouseholdService households;
    private readonly IHouseholdBedInventory beds;

    public HouseholdBirthAssignmentRuntime(
        IHouseholdService households,
        IHouseholdBedInventory beds)
    {
        this.households = households
            ?? throw new ArgumentNullException(nameof(households));
        this.beds = beds ?? throw new ArgumentNullException(nameof(beds));
    }

    public bool TryResolveBirth(
        CharacterId childId,
        CharacterId firstParentId,
        CharacterId secondParentId,
        CharacterId carrierId,
        int absoluteDay,
        out HouseholdBirthAssignmentResolution resolution,
        out string failureReason)
    {
        resolution = HouseholdBirthAssignmentResolution.None;
        failureReason = string.Empty;
        if (!childId.IsValid
            || !firstParentId.IsValid
            || secondParentId.Value.Length > 0 && !secondParentId.IsValid
            || carrierId.Value.Length > 0 && !carrierId.IsValid
            || absoluteDay < 1)
        {
            failureReason = "household-birth-request-invalid";
            return false;
        }

        if (households.TryGet(childId, out CharacterRoomAssignmentSaveData existing))
        {
            HouseholdId expected = ResolveParentHousehold(
                carrierId,
                firstParentId,
                secondParentId);
            if (!expected.IsValid
                || !string.Equals(
                    existing.householdId,
                    expected.Value,
                    StringComparison.Ordinal))
            {
                failureReason = "household-birth-existing-assignment-conflict";
                return false;
            }
            resolution = HouseholdBirthAssignmentResolution.Assigned;
            return true;
        }

        HouseholdId householdId = ResolveParentHousehold(
            carrierId,
            firstParentId,
            secondParentId);
        if (!householdId.IsValid)
        {
            households.MarkPending(
                childId,
                default,
                firstParentId,
                secondParentId,
                carrierId,
                absoluteDay,
                ParentHouseholdUnavailableReason);
            resolution = HouseholdBirthAssignmentResolution
                .PendingParentHousehold;
            return true;
        }

        HashSet<string> occupied = households.Assignments
            .Select(value => BedSlotKey(
                new BuildingInstanceId(value.bedBuildingId),
                value.bedSlotIndex))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> householdRooms = households.Assignments
            .Where(value => string.Equals(
                value.householdId,
                householdId.Value,
                StringComparison.Ordinal))
            .Select(value => value.roomBuildingId)
            .ToHashSet(StringComparer.Ordinal);
        HouseholdBedCandidate? available = beds.GetOperationalBedSlots()
            .Where(value => !occupied.Contains(BedSlotKey(
                value.BedId,
                value.SlotIndex)))
            .OrderByDescending(value => householdRooms.Contains(
                value.RoomId.Value))
            .ThenByDescending(value => value.RoomQuality)
            .ThenBy(value => value.RoomId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.BedId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.SlotIndex)
            .Cast<HouseholdBedCandidate?>()
            .FirstOrDefault();
        if (!available.HasValue)
        {
            households.MarkPending(
                childId,
                householdId,
                firstParentId,
                secondParentId,
                carrierId,
                absoluteDay,
                BedCapacityUnavailableReason);
            resolution = HouseholdBirthAssignmentResolution.PendingBedCapacity;
            return true;
        }

        HouseholdBedCandidate selected = available.Value;
        households.Assign(
            childId,
            householdId,
            selected.RoomId,
            selected.BedId,
            selected.SlotIndex);
        resolution = HouseholdBirthAssignmentResolution.Assigned;
        return true;
    }

    public bool TryResumePending(out string failureReason)
    {
        failureReason = string.Empty;
        foreach (HouseholdPendingAssignmentSaveData pending in
                 households.PendingAssignments
                     .OrderBy(value => value.requestedAbsoluteDay)
                     .ThenBy(value => value.characterId, StringComparer.Ordinal))
        {
            if (!TryResolveBirth(
                    new CharacterId(pending.characterId),
                    new CharacterId(pending.firstParentId),
                    new CharacterId(pending.secondParentId),
                    new CharacterId(pending.carrierId),
                    pending.requestedAbsoluteDay,
                    out _,
                    out failureReason))
                return false;
        }
        return true;
    }

    private HouseholdId ResolveParentHousehold(
        CharacterId carrierId,
        CharacterId firstParentId,
        CharacterId secondParentId)
    {
        foreach (CharacterId candidate in new[]
                 {
                     carrierId,
                     firstParentId,
                     secondParentId
                 })
        {
            if (candidate.IsValid
                && households.TryGet(
                    candidate,
                    out CharacterRoomAssignmentSaveData assignment))
                return new HouseholdId(assignment.householdId);
        }
        return default;
    }

    private static string BedSlotKey(
        BuildingInstanceId bedId,
        int slotIndex) => bedId.Value + ":" + slotIndex;
}

public sealed class HouseholdBirthAssignmentRecoveryAdapter :
    IStartable,
    IDisposable
{
    private readonly IHouseholdBirthAssignmentRecovery recovery;
    private readonly IGameEventBus events;
    private IDisposable dayEndedSubscription;

    public HouseholdBirthAssignmentRecoveryAdapter(
        IHouseholdBirthAssignmentRecovery recovery,
        IGameEventBus events)
    {
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        Resume("startup");
        dayEndedSubscription ??=
            events.Subscribe<OperatingDayEndedEvent>(_ => Resume("day-ended"));
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    private void Resume(string trigger)
    {
        if (!recovery.TryResumePending(out string failureReason))
            Debug.LogError(
                "Household birth assignment recovery failed at "
                + trigger
                + ": "
                + failureReason);
    }
}
