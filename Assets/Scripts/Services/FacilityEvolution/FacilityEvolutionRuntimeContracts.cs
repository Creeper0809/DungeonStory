using System.Collections.Generic;
using UnityEngine;

public interface IFacilityEvolutionRuntime
{
    FacilityEvolutionState GetState(BuildableObject facility);
    FacilityEvolutionState RecordUsage(
        BuildableObject facility,
        string eventId,
        float mastery,
        float amount = 1f,
        string actorId = "",
        IEnumerable<string> sourceTags = null);
    IReadOnlyList<FacilityGenerationCandidate> GetGenerationCandidates(
        BuildableObject facility);
    bool TryQueueCandidate(
        BuildableObject facility,
        string candidateId,
        out FacilityModificationOrder order,
        out string failureReason);
    bool TryQueueCandidate(
        BuildableObject facility,
        string candidateId,
        string catalystItemId,
        out FacilityModificationOrder order,
        out string failureReason);
    bool TryQueueRecalibration(
        BuildableObject facility,
        string nodeId,
        EvolutionModuleActivationRule targetRule,
        string catalystItemId,
        out FacilityRecalibrationOrder order,
        out string failureReason);
    bool TryQueueRecalibrationToCurrentRoom(
        BuildableObject facility,
        string nodeId,
        string catalystItemId,
        out FacilityRecalibrationOrder order,
        out string failureReason);
    bool TryQueueRelocation(
        BuildableObject facility,
        Vector2Int destination,
        out FacilityRelocationOrder order,
        out string failureReason);
    bool TryGetPendingWork(
        BuildableObject facility,
        out FacilityModificationOrder modification,
        out FacilityRecalibrationOrder recalibration);
    bool TryGetPendingRelocation(
        BuildableObject facility,
        out FacilityRelocationOrder relocation);
    bool ApplyPendingWork(
        BuildableObject facility,
        float workUnits,
        out EvolutionNode completedNode,
        out bool completed,
        out string failureReason);
    bool ApplyRelocationWork(
        BuildableObject facility,
        float workUnits,
        out BuildableObject relocatedFacility,
        out bool completed,
        out string failureReason);
    bool CancelPendingWork(
        BuildableObject facility,
        out string failureReason);
    bool RefreshRoomActivation(BuildableObject facility);
}

public static class FacilityEvolutionWorkUtility
{
    public static bool HasPendingWork(BuildableObject building)
    {
        FacilityEvolutionState state = GetState(building);
        return state != null
            && (state.modificationOrder != null
                || state.recalibrationOrder != null
                || state.relocationOrder != null);
    }

    public static bool IsRelocating(BuildableObject building)
    {
        return GetState(building)?.relocationOrder != null;
    }

    public static FacilityWorkType AddFallbackWorkTypes(
        BuildableObject building,
        FacilityWorkType current)
    {
        return HasPendingWork(building)
            ? current | FacilityWorkType.Craft
            : current;
    }

    private static FacilityEvolutionState GetState(
        BuildableObject building)
    {
        if (building == null || building.isDestroy)
        {
            return null;
        }

        FacilityEvolutionStateComponent component =
            building.GetComponent<FacilityEvolutionStateComponent>();
        return component != null ? component.InstanceEvolution : null;
    }
}


