using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IRoomFacilityPolicy : IBuildingRoomPolicyPort
{
    bool IsFacilityRoleAvailable(
        BuildableObject building,
        FacilityRole requestedRole,
        out string rejectReason);

    float GetRoomUtilityScore(BuildableObject building, FacilityRole role);
    int GetEffectiveCapacity(BuildableObject building);
    FacilityRoomOperationalProfile GetOperationalProfile(BuildableObject building);
}

public sealed class FacilityRoomOperationalProfile
{
    private readonly Dictionary<StockCategory, int> storageByCategory =
        new Dictionary<StockCategory, int>();

    public FacilityRoomOperationalProfile(
        RoomInstance room,
        IReadOnlyList<BuildableObject> parts,
        int seatCapacity,
        int tableCapacity,
        int serviceCapacity,
        StockCategory retailCategory,
        IReadOnlyDictionary<StockCategory, int> storage)
    {
        Room = room;
        Parts = parts ?? Array.Empty<BuildableObject>();
        SeatCapacity = Mathf.Max(0, seatCapacity);
        TableCapacity = Mathf.Max(0, tableCapacity);
        ServiceCapacity = Mathf.Max(0, serviceCapacity);
        RetailCategory = retailCategory;
        if (storage != null)
        {
            foreach (KeyValuePair<StockCategory, int> item in storage)
            {
                storageByCategory[item.Key] = Mathf.Max(0, item.Value);
            }
        }
    }

    public RoomInstance Room { get; }
    public IReadOnlyList<BuildableObject> Parts { get; }
    public int SeatCapacity { get; }
    public int TableCapacity { get; }
    public int ServiceCapacity { get; }
    public StockCategory RetailCategory { get; }
    public bool IsUsableRoom => Room != null && Room.IsUsable && !Room.IsSelfContained;

    public int GetStorageCapacity(StockCategory category)
    {
        return storageByCategory.TryGetValue(category, out int capacity) ? capacity : 0;
    }
}

