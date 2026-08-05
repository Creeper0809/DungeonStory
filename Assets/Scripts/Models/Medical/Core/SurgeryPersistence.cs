using System;
using System.Linq;

public sealed class SurgeryPersistence
{
    private readonly SurgeryAggregateStateStore stateStore;

    public SurgeryPersistence(SurgeryAggregateStateStore stateStore)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public DungeonSurgerySaveData Capture()
    {
        SurgeryAggregateState state = stateStore.State;
        return new DungeonSurgerySaveData
        {
            version = DungeonSurgerySaveData.CurrentVersion,
            orders = state.Orders
                .Select(SurgeryStateCloner.CloneOrder)
                .ToList(),
            parts = state.Parts
                .Select(SurgeryStateCloner.ClonePart)
                .ToList(),
            organStorageStates = state.OrganStorage.Values
                .OrderBy(item => item.facilityId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToList(),
            corpseFreshness = state.CorpseFreshness.Values
                .OrderBy(item => item.stackId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToList(),
            policies = state.Policies
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new SurgerySubjectPolicyState
                {
                    subjectId = item.Key,
                    automaticEmergencySurgery = item.Value
                })
                .ToList(),
            corpseRecords = state.ExtractedNodesByCorpse
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new CorpseSurgicalRecord
                {
                    stackId = item.Key,
                    extractedNodeIds = item.Value
                        .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
                        .ToList()
                })
                .ToList(),
            wildlifeAnatomy = state.WildlifeAnatomy.Values
                .OrderBy(item => item.wildlifeId, StringComparer.Ordinal)
                .Select(SurgeryStateCloner.CloneWildlifeAnatomy)
                .ToList(),
            orderSequence = state.OrderSequence,
            partSequence = state.PartSequence
        };
    }
}
