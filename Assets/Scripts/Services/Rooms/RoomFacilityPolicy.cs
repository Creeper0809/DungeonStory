using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IRoomFacilityPolicy
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

public sealed class RoomFacilityPolicyService : IRoomFacilityPolicy
{
    private readonly IRoomLayoutCache roomLayoutCache;

    public RoomFacilityPolicyService(IRoomLayoutCache roomLayoutCache)
    {
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
    }

    public bool IsFacilityRoleAvailable(
        BuildableObject building,
        FacilityRole requestedRole,
        out string rejectReason)
    {
        rejectReason = string.Empty;
        if (building == null || building.Facility == null)
        {
            return true;
        }

        if (!building.BuildingData.RequiresRoomRole())
        {
            return true;
        }

        FacilityRole relevantRole = requestedRole & building.Facility.roles;
        if (relevantRole == FacilityRole.None)
        {
            relevantRole = requestedRole;
        }

        if (!roomLayoutCache.TryGetRoom(building, out RoomInstance room))
        {
            rejectReason = "valid room not found";
            return false;
        }

        if (!room.IsUsable)
        {
            rejectReason = "room is not closed by walls/doors";
            return false;
        }

        if ((room.FacilityRoles & relevantRole) == 0)
        {
            rejectReason = "room role mismatch";
            return false;
        }

        return true;
    }

    public float GetRoomUtilityScore(BuildableObject building, FacilityRole role)
    {
        if (building == null || !roomLayoutCache.TryGetRoom(building, out RoomInstance room))
        {
            return 0.5f;
        }

        if (!room.SupportsFacilityRole(role))
        {
            return 0f;
        }

        return Mathf.Clamp01(room.GetQualityScore());
    }

    public int GetEffectiveCapacity(BuildableObject building)
    {
        int baseCapacity = building?.Facility != null
            ? Mathf.Max(0, building.Facility.capacity)
            : 0;
        if (building == null || building.Facility == null || baseCapacity == 0)
        {
            return baseCapacity;
        }

        FacilityRoomOperationalProfile profile = GetOperationalProfile(building);
        if (!profile.IsUsableRoom)
        {
            return baseCapacity;
        }

        if (building.SupportsFacilityRole(FacilityRole.Meal))
        {
            int diningCapacity = Mathf.Min(profile.SeatCapacity, profile.TableCapacity);
            return Mathf.Max(baseCapacity, diningCapacity);
        }

        if (building.SupportsFacilityRole(FacilityRole.Purchase))
        {
            return Mathf.Max(baseCapacity, baseCapacity + profile.ServiceCapacity);
        }

        return baseCapacity;
    }

    public FacilityRoomOperationalProfile GetOperationalProfile(BuildableObject building)
    {
        if (building == null)
        {
            return EmptyProfile(null);
        }

        roomLayoutCache.TryGetRoom(building, out RoomInstance room);
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
                    foreach (StockCategoryDefinition definition in StockCategoryCatalog.All)
                    {
                        StockCategory storageCategory = definition.Category;
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
        return new FacilityRoomOperationalProfile(room, parts, seats, tables, service, category, storage);
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
}
