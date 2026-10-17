#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    public static void RunOrThrow()
    {
        VerifyNaturalDeathPrepareRejectionAndDeliveryRestore();
        VerifyAutomaticNaturalDeathAcknowledgementRestore();
        VerifyDestroyedPlotRestoreAndReplay();
        VerifyAutomaticDestroyedPlotRetiresOnce();
        VerifyBeforeSowDestroyedPlot();
    }

    public static void RunEec01FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_EEC01_CROP_FAILURE_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_EEC01_CROP_FAILURE_REPORT_PATH is required.");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "EEC01 crop-failure report must be a create-only path under Artifacts/QA.");
        }

        RunOrThrow();

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        using FileStream report = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(report);
        writer.Write(
            "U0_EEC01_CROP_FAILURE=PASS\n"
            + "runtimeEntry=CropPlotRuntime crop plan lifecycle\n"
            + "plans=explicit,automatic\n"
            + "causes=natural-death,active-plot-destroyed,pre-sow-plot-destroyed\n"
            + "faults=prepare-rejection,delivery-fault,acknowledgement-fault\n"
            + "persistence=failed-owner,ledger-outbox,json-round-trip,retry-retire\n"
            + "exactlyOnce=one-acknowledged-failed-crop-terminal,replay-no-duplicate\n"
            + "destructionChild=not-applicable-direct-plot-state-cause\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_EEC01_CROP_FAILURE=PASS; report=" + fullPath);
    }

    private static void VerifyNaturalDeathPrepareRejectionAndDeliveryRestore()
    {
        CropFailureFaultingRecorder fault = null;
        using Fixture fixture = new(
            "run:crop-failure-death",
            recorderDecorator: inner => fault = new CropFailureFaultingRecorder(inner),
            outdoorTemperature: -100f);
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(fixture, runtime, "action:crop-failure-death");

        fault.RejectPrepare = true;
        fixture.Events.Publish(new OperatingDayEndedEvent(23));
        fixture.Events.Publish(new OperatingDayEndedEvent(24));
        Require(runtime.Capture().plots.Single().cycleExecutionReceipt.status
            == CropCycleExecutionReceiptStatus.Active,
            "Two lethal days prematurely ended the crop cycle.");
        fixture.Events.Publish(new OperatingDayEndedEvent(25));
        CropPlotSaveData rejected = runtime.Capture().plots.Single();
        Require(rejected.cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.FailedCropDeath
            && rejected.materialsConsumed
            && !rejected.failedOutcome.IsEmpty
            && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0
            && fixture.Ledger.CaptureGameplayOutcomes().outbox.Count == 0,
            "Prepare rejection changed the crop owner or published a result.");

        fault.RejectPrepare = false;
        fault.FaultDelivery = true;
        runtime.Tick();
        Require(runtime.Capture().plots.Single().cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.FailedCropDeath
            && fixture.Ledger.CaptureGameplayOutcomes().outbox.Count == 1,
            "Delivery fault did not retain one durable pending crop result.");

        RestoreFailureCheckpoint(fixture, runtime);
        Require(runtime.Capture().plots.Single().cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.FailedCropDeath,
            "Crop failure owner did not survive canonical save/restore.");
        fault.FaultDelivery = false;
        fault.RetryPendingDeliveries(10);
        runtime.Tick();
        AcknowledgeFailure(runtime, "action:crop-failure-death");
        Require(runtime.Capture().plots.Single().cycleExecutionReceipt.IsEmpty,
            "Acknowledged natural death did not retire its pending owner.");
        RequireOneFailedCropOutcome(fixture);
        runtime.Tick();
        RequireOneFailedCropOutcome(fixture);
    }

    private static void VerifyAutomaticNaturalDeathAcknowledgementRestore()
    {
        CropFailureFaultingRecorder fault = null;
        using Fixture fixture = new(
            "run:crop-failure-auto-death",
            recorderDecorator: inner => fault = new CropFailureFaultingRecorder(inner),
            outdoorTemperature: -100f);
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(fixture, runtime, correlationId: null);

        CropPlotSaveData active = runtime.Capture().plots.Single();
        Require(active.cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.Active
            && !active.cycleExecutionReceipt.explicitCorrelation
            && active.cycleExecutionReceipt.correlationId.StartsWith(
                "crop-cycle:", StringComparison.Ordinal),
            "Automatic sow did not create a non-explicit crop plan owner.");

        fault.FaultAcknowledgement = true;
        fixture.Events.Publish(new OperatingDayEndedEvent(23));
        fixture.Events.Publish(new OperatingDayEndedEvent(24));
        fixture.Events.Publish(new OperatingDayEndedEvent(25));
        CropPlotSaveData blocked = runtime.Capture().plots.Single();
        Require(blocked.cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.FailedCropDeath
            && !blocked.cycleExecutionReceipt.explicitCorrelation
            && !blocked.failedOutcome.IsEmpty,
            "Acknowledgement failure did not retain the automatic crop failure owner.");
        RequireOneAwaitingAcknowledgementCropOutcome(fixture);

        RestoreFailureCheckpoint(fixture, runtime);
        runtime.Tick();
        Require(runtime.Capture().plots.Single().cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.FailedCropDeath,
            "Automatic crop failure did not remain pending after restore.");
        RequireOneAwaitingAcknowledgementCropOutcome(fixture);

        fault.FaultAcknowledgement = false;
        Require(fault.RetryPendingDeliveries(10) == 1,
            "Automatic crop failure acknowledgement retry did not settle exactly once.");
        runtime.Tick();
        CropPlotSaveData settled = runtime.Capture().plots.Single();
        Require(settled.cycleExecutionReceipt.IsEmpty
            && settled.failedOutcome.IsEmpty,
            "Acknowledged automatic crop failure did not retire its pending owner.");
        RequireOneFailedCropOutcome(fixture);
        runtime.Tick();
        RequireOneFailedCropOutcome(fixture);
    }

    private static void VerifyDestroyedPlotRestoreAndReplay()
    {
        CropFailureFaultingRecorder fault = null;
        using Fixture fixture = new(
            "run:crop-failure-destruction",
            recorderDecorator: inner => fault = new CropFailureFaultingRecorder(inner));
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(
            fixture, runtime, "action:crop-failure-destruction");

        fault.FaultDelivery = true;
        fixture.Plot.isDestroy = true;
        runtime.Tick();
        Require(runtime.Capture().plots.Count == 1
            && runtime.Capture().plots.Single().cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.FailedPlotDestroyed
            && fixture.Ledger.CaptureGameplayOutcomes().outbox.Count == 1,
            "A destroyed plot was deleted before terminal acknowledgement.");

        RestoreFailureCheckpoint(fixture, runtime);
        Require(runtime.Capture().plots.Count == 1,
            "Destroyed plot pending owner did not survive restore.");
        fault.FaultDelivery = false;
        fault.RetryPendingDeliveries(10);
        runtime.Tick();
        Require(runtime.Capture().plots.Count == 1,
            "Explicit destroyed-plot receipt was removed without consumer acknowledgement.");
        AcknowledgeFailure(runtime, "action:crop-failure-destruction");
        runtime.Tick();
        Require(runtime.Capture().plots.Count == 0,
            "Acknowledged destroyed plot was not retired.");
        RequireOneFailedCropOutcome(fixture);
        runtime.Tick();
        RequireOneFailedCropOutcome(fixture);
    }

    private static void VerifyAutomaticDestroyedPlotRetiresOnce()
    {
        using Fixture fixture = new("run:crop-failure-auto-destruction");
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(fixture, runtime, correlationId: null);
        Require(!runtime.Capture().plots.Single().cycleExecutionReceipt
                .explicitCorrelation,
            "Automatic destroyed-plot setup unexpectedly used an explicit plan.");

        fixture.Plot.isDestroy = true;
        runtime.Tick();
        Require(runtime.Capture().plots.Count == 0,
            "Acknowledged automatic destroyed-plot failure did not retire its owner.");
        RequireOneFailedCropOutcome(fixture);
        runtime.Tick();
        Require(runtime.Capture().plots.Count == 0,
            "Automatic destroyed plot reappeared during terminal replay.");
        RequireOneFailedCropOutcome(fixture);
    }

    private static void VerifyBeforeSowDestroyedPlot()
    {
        using Fixture fixture = new("run:crop-failure-before-sow");
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        string plotId = fixture.Plot.RequirePersistentInstanceId().Value;
        Require(runtime.TrySetCrop(
                fixture.Plot, fixture.Crop.CropId, out string cropFailure),
            "Pre-sow crop selection failed: " + cropFailure);
        Require(runtime.TryBindNextCycle(
                "action:crop-failure-before-sow",
                plotId,
                fixture.Crop.CropId,
                out string bindFailure),
            "Pre-sow cycle binding failed: " + bindFailure);
        fixture.Plot.isDestroy = true;
        runtime.Tick();
        Require(runtime.Capture().plots.Count == 1,
            "Explicit pre-sow failure was discarded before receipt acknowledgement.");
        RequireOneFailedCropOutcome(fixture);
        RestoreFailureCheckpoint(fixture, runtime);
        Require(runtime.Capture().plots.Count == 1,
            "Pre-sow failed receipt did not survive restore.");
        AcknowledgeFailure(runtime, "action:crop-failure-before-sow");
        runtime.Tick();
        Require(runtime.Capture().plots.Count == 0,
            "Acknowledged pre-sow destroyed plot was not retired.");
        RequireOneFailedCropOutcome(fixture);
    }

    private static void PrepareFailureGrowingCrop(
        Fixture fixture,
        CropPlotRuntime runtime,
        string correlationId)
    {
        string plotId = fixture.Plot.RequirePersistentInstanceId().Value;
        Require(runtime.TrySetCrop(
                fixture.Plot, fixture.Crop.CropId, out string cropFailure),
            "Crop setup failed: " + cropFailure);
        if (!string.IsNullOrEmpty(correlationId))
        {
            Require(runtime.TryBindNextCycle(
                    correlationId,
                    plotId,
                    fixture.Crop.CropId,
                    out string bindFailure),
                "Crop cycle binding failed: " + bindFailure);
        }
        runtime.Tick();
        CropPlotSnapshot waiting = runtime.Plots.Single();
        foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials)
        {
            if (string.Equals(
                    material.Key,
                    fixture.Crop.SeedItemId,
                    StringComparison.Ordinal))
            {
                for (int index = 0; index < material.Value; index++)
                    fixture.AddSeed(
                        WorldItemStackState.FacilityBuffer,
                        waiting.MaterialDestinationId,
                        fixture.Plot.centerPos,
                        pathogenLoad: 0f);
            }
            else
            {
                WorldItemRepositoryEditorAccess.AddStack(
                    fixture.Repository,
                    material.Key,
                    material.Value,
                    WorldItemStackState.FacilityBuffer,
                    destinationId: waiting.MaterialDestinationId,
                    position: fixture.Plot.centerPos);
            }
        }
        runtime.Tick();
        Require(runtime.TryGetWork(
                fixture.Plot,
                BuiltInWorkTypeIds.Sow,
                out CropPlotWorkSnapshot sow)
            && sow.Available
            && runtime.ApplyWork(
                fixture.Plot,
                BuiltInWorkTypeIds.Sow,
                sow.RequiredWork,
                out bool sowed)
            && sowed,
            "Crop setup did not reach the production growing entry.");
    }

    private static void RestoreFailureCheckpoint(
        Fixture fixture,
        CropPlotRuntime runtime)
    {
        DungeonCropPlotSaveData crops = JsonUtility.FromJson<DungeonCropPlotSaveData>(
            JsonUtility.ToJson(runtime.Capture()));
        CropEcologyWorldSaveData ecology = JsonUtility.FromJson<CropEcologyWorldSaveData>(
            JsonUtility.ToJson(fixture.Ecology.Capture()));
        DungeonPhysicalItemSaveData physical =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(fixture.WorldItems.Capture()));
        GameplayOutcomeLedgerSaveData outcomes = fixture.Ledger.CaptureGameplayOutcomes();
        fixture.Ledger.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
            fixture.Ledger.PrepareGameplayOutcomeRestore(outcomes);
        fixture.Ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
        fixture.Ledger.PublishRestoreCandidate();
        fixture.Ledger.CompleteRestoreCandidate();
        fixture.WorldItems.Restore(physical);
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(ecology));
        runtime.Restore(runtime.BuildRestore(crops));
    }

    private static void RequireOneFailedCropOutcome(Fixture fixture)
    {
        GameplayOutcomeSnapshot[] exact = fixture.Ledger
            .CaptureGameplayOutcomes().exactOutcomes
            .Where(value => value != null
                && value.producerId == EnvironmentOutcomeIds.CropPlanProducer
                && value.outcomeTypeId == EnvironmentOutcomeIds.CropPlanTerminal.Value)
            .ToArray();
        Require(exact.Length == 1
            && exact[0].status == GameplayOutcomeStatus.Failed
            && exact[0].lifecycle
                == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
            && fixture.Ledger.CaptureGameplayOutcomes().outbox.Count == 0,
            "Crop failure did not retain exactly one acknowledged typed result.");
    }

    private static void RequireOneAwaitingAcknowledgementCropOutcome(
        Fixture fixture)
    {
        GameplayOutcomeLedgerSaveData saved = fixture.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeSnapshot[] exact = saved.exactOutcomes
            .Where(value => value != null
                && value.producerId == EnvironmentOutcomeIds.CropPlanProducer
                && value.outcomeTypeId == EnvironmentOutcomeIds.CropPlanTerminal.Value)
            .ToArray();
        Require(saved.outbox.Count == 0
            && exact.Length == 1
            && exact[0].status == GameplayOutcomeStatus.Failed
            && exact[0].lifecycle
                == GameplayOutcomeRecordLifecycle.PublishedAwaitingAcknowledgement,
            "Crop failure did not retain exactly one published result awaiting acknowledgement.");
    }

    private static void AcknowledgeFailure(
        CropPlotRuntime runtime,
        string correlationId)
    {
        Require(runtime.TryCaptureExecutionReceipt(
                correlationId,
                out CropPlanExecutionReceipt receipt)
            && !receipt.Succeeded,
            "Explicit crop failure receipt was not retained.");
        bool acknowledged = runtime.TryAcknowledgeExecutionReceipt(
            correlationId,
            receipt.RuntimeReceiptDigest,
            out string failureReason);
        Require(acknowledged,
            "Explicit crop failure could not be acknowledged: "
            + failureReason);
    }

    private sealed class CropFailureFaultingRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;
        internal CropFailureFaultingRecorder(IGameplayOutcomeRecorder inner) =>
            this.inner = inner;
        internal bool RejectPrepare { get; set; }
        internal bool FaultDelivery { get; set; }
        internal bool FaultAcknowledgement { get; set; }
        public OutcomePrepareResult TryPrepare<TReceipt>(
            in TReceipt receipt,
            out PreparedOutcomeToken prepared)
        {
            if (RejectPrepare)
            {
                prepared = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "injected-crop-prepare-rejection");
            }
            return inner.TryPrepare(receipt, out prepared);
        }
        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation)
        {
            if (RejectPrepare)
            {
                reservation = default;
                return new OutcomePrepareResult(OutcomePrepareCode.CapacityDeferred,
                    "injected-crop-reservation-rejection");
            }
            return inner.TryReserve(requirements, out reservation);
        }
        public OutcomePrepareResult TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared) =>
            inner.TryWriteReserved(receipt, reservation, out prepared);
        public void CancelReservation(in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);
        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            inner.CancelPrepared(prepared);
        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed) =>
            inner.CommitPrepared(prepared, expectedOwnerRevision, out committed);
        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] expectedOwnerRevisions,
            CommittedOutcomeToken[] committed) =>
            inner.CommitPreparedBatch(prepared, expectedOwnerRevisions, committed);
        public OutcomeDeliveryResult TryDeliver(in CommittedOutcomeToken committed) =>
            FaultDelivery
                ? new OutcomeDeliveryResult(
                    OutcomeDeliveryCode.PendingDeliveryFault,
                    committed.OutcomeId,
                    "injected-crop-delivery-fault")
                : inner.TryDeliver(committed);
        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            FaultAcknowledgement
                ? new OutcomeAcknowledgeResult(OutcomeAcknowledgeCode.NotPublished)
                : inner.Acknowledge(committed);
        public int RetryPendingDeliveries(int maximumCount) =>
            FaultDelivery || FaultAcknowledgement
                ? 0
                : inner.RetryPendingDeliveries(maximumCount);
    }
}
#endif