public sealed class RoomFacilityPolicyService :
    IRoomFacilityPolicy,
    IBuildingRoomPolicyPort
{
    private sealed class CachedOperationalProfile
    {
        public Grid Grid;
        public int StructuralVersion = -1;
        public FacilityRoomOperationalProfile Profile;
    }

    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly Dictionary<BuildableObject, CachedOperationalProfile> profileCache =
        new Dictionary<BuildableObject, CachedOperationalProfile>();

    public RoomFacilityPolicyService(IRoomLayoutCache roomLayoutCache)
    {
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
    }

    bool IBuildingRoomPolicyPort.IsFacilityRoleAvailable(
        IBuildingWorldEntryPort building,
        FacilityRole requestedRole,
        out string rejectReason)
    {
        return IsFacilityRoleAvailable(
            RequireBuildableObject(building),
            requestedRole,
            out rejectReason);
    }

    float IBuildingRoomPolicyPort.GetRoomUtilityScore(
        IBuildingWorldEntryPort building,
        FacilityRole role)
    {
        return GetRoomUtilityScore(RequireBuildableObject(building), role);
    }

    int IBuildingRoomPolicyPort.GetEffectiveCapacity(
        IBuildingWorldEntryPort building)
    {
        return GetEffectiveCapacity(RequireBuildableObject(building));
    }

    BuildingRoomOperationalSnapshot IBuildingRoomPolicyPort.GetOperationalProfile(
        IBuildingWorldEntryPort building)
    {
        FacilityRoomOperationalProfile profile =
            GetOperationalProfile(RequireBuildableObject(building));
        Dictionary<StockCategory, int> storage = Enum
            .GetValues(typeof(StockCategory))
            .Cast<StockCategory>()
            .ToDictionary(
                category => category,
                profile.GetStorageCapacity);
        return new BuildingRoomOperationalSnapshot(
            profile.Parts.Cast<IBuildingWorldEntryPort>().ToArray(),
            profile.Room != null,
            profile.IsUsableRoom,
            profile.Room?.GetQualityScore() ?? 0f,
            profile.SeatCapacity,
            profile.TableCapacity,
            profile.ServiceCapacity,
            profile.RetailCategory,
            storage);
    }

    public bool IsFacilityRoleAvailable(
        BuildableObject building,
        FacilityRole requestedRole,
        out string rejectReason)
    {
        roomLayoutCache.TryGetRoom(building, out RoomInstance room);
        DungeonStory.Rooms.RoomFacilityRejection rejection =
            DungeonStory.Rooms.RoomFacilityPolicyRules.GetRejection(
                CreatePolicyInput(building, requestedRole, room));
        rejectReason = rejection switch
        {
            DungeonStory.Rooms.RoomFacilityRejection.MissingRoom => "valid room not found",
            DungeonStory.Rooms.RoomFacilityRejection.UnusableRoom => "room is not closed by walls/doors",
            DungeonStory.Rooms.RoomFacilityRejection.RoleMismatch => "room role mismatch",
            _ => string.Empty
        };
        return rejection == DungeonStory.Rooms.RoomFacilityRejection.None;
    }

    public float GetRoomUtilityScore(BuildableObject building, FacilityRole role)
    {
        roomLayoutCache.TryGetRoom(building, out RoomInstance room);
        return DungeonStory.Rooms.RoomFacilityPolicyRules.GetUtilityScore(
            CreatePolicyInput(building, role, room));
    }

    public int GetEffectiveCapacity(BuildableObject building)
    {
        int baseCapacity = building?.Facility != null
            ? Mathf.Max(0, building.Facility.capacity)
            : 0;
        roomLayoutCache.TryGetRoom(building, out RoomInstance room);
        FacilityRoomOperationalProfile profile = building != null
            ? GetOperationalProfile(building, room)
            : null;
        return DungeonStory.Rooms.RoomFacilityPolicyRules.GetEffectiveCapacity(
            CreatePolicyInput(building, FacilityRole.None, room, profile));
    }

    public FacilityRoomOperationalProfile GetOperationalProfile(BuildableObject building)
    {
        if (building == null)
        {
            return EmptyProfile(null);
        }

        roomLayoutCache.TryGetRoom(building, out RoomInstance room);
        return GetOperationalProfile(building, room);
    }

    private FacilityRoomOperationalProfile GetOperationalProfile(
        BuildableObject building,
        RoomInstance room)
    {
        Grid grid = building != null ? building.Grid : null;
        int structuralVersion = grid != null ? grid.StructuralVersion : -1;
        if (building != null
            && profileCache.TryGetValue(building, out CachedOperationalProfile cached)
            && cached.Grid == grid
            && cached.StructuralVersion == structuralVersion
            && cached.Profile != null)
        {
            return cached.Profile;
        }

        List<BuildableObject> parts = CollectRoomParts(building, room);
        int seats = 0;
        int tables = 0;
        int service = 0;
        Dictionary<StockCategory, int> stockCategorySignals = new Dictionary<StockCategory, int>();
        Dictionary<StockCategory, int> storage = new Dictionary<StockCategory, int>();

        foreach (BuildableObject part in parts)
        {
            if (part?.BuildingData == null)
            {
                continue;
            }

            seats += part.GetSeatCapacity();
            tables += part.GetTableCapacity();
            service += part.GetServiceCapacity();
            int storageCapacity = part.GetStorageCapacity();
            if (storageCapacity > 0)
            {
                if (part.StoresAllCategories())
                {
                    foreach (StockCategory storageCategory in Enum.GetValues(typeof(StockCategory)))
                    {
                        storage.TryGetValue(storageCategory, out int current);
                        storage[storageCategory] = current + storageCapacity;
                    }
                }
                else
                {
                    StockCategory storageCategory = part.GetStorageCategory();
                    storage.TryGetValue(storageCategory, out int current);
                    storage[storageCategory] = current + storageCapacity;
                }
            }

            foreach (StockCategory signal in part.BuildingData.GetStockCategorySignals())
            {
                stockCategorySignals.TryGetValue(signal, out int signalCount);
                stockCategorySignals[signal] = signalCount + 1;
            }
        }

        StockCategory category = ResolveRetailCategory(stockCategorySignals);
        FacilityRoomOperationalProfile profile = new FacilityRoomOperationalProfile(
            room,
            parts,
            seats,
            tables,
            service,
            category,
            storage);
        if (building != null)
        {
            profileCache[building] = new CachedOperationalProfile
            {
                Grid = grid,
                StructuralVersion = structuralVersion,
                Profile = profile
            };
        }

        return profile;
    }

    private static List<BuildableObject> CollectRoomParts(BuildableObject building, RoomInstance room)
    {
        if (building.Grid == null || room == null || room.IsSelfContained)
        {
            return new List<BuildableObject> { building };
        }

        return building.Grid.FindAllOccupants(null)
            .OfType<BuildableObject>()
            .Where((part) => part != null
                && !part.isDestroy
                && part.buildPoses != null
                && part.buildPoses.Any(room.ContainsCell))
            .Distinct()
            .ToList();
    }

    private static StockCategory ResolveRetailCategory(IReadOnlyDictionary<StockCategory, int> signals)
    {
        if (signals == null || signals.Count == 0)
        {
            return StockCategory.General;
        }

        int highest = signals.Values.DefaultIfEmpty(0).Max();
        if (highest <= 0)
        {
            return StockCategory.General;
        }

        StockCategory[] leaders = signals
            .Where(pair => pair.Value == highest)
            .Select(pair => pair.Key)
            .OrderBy(category => Convert.ToInt32(category))
            .ToArray();
        if (leaders.Length != 1)
        {
            return StockCategory.General;
        }

        return leaders[0];
    }

    private static FacilityRoomOperationalProfile EmptyProfile(BuildableObject building)
    {
        return new FacilityRoomOperationalProfile(
            null,
            building != null ? new[] { building } : Array.Empty<BuildableObject>(),
            0,
            0,
            0,
            StockCategory.General,
            null);
    }

    private static BuildableObject RequireBuildableObject(
        IBuildingWorldEntryPort building)
    {
        if (building == null)
        {
            return null;
        }

        return building as BuildableObject
            ?? throw new ArgumentException(
                $"{nameof(IBuildingRoomPolicyPort)} only accepts {nameof(BuildableObject)} facilities.",
                nameof(building));
    }

    private static DungeonStory.Rooms.RoomFacilityPolicyInput CreatePolicyInput(
        BuildableObject building,
        FacilityRole requestedRole,
        RoomInstance room,
        FacilityRoomOperationalProfile profile = null)
    {
        FacilityRole roles = building?.Facility?.roles ?? FacilityRole.None;
        DungeonStory.Rooms.RoomPolicyRoomSnapshot roomSnapshot = room == null
            ? null
            : new DungeonStory.Rooms.RoomPolicyRoomSnapshot(
                room.IsUsable,
                room.IsSelfContained,
                room.Roles,
                room.GetQualityScore());
        return new DungeonStory.Rooms.RoomFacilityPolicyInput(
            building?.Facility != null,
            building?.BuildingData?.RequiresRoomRole() == true,
            roles,
            requestedRole,
            roomSnapshot,
            building?.Facility != null ? Mathf.Max(0, building.Facility.capacity) : 0,
            profile?.SeatCapacity ?? 0,
            profile?.TableCapacity ?? 0,
            profile?.ServiceCapacity ?? 0);
    }
}
