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
    bool SpawnStockInWarehouse(
        IBuildingWorldEntryPort warehouse,
        StockCategory category,
        int amount,
        out int spawned);
}

public interface IBuildingVisitEventPort
{
    void CommitVisit(
        IBuildingCharacterPort visitor,
        IBuildingWorldEntryPort facility);

    FacilityVisitCommitResult CommitVisit(
        IBuildingCharacterPort visitor,
        IBuildingWorldEntryPort facility,
        CharacterFacilityUseOutcomeReceipt receipt)
    {
        CommitVisit(visitor, facility);
        return FacilityVisitCommitResult.Committed(string.Empty);
    }

    FacilityVisitCommitResult ReconcilePendingVisit(
        IBuildingWorldEntryPort facility) =>
        FacilityVisitCommitResult.Committed(string.Empty);
}

public enum CharacterFacilityUseOutcomeStatus
{
    Completed = 0,
    Cancelled = 1,
    Failed = 2,
    Partial = 3
}

public sealed class CharacterFacilityUseOutcomeReceipt
{
    public string OperationId { get; set; } = string.Empty;
    public long OperationSequence { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string FacilityRole { get; set; } = string.Empty;
    public string ServiceSessionId { get; set; } = string.Empty;
    public CharacterFacilityUseOutcomeStatus Status { get; set; }
    public bool EffectsCommitted { get; set; }
    public string FailureCode { get; set; } = string.Empty;
    public BuildingVisitorSnapshot Before { get; set; }
    public BuildingVisitorSnapshot After { get; set; }
    public IReadOnlyList<string> ChildResultKeys { get; set; } =
        Array.Empty<string>();

    public bool IsValid => OperationSequence > 0L
        && !string.IsNullOrWhiteSpace(OperationId)
        && !string.IsNullOrWhiteSpace(ActorId)
        && Enum.IsDefined(typeof(CharacterFacilityUseOutcomeStatus), Status);
}

public readonly struct FacilityVisitCommitResult
{
    private FacilityVisitCommitResult(
        bool durablyCommitted,
        bool pending,
        string resultKey,
        string detailCode)
    {
        DurablyCommitted = durablyCommitted;
        Pending = pending;
        ResultKey = resultKey ?? string.Empty;
        DetailCode = detailCode ?? string.Empty;
    }

    public bool DurablyCommitted { get; }
    public bool Pending { get; }
    public string ResultKey { get; }
    public string DetailCode { get; }

    public static FacilityVisitCommitResult Committed(string resultKey) =>
        new(true, false, resultKey, string.Empty);

    public static FacilityVisitCommitResult Deferred(
        string resultKey,
        string detailCode) =>
        new(false, true, resultKey, detailCode);

    public static FacilityVisitCommitResult Rejected(string detailCode) =>
        new(false, false, string.Empty, detailCode);
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
