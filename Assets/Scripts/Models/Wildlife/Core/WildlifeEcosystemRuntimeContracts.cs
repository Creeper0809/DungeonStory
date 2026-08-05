using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WildlifeGridAreaType
{
    DungeonInterior = 0,
    Entrance = 1,
    DropZone = 2,
    ExteriorPath = 3,
    BlockedExterior = 4
}

public interface IWildlifeGridCellPort
{
    Vector2Int Position { get; }
    WildlifeGridAreaType AreaType { get; }
    bool IsWalkable { get; }
    bool HasWildlifeOccupant { get; }
    bool IsOutdoorSurface { get; }
}

public interface IWildlifeGridPort
{
    int Width { get; }
    Vector2Int GetCellPosition(Vector3 worldPosition);
    Vector3 GetWorldPosition(Vector2Int cellPosition);
    bool IsValidGridPos(Vector2Int position);
    bool IsWalkable(Vector2Int position);
    IWildlifeGridCellPort GetGridCell(Vector2Int position);
    IReadOnlyList<IWildlifeGridCellPort> GetCells();
}

public interface IWildlifeOverlayRootPort
{
    void ParentOverlayRoot(GameObject root);
}

public interface IWildlifeAnimalPort
{
    string WildlifeId { get; }
    string SpeciesId { get; }
    WildlifeSpeciesDefinition Species { get; }
    int MaxHealth { get; }
    int CurrentHealth { get; }
    WildlifeState State { get; }
    Vector2Int GridPosition { get; }
    float Fear { get; }
    float Hunger { get; }
    float Thirst { get; }
    WildlifeIntent Intent { get; }
    Vector2Int TerritoryCenter { get; }
    bool HasLastThreatPosition { get; }
    Vector2Int LastThreatPosition { get; }
    float LastThreatAge { get; }
    bool CanEnterDungeon { get; }
    bool IsAlive { get; }
    bool IsDangerous { get; }
    void SetIntent(WildlifeIntent intent, string reason);
    void ChangeHunger(float delta);
    void ChangeThirst(float delta);
}

public readonly struct WildlifeCarcassStackSnapshot
{
    public WildlifeCarcassStackSnapshot(
        string itemId,
        int quantity,
        bool forbidden,
        Vector2Int position)
    {
        ItemId = itemId ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
        Forbidden = forbidden;
        Position = position;
    }

    public string ItemId { get; }
    public int Quantity { get; }
    public bool Forbidden { get; }
    public Vector2Int Position { get; }
}

public readonly struct WildlifeWaterSourceSnapshot
{
    public WildlifeWaterSourceSnapshot(
        string sourceId,
        Vector2Int position,
        bool deepWater,
        bool foul,
        float capacity,
        float remaining)
    {
        SourceId = sourceId ?? string.Empty;
        Position = position;
        DeepWater = deepWater;
        Foul = foul;
        Capacity = Mathf.Max(0f, capacity);
        Remaining = Mathf.Clamp(remaining, 0f, Capacity);
    }

    public string SourceId { get; }
    public Vector2Int Position { get; }
    public bool DeepWater { get; }
    public bool Foul { get; }
    public float Capacity { get; }
    public float Remaining { get; }
}

public interface IWildlifeEcosystemWorldPort
{
    bool TryGetGrid(out IWildlifeGridPort grid);
    IReadOnlyList<WildlifeHabitatPatch> GetMarkerPatches(
        IWildlifeGridPort grid,
        IPersistentIdGenerator persistentIds);
    IReadOnlyList<WildlifeWaterSourceSnapshot> GetWaterSources();
    bool TryGetWaterSource(
        string sourceId,
        out WildlifeWaterSourceSnapshot source);
    bool TryDrinkWater(
        string sourceId,
        float amount,
        out float consumed);
}

public interface IWildlifeEcosystemPresentationPort : IDisposable
{
    bool OverlayEnabled { get; }
    void SetOverlayEnabled(bool enabled);
    void Clear();
    void Rebuild(
        IWildlifeGridPort grid,
        IReadOnlyList<WildlifeHabitatPatch> patches);
    void RefreshOverlay(
        IWildlifeGridPort grid,
        IReadOnlyList<WildlifeHabitatPatch> patches);
    void RefreshPatches(IReadOnlyList<WildlifeHabitatPatch> patches);
    void RefreshPatch(WildlifeHabitatPatch patch);
}
