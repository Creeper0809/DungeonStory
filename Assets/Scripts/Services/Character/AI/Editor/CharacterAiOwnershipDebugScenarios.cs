#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class CharacterAiOwnershipDebugScenarios
{
    public static void Run(Action<string, string> writeNew)
    {
        var rows = new List<string> { "case\tresult" };
        AIWait first = ScriptableObject.CreateInstance<AIWait>();
        AIWait second = ScriptableObject.CreateInstance<AIWait>();
        AILookAround look = ScriptableObject.CreateInstance<AILookAround>();
        try
        {
            Check(rows, "preference-identity-minimum-persistence-and-inclusive-expiry", () =>
            {
                var clock = new Clock { TimeValue = 10 }; var state = Preferred(clock);
                state.SetPreferredAction(first, 0);
                var matcher = state.GetPreferredActionMatcher();
                Require(ReferenceEquals(matcher, state.GetPreferredActionMatcher()) && matcher(first) && !matcher(second)
                    && state.IsActionPreferredForNextDecision<AIWait>() && state.IsRoutineGroupPreferredForNextDecision(CharacterAiBranch.Idle), "Definition identity or group changed.");
                clock.TimeValue = 12;
                Require(state.IsBranchPreferredForNextDecision(CharacterAiBranch.Wait), "Inclusive expiry boundary changed.");
                clock.TimeValue = 12.01f;
                Require(!matcher(first) && !state.IsActionPreferredForNextDecision<AIWait>(), "Expired request still matches.");
            });
            Check(rows, "preference-deferred-branch-ownership-and-fallback", () =>
            {
                var state = Preferred(new Clock()); state.SetPreferredAction(first, 30);
                var deferred = AIActionFailure.Create(AIActionFailureKind.PathSearchDeferred, "deferred-evidence");
                long revision = state.PreferredActionDispositionRevision;
                Require(!state.TryRetainPreferredBranchDeferred(CharacterAiBranch.LookAround, deferred)
                    && !state.TryRetainPreferredBranchDeferred(CharacterAiBranch.Wait, AIActionFailure.Create(AIActionFailureKind.NoPath))
                    && state.PreferredActionDeferredCount == 0 && state.PreferredActionDispositionRevision == revision, "Rejected deferral changed request.");
                Require(state.TryRetainPreferredBranchDeferred(CharacterAiBranch.Wait, deferred) && state.IsDeferred
                    && state.LastPreferredActionDeferredFailure.Reason == "deferred-evidence", "Owning deferral was lost.");
                state.RecordDeferredFallbackSuppression();
                state.RecordCandidateCommitted(CharacterAiBranch.Wait);
                Require(state.IsDeferred && state.PreferredActionCommitCount == 1
                    && state.PreferredActionDeferredFallbackSuppressionCount == 1
                    && state.PreferredActionDisposition == CharacterAiPreferredActionDisposition.Selected, "Candidate commit changed pre-clear ordering.");
                state.ClearPreferredAction(); Require(!state.IsDeferred, "Commit cleanup retained deferral.");
            });
            Check(rows, "preference-first-hard-failure-consumed-once-and-reset", () =>
            {
                var state = Preferred(new Clock()); state.SetPreferredAction(first, 30);
                var hard = AIActionFailure.Create(AIActionFailureKind.NoPath, "first-evidence");
                Require(!state.RetirePreferredBranchAfterHardFailure(CharacterAiBranch.Wait,
                    AIActionFailure.Create(AIActionFailureKind.CandidateEvaluationDeferred), CharacterAiPreferredActionFailureSource.DirectActionEvaluation), "Deferred state retired a request.");
                Require(state.RetirePreferredBranchAfterHardFailure(CharacterAiBranch.Wait, hard,
                    CharacterAiPreferredActionFailureSource.JobGiverCandidateCommit), "Hard failure did not retire owner.");
                Require(!state.RetirePreferredBranchAfterHardFailure(CharacterAiBranch.Wait,
                    AIActionFailure.Create(AIActionFailureKind.Destroyed, "later"), CharacterAiPreferredActionFailureSource.DirectActionEvaluation), "Retired branch accepted another failure.");
                Require(state.TryConsumePreferredActionHardFailureDecision(out var failure, out var source)
                    && failure.Reason == "first-evidence" && source == CharacterAiPreferredActionFailureSource.JobGiverCandidateCommit
                    && !state.TryConsumePreferredActionHardFailureDecision(out _, out _), "First failure was replaced or consumed twice.");
                state.SetPreferredAction(look, 30);
                Require(!state.FirstPreferredActionHardFailure.HasFailure && !state.PreferredActionHardFailureDecisionPending
                    && state.PreferredActionHardFailureCount == 1, "New request failed to reset evidence or reset cumulative count.");
            });
            Check(rows, "preference-work-scope-clear-consume-and-expiry", () =>
            {
                var clock = new Clock(); var state = Preferred(clock); state.SetPreferredAction(first, 10);
                state.PreferWorkType(BuiltInWorkTypeIds.Haul, 10);
                state.ConsumePreferredWorkType(BuiltInWorkTypeIds.Clean);
                Require(state.TryGetPreferredWorkType(out var work) && work == BuiltInWorkTypeIds.Haul, "Foreign consumption cleared work preference.");
                state.ClearPreferredAction(preserveWorkType: true);
                Require(state.HasPreferredWorkType, "Work-preserving commit cleared work scope.");
                state.ConsumePreferredWorkType(BuiltInWorkTypeIds.Haul);
                Require(!state.HasPreferredWorkType, "Exact work consumption did not clear scope.");
                state.PreferWorkType(BuiltInWorkTypeIds.Clean, 2); clock.TimeValue = 2.01f;
                Require(!state.TryGetPreferredWorkType(out work) && !work.IsValid, "Expired work preference was returned.");
            });
            Check(rows, "preference-scoring-expiry-preserves-independent-work", () =>
            {
                var state = Preferred(new Clock()); state.SetPreferredAction(first, 2);
                state.PreferWorkType(BuiltInWorkTypeIds.Haul, 20);
                state.TryRetainPreferredBranchDeferred(CharacterAiBranch.Wait, AIActionFailure.Create(AIActionFailureKind.PathSearchDeferred));
                long revision = state.PreferredActionDispositionRevision;
                state.ExpireDefinitionForScoring();
                Require(state.PreferredNextActionSet == null && !state.IsDeferred && state.HasPreferredWorkType
                    && state.PreferredActionDispositionRevision == revision && state.LastPreferredActionDeferredFailure.IsDeferred,
                    "Scoring expiry became a full work/evidence reset.");
                state.SetPreferredAction(null, 2);
                Require(!state.HasPreferredWorkType && !state.LastPreferredActionDeferredFailure.HasFailure, "New null request retained prior work/evidence.");
            });
            Check(rows, "preference-destroyed-unity-definition-compatibility", () =>
            {
                var state = Preferred(new Clock()); AIWait destroyed = ScriptableObject.CreateInstance<AIWait>();
                state.SetPreferredAction(destroyed, 20); UnityEngine.Object.DestroyImmediate(destroyed);
                Require(state.IsActionPreferredForNextDecision<AIWait>()
                    && !state.IsBranchPreferredForNextDecision(CharacterAiBranch.Wait)
                    && !state.GetPreferredActionMatcher()(destroyed), "Existing C# type/Unity-null query semantics changed.");
            });
            Check(rows, "intent-invalid-manual-and-equal-priority-rejection", () =>
            {
                var state = new CharacterExternalActionIntentRuntime();
                Require(state.Admit(" ", CharacterActionIntentKind.RoutineNeed) == CharacterExternalIntentAdmission.Rejected
                    && state.Admit("owner", CharacterActionIntentKind.None) == CharacterExternalIntentAdmission.Rejected, "Invalid intent admitted.");
                state.BeginManualCommand();
                Require(state.Admit("owner", CharacterActionIntentKind.Breakdown) == CharacterExternalIntentAdmission.Rejected, "External intent displaced manual command.");
                state.EndManualCommand(); state.Begin("owner", CharacterActionIntentKind.RoutineNeed); state.RecordTransition(false);
                Require(state.Admit("foreign", CharacterActionIntentKind.RoutineNeed) == CharacterExternalIntentAdmission.Rejected
                    && state.ExternalIntentEpoch == 1 && state.ExternalIntentRejectedCount == 4, "Rejected intent changed epoch/owner.");
            });
            Check(rows, "intent-same-owner-renew-and-higher-priority-preemption", () =>
            {
                var state = new CharacterExternalActionIntentRuntime(); state.Begin("low", CharacterActionIntentKind.RoutineNeed); state.RecordTransition(false);
                var old = state.CurrentLease;
                Require(state.Admit("low", CharacterActionIntentKind.Breakdown) == CharacterExternalIntentAdmission.Renewed
                    && state.OwnsExternalIntent(old) && state.ExternalIntentKind == CharacterActionIntentKind.RoutineNeed, "Same owner renewal changed lease priority/epoch.");
                Require(state.Admit("high", CharacterActionIntentKind.Breakdown) == CharacterExternalIntentAdmission.Preempt, "Higher priority did not preempt.");
                state.Begin("high", CharacterActionIntentKind.Breakdown); state.RecordTransition(true);
                Require(!state.OwnsExternalIntent(old) && state.OwnsExternalIntent(state.CurrentLease)
                    && state.ExternalIntentEpoch == 2 && state.ExternalIntentTransitionCount == 2
                    && state.ExternalIntentPreemptionCount == 1 && state.ExternalIntentTerminalCount == 1
                    && state.LastExternalIntentTerminalKind == CharacterAiActionTerminalKind.Cancelled, "Preemption conservation changed.");
            });
            Check(rows, "intent-replan-latch-terminal-and-lifecycle-presence", () =>
            {
                var state = new CharacterExternalActionIntentRuntime(); state.Begin("owner", CharacterActionIntentKind.RoutineNeed); state.RecordTransition(false);
                var lease = state.CurrentLease; state.RequestClearFailures(true); state.RequestClearFailures(false);
                Require(state.End(false, CharacterAiActionTerminalKind.None)
                    && !state.OwnsExternalIntent(lease) && !state.ExternallyDrivenActionActive
                    && state.LastExternalIntentTerminalKind == CharacterAiActionTerminalKind.Completed
                    && state.ExternalIntentTerminalCount == 1, "Terminal lost pending clear or terminal normalization.");
                state.RejectStaleCompletion(); state.RejectUpdate(); state.BeginManualCommand(); state.ResetLifecyclePresence();
                Require(!state.ManualCommandActive && !state.ExternallyDrivenActionActive && state.ExternalIntentOwnerId == string.Empty
                    && state.ExternalIntentEpoch == 1 && state.ExternalIntentStaleCompletionCount == 1
                    && state.ExternalIntentRejectedCount == 1 && state.ExternalIntentTerminalCount == 1, "Lifecycle reset changed terminal history.");
            });
            Check(rows, "failure-loop-window-definition-identity-and-no-action", () =>
            {
                var stats = new CharacterAiFailureDiagnosticsRuntime(); var ledger = new CharacterAiExecutionLedger();
                for (int index = 0; index < 5; index++) Track(stats, ledger, first, index, false);
                Require(stats.CurrentRepeatedFailureCount == 5 && stats.PeakRepeatedFailureCount == 5 && stats.FailureLoopCount == 1, "Loop threshold counted repeatedly or too early.");
                Track(stats, ledger, second, 5, true);
                Require(stats.CurrentRepeatedFailureCount == 1 && stats.NoActionFailureCount == 1, "New definition did not reset repetition.");
                Track(stats, ledger, second, 10, false);
                Require(stats.CurrentRepeatedFailureCount == 2, "Inclusive repeat window changed.");
                Track(stats, ledger, second, 15.01f, false);
                Require(stats.CurrentRepeatedFailureCount == 1 && stats.ExecutionFailureCount == 8 && stats.FailureLoopCount == 1, "Expired repeat window retained repetition.");
            });
            Check(rows, "failure-jobgiver-deferred-counts-pass-scoping-and-clones", () =>
            {
                var stats = new CharacterAiFailureDiagnosticsRuntime(); stats.BeginJobGiverEvaluationPass();
                stats.RecordJobGiverKind(CharacterAiBranch.Work, AIActionFailureKind.PathSearchDeferred);
                Require(stats.JobGiverEvaluationRejectionCount == 0 && stats.GetCurrentJobGiverEvaluationRejectionSummary(CharacterAiBranch.Work) == "none", "Backpressure became a hard rejection.");
                stats.RecordJobGiverKind(CharacterAiBranch.Work, AIActionFailureKind.NoWork);
                stats.RecordHardJobGiverRejection(CharacterAiBranch.Work, AIActionFailureKind.NoWork, "no-work-evidence");
                Require(stats.JobGiverEvaluationRejectionCount == 1
                    && stats.GetCurrentJobGiverEvaluationRejectionSummary(CharacterAiBranch.Work).Contains("no-work-evidence"), "Current hard rejection lost evidence.");
                var copy = stats.CopyJobGiverRejections(); Require(copy.Sum() == 2, "Typed counters lost deferred/hard rejection.");
                Array.Clear(copy, 0, copy.Length); Require(stats.CopyJobGiverRejections().Sum() == 2, "Snapshot mutated failure counters.");
                stats.BeginJobGiverEvaluationPass();
                Require(stats.GetCurrentJobGiverEvaluationRejectionSummary(CharacterAiBranch.Work) == "none"
                    && stats.GetCurrentJobGiverEvaluationRejectionSummary((CharacterAiBranch)(-1)) == "none", "Old pass or invalid branch became current.");
            });
            Check(rows, "failure-trace-evidence-and-candidate-buckets", () =>
            {
                var stats = new CharacterAiFailureDiagnosticsRuntime(); var ledger = new CharacterAiExecutionLedger();
                Track(stats, ledger, first, 17, true);
                var trace = ledger.CopyRuntimeTrace().Single();
                Require(trace.Kind == CharacterAiRuntimeTraceKind.ExecutionFailure && trace.FailureKind == AIActionFailureKind.NoPath
                    && trace.GameTime == 17 && stats.LastExecutionFailureDetail == "controlled-evidence", "Failure trace changed timing/type/evidence.");
                stats.RecordCandidateRejection(AIActionFailureKind.None); stats.RecordCandidateRejection(AIActionFailureKind.NoWork);
                Require(stats.CandidateRejectionCount == 2 && stats.CopyCandidateRejections().Sum() == 1
                    && stats.CopyExecutionFailures().Sum() == 1, "Candidate/execution buckets were merged or None bucket incremented.");
            });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(look);
            writeNew("ai-request-ownership-boundary.tsv", string.Join("\n", rows));
        }
        Debug.Log("ARCHITECTURE_AI_REQUEST_OWNERSHIP_BOUNDARY=PASS; groups=12");
    }

    private static CharacterAiPreferredActionRuntime Preferred(Clock clock)
    {
        var state = new CharacterAiPreferredActionRuntime(); state.BindClock(clock); return state;
    }
    private static void Track(CharacterAiFailureDiagnosticsRuntime stats, CharacterAiExecutionLedger ledger, UnityEngine.Object definition, float now, bool noAction) =>
        stats.TrackExecutionFailure(definition, AIActionFailureKind.NoPath, noAction, "controlled-evidence", 1, now, ledger,
            new CharacterAiTraceContext(now, 11, 17, CharacterAiBranch.Wait, 0));
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
