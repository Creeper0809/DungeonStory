using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum FacilityShopRarity
{
    Common = 0,
    Rare = 1,
    Special = 2
}

public readonly struct FacilityShopCatalogDefinition
{
    public FacilityShopCatalogDefinition(int id, string displayName, int star)
    {
        Id = id;
        DisplayName = displayName?.Trim() ?? string.Empty;
        Star = Math.Max(1, star);
    }

    public int Id { get; }
    public string DisplayName { get; }
    public int Star { get; }
}

public interface IFacilityShopDefinitionCatalog
{
    IReadOnlyCollection<FacilityShopCatalogDefinition> Buildings { get; }
    IReadOnlyCollection<int> BlueprintIds { get; }
}

public sealed class FacilityShopStateSnapshot
{
    public FacilityShopStateSnapshot(
        int currentOfferDay,
        IEnumerable<int> basicPurchaseBuildingIds,
        IEnumerable<int> acquiredBlueprintIds)
    {
        CurrentOfferDay = Math.Max(1, currentOfferDay);
        BasicPurchaseBuildingIds = Array.AsReadOnly(
            CanonicalIds(basicPurchaseBuildingIds));
        AcquiredBlueprintIds = Array.AsReadOnly(
            CanonicalIds(acquiredBlueprintIds));
    }

    public int CurrentOfferDay { get; }
    public IReadOnlyList<int> BasicPurchaseBuildingIds { get; }
    public IReadOnlyList<int> AcquiredBlueprintIds { get; }

    private static int[] CanonicalIds(IEnumerable<int> ids) =>
        (ids ?? Array.Empty<int>())
        .Where(id => id >= 0)
        .Distinct()
        .OrderBy(id => id)
        .ToArray();
}

public interface IFacilityShopPersistence
{
    FacilityShopStateSnapshot CaptureState();
    FacilityShopRestoreCandidate BuildRestoreCandidate(
        FacilityShopStateSnapshot snapshot);
    void PublishRestoreCandidate(FacilityShopRestoreCandidate candidate);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class FacilityShopAggregateState
{
    internal readonly HashSet<int> BasicPurchaseBuildingIds = new();
    internal readonly HashSet<int> AcquiredBlueprintIds = new();
    internal int CurrentOfferDay = 1;

    internal FacilityShopAggregateState DeepClone()
    {
        FacilityShopAggregateState clone = new()
        {
            CurrentOfferDay = CurrentOfferDay
        };
        clone.BasicPurchaseBuildingIds.UnionWith(BasicPurchaseBuildingIds);
        clone.AcquiredBlueprintIds.UnionWith(AcquiredBlueprintIds);
        return clone;
    }
}

public sealed class FacilityShopRestoreCandidate
{
    internal FacilityShopRestoreCandidate(FacilityShopAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal FacilityShopAggregateState State { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class FacilityShopUnlockState
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private FacilityShopAggregateState localState;

    public FacilityShopUnlockState()
    {
        localState = new FacilityShopAggregateState();
    }

    public FacilityShopUnlockState(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyCollection<int> BasicPurchaseBuildingIds =>
        Array.AsReadOnly(Current.BasicPurchaseBuildingIds.OrderBy(id => id).ToArray());
    public IReadOnlyCollection<int> AcquiredBlueprintIds =>
        Array.AsReadOnly(Current.AcquiredBlueprintIds.OrderBy(id => id).ToArray());
    public int CurrentOfferDay => Current.CurrentOfferDay;

    public bool UnlockBasicPurchaseById(int buildingId) =>
        buildingId >= 0 && Writable.BasicPurchaseBuildingIds.Add(buildingId);
    public bool IsBasicPurchaseUnlocked(int buildingId) =>
        buildingId >= 0 && Current.BasicPurchaseBuildingIds.Contains(buildingId);
    public bool MarkBlueprintAcquiredById(int blueprintId) =>
        blueprintId >= 0 && Writable.AcquiredBlueprintIds.Add(blueprintId);
    public bool IsBlueprintAcquired(int blueprintId) =>
        blueprintId >= 0 && Current.AcquiredBlueprintIds.Contains(blueprintId);
    public void SetCurrentOfferDay(int day) =>
        Writable.CurrentOfferDay = Math.Max(1, day);

    public FacilityShopStateSnapshot Capture() =>
        new(
            Current.CurrentOfferDay,
            Current.BasicPurchaseBuildingIds,
            Current.AcquiredBlueprintIds);

    public void Restore(FacilityShopStateSnapshot snapshot)
    {
        PublishRestore(PrepareRestore(snapshot));
    }

    public FacilityShopRestoreCandidate PrepareRestore(
        FacilityShopStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }
        FacilityShopAggregateState restored = new()
        {
            CurrentOfferDay = snapshot.CurrentOfferDay
        };
        restored.BasicPurchaseBuildingIds.UnionWith(
            snapshot.BasicPurchaseBuildingIds);
        restored.AcquiredBlueprintIds.UnionWith(snapshot.AcquiredBlueprintIds);
        return new FacilityShopRestoreCandidate(restored);
    }

    public void PublishRestore(FacilityShopRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        if (aggregateRootStore != null)
        {
            aggregateRootStore.Replace(candidate.State);
        }
        else
        {
            localState = candidate.State;
        }
    }

    private FacilityShopAggregateState Current =>
        aggregateRootStore != null
            ? aggregateRootStore.GetOrCreate(() => new FacilityShopAggregateState())
            : localState;
    private FacilityShopAggregateState Writable =>
        aggregateRootStore != null
            ? aggregateRootStore.GetOrCreateWritable(
                () => new FacilityShopAggregateState(),
                state => state.DeepClone())
            : localState;
}

public sealed class FacilityShopApplication
{
    private readonly FacilityShopUnlockState state;

    public FacilityShopApplication(FacilityShopUnlockState state)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public int CurrentOfferDay => state.CurrentOfferDay;
    public void RefreshForDay(int day) => state.SetCurrentOfferDay(day);
    public FacilityShopStateSnapshot Capture() => state.Capture();
    public void Restore(FacilityShopStateSnapshot snapshot) => state.Restore(snapshot);
    public FacilityShopRestoreCandidate PrepareRestore(
        FacilityShopStateSnapshot snapshot) => state.PrepareRestore(snapshot);
    public void PublishRestore(FacilityShopRestoreCandidate candidate) =>
        state.PublishRestore(candidate);
}
