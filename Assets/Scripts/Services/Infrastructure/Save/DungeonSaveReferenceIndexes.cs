using System;
using System.Collections.Generic;


internal sealed class DungeonSaveCharacterReferenceIndex
{
    internal HashSet<string> KnownCharacterIds { get; } =
        new(StringComparer.Ordinal);
    internal HashSet<string> ActorIds { get; } =
        new(StringComparer.Ordinal);
    internal HashSet<string> LivingCharacterIds { get; } =
        new(StringComparer.Ordinal);
}


internal sealed class DungeonSavePhysicalReferenceIndex
{
    internal HashSet<string> StackIds { get; } =
        new HashSet<string>(StringComparer.Ordinal);
    internal Dictionary<string, WorldItemStackSaveData> StacksById { get; } =
        new(StringComparer.Ordinal);
    internal HashSet<string> ItemInstanceIds { get; } =
        new HashSet<string>(StringComparer.Ordinal);
    internal HashSet<string> UniqueItemInstanceIds { get; } =
        new HashSet<string>(StringComparer.Ordinal);
    internal HashSet<string> EquipmentInstanceIds { get; } =
        new HashSet<string>(StringComparer.Ordinal);
    internal Dictionary<string, string> EquipmentDefinitionIds { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
    internal Dictionary<string, CombatEquipmentInstance> EquipmentInstances { get; } =
        new Dictionary<string, CombatEquipmentInstance>(StringComparer.Ordinal);
    internal Dictionary<string, UniqueItemInstanceSaveData> UniqueItems { get; } =
        new Dictionary<string, UniqueItemInstanceSaveData>(StringComparer.Ordinal);
    internal List<(string itemDefinitionId, SeedLotState state)> SeedLots { get; } = new();
    internal HashSet<string> ReservationOwnerOperationIds { get; } =
        new HashSet<string>(StringComparer.Ordinal);
    internal Dictionary<string, PhysicalItemBatchDispositionSaveData>
        PendingBatchByOperation { get; } =
            new(StringComparer.Ordinal);
    internal HashSet<string> PendingBatchCommitIds { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, WorldItemStackSaveData> OutputByCommitId
        { get; } = new(StringComparer.Ordinal);
}


internal sealed class DungeonSaveBuildingReferenceIndex
{
    internal HashSet<string> InstanceIds { get; } =
        new HashSet<string>(StringComparer.Ordinal);
    internal Dictionary<string, BuildingSO> InstanceDefinitions { get; } =
        new Dictionary<string, BuildingSO>(StringComparer.Ordinal);
}


internal sealed class DungeonSaveKinshipReferenceIndex
{
    internal DungeonSaveKinshipReferenceIndex(IEnumerable<string> livingCharacterIds)
    {
        AllCharacterIds.UnionWith(livingCharacterIds ?? Array.Empty<string>());
    }

    internal HashSet<string> TombstoneIds { get; } =
        new(StringComparer.Ordinal);
    internal HashSet<string> AllCharacterIds { get; } =
        new(StringComparer.Ordinal);
    internal HashSet<string> HouseholdIds { get; } =
        new(StringComparer.Ordinal);
}
