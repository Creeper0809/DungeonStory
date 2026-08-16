using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEditor;
using UnityEngine;

public static class FacilityEvolutionActivationProjectionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Facility Evolution/Run Activation Projection Scenarios")]
    public static void RunFromMenu()
    {
        RunAll(log: true);
    }

    public static bool RunAll(bool log = false)
    {
        bool mutationPass = VerifyMutationUsesSnapshotAndReplaysNextTick();
        bool reentrancyPass = VerifyReentrancyDefersToNextTick();
        bool exceptionPass = VerifyFailedPassRetriesWithoutCommittingVersions();
        bool passed = mutationPass && reentrancyPass && exceptionPass;
        if (!passed)
        {
            Debug.LogError(
                "FacilityEvolutionActivationProjectionDebugScenarios failed: "
                + $"mutation={mutationPass}; reentrancy={reentrancyPass}; "
                + $"exceptionRetry={exceptionPass}");
            return false;
        }

        if (log)
        {
            Debug.Log(
                "FacilityEvolutionActivationProjectionDebugScenarios passed: "
                + "snapshot mutation replay and failed-pass retry are deterministic.");
        }

        return true;
    }

    private static bool VerifyReentrancyDefersToNextTick()
    {
        GameObject firstObject = CreateFacility("activation-reentrant-a");
        GameObject secondObject = CreateFacility("activation-reentrant-b");
        try
        {
            BuildableObject first = firstObject.GetComponent<BuildableObject>();
            BuildableObject second = secondObject.GetComponent<BuildableObject>();
            MutableBuildingWorldQuery buildings = new MutableBuildingWorldQuery();
            buildings.Register(first);
            buildings.Register(second);
            RecordingEvolutionRuntime evolution = new RecordingEvolutionRuntime();
            FacilityEvolutionActivationProjection projection = null;
            bool requested = false;
            evolution.OnRefresh = _ =>
            {
                if (requested)
                {
                    return;
                }

                requested = true;
                projection.Tick();
            };
            projection = new FacilityEvolutionActivationProjection(
                buildings,
                new MutableFacilityCandidateCache(),
                evolution);

            projection.Initialize();
            bool initialSnapshot = Matches(
                evolution.Refreshes,
                first,
                second);

            projection.Tick();
            bool deferredReplay = Matches(
                evolution.Refreshes,
                first,
                second,
                first,
                second);

            projection.Tick();
            return initialSnapshot
                && deferredReplay
                && evolution.Refreshes.Count == 4;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
        }
    }

    private static bool VerifyMutationUsesSnapshotAndReplaysNextTick()
    {
        GameObject firstObject = CreateFacility("activation-a");
        GameObject secondObject = CreateFacility("activation-b");
        GameObject replacementObject = CreateFacility("activation-c");
        try
        {
            BuildableObject first = firstObject.GetComponent<BuildableObject>();
            BuildableObject second = secondObject.GetComponent<BuildableObject>();
            BuildableObject replacement = replacementObject.GetComponent<BuildableObject>();
            MutableBuildingWorldQuery buildings = new MutableBuildingWorldQuery();
            buildings.Register(first);
            buildings.Register(second);
            MutableFacilityCandidateCache facilityStates =
                new MutableFacilityCandidateCache();
            RecordingEvolutionRuntime evolution = new RecordingEvolutionRuntime();
            evolution.OnRefresh = building =>
            {
                if (!ReferenceEquals(building, first) || evolution.MutationApplied)
                {
                    return;
                }

                evolution.MutationApplied = true;
                buildings.Unregister(first);
                buildings.Register(replacement);
                facilityStates.MarkDynamicStateDirty();
            };
            FacilityEvolutionActivationProjection projection =
                new FacilityEvolutionActivationProjection(
                    buildings,
                    facilityStates,
                    evolution);

            projection.Initialize();
            bool initialSnapshot = Matches(
                evolution.Refreshes,
                first,
                second);

            projection.Tick();
            bool replayedChangedAuthority = Matches(
                evolution.Refreshes,
                first,
                second,
                second,
                replacement);

            projection.Tick();
            return initialSnapshot
                && replayedChangedAuthority
                && evolution.Refreshes.Count == 4;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
            UnityEngine.Object.DestroyImmediate(replacementObject);
        }
    }

    private static bool VerifyFailedPassRetriesWithoutCommittingVersions()
    {
        GameObject firstObject = CreateFacility("activation-retry-a");
        GameObject secondObject = CreateFacility("activation-retry-b");
        try
        {
            BuildableObject first = firstObject.GetComponent<BuildableObject>();
            BuildableObject second = secondObject.GetComponent<BuildableObject>();
            MutableBuildingWorldQuery buildings = new MutableBuildingWorldQuery();
            buildings.Register(first);
            buildings.Register(second);
            RecordingEvolutionRuntime evolution = new RecordingEvolutionRuntime
            {
                ThrowOnFirstRefresh = true
            };
            FacilityEvolutionActivationProjection projection =
                new FacilityEvolutionActivationProjection(
                    buildings,
                    new MutableFacilityCandidateCache(),
                    evolution);

            bool failedLoudly = false;
            try
            {
                projection.Initialize();
            }
            catch (InvalidOperationException exception)
            {
                failedLoudly = string.Equals(
                    exception.Message,
                    RecordingEvolutionRuntime.InjectedFailure,
                    StringComparison.Ordinal);
            }

            projection.Tick();
            bool retriedWholePass = Matches(
                evolution.Refreshes,
                first,
                first,
                second);

            projection.Tick();
            return failedLoudly
                && retriedWholePass
                && evolution.Refreshes.Count == 3;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
        }
    }

    private static GameObject CreateFacility(string name)
    {
        GameObject result = new GameObject(name);
        result.AddComponent<BuildableObject>();
        result.AddComponent<FacilityEvolutionStateComponent>();
        return result;
    }

    private static bool Matches(
        IReadOnlyList<BuildableObject> actual,
        params BuildableObject[] expected)
    {
        if (actual == null || actual.Count != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!ReferenceEquals(actual[index], expected[index]))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class MutableBuildingWorldQuery : IBuildingWorldQuery
    {
        private readonly List<BuildableObject> entries = new List<BuildableObject>();
        private readonly ReadOnlyCollection<BuildableObject> view;

        public MutableBuildingWorldQuery()
        {
            view = entries.AsReadOnly();
        }

        public int BuildingVersion { get; private set; }
        public IReadOnlyList<BuildableObject> Buildings => view;

        public void Register(BuildableObject building)
        {
            entries.Add(building);
            BuildingVersion++;
        }

        public void Unregister(BuildableObject building)
        {
            entries.Remove(building);
            BuildingVersion++;
        }
    }

    private sealed class MutableFacilityCandidateCache : IFacilityCandidateCache
    {
        public int DynamicStateVersion { get; private set; }
        public bool HasPendingIndexBuild => false;
        public int CandidateIndexVersion => 0;

        public IReadOnlyList<BuildableObject> GetCandidates(
            Grid grid,
            FacilityRole role) => Array.Empty<BuildableObject>();

        public bool TryGetNearestCandidates(
            Grid grid,
            FacilityRole role,
            Vector2Int origin,
            int maximumCount,
            double budgetMilliseconds,
            out IReadOnlyList<BuildableObject> candidates)
        {
            candidates = Array.Empty<BuildableObject>();
            return true;
        }

        public IReadOnlyList<BuildableObject> GetWorkCandidates(
            Grid grid,
            FacilityWorkType workType) => Array.Empty<BuildableObject>();

        public FacilityRole GetAvailableRoles(Grid grid) => FacilityRole.None;
        public int AdvanceIndex(double budgetMilliseconds) => 0;
        public void Clear() => MarkDynamicStateDirty();

        public void MarkDynamicStateDirty()
        {
            DynamicStateVersion++;
        }
    }

    private sealed class RecordingEvolutionRuntime : IFacilityEvolutionRuntime
    {
        public const string InjectedFailure = "injected activation refresh failure";

        public readonly List<BuildableObject> Refreshes =
            new List<BuildableObject>();

        public Action<BuildableObject> OnRefresh { get; set; }
        public bool MutationApplied { get; set; }
        public bool ThrowOnFirstRefresh { get; set; }

        public bool RefreshRoomActivation(BuildableObject facility)
        {
            Refreshes.Add(facility);
            if (ThrowOnFirstRefresh)
            {
                ThrowOnFirstRefresh = false;
                throw new InvalidOperationException(InjectedFailure);
            }

            OnRefresh?.Invoke(facility);
            return true;
        }

        public FacilityEvolutionState GetState(BuildableObject facility) =>
            throw Unexpected();

        public FacilityEvolutionState RecordUsage(
            BuildableObject facility,
            string eventId,
            float mastery,
            float amount = 1f,
            string actorId = "",
            IEnumerable<string> sourceTags = null) => throw Unexpected();

        public IReadOnlyList<FacilityGenerationCandidate> GetGenerationCandidates(
            BuildableObject facility) => throw Unexpected();

        public bool TryQueueCandidate(
            BuildableObject facility,
            string candidateId,
            out FacilityModificationOrder order,
            out string failureReason) => throw Unexpected();

        public bool TryQueueCandidate(
            BuildableObject facility,
            string candidateId,
            string catalystItemId,
            out FacilityModificationOrder order,
            out string failureReason) => throw Unexpected();

        public bool TryQueueRecalibration(
            BuildableObject facility,
            string nodeId,
            EvolutionModuleActivationRule targetRule,
            string catalystItemId,
            out FacilityRecalibrationOrder order,
            out string failureReason) => throw Unexpected();

        public bool TryQueueRecalibrationToCurrentRoom(
            BuildableObject facility,
            string nodeId,
            string catalystItemId,
            out FacilityRecalibrationOrder order,
            out string failureReason) => throw Unexpected();

        public bool TryQueueRelocation(
            BuildableObject facility,
            Vector2Int destination,
            out FacilityRelocationOrder order,
            out string failureReason) => throw Unexpected();

        public bool TryGetPendingWork(
            BuildableObject facility,
            out FacilityModificationOrder modification,
            out FacilityRecalibrationOrder recalibration) => throw Unexpected();

        public bool TryGetPendingRelocation(
            BuildableObject facility,
            out FacilityRelocationOrder relocation) => throw Unexpected();

        public bool ApplyPendingWork(
            BuildableObject facility,
            float workUnits,
            out EvolutionNode completedNode,
            out bool completed,
            out string failureReason) => throw Unexpected();

        public bool ApplyRelocationWork(
            BuildableObject facility,
            float workUnits,
            out BuildableObject relocatedFacility,
            out bool completed,
            out string failureReason) => throw Unexpected();

        public bool CancelPendingWork(
            BuildableObject facility,
            out string failureReason) => throw Unexpected();

        private static InvalidOperationException Unexpected() =>
            new InvalidOperationException(
                "Activation projection invoked an unrelated evolution command.");
    }
}
