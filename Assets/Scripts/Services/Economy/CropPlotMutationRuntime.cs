using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

internal sealed class CropPlotMutationRuntime
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IProductionFacilityMutationEpochQuery facilityMutations;
    private readonly IFacilityCandidateCache facilityCandidates;
    private readonly IWorkforceReplanService workforce;

    internal CropPlotMutationRuntime(CropPlotWorldDependencies world,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IProductionFacilityMutationEpochQuery facilityMutations)
    {
        this.aggregateRootStore = aggregateRootStore;
        this.facilityMutations = facilityMutations;
        facilityCandidates = world.FacilityCandidates;
        workforce = world.Workforce;
    }

    private CropPlotAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState());
    private Dictionary<BuildingInstanceId, CropPlotState> states => aggregateState.States;
    private int Version
    {
        get => aggregateState.Version;
        set => aggregateState.Version = value;
    }
    private bool snapshotsDirty
    {
        set => aggregateState.SnapshotsDirty = value;
    }

    internal void SetBlocked(CropPlotState state, string reason)
    {
        string normalized = string.IsNullOrWhiteSpace(reason)
            ? "작업이 막혔습니다."
            : reason.Trim();
        if (state.Phase == CropPlotPhase.Blocked
            && string.Equals(state.BlockedReason, normalized, StringComparison.Ordinal))
        {
            return;
        }

        state.Phase = CropPlotPhase.Blocked;
        state.BlockedReason = normalized;
        MarkChanged();
    }

    internal void MarkChanged(bool replan = true)
    {
        Version++;
        snapshotsDirty = true;
        facilityCandidates.MarkDynamicStateDirty();
        if (replan)
        {
            workforce.RequestIdleWorkersToReplan();
        }
    }

    internal bool TryRequireMutable(
        BuildingInstanceId plotId,
        out string failureReason)
    {
        if (states.TryGetValue(plotId, out CropPlotState state)
            && state.IrrigationPublication.pending)
        {
            failureReason = "crop-irrigation-publication-pending:" + state.IrrigationPublication.failureReason;
            return false;
        }
        if (ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                facilityMutations,
                plotId,
                out DomainFailure failure))
        {
            failureReason = string.Empty;
            return true;
        }

        ReadOnlySpan<string> parameters = failure.Parameters;
        failureReason = parameters.Length == 0
            ? "production-facility-mutation-open"
            : parameters[parameters.Length - 1];
        return false;
    }
}
