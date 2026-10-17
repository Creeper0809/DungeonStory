#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;

public static class CharacterOperationObservationDebugScenarios
{
    public static void Run(Action<string, string> writeNew)
    {
        var rows = new List<string> { "case\tresult" };
        var firstRoot = new GameObject("ObservationBoundaryFirst");
        var secondRoot = new GameObject("ObservationBoundarySecond");
        var definition = ScriptableObject.CreateInstance<AIWait>();
        var otherDefinition = ScriptableObject.CreateInstance<AIWait>();
        try
        {
            var first = firstRoot.AddComponent<BuildableObject>();
            var second = secondRoot.AddComponent<BuildableObject>();
            SetIdentity(first, "building:observation-first"); SetIdentity(second, "building:observation-second");
            var action = new AIAction(definition, AIActionPlan.AtDestination(first));
            var clock = new Clock { TimeValue = 12 };
            var evaluator = new AIBrainActionEvaluator(clock, new EditorCharacterAiPerformanceRecorder());
            bool Record(CharacterOperationBlockObservationRuntime state, AIActionFailure failure,
                long epoch = 7, bool history = true, bool cooldown = false,
                DomainFailure domain = default, CharacterOperationBlockAxis axis = CharacterOperationBlockAxis.Unknown) =>
                state.Record(definition, failure, domain, axis, CharacterAiRetryKind.FailureCooldown,
                    cooldown, history, null, action, epoch, evaluator, clock);
            CharacterOperationBlockSnapshot Read(CharacterOperationBlockObservationRuntime state, long epoch = 7, bool live = true) =>
                state.CaptureCurrentOperationBlock(evaluator, live, epoch, action);
            var failure = AIActionFailure.Create(AIActionFailureKind.NoPath, "original-evidence", first);

            Check(rows, "invalid-record-keeps-current-and-history", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime(); Require(Record(state, failure), "Valid observation rejected.");
                Require(!Record(state, failure, epoch: 0) && !Record(state, AIActionFailure.None), "Invalid epoch or empty failure accepted.");
                first.isDestroy = true; Require(!Record(state, failure), "Destroyed target accepted."); first.isDestroy = false;
                SetIdentity(first, string.Empty); Require(!Record(state, failure), "Missing stable identity accepted."); SetIdentity(first, "building:observation-first");
                Require(Read(state).Failure.Reason == "original-evidence" && state.RecentOperationFailure.Failure.Reason == "original-evidence", "Rejected record mutated evidence.");
            });
            Check(rows, "current-epoch-target-and-immutable-history", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime(); Require(Record(state, failure), "Record failed.");
                var snapshot = Read(state); var history = state.RecentOperationFailure;
                Require(snapshot.HasCurrentBlock && snapshot.ActionEpoch == 7 && snapshot.ObservedAt == 12
                    && snapshot.Axis == CharacterOperationBlockAxis.Access && snapshot.ActionStableId == "ai-action:" + typeof(AIWait).FullName
                    && ReferenceEquals(snapshot.Failure.Target, first), "Typed identity/time/failure projection changed.");
                Require(!Read(state, epoch: 8).HasCurrentBlock && !Read(state, live: false).HasCurrentBlock
                    && Read(state, epoch: 8).HasObservation && !Read(state, epoch: 8).IsCurrentnessConfirmed, "Old epoch became a confirmed block.");
                Require(history.HasObservation && !history.IsCurrent && !history.HasKnownExpiry && !history.IsCurrentnessConfirmed
                    && snapshot.HasCurrentBlock, "History conversion mutated captured current snapshot.");
            });
            Check(rows, "domain-revision-overrides-epoch-without-active-probes", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime(); Record(state, failure);
                int revision = 3, reads = 0; state.BindSourceRevision(() => { reads++; return revision; }, 3);
                Require(Read(state, epoch: 99, live: false).HasCurrentBlock, "Live domain revision was tied to old epoch.");
                revision++; Require(!Read(state).HasCurrentBlock && Read(state).HasObservation && reads == 3, "Revision drift or read count changed.");
                state.BindSourceRevision(() => throw new InvalidOperationException("revision-reader-failure"), 0);
                bool propagated = false; try { Read(state); } catch (InvalidOperationException e) { propagated = e.Message == "revision-reader-failure"; }
                Require(propagated && state.RecentOperationFailure.HasObservation, "Revision failure was hidden or history lost.");
            });
            Check(rows, "cooldown-requires-live-expiry-and-preserves-history", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime();
                evaluator.ClearCooldowns(); evaluator.StartCooldown(definition, 2); evaluator.StartDestinationCooldown(definition, first, 4);
                Record(state, failure, cooldown: true); Require(Read(state).ExpiresAt == 16, "Maximum applicable expiry lost.");
                clock.TimeValue = 16; Require(!Read(state).HasObservation && state.RecentOperationFailure.HasObservation, "Expired cooldown stayed current or erased history.");
                clock.TimeValue = 12; evaluator.ClearCooldowns(); Record(state, failure, cooldown: true);
                Require(!Read(state).HasObservation, "Missing cooldown produced a fake current block.");
            });
            Check(rows, "deferred-observation-resets-provenance-keeps-hard-history", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime(); Record(state, failure);
                state.BindSourceRevision(() => throw new InvalidOperationException("retired-reader"), 1);
                Require(Record(state, AIActionFailure.Create(AIActionFailureKind.CannotStart, "executor-deferred"), history: false), "Fallback target was not retained.");
                Require(Read(state).Failure.Reason == "executor-deferred" && Read(state).TargetStableId == "building:observation-first"
                    && state.RecentOperationFailure.Failure.Reason == "original-evidence", "Deferral replaced history or retained retired revision reader.");
            });
            Check(rows, "retire-matching-definition-and-different-destination", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime(); Record(state, failure);
                state.ClearCurrentOperationBlockFor(new AIAction(otherDefinition, AIActionPlan.AtDestination(first)));
                state.RetireForDifferentDestination(first); state.RetireForDifferentDestination(null);
                Require(Read(state).HasCurrentBlock, "Unrelated action/same destination cleared observation.");
                state.RetireForDifferentDestination(second); Require(!Read(state).HasObservation && state.RecentOperationFailure.HasObservation, "Destination transition failed to retire only current.");
                Record(state, failure); state.ClearCurrentOperationBlockFor(new AIAction(definition, AIActionPlan.AtDestination(second)));
                Require(!Read(state).HasObservation, "Same definition cleanup was narrowed to action instance.");
                state.ClearAllOperationDiagnostics(); Require(!state.RecentOperationFailure.HasObservation, "Lifecycle retained stale history.");
            });
            Check(rows, "target-identity-drift-and-destruction-are-not-current", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime(); Record(state, failure);
                SetIdentity(first, "building:replaced"); Require(!Read(state).HasObservation, "Identity replacement retained current block.");
                SetIdentity(first, "building:observation-first"); first.isDestroy = true; Require(!Read(state).HasObservation, "Domain destruction retained current block."); first.isDestroy = false;
                var temporary = new GameObject("DestroyedObservationTarget"); var destroyed = temporary.AddComponent<BuildableObject>(); SetIdentity(destroyed, "building:destroyed");
                Require(Record(state, AIActionFailure.Create(AIActionFailureKind.Destroyed, "destroyed", destroyed)), "Managed target record failed.");
                UnityEngine.Object.DestroyImmediate(temporary);
                Require(!Read(state).HasObservation && state.RecentOperationFailure.HasObservation, "Destroyed Unity target appeared current.");
            });
            Check(rows, "domain-axis-precedence-and-explicit-axis", () =>
            {
                var state = new CharacterOperationBlockObservationRuntime();
                Record(state, failure, domain: new DomainFailure(FailureCode.AutomationUnpowered));
                Require(Read(state).Axis == CharacterOperationBlockAxis.Power, "Typed domain mapping lost precedence.");
                Record(state, failure, domain: new DomainFailure(FailureCode.AutomationUnpowered), axis: CharacterOperationBlockAxis.Identity);
                Require(Read(state).Axis == CharacterOperationBlockAxis.Identity, "Explicit observation axis was overwritten.");
            });
            Check(rows, "facility-cache-clamps-scopes-and-isolates-owners", () =>
            {
                var cache = new CharacterAiDecisionObservationCache(); var other = new CharacterAiDecisionObservationCache();
                cache.CacheFacilityScore(first, (FacilityRole)0, 2); cache.CacheFacilityScore(first, (FacilityRole)1, -2);
                cache.CacheFacilityScore(null, (FacilityRole)0, 0.5f);
                Require(cache.TryGetCachedFacilityScore(first, (FacilityRole)0, out float a) && a == 1
                    && cache.TryGetCachedFacilityScore(first, (FacilityRole)1, out float b) && b == 0
                    && !cache.TryGetCachedFacilityScore(second, (FacilityRole)0, out _) && !other.TryGetCachedFacilityScore(first, (FacilityRole)0, out _), "Cache keys/clamp/owner isolation changed.");
                cache.ClearFacilityScores(); Require(!cache.TryGetCachedFacilityScore(first, (FacilityRole)0, out _), "Facility invalidation retained score.");
            });
            Check(rows, "world-cache-movement-branch-and-independent-invalidation", () =>
            {
                var cache = new CharacterAiDecisionObservationCache(); var signal = CharacterAiWorldSignalSnapshot.Neutral.WithScheduleScore(0.75f);
                cache.CacheFacilityScore(first, (FacilityRole)0, 0.5f); cache.CacheWorldSignal(CharacterAiBranch.Wait, Vector2Int.zero, signal);
                Require(cache.TryGetCachedWorldSignal(CharacterAiBranch.Wait, Vector2Int.zero, out var same) && same.ScheduleScore == 0.75f
                    && !cache.TryGetCachedWorldSignal(CharacterAiBranch.LookAround, Vector2Int.zero, out _)
                    && !cache.TryGetCachedWorldSignal(CharacterAiBranch.Wait, Vector2Int.right, out _), "Branch/position cache scope changed.");
                cache.CacheWorldSignal(CharacterAiBranch.LookAround, Vector2Int.right, signal);
                Require(!cache.TryGetCachedWorldSignal(CharacterAiBranch.Wait, Vector2Int.zero, out _), "Moving cache retained old position.");
                cache.ClearWorldSignalCache(); cache.InvalidateCrowdSensitivity();
                Require(!cache.TryGetCachedWorldSignal(CharacterAiBranch.LookAround, Vector2Int.right, out _)
                    && cache.TryGetCachedFacilityScore(first, (FacilityRole)0, out _), "Independent invalidation cleared wrong cache.");
            });
            Check(rows, "existing-operation-ui-projection-is-passive", () => Require(GameplayFlowDiagnosticsDebugScenarios.RunAll(false), "Existing operation flow checks failed."));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstRoot); UnityEngine.Object.DestroyImmediate(secondRoot);
            UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(otherDefinition);
            writeNew("ai-operation-observation-boundary.tsv", string.Join("\n", rows));
        }
        Debug.Log("ARCHITECTURE_AI_OPERATION_OBSERVATION_BOUNDARY=PASS; groups=11");
    }

    private static void SetIdentity(BuildableObject target, string id) =>
        typeof(BuildableObject).GetField("persistentInstanceId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, id);
    private static void Check(List<string> rows, string name, Action action)
    {
        try { action(); rows.Add(name + "\tPASS"); } catch { rows.Add(name + "\tFAIL"); throw; }
    }
    private static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }
    private sealed class Clock : IGameClock
    {
        public float TimeValue;
        public float Time => TimeValue;
        public float DeltaTime => 1f / 60;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
}
#endif
