using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public interface IBuildingCoverDurabilityPort
{
    bool TryApplyDamage(string sourceId, float damage);
}

public interface IBuildingEquipmentCraftingRuntimePort
{
    bool HasPendingCraftWork(IEnumerable<string> craftableDefinitionIds);
}

public interface IBuildingEquipmentCraftingDefinition
{
    IReadOnlyList<string> CraftableEquipmentIds { get; }
}

public interface IBuildingDamageRulePort
{
    bool ShouldBlockFacilityDamage(bool damaged);
}

public interface IBuildingPaidFacilityContractPort
{
    bool CanBeginUse(
        IBuildingWorldEntryPort facility,
        out string failureReason);

    bool TryChargeUse(
        IBuildingWorldEntryPort facility,
        out string failureReason);

    void SynchronizeFacility(IBuildingWorldEntryPort facility);
    void RemoveFacility(IBuildingWorldEntryPort facility);
}

public interface IBuildingEvolutionStatePort
{
    void EnsureInitialized(IBuildingWorldEntryPort facility);
}

public interface IBuildingResearchWorkPort
{
    bool HasResearchWorkFor(IBuildingWorldEntryPort facility);
}

public interface IBuildingPresentationSettingsPort
{
    bool ReducedMotion { get; }
}

public interface IBuildingInfoPresentationPort
{
    void ShowBuildingInfo(IBuildingWorldEntryPort building);
}

public interface IBuildingItemStackPort
{
    int ConsumeWarehouseStock(
        IBuildingWorldEntryPort warehouse,
        StockCategory category,
        int amount);

    bool SpawnStockInWarehouse(
        IBuildingWorldEntryPort warehouse,
        StockCategory category,
        int amount,
        out int spawned);

    bool SpawnFacilityBufferItem(
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out int spawned);

    bool SpawnExistingFacilityBufferUniqueItem(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        string destinationId,
        out string stackId);
}

public interface IBuildingVisitEventPort
{
    void PublishVisit(
        IBuildingCharacterPort visitor,
        IBuildingWorldEntryPort facility);
}

public interface IBuildingRoomPolicyPort
{
    bool IsFacilityRoleAvailable(
        IBuildingWorldEntryPort building,
        FacilityRole requestedRole,
        out string rejectReason);

    float GetRoomUtilityScore(
        IBuildingWorldEntryPort building,
        FacilityRole role);

    int GetEffectiveCapacity(IBuildingWorldEntryPort building);
    BuildingRoomOperationalSnapshot GetOperationalProfile(
        IBuildingWorldEntryPort building);
}

public sealed class BuildingRoomOperationalSnapshot
{
    private readonly Dictionary<StockCategory, int> storageByCategory;

    public BuildingRoomOperationalSnapshot(
        IReadOnlyList<IBuildingWorldEntryPort> parts,
        bool hasRoom,
        bool isUsableRoom,
        float qualityScore,
        int seatCapacity,
        int tableCapacity,
        int serviceCapacity,
        StockCategory retailCategory,
        IReadOnlyDictionary<StockCategory, int> storage)
    {
        Parts = parts ?? Array.Empty<IBuildingWorldEntryPort>();
        HasRoom = hasRoom;
        IsUsableRoom = isUsableRoom;
        QualityScore = Mathf.Clamp01(qualityScore);
        SeatCapacity = Mathf.Max(0, seatCapacity);
        TableCapacity = Mathf.Max(0, tableCapacity);
        ServiceCapacity = Mathf.Max(0, serviceCapacity);
        RetailCategory = retailCategory;
        storageByCategory = storage != null
            ? new Dictionary<StockCategory, int>(storage)
            : new Dictionary<StockCategory, int>();
    }

    public IReadOnlyList<IBuildingWorldEntryPort> Parts { get; }
    public bool HasRoom { get; }
    public bool IsUsableRoom { get; }
    public float QualityScore { get; }
    public int SeatCapacity { get; }
    public int TableCapacity { get; }
    public int ServiceCapacity { get; }
    public StockCategory RetailCategory { get; }

    public int GetStorageCapacity(StockCategory category)
    {
        return storageByCategory.TryGetValue(category, out int capacity)
            ? Mathf.Max(0, capacity)
            : 0;
    }
}
