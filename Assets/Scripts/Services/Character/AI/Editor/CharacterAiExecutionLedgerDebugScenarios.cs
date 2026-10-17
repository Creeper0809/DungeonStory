#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterAiExecutionLedgerDebugScenarios
{
    public static void Run(Action<string, string> writeNew)
    {
        var rows = new List<string> { "case\tresult" };
        try
        {
            Check(rows, "epoch-terminal-once-and-definition-identity", () =>
            {
                var ledger = new CharacterAiExecutionLedger();
                AIWait first = ScriptableObject.CreateInstance<AIWait>();
                AIWait second = ScriptableObject.CreateInstance<AIWait>();
                try
                {
                    var context = Context();
                    foreach (var definition in new[] { first, first, second })
                    {
                        ledger.NoteActionStarted(definition);
                        ledger.BeginEpoch(definition.GetInstanceID(), 17, CharacterAiBranch.Wait, context);
                        var terminal = CharacterAiActionTerminalKind.Completed;
                        Require(ledger.TryCloseEpoch(ref terminal, context), "Live epoch did not close.");
                        ledger.RecordTerminal(terminal, context);
                        Require(!ledger.TryCloseEpoch(ref terminal, context), "Duplicate terminal closed another epoch.");
                    }
                    var gate = ledger.CaptureRuntimeGateSnapshot(0);
                    Require(ledger.CurrentActionEpoch == 3 && gate.ActionStarts == 3 && gate.ActionTerminals == 3
                        && gate.ActionCompleted == 3 && gate.LiveActions == 0
                        && ledger.SameActionRestartCount == 1 && ledger.ActionSwitchCount == 1,
                        "Definition restart/switch or terminal conservation changed.");
                    Require(gate.GetBranch(CharacterAiBranch.Wait).ActionStarts == 3
                        && gate.GetBranch(CharacterAiBranch.Wait).ActionTerminals == 3, "Branch epochs did not conserve.");
                }
                finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
            });
            Check(rows, "typed-failure-and-recovered-terminal", () =>
            {
                var ledger = new CharacterAiExecutionLedger();
                foreach (var expected in new[] { CharacterAiActionTerminalKind.Failed, CharacterAiActionTerminalKind.Recovered })
                {
                    ledger.BeginEpoch(11, 17, CharacterAiBranch.Wait, Context());
                    var terminal = expected;
                    Require(ledger.TryCloseEpoch(ref terminal, Context()) && terminal == expected, "Terminal classification changed.");
                    ledger.RecordTerminal(terminal, Context());
                }
                var gate = ledger.CaptureRuntimeGateSnapshot(0);
                Require(gate.ActionFailed == 1 && gate.ActionCancelled == 1 && gate.ActionCompleted == 0, "Typed terminal totals changed.");
            });
            Check(rows, "deferred-path-retry-and-stale-result", () =>
            {
                var ledger = new CharacterAiExecutionLedger(); var context = Context();
                int first = ledger.NotifyPathRequested(context, false, CharacterAiBranch.Wait);
                ledger.NotifyPathResult(context, first, CharacterAiPathTraceState.Deferred, 0);
                Require(ledger.CommittedPathSearchDeferred && ledger.CommittedPathSearchDeferralCount == 1
                    && ledger.LivePathRequestCount == 0, "Deferred result lost retry ownership.");
                int second = ledger.NotifyPathRequested(context, true, CharacterAiBranch.Wait);
                ledger.NotifyPathResult(context, first, CharacterAiPathTraceState.Found, 2);
                Require(!ledger.CommittedPathSearchDeferred && ledger.LivePathRequestCount == 1
                    && ledger.LastInvariantAnomaly == CharacterAiRuntimeInvariant.PathCounterMismatch, "Stale result consumed current request.");
                ledger.NotifyPathResult(context, second, CharacterAiPathTraceState.Found, 2);
                var gate = ledger.CaptureRuntimeGateSnapshot(0);
                Require(gate.PathRequests == 2 && gate.PathResults == 3 && gate.LivePathRequests == 0
                    && gate.GetBranch(CharacterAiBranch.Wait).LivePathRequests == 0, "Path accounting changed.");
            });
            Check(rows, "reservation-conservation-and-invariant-edge", () =>
            {
                var ledger = new CharacterAiExecutionLedger(); var context = Context();
                ledger.NotifyReservationAcquired(CharacterAiBranch.Wait, 33, context);
                var before = ledger.CaptureRuntimeGateSnapshot(0);
                ledger.NotifyReservationReleased(CharacterAiBranch.Wait, 33, context);
                var after = ledger.CaptureRuntimeGateSnapshot(0);
                Require(after.ReservationAcquires == 1 && after.ReservationReleases == 1 && after.LiveReservations == 0
                    && after.ConservesObservedBranchesFrom(before), "Reservation balance changed.");
                ledger.AuditRuntimeInvariants(CharacterAiRuntimeInvariant.ExecutedWithoutActionOwner, 0, context);
                long first = ledger.InvariantAnomalyCount;
                ledger.AuditRuntimeInvariants(CharacterAiRuntimeInvariant.ExecutedWithoutActionOwner, 0, context);
                Require(ledger.InvariantAnomalyCount == first, "Persistent invariant was counted repeatedly.");
                ledger.AuditRuntimeInvariants(CharacterAiRuntimeInvariant.None, 0, context);
                ledger.AuditRuntimeInvariants(CharacterAiRuntimeInvariant.ExecutedWithoutActionOwner, 0, context);
                Require(ledger.InvariantAnomalyCount == first + 1, "New invariant edge was lost.");
            });
            Check(rows, "runtime-heartbeat-versus-gameplay-progress-and-rejection", () =>
            {
                var ledger = new CharacterAiExecutionLedger(); var context = Context();
                ledger.NotifyFacilityQueueHeartbeat(context, 2); ledger.NotifyFacilityQueueHeartbeat(context, 2);
                Require(ledger.RuntimeProgressRevision == 2 && ledger.GameplayProgressRevision == 0
                    && ledger.FacilityQueueHeartbeatCount == 2 && ledger.FacilityQueuePositionRevision == 1,
                    "Queue heartbeat became gameplay progress or changed unchanged-position revision.");
                ledger.NotifyFacilityQueueHeartbeat(context, 1); ledger.NotifyFacilityServiceHeartbeat(context);
                ledger.NotifyGameplayWorkProgress(context, .0015f);
                Require(ledger.FacilityQueuePositionRevision == 2 && ledger.FacilityServiceHeartbeatCount == 1
                    && ledger.GameplayProgressRevision == 2 && ledger.CopyRuntimeTrace().Last().ProgressMilli == 2,
                    "Service/work progression or work rounding changed.");
                long revision = ledger.RuntimeProgressRevision;
                Reject<ArgumentOutOfRangeException>(() => ledger.NotifyFacilityQueueHeartbeat(context, -1));
                foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity })
                    Reject<ArgumentOutOfRangeException>(() => ledger.NotifyGameplayWorkProgress(context, value));
                Require(ledger.RuntimeProgressRevision == revision, "Rejected progress partially changed state.");
            });
            Check(rows, "bounded-detached-trace-context-and-fallback", () =>
            {
                var ledger = new CharacterAiExecutionLedger(); ledger.SetSelectedDefinition(11, 17, CharacterAiBranch.Wait);
                var context = new CharacterAiTraceContext(42, null, null, null, 99);
                for (int index = 0; index < 40; index++) ledger.RecordRuntimeTrace(context, CharacterAiRuntimeTraceKind.Progress);
                var trace = ledger.CopyRuntimeTrace();
                Require(trace.Length == 32 && trace[0].Sequence == 9 && trace[31].Sequence == 40
                    && trace.All(value => value.GameTime == 42 && value.ActionDefinitionInstanceId == 11
                        && value.DestinationInstanceId == 17 && value.Branch == CharacterAiBranch.Wait && value.PhaseCode == 99),
                    "Ring order, bound, time or stored identity fallback changed.");
                trace[0] = default;
                Require(ledger.CopyRuntimeTrace()[0].Sequence == 9, "Snapshot mutates the retained journal.");
                ledger.RecordRuntimeTrace(new CharacterAiTraceContext(43, 21, 27, CharacterAiBranch.LookAround, 100), CharacterAiRuntimeTraceKind.Progress);
                var selected = ledger.CopyRuntimeTrace().Last();
                Require(selected.ActionDefinitionInstanceId == 21 && selected.DestinationInstanceId == 27
                    && selected.Branch == CharacterAiBranch.LookAround && selected.GameTime == 43, "Live selection context did not override stored identity.");
            });
            Check(rows, "retry-classification-and-scheduler-delay", () =>
            {
                var ledger = new CharacterAiExecutionLedger(); var context = Context();
                Reject<ArgumentException>(() => ledger.NotifyRetryScheduled(context, 1, CharacterAiRetryKind.None));
                Require(ledger.CaptureRuntimeGateSnapshot(0).RetrySchedules == 0, "Rejected retry changed counters.");
                ledger.NotifyRetryScheduled(context, .25f, CharacterAiRetryKind.PathSearch);
                ledger.NotifyRetryAttempted(context);
                ledger.NotifySchedulerDecisionProcessed(context, 1, 3.5f, true, false);
                ledger.NotifyRetryAttempted(context);
                var gate = ledger.CaptureRuntimeGateSnapshot(3);
                Require(gate.RetrySchedules == 1 && gate.RetryAttempts == 1 && gate.SchedulerProcesses == 1
                    && gate.SchedulerOverdue == 1 && gate.MaximumSchedulerDelayMilliseconds == 2500 && gate.FailureLoops == 3,
                    "Retry reset, delay or host failure-loop projection changed.");
                ledger.ResetSchedulerDelayTelemetryForDiagnostics();
                Require(ledger.MaximumSchedulerDelayMilliseconds == 0 && ledger.SchedulerProcessCount == 1, "Delay reset changed execution counts.");
            });
            Check(rows, "schedule-observation-clr-values-and-retry-projection", () =>
            {
                Require(typeof(CharacterAiRetryKind).Assembly == typeof(AIBrain).Assembly
                    && typeof(CharacterAiDecisionScheduleObservation).Assembly == typeof(AIBrain).Assembly
                    && typeof(CharacterAiRetryKind).FullName == "CharacterAiRetryKind"
                    && typeof(CharacterAiDecisionScheduleObservation).FullName == "CharacterAiDecisionScheduleObservation",
                    "Schedule observation CLR/assembly identity changed.");
                Require(Enum.GetValues(typeof(CharacterAiRetryKind)).Cast<CharacterAiRetryKind>()
                    .Select(value => (int)value).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }), "Retry kind values changed.");
                var scheduled = new CharacterAiDecisionScheduleObservation(true, 2.5f, 2, CharacterAiRetryKind.PathSearch);
                var unscheduled = new CharacterAiDecisionScheduleObservation(false, 2.5f, 2, CharacterAiRetryKind.PathSearch);
                var overdue = new CharacterAiDecisionScheduleObservation(true, 2, 3, CharacterAiRetryKind.None);
                Require(scheduled.IsRetry && scheduled.RemainingSeconds == .5f && scheduled.DueTime == 2.5f
                    && !unscheduled.IsRetry && unscheduled.RetryKind == CharacterAiRetryKind.None
                    && unscheduled.RemainingSeconds == 0 && overdue.RemainingSeconds == 0 && !overdue.IsRetry,
                    "Schedule/retry observation semantics changed.");
            });
            Check(rows, "actual-brain-operation-block-flow-projection", () =>
                Require(GameplayFlowDiagnosticsDebugScenarios.RunAll(logSuccess: true), "Gameplay flow projection regression failed."));
        }
        finally { writeNew("ai-execution-ledger-boundary.tsv", string.Join("\n", rows)); }
        Debug.Log("ARCHITECTURE_AI_EXECUTION_LEDGER_BOUNDARY=PASS; groups=9");
    }

    private static CharacterAiTraceContext Context() => new CharacterAiTraceContext(1, 11, 17, CharacterAiBranch.Wait, 5);
    private static void Check(List<string> rows, string name, Action action)
    {
        try { action(); rows.Add(name + "\tPASS"); }
        catch { rows.Add(name + "\tFAIL"); throw; }
    }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected rejection: " + typeof(T).Name);
    }
    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }
}
#endif
