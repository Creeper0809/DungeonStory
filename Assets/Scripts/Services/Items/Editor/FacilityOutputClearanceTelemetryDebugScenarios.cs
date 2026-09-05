using System;
using DungeonStory.Foundation;
using UnityEditor;

public static class FacilityOutputClearanceTelemetryDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/QA/V27/Facility Output Clearance Telemetry")]
    public static void RunFromMenu()
    {
        RunAll();
        UnityEngine.Debug.Log(
            "V27 facility output-clearance telemetry scenarios passed.");
    }

    public static void RunAll()
    {
        VerifyFixedPointClock();
        VerifyDisabledRuntimeIsNoOp();
        VerifyPartialPickupCompletesExactlyOnce();
        VerifyRecoveryReplayIsIdempotent();
        VerifyRollbackRemovesUncommittedSample();
        VerifyInvalidObservationCounters();
    }

    private static void VerifyDisabledRuntimeIsNoOp()
    {
        FacilityOutputClearanceTelemetryRuntime telemetry = new();
        telemetry.RecordPublication(new FacilityOutputBatchPublishedObservation(
            "batch:qa:clearance:disabled",
            "facility:qa:clearance",
            100L,
            0L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:disabled",
            "slice:qa:clearance:disabled",
            100L,
            1L));
        FacilityOutputClearanceTelemetrySnapshot snapshot = telemetry.Capture();
        Require(snapshot.ActiveBatchCount == 0
            && snapshot.Completed.Count == 0
            && snapshot.IsClean,
            "Player runtime must remain a no-op until an audit session is explicit.");
    }

    private static void VerifyFixedPointClock()
    {
        MutableClock clock = new() { TimeValue = 7.5f };
        Require(
            FacilityOutputClearanceTelemetryRuntime
                .CaptureMicroGameHours(clock) == 1_000_000L,
            "7.5 game seconds must equal exactly 1000000 micro-hours.");
        clock.TimeValue = 3.75f;
        Require(
            FacilityOutputClearanceTelemetryRuntime
                .CaptureMicroGameHours(clock) == 500_000L,
            "3.75 game seconds must equal exactly 500000 micro-hours.");
    }

    private static void VerifyPartialPickupCompletesExactlyOnce()
    {
        FacilityOutputClearanceTelemetryRuntime telemetry = new();
        telemetry.BeginCapture("qa:clearance:partial");
        telemetry.RecordPublication(new FacilityOutputBatchPublishedObservation(
            "batch:qa:clearance:partial",
            "facility:qa:clearance",
            1_000L,
            1_000_000L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:partial",
            "slice:qa:clearance:partial:0",
            400L,
            1_200_000L));
        FacilityOutputClearanceTelemetrySnapshot partial = telemetry.Capture();
        Require(partial.ActiveBatchCount == 1
            && partial.Completed.Count == 0
            && !partial.IsClean,
            "A partial pickup must not complete the clearance sample.");

        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:partial",
            "slice:qa:clearance:partial:1",
            600L,
            1_500_001L));
        FacilityOutputClearanceTelemetrySnapshot completed = telemetry.Capture();
        Require(completed.ActiveBatchCount == 0
            && completed.Completed.Count == 1
            && completed.Completed[0].BatchMassGrams == 1_000L
            && completed.Completed[0].ClearanceMicroHours == 500_001L
            && completed.Completed[0].ClearanceMilliHours == 501L
            && completed.IsClean,
            "Elapsed micro-hours must be quantized once with conservative Ceil.");
        FacilityOutputClearanceTelemetrySnapshot ended = telemetry.EndCapture();
        Require(ended.Completed.Count == 1 && !telemetry.IsCaptureActive,
            "Ending capture must return the final immutable sample set.");
    }

    private static void VerifyRecoveryReplayIsIdempotent()
    {
        FacilityOutputClearanceTelemetryRuntime telemetry = new();
        telemetry.BeginCapture("qa:clearance:recovery-replay");
        telemetry.RecordPublication(new FacilityOutputBatchPublishedObservation(
            "batch:qa:clearance:recovery-replay",
            "facility:qa:clearance",
            1_000L,
            100L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:recovery-replay",
            "slice:qa:clearance:recovery-replay:a",
            600L,
            110L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:recovery-replay",
            "slice:qa:clearance:recovery-replay:a",
            600L,
            120L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:recovery-replay",
            "slice:qa:clearance:recovery-replay:b",
            400L,
            130L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:recovery-replay",
            "slice:qa:clearance:recovery-replay:b",
            400L,
            140L));
        FacilityOutputClearanceTelemetrySnapshot snapshot = telemetry.Capture();
        Require(snapshot.Completed.Count == 1
            && snapshot.ActiveBatchCount == 0
            && snapshot.OrphanPickupCount == 0
            && snapshot.OverPickupCount == 0
            && snapshot.ConflictingPublicationCount == 0
            && snapshot.IsClean,
            "A recovery drop and exact physical-slice re-pickup must be an idempotent observation, including after batch completion.");

        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:recovery-replay",
            "slice:qa:clearance:recovery-replay:b",
            401L,
            150L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:recovery-replay",
            "slice:qa:clearance:recovery-replay:c",
            1L,
            160L));
        snapshot = telemetry.Capture();
        Require(snapshot.ConflictingPublicationCount == 1
            && snapshot.OverPickupCount == 1,
            "A reused slice identity with different mass must conflict, and a distinct post-completion slice must remain an over-pickup.");
    }

    private static void VerifyRollbackRemovesUncommittedSample()
    {
        FacilityOutputClearanceTelemetryRuntime telemetry = new();
        telemetry.BeginCapture("qa:clearance:rollback");
        telemetry.RecordPublication(new FacilityOutputBatchPublishedObservation(
            "batch:qa:clearance:rollback",
            "facility:qa:clearance",
            250L,
            10L));
        telemetry.RecordPublicationRollback("batch:qa:clearance:rollback");
        FacilityOutputClearanceTelemetrySnapshot snapshot = telemetry.Capture();
        Require(snapshot.ActiveBatchCount == 0
            && snapshot.Completed.Count == 0
            && snapshot.IsClean,
            "A physical publication rollback must remove the raw sample candidate.");
    }

    private static void VerifyInvalidObservationCounters()
    {
        FacilityOutputClearanceTelemetryRuntime telemetry = new();
        telemetry.BeginCapture("qa:clearance:invalid");
        telemetry.RecordPublication(new FacilityOutputBatchPublishedObservation(
            "batch:qa:clearance:conflict",
            "facility:qa:clearance",
            100L,
            20L));
        telemetry.RecordPublication(new FacilityOutputBatchPublishedObservation(
            "batch:qa:clearance:conflict",
            "facility:qa:other",
            100L,
            20L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:orphan",
            "slice:qa:clearance:orphan",
            1L,
            21L));
        telemetry.RecordCommittedPickup(new FacilityOutputBatchPickupObservation(
            "batch:qa:clearance:conflict",
            "slice:qa:clearance:conflict:over",
            101L,
            22L));
        FacilityOutputClearanceTelemetrySnapshot snapshot = telemetry.Capture();
        Require(!snapshot.IsClean
            && snapshot.ConflictingPublicationCount == 1
            && snapshot.OrphanPickupCount == 1
            && snapshot.OverPickupCount == 1
            && snapshot.ActiveBatchCount == 1,
            "Conflicting, orphan, and over-pickup observations must remain visible and unusable for p95 generation.");

        RequireThrows(
            telemetry.ValidateBeforeCapture,
            "Save capture must fail loudly while diagnostics capture is active.");
        telemetry.OnRestoreCompleted();
        Require(telemetry.Capture().RestoreInterruptionCount == 1
            && !telemetry.Capture().IsClean,
            "Restore interruption must invalidate the measurement session.");
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class MutableClock : IGameClock
    {
        internal float TimeValue;
        public float DeltaTime => 0f;
        public float Time => TimeValue;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
}
