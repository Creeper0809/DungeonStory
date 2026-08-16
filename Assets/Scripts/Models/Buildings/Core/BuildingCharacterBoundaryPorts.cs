using System.Collections.Generic;
using System;
using DungeonStory.Foundation;

[Flags]
public enum BuildingTransientOwnershipKind
{
    None = 0,
    VisitReservation = 1 << 0,
    ActiveUse = 1 << 1,
    WorkerReservation = 1 << 2,
    AllocatedWorker = 1 << 3
}

public interface IBuildingWorldEntryPort
{
    BuildingInstanceId BuildingInstanceId { get; }
    bool IsBuildingDestroyed { get; }
}

public interface IBuildingWorldRegistryPort
{
    int BuildingVersion { get; }
    IReadOnlyList<IBuildingWorldEntryPort> Buildings { get; }
    void RegisterBuilding(IBuildingWorldEntryPort building);
    void UnregisterBuilding(IBuildingWorldEntryPort building);
    void TrackTransientCharacterOwnership(
        IBuildingWorldEntryPort building,
        CharacterId characterId,
        BuildingTransientOwnershipKind kind);
    void UntrackTransientCharacterOwnership(
        IBuildingWorldEntryPort building,
        CharacterId characterId,
        BuildingTransientOwnershipKind kind);
    void UntrackAllTransientCharacterOwnership(
        IBuildingWorldEntryPort building);
}

public interface IBuildingFacilityStateChangePort
{
    void MarkDynamicStateDirty();
}

public interface IBuildingCharacterPort
{
    CharacterId BuildingCharacterId { get; }
    string BuildingDisplayName { get; }
    bool IsBuildingInteractionAvailable { get; }
}
