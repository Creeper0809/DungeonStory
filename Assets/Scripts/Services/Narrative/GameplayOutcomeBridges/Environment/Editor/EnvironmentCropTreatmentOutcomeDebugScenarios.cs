#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    private static JArray e03BatchObservations;

    private static void ObserveE03Treatment(
        string stage,
        Fixture fixture,
        CropPlotRuntime runtime,
        string workerId,
        string itemId,
        string operationId)
    {
        if (e03BatchObservations == null)
            return;

        DungeonCropPlotSaveData crops = runtime.Capture();
        CropPlotSaveData plot = crops.plots.Single(value => string.Equals(
            value.buildingInstanceId,
            fixture.Plot.RequirePersistentInstanceId().Value,
            StringComparison.Ordinal));
        e03BatchObservations.Add(new JObject
        {
            ["stage"] = stage,
            ["workerId"] = workerId,
            ["plotId"] = plot.buildingInstanceId,
            ["itemId"] = itemId,
            ["operationId"] = operationId,
            ["phaseName"] = plot.treatment.phase.ToString(),
            ["itemStock"] = fixture.CountItem(itemId),
            ["cropPlots"] = JObject.Parse(JsonUtility.ToJson(crops)),
            ["cropEcology"] = JObject.Parse(JsonUtility.ToJson(fixture.Ecology.Capture())),
            ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(fixture.WorldItems.Capture())),
            ["ledger"] = JObject.Parse(JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()))
        });
    }

    private static string[] MissingE03ObservationStages()
    {
        string[] stages = e03BatchObservations.OfType<JObject>()
            .Select(value => (string)value["stage"])
            .ToArray();
        string[] normal = { "PestLure", "BotanicalPesticide", "Fungicide" };
        string[] required = normal.SelectMany(kind => new[]
            {
                "normal-ready:" + kind,
                "normal-partial:" + kind,
                "normal-completed:" + kind,
                "recovery-ready:" + kind,
                "recovered-terminal:" + kind,
                "recovered-replay:" + kind
            })
            .Concat(new[]
            {
                "reserve-rejected",
                "input-committed",
                "input-committed-restored",
                "ecology-applied-before-parent",
                "ecology-applied-restored",
                "cancel-before-input"
            }).ToArray();
        return required.Where(stage => stages.Count(value => string.Equals(
                value, stage, StringComparison.Ordinal)) != 1)
            .ToArray();
    }

    private static void VerifyCropTreatmentOutcomes(
        Fixture fixture,
        CropPlotRuntime runtime,
        BuildableObject plot,
        string plotId)
    {
        using WorkerFixture worker = new(
            "character:e03-crop-worker",
            "E03 작물 관리인",
            6301);
        SetTreatmentPressures(fixture, plotId, 80f, 50f);
        (string ItemId, CropTreatmentKind Kind)[] cases =
        {
            ("supply:pest-lure", CropTreatmentKind.PestLure),
            ("supply:botanical-pesticide", CropTreatmentKind.BotanicalPesticide),
            ("supply:fungicide", CropTreatmentKind.Fungicide)
        };

        int initialSequence = runtime.Capture().plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal)).nextTreatmentOperationSequence;
        int publishedBefore = CountTreatmentOutcomes(fixture);
        foreach ((string itemId, CropTreatmentKind kind) in cases)
        {
            Require(fixture.Economy.TryGetItem(
                    itemId,
                    out ResourceItemDefinitionSO definition),
                "Authored crop-treatment item is unavailable: " + itemId);
            Require(definition.TryGetCropTreatment(out CropTreatmentPolicy policy)
                    && policy.Kind == kind,
                "Authored crop-treatment policy is invalid: " + itemId);
            int stockBefore = fixture.CountItem(itemId);
            CropEcologyPlotSaveData ecologyBefore = RequireEcology(
                fixture,
                plotId);
            float pestBefore = ecologyBefore.pestPressure;
            float diseaseBefore = ecologyBefore.diseasePressure;
            Require(runtime.TryScheduleTreatment(
                    plot,
                    itemId,
                    out string scheduleFailure),
                "Crop treatment scheduling failed: " + scheduleFailure);
            CropPlotSaveData scheduled = runtime.Capture().plots.Single(value =>
                string.Equals(
                    value.buildingInstanceId,
                    plotId,
                    StringComparison.Ordinal));
            string operationId = scheduled.treatment.operationId;
            WorldItemRepositoryEditorAccess.AddStack(
                fixture.Repository,
                itemId,
                policy.QuantityPerApplication,
                WorldItemStackState.FacilityBuffer,
                destinationId: scheduled.treatment.destinationId,
                position: plot.centerPos);
            runtime.Tick();
            Require(runtime.TryGetWork(
                    plot,
                    BuiltInWorkTypeIds.Treat,
                    out CropPlotWorkSnapshot work)
                && work.Available,
                "Crop treatment work was unavailable: "
                + work.UnavailableReason);
            ObserveE03Treatment("normal-ready:" + kind, fixture, runtime,
                worker.Actor.Identity.PersistentId, itemId, operationId);
            float first = work.RequiredWork * 0.4f;
            Require(runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Treat,
                    first,
                    worker.Actor,
                    out bool partialComplete)
                && !partialComplete,
                "Partial crop-treatment work unexpectedly completed.");
            CropTreatmentOrderSaveData partial = runtime.Capture().plots
                .Single(value => string.Equals(
                    value.buildingInstanceId,
                    plotId,
                    StringComparison.Ordinal)).treatment;
            Require(partial.workContributions.Count == 1
                    && string.Equals(
                        partial.workContributions[0].characterId,
                        worker.Actor.Identity.PersistentId,
                        StringComparison.Ordinal)
                    && Mathf.Approximately(
                        partial.workContributions[0].contributedWork,
                        first),
                "Partial crop-treatment contribution was not saved exactly.");
            ObserveE03Treatment("normal-partial:" + kind, fixture, runtime,
                worker.Actor.Identity.PersistentId, itemId, operationId);
            bool finalApplied = runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Treat,
                    work.RequiredWork - first,
                    worker.Actor,
                    out bool completed);
            if (!finalApplied || !completed)
            {
                CropTreatmentOrderSaveData failed = runtime.Capture().plots
                    .Single(value => string.Equals(
                        value.buildingInstanceId,
                        plotId,
                        StringComparison.Ordinal)).treatment;
                GameplayOutcomeOutboxSnapshot pendingOutcome = fixture.Ledger
                    .GetOutboxSnapshot().FirstOrDefault(value =>
                        string.Equals(
                            value.ResultKey.OperationId.Value,
                            operationId,
                            StringComparison.Ordinal));
                string outbox = string.Join(" | ", fixture.Ledger
                    .GetOutboxSnapshot()
                    .Select(value => value.ResultKey + ":"
                        + value.Lifecycle + ":"
                        + value.LastFaultCode));
                throw new InvalidOperationException(
                    "Crop treatment did not reach its terminal ledger boundary: "
                    + $"phase={failed.phase}; failure={failed.failureReason}; "
                    + $"worker={failed.responsibleWorkerId}; "
                    + $"before={failed.ecologyBeforeFingerprint}; "
                    + $"after={failed.ecologyAfterFingerprint}; "
                    + $"ledger={pendingOutcome.Lifecycle}; "
                    + $"faults={pendingOutcome.DeliveryFaultCount}; "
                    + $"fault={pendingOutcome.LastFaultCode}; "
                    + $"outbox=[{outbox}].");
            }

            CropPlotSaveData terminal = runtime.Capture().plots.Single(value =>
                string.Equals(
                    value.buildingInstanceId,
                    plotId,
                    StringComparison.Ordinal));
            CropEcologyPlotSaveData ecologyAfter = RequireEcology(
                fixture,
                plotId);
            float expectedPest = kind == CropTreatmentKind.Fungicide
                ? pestBefore
                : Mathf.Max(0f, pestBefore - policy.EffectAmount);
            float expectedDisease = kind == CropTreatmentKind.Fungicide
                ? Mathf.Max(0f, diseaseBefore - policy.EffectAmount)
                : diseaseBefore;
            Require(terminal.treatment.phase == CropTreatmentOrderPhase.None
                    && terminal.nextTreatmentOperationSequence
                        == initialSequence + publishedBefore + 1
                    && Mathf.Approximately(
                        ecologyAfter.pestPressure,
                        expectedPest)
                    && Mathf.Approximately(
                        ecologyAfter.diseasePressure,
                        expectedDisease)
                    && fixture.CountItem(itemId) == stockBefore,
                "Crop treatment did not consume/apply/retire exactly once.");
            publishedBefore++;
            GameplayOutcomeSnapshot outcome = fixture.Ledger
                .CaptureGameplayOutcomes().exactOutcomes.Single(value =>
                    value != null
                    && string.Equals(
                        value.producerId,
                        EnvironmentOutcomeIds.CropTreatmentProducer,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.operationId,
                        operationId,
                        StringComparison.Ordinal));
            VerifyTreatmentOutcome(
                outcome,
                worker.Actor.Identity.PersistentId,
                plotId,
                itemId,
                kind,
                pestBefore,
                expectedPest,
                diseaseBefore,
                expectedDisease,
                policy.CooldownDays);
            ObserveE03Treatment("normal-completed:" + kind, fixture, runtime,
                worker.Actor.Identity.PersistentId, itemId, operationId);
        }
    }

    private static void VerifyCropTreatmentRecoveryAndFailureBoundaries()
    {
        VerifyTreatmentReserveRejection();
        VerifyTreatmentInputCommittedRestore();
        VerifyTreatmentEcologyAppliedRestore();
        VerifyTreatmentCancellationDoesNotPublish();
    }

    private static void VerifyTreatmentReserveRejection()
    {
        CropTreatmentFaultingRecorder fault = null;
        using Fixture fixture = new(
            "run:e03-treatment-reserve-rejection",
            recorderDecorator: inner =>
                fault = new CropTreatmentFaultingRecorder(inner));
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(
            fixture,
            runtime,
            "action:e03-treatment-reserve-rejection");
        using WorkerFixture worker = new(
            "character:e03-reserve-worker",
            "예약 거절 관리인",
            6311);
        ArrangeReadyTreatment(
            fixture,
            runtime,
            worker.Actor,
            "supply:pest-lure",
            out CropTreatmentPolicy policy,
            out CropPlotWorkSnapshot work,
            out string operationId,
            out int stockBefore,
            out float pestBefore,
            out float diseaseBefore);

        fault.RejectReserve = true;
        runtime.ApplyWork(
            fixture.Plot,
            BuiltInWorkTypeIds.Treat,
            work.RequiredWork,
            worker.Actor,
            out _);
        CropTreatmentOrderSaveData rejected = runtime.Capture().plots
            .Single().treatment;
        Require(rejected.phase == CropTreatmentOrderPhase.Working
                && Mathf.Approximately(
                    rejected.completedWork,
                    rejected.requiredWork)
                && fixture.CountItem("supply:pest-lure")
                    == stockBefore + policy.QuantityPerApplication
                && Mathf.Approximately(
                    RequireEcology(fixture, fixture.Plot
                        .RequirePersistentInstanceId().Value).pestPressure,
                    pestBefore)
                && CountTreatmentOutcomes(fixture) == 0,
            "Reservation rejection crossed the crop-treatment physical or ecology boundary.");
        ObserveE03Treatment("reserve-rejected", fixture, runtime,
            worker.Actor.Identity.PersistentId, "supply:pest-lure", operationId);

        fault.RejectReserve = false;
        runtime.Tick();
        RequireRecoveredTreatment(
            fixture,
            runtime,
            worker.Actor.Identity.PersistentId,
            "supply:pest-lure",
            CropTreatmentKind.PestLure,
            operationId,
            stockBefore,
            pestBefore,
            pestBefore - policy.EffectAmount,
            diseaseBefore,
            diseaseBefore,
            policy.CooldownDays);
    }

    private static void VerifyTreatmentInputCommittedRestore()
    {
        FailOnceTreatmentTare tare = null;
        using Fixture fixture = new(
            "run:e03-treatment-input-restore",
            tareDecorator: inner =>
                tare = new FailOnceTreatmentTare(inner));
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(
            fixture,
            runtime,
            "action:e03-treatment-input-restore");
        using WorkerFixture worker = new(
            "character:e03-input-worker",
            "입력 복원 관리인",
            6312);
        ArrangeReadyTreatment(
            fixture,
            runtime,
            worker.Actor,
            "supply:botanical-pesticide",
            out CropTreatmentPolicy policy,
            out CropPlotWorkSnapshot work,
            out string operationId,
            out int stockBefore,
            out float pestBefore,
            out float diseaseBefore);

        tare.RejectNext = true;
        runtime.ApplyWork(
            fixture.Plot,
            BuiltInWorkTypeIds.Treat,
            work.RequiredWork,
            worker.Actor,
            out _);
        CropTreatmentOrderSaveData interrupted = runtime.Capture().plots
            .Single().treatment;
        Require(interrupted.phase == CropTreatmentOrderPhase.InputCommitted
                && fixture.CountItem("supply:botanical-pesticide")
                    == stockBefore
                && Mathf.Approximately(
                    RequireEcology(fixture, fixture.Plot
                        .RequirePersistentInstanceId().Value).pestPressure,
                    pestBefore)
                && CountTreatmentOutcomes(fixture) == 0,
            "InputCommitted interruption did not preserve its exact transaction boundary.");
        ObserveE03Treatment("input-committed", fixture, runtime,
            worker.Actor.Identity.PersistentId, "supply:botanical-pesticide", operationId);

        RestoreFailureCheckpoint(fixture, runtime);
        ObserveE03Treatment("input-committed-restored", fixture, runtime,
            worker.Actor.Identity.PersistentId, "supply:botanical-pesticide", operationId);
        runtime.Tick();
        RequireRecoveredTreatment(
            fixture,
            runtime,
            worker.Actor.Identity.PersistentId,
            "supply:botanical-pesticide",
            CropTreatmentKind.BotanicalPesticide,
            operationId,
            stockBefore,
            pestBefore,
            pestBefore - policy.EffectAmount,
            diseaseBefore,
            diseaseBefore,
            policy.CooldownDays);
        Require(tare.CallCount >= 2,
            "InputCommitted recovery did not retry the idempotent tare boundary.");
    }

    private static void VerifyTreatmentEcologyAppliedRestore(bool hiddenAcknowledgement = false)
    {
        CropTreatmentFaultingRecorder fault = null;
        using Fixture fixture = new(
            "run:e03-treatment-ecology-restore:" + hiddenAcknowledgement,
            recorderDecorator: inner =>
                fault = new CropTreatmentFaultingRecorder(inner));
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(
            fixture,
            runtime,
            "action:e03-treatment-ecology-restore");
        using WorkerFixture worker = new(
            "character:e03-ecology-worker",
            "생태 복원 관리인",
            6313);
        ArrangeReadyTreatment(
            fixture,
            runtime,
            worker.Actor,
            "supply:fungicide",
            out CropTreatmentPolicy policy,
            out CropPlotWorkSnapshot work,
            out string operationId,
            out int stockBefore,
            out float pestBefore,
            out float diseaseBefore);

        CropCommandProbe probe = new(fixture.domainFacts) { HideNextAcknowledgement = hiddenAcknowledgement };
        runtime.ConstructDomainFacts(probe);
        fault.RejectWrite = !hiddenAcknowledgement;
        runtime.ApplyWork(
            fixture.Plot,
            BuiltInWorkTypeIds.Treat,
            work.RequiredWork,
            worker.Actor,
            out _);
        CropTreatmentOrderSaveData interrupted = runtime.Capture().plots
            .Single().treatment;
        float expectedDisease = diseaseBefore - policy.EffectAmount;
        Require(interrupted.phase == CropTreatmentOrderPhase.OutcomePublished
                && fixture.CountItem("supply:fungicide") == stockBefore
                && Mathf.Approximately(
                    RequireEcology(fixture, fixture.Plot
                        .RequirePersistentInstanceId().Value).diseasePressure,
                    expectedDisease)
                && CountTreatmentOutcomes(fixture) == (hiddenAcknowledgement ? 1 : 0),
            "Ecology-applied interruption did not retain its exact pre-ledger owner.");
        VerifyTreatmentPhysicalConservation(fixture, interrupted);
        ObserveE03Treatment("ecology-applied-before-parent", fixture, runtime,
            worker.Actor.Identity.PersistentId, "supply:fungicide", operationId);

        string savedKey = interrupted.publicationKey;
        CropTreatmentFact fact = new(fixture.Plot.RequirePersistentInstanceId(), interrupted);
        CropTreatmentOrderSaveData mutable = fact.Snapshot();
        mutable.sourceStackIds.Clear();
        mutable.workContributions.Clear();
        mutable.itemDisplayName = "outside mutation";
        Require(fact.Snapshot().sourceStackIds.Count > 0 && fact.Snapshot().workContributions.Count > 0
            && fact.Snapshot().itemDisplayName == interrupted.itemDisplayName,
            "Treatment fact exposes mutable input, work or presentation state.");
        DungeonCropPlotSaveData invalid = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(runtime.Capture()));
        invalid.plots.Single().treatment.publicationKey = string.Empty;
        RequireThrows(() => runtime.BuildRestore(invalid), "Treatment pending owner accepted a missing publication key.");
        invalid.plots.Single().treatment.publicationKey = savedKey;
        invalid.plots.Single().treatment.outcomeSchemaVersion = 0;
        RequireThrows(() => runtime.BuildRestore(invalid), "Treatment accepted the removed no-observer legacy schema.");
        VerifyIrrigationRawSaveFields(runtime, runtime.Capture());

        RestoreFailureCheckpoint(fixture, runtime);
        DungeonCropPlotSaveData saved = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(runtime.Capture()));
        CropEcologyWorldSaveData ecologySaved = fixture.Ecology.Capture();
        using CropPlotRuntime resumed = fixture.CreateCropRuntime(allowReplacement: true);
        resumed.ConstructDomainFacts(probe);
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(ecologySaved));
        resumed.Restore(resumed.BuildRestore(saved));
        ObserveE03Treatment("ecology-applied-restored", fixture, resumed,
            worker.Actor.Identity.PersistentId, "supply:fungicide", operationId);
        fault.RejectWrite = false;
        resumed.Tick();
        GameplayOutcomeSnapshot recovered = RequireRecoveredTreatment(
            fixture,
            resumed,
            worker.Actor.Identity.PersistentId,
            "supply:fungicide",
            CropTreatmentKind.Fungicide,
            operationId,
            stockBefore,
            pestBefore,
            pestBefore,
            diseaseBefore,
            expectedDisease,
            policy.CooldownDays);
        VerifyTreatmentOutcomePhysicalJoin(recovered, interrupted);
        GameplayOutcomeReplayIdentity identity = fixture.RequirePublishedIdentity(
            EnvironmentOutcomeIds.CropTreatmentProducer, EnvironmentOutcomeIds.CropTreatmentApplied);
        Require(probe.LastReplayPublicationKey == savedKey && identity.ResultKey.ToString() == savedKey,
            "Treatment fresh-owner restore changed the stored observer key.");
        RequireReplayRejected(fixture.domainFacts, savedKey + ":wrong", fact, "treatment wrong key");
        CropTreatmentOrderSaveData changed = fact.Snapshot();
        changed.itemDisplayName += " changed";
        RequireReplayRejected(fixture.domainFacts, savedKey,
            new CropTreatmentFact(fixture.Plot.RequirePersistentInstanceId(), changed), "treatment changed payload");
    }

    private static void VerifyTreatmentCancellationDoesNotPublish()
    {
        using Fixture fixture = new("run:e03-treatment-cancel");
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        PrepareFailureGrowingCrop(
            fixture,
            runtime,
            "action:e03-treatment-cancel");
        string plotId = fixture.Plot.RequirePersistentInstanceId().Value;
        SetTreatmentPressures(fixture, plotId, 80f, 50f);
        int stockBefore = fixture.CountItem("supply:pest-lure");
        Require(runtime.TryScheduleTreatment(
                fixture.Plot,
                "supply:pest-lure",
                out string scheduleFailure),
            "Cancellation treatment scheduling failed: " + scheduleFailure);
        string operationId = runtime.Capture().plots.Single().treatment.operationId;
        Require(runtime.TryCancelTreatment(
                fixture.Plot,
                out string cancelMessage)
            && runtime.Capture().plots.Single().treatment.phase
                == CropTreatmentOrderPhase.None
            && fixture.CountItem("supply:pest-lure") == stockBefore
            && Mathf.Approximately(
                RequireEcology(fixture, plotId).pestPressure,
                80f)
            && CountTreatmentOutcomes(fixture) == 0,
            "Cancelled crop treatment published success or lost physical stock: "
            + cancelMessage);
        ObserveE03Treatment("cancel-before-input", fixture, runtime,
            string.Empty, "supply:pest-lure", operationId);
    }

    private static void ArrangeReadyTreatment(
        Fixture fixture,
        CropPlotRuntime runtime,
        CharacterActor worker,
        string itemId,
        out CropTreatmentPolicy policy,
        out CropPlotWorkSnapshot work,
        out string operationId,
        out int stockBefore,
        out float pestBefore,
        out float diseaseBefore)
    {
        string plotId = fixture.Plot.RequirePersistentInstanceId().Value;
        SetTreatmentPressures(fixture, plotId, 80f, 50f);
        CropEcologyPlotSaveData ecology = RequireEcology(fixture, plotId);
        pestBefore = ecology.pestPressure;
        diseaseBefore = ecology.diseasePressure;
        policy = default;
        Require(fixture.Economy.TryGetItem(
                itemId,
                out ResourceItemDefinitionSO item),
            "Recovery treatment item is missing: " + itemId);
        Require(item.TryGetCropTreatment(out policy),
            "Recovery treatment policy is missing: " + itemId);
        stockBefore = fixture.CountItem(itemId);
        Require(runtime.TryScheduleTreatment(
                fixture.Plot,
                itemId,
                out string scheduleFailure),
            "Recovery treatment scheduling failed: " + scheduleFailure);
        CropTreatmentOrderSaveData scheduled = runtime.Capture().plots
            .Single().treatment;
        operationId = scheduled.operationId;
        WorldItemRepositoryEditorAccess.AddStack(
            fixture.Repository,
            itemId,
            policy.QuantityPerApplication,
            WorldItemStackState.FacilityBuffer,
            destinationId: scheduled.destinationId,
            position: fixture.Plot.centerPos);
        runtime.Tick();
        Require(runtime.TryGetWork(
                fixture.Plot,
                BuiltInWorkTypeIds.Treat,
                out work)
            && work.Available
            && worker != null,
            "Recovery treatment work did not become available.");
        ObserveE03Treatment("recovery-ready:" + policy.Kind, fixture, runtime,
            worker.Identity.PersistentId, itemId, operationId);
    }

    private static GameplayOutcomeSnapshot RequireRecoveredTreatment(
        Fixture fixture,
        CropPlotRuntime runtime,
        string workerId,
        string itemId,
        CropTreatmentKind kind,
        string operationId,
        int stockBefore,
        float pestBefore,
        float pestAfter,
        float diseaseBefore,
        float diseaseAfter,
        int cooldownDays)
    {
        string plotId = fixture.Plot.RequirePersistentInstanceId().Value;
        CropEcologyPlotSaveData ecology = RequireEcology(fixture, plotId);
        Require(runtime.Capture().plots.Single().treatment.phase
                    == CropTreatmentOrderPhase.None
                && fixture.CountItem(itemId) == stockBefore
                && Mathf.Approximately(ecology.pestPressure, pestAfter)
                && Mathf.Approximately(ecology.diseasePressure, diseaseAfter)
                && CountTreatmentOutcomes(fixture) == 1,
            "Recovered crop treatment did not settle exactly once.");
        GameplayOutcomeSnapshot outcome = fixture.Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Single(value =>
                value != null
                && string.Equals(
                    value.producerId,
                    EnvironmentOutcomeIds.CropTreatmentProducer,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.operationId,
                    operationId,
                    StringComparison.Ordinal));
        VerifyTreatmentOutcome(
            outcome,
            workerId,
            plotId,
            itemId,
            kind,
            pestBefore,
            pestAfter,
            diseaseBefore,
            diseaseAfter,
            cooldownDays);
        ObserveE03Treatment("recovered-terminal:" + kind, fixture, runtime,
            workerId, itemId, operationId);
        runtime.Tick();
        Require(CountTreatmentOutcomes(fixture) == 1
                && fixture.CountItem(itemId) == stockBefore
                && Mathf.Approximately(
                    RequireEcology(fixture, plotId).pestPressure,
                    pestAfter)
                && Mathf.Approximately(
                    RequireEcology(fixture, plotId).diseasePressure,
                    diseaseAfter),
            "Recovered crop treatment replayed a physical or ecology effect.");
        ObserveE03Treatment("recovered-replay:" + kind, fixture, runtime,
            workerId, itemId, operationId);
        return outcome;
    }

    private static void VerifyTreatmentPhysicalConservation(
        Fixture fixture,
        CropTreatmentOrderSaveData owner)
    {
        Require(fixture.TryGetPendingTreatmentReceipt(
                owner.operationId,
                out PhysicalItemBatchDispositionReceipt physicalReceipt),
            "Crop-treatment physical receipt is not pending at the durable outcome boundary.");
        Require(CropTreatmentPhysicalOutbox.ValidateReceipt(
                owner,
                physicalReceipt,
                out string receiptFailure),
            "Crop-treatment physical receipt does not match its owner: " + receiptFailure);
        bool isPackagedLot = fixture.TryGetPackagedLot(
            owner.itemId,
            out PackagedLotDefinitionSnapshot packagedLot);
        Require(fixture.EnsureTreatmentTareReceipt(
                owner.itemId,
                owner.quantity,
                fixture.Plot.centerPos,
                owner.commitId,
                out PackagedLotTareOutputReceipt tareReceipt,
                out string tareFailure),
            "Crop-treatment tare receipt could not be read idempotently: " + tareFailure);

        string[] ownerTareCommits = (owner.tareOutputCommitIds
                ?? new List<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] receiptTareCommits = (tareReceipt.OutputCommitIds
                ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        long unitMass = fixture.GetDefinitionUnitMass(owner.itemId).Value;
        long expectedInputMass = checked(unitMass * owner.quantity);
        long expectedTareMass = isPackagedLot
            ? checked(packagedLot.TareMass.Value * owner.quantity)
            : 0L;
        long expectedContentMass = checked(expectedInputMass - expectedTareMass);

        Require(string.Equals(
                    physicalReceipt.CommitId,
                    owner.commitId,
                    StringComparison.Ordinal)
                && string.Equals(
                    tareReceipt.ParentCommitId,
                    owner.commitId,
                    StringComparison.Ordinal)
                && owner.inputMassGrams == expectedInputMass
                && owner.inputMassGrams > 0L
                && (!isPackagedLot
                    || packagedLot.TotalUnitMass.Value == unitMass)
                && tareReceipt.AccountedTareMassGrams == expectedTareMass
                && owner.inputMassGrams - tareReceipt.AccountedTareMassGrams
                    == expectedContentMass
                && expectedContentMass > 0L
                && owner.tareOutputQuantity == tareReceipt.OutputQuantity
                && owner.tareOutputMassGrams == tareReceipt.OutputMassGrams
                && owner.destroyedTareMassGrams
                    == tareReceipt.DestroyedTareMassGrams
                && ownerTareCommits.SequenceEqual(
                    receiptTareCommits,
                    StringComparer.Ordinal),
            "Crop-treatment input/content/tare conservation or commit join drifted.");

        if (!isPackagedLot)
        {
            Require(receiptTareCommits.Length == 0
                    && tareReceipt.OutputQuantity == 0
                    && tareReceipt.OutputMassGrams == 0L
                    && tareReceipt.DestroyedTareMassGrams == 0L,
                "Unpackaged crop treatment produced non-zero tare evidence.");
            return;
        }

        switch (packagedLot.TareDisposition)
        {
            case PackageTareDisposition.ReusableContainerReturn:
            case PackageTareDisposition.DisposableWasteByproduct:
            {
                string outputItemId = packagedLot.ContainerItemId.Value;
                string expectedCommitId = owner.commitId + ":tare:" + outputItemId;
                WorldItemStackSnapshot[] outputs = fixture.WorldItems.GetAllStacks()
                    .Where(value => value != null
                        && string.Equals(
                            value.ItemId,
                            outputItemId,
                            StringComparison.Ordinal)
                        && ProductionOutputCommitComponentCodec.Matches(
                            value.Components,
                            expectedCommitId))
                    .ToArray();
                Require(receiptTareCommits.SequenceEqual(
                            new[] { expectedCommitId },
                            StringComparer.Ordinal)
                        && tareReceipt.OutputQuantity == owner.quantity
                        && tareReceipt.OutputMassGrams == expectedTareMass
                        && tareReceipt.DestroyedTareMassGrams == 0L
                        && outputs.Length == 1
                        && outputs[0].Quantity == owner.quantity
                        && outputs[0].State == WorldItemStackState.Loose
                        && outputs[0].Position == fixture.Plot.centerPos,
                    "Crop-treatment returned tare is not joined to one exact physical output.");
                break;
            }
            case PackageTareDisposition.DestroyedDuringUse:
                Require(receiptTareCommits.Length == 0
                        && tareReceipt.OutputQuantity == 0
                        && tareReceipt.OutputMassGrams == 0L
                        && tareReceipt.DestroyedTareMassGrams == expectedTareMass,
                    "Crop-treatment destroyed tare mass is not exact.");
                break;
            default:
                Require(false,
                    "Crop-treatment terminal sink used an unsupported tare disposition: "
                    + packagedLot.TareDisposition);
                break;
        }
    }

    private static void VerifyTreatmentOutcomePhysicalJoin(
        GameplayOutcomeSnapshot outcome,
        CropTreatmentOrderSaveData owner)
    {
        RequireMetric(outcome, EnvironmentOutcomeIds.InputQuantityMetric, owner.quantity);
        RequireMetric(outcome, EnvironmentOutcomeIds.InputMassMetric, owner.inputMassGrams);
        RequireMetric(
            outcome,
            EnvironmentOutcomeIds.OutputQuantityMetric,
            owner.tareOutputQuantity);
        RequireMetric(
            outcome,
            EnvironmentOutcomeIds.OutputMassMetric,
            owner.tareOutputMassGrams);
        RequireFact(outcome, EnvironmentOutcomeIds.CommitFact, owner.commitId);
        string expectedTareFact = owner.tareOutputCommitIds != null
            && owner.tareOutputCommitIds.Count > 0
                ? string.Join(",", owner.tareOutputCommitIds)
                : "none";
        RequireFact(
            outcome,
            EnvironmentOutcomeIds.TareCommitFact,
            expectedTareFact);
    }

    private static void VerifyTreatmentOutcome(
        GameplayOutcomeSnapshot outcome,
        string workerId,
        string plotId,
        string itemId,
        CropTreatmentKind kind,
        float pestBefore,
        float pestAfter,
        float diseaseBefore,
        float diseaseAfter,
        int cooldownDays)
    {
        Require(outcome.lifecycle
                    == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
                && outcome.status == GameplayOutcomeStatus.Succeeded
                && outcome.participants.Count == 3
                && outcome.metrics.Count == 12
                && outcome.subjects.Count == 3
                && outcome.tags.SequenceEqual(new[]
                {
                    EnvironmentOutcomeIds.AgricultureTag.Value
                })
                && outcome.provenance.Count == 1
                && outcome.facts.Count == 11,
            "Crop-treatment result shape is not exact.");
        Require(outcome.participants.Any(value =>
                    string.Equals(value.roleId, EnvironmentOutcomeIds.ActorRole.Value, StringComparison.Ordinal)
                    && string.Equals(value.entityId, workerId, StringComparison.Ordinal))
                && outcome.participants.Any(value =>
                    string.Equals(value.roleId, EnvironmentOutcomeIds.FacilityRole.Value, StringComparison.Ordinal)
                    && string.Equals(value.entityId, plotId, StringComparison.Ordinal))
                && outcome.participants.Any(value =>
                    string.Equals(value.roleId, EnvironmentOutcomeIds.ItemRole.Value, StringComparison.Ordinal)
                    && string.Equals(value.entityId, itemId, StringComparison.Ordinal)),
            "Crop-treatment result lost worker, plot or item identity.");
        RequireMetric(outcome, EnvironmentOutcomeIds.InputQuantityMetric, 1d);
        Require(FindMetric(outcome, EnvironmentOutcomeIds.InputMassMetric) > 0d,
            "Crop-treatment input mass was not positive.");
        RequireMetric(outcome, EnvironmentOutcomeIds.PestPressureBeforeMetric, pestBefore);
        RequireMetric(outcome, EnvironmentOutcomeIds.PestPressureAfterMetric, pestAfter);
        RequireMetric(outcome, EnvironmentOutcomeIds.DiseasePressureBeforeMetric, diseaseBefore);
        RequireMetric(outcome, EnvironmentOutcomeIds.DiseasePressureAfterMetric, diseaseAfter);
        double cooldownBefore = FindMetric(
            outcome,
            EnvironmentOutcomeIds.CooldownBeforeDayMetric);
        double cooldownAfter = FindMetric(
            outcome,
            EnvironmentOutcomeIds.CooldownAfterDayMetric);
        Require(cooldownBefore <= outcome.absoluteDay
                && Math.Abs(
                    cooldownAfter - outcome.absoluteDay - cooldownDays)
                    < 0.001d,
            "Crop-treatment cooldown boundary drifted from authored policy.");
        RequireFact(outcome, EnvironmentOutcomeIds.TreatmentKindFact, kind.ToString());
        RequireFact(outcome, EnvironmentOutcomeIds.ItemDefinitionFact, itemId);
        string contributors = FindFact(
            outcome,
            EnvironmentOutcomeIds.TreatmentContributorsFact);
        Require(contributors.StartsWith(workerId + "=", StringComparison.Ordinal),
            "Crop-treatment contributor fact is missing the actual worker.");
        Require(FindFact(outcome, EnvironmentOutcomeIds.CommitFact).Length > 0
                && FindFact(outcome, EnvironmentOutcomeIds.EcologyBeforeFact).Length > 0
                && FindFact(outcome, EnvironmentOutcomeIds.EcologyAfterFact).Length > 0
                && !string.Equals(
                    FindFact(outcome, EnvironmentOutcomeIds.EcologyBeforeFact),
                    FindFact(outcome, EnvironmentOutcomeIds.EcologyAfterFact),
                    StringComparison.Ordinal),
            "Crop-treatment physical/ecology provenance is incomplete.");
    }

    private static int CountTreatmentOutcomes(Fixture fixture) =>
        fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
            value != null
            && string.Equals(
                value.producerId,
                EnvironmentOutcomeIds.CropTreatmentProducer,
                StringComparison.Ordinal)
            && string.Equals(
                value.outcomeTypeId,
                EnvironmentOutcomeIds.CropTreatmentApplied.Value,
                StringComparison.Ordinal));

    private static void SetTreatmentPressures(
        Fixture fixture,
        string plotId,
        float pest,
        float disease)
    {
        CropEcologyWorldSaveData save = fixture.Ecology.Capture();
        CropEcologyPlotSaveData plot = save.plots.Single(value => value != null
            && string.Equals(value.plotId, plotId, StringComparison.Ordinal));
        plot.pestPressure = pest;
        plot.diseasePressure = disease;
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(save));
    }

    private static CropEcologyPlotSaveData RequireEcology(
        Fixture fixture,
        string plotId) => fixture.Ecology.Plots.Single(value => value != null
            && string.Equals(value.plotId, plotId, StringComparison.Ordinal));

    private static double FindMetric(
        GameplayOutcomeSnapshot outcome,
        GameplayMetricId id) => outcome.metrics.Single(value => string.Equals(
        value.metricId,
        id.Value,
        StringComparison.Ordinal)).value;

    private static void RequireMetric(
        GameplayOutcomeSnapshot outcome,
        GameplayMetricId id,
        double expected) => Require(
        Math.Abs(FindMetric(outcome, id) - expected) < 0.001d,
        "Crop-treatment metric mismatch: " + id.Value);

    private static string FindFact(
        GameplayOutcomeSnapshot outcome,
        GameplayOutcomeFactId id) => outcome.facts.Single(value => string.Equals(
        value.factId,
        id.Value,
        StringComparison.Ordinal)).value;

    private static void RequireFact(
        GameplayOutcomeSnapshot outcome,
        GameplayOutcomeFactId id,
        string expected) => Require(string.Equals(
            FindFact(outcome, id),
            expected,
            StringComparison.Ordinal),
        "Crop-treatment fact mismatch: " + id.Value);

    private sealed class FailOnceTreatmentTare :
        IPackagedLotTareDispositionService
    {
        private readonly IPackagedLotTareDispositionService inner;

        internal FailOnceTreatmentTare(
            IPackagedLotTareDispositionService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectNext { get; set; }
        internal int CallCount { get; private set; }

        public bool EnsureTerminalSinkOutputs(
            IReadOnlyDictionary<string, int> consumedItems,
            Vector2Int outputPosition,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            if (RejectNext)
            {
                RejectNext = false;
                receipt = default;
                failureReason = "injected-treatment-tare-rejection";
                return false;
            }
            return inner.EnsureTerminalSinkOutputs(
                consumedItems,
                outputPosition,
                parentCommitId,
                out receipt,
                out failureReason);
        }
    }

    private sealed class CropTreatmentFaultingRecorder :
        IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;

        internal CropTreatmentFaultingRecorder(
            IGameplayOutcomeRecorder inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectReserve { get; set; }
        internal bool RejectWrite { get; set; }

        public OutcomePrepareResult TryPrepare<TReceipt>(
            in TReceipt receipt,
            out PreparedOutcomeToken prepared) =>
            inner.TryPrepare(receipt, out prepared);

        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation)
        {
            if (RejectReserve
                && requirements.OutcomeTypeId
                    == EnvironmentOutcomeIds.CropTreatmentApplied)
            {
                reservation = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "injected-treatment-reserve-rejection");
            }
            return inner.TryReserve(requirements, out reservation);
        }

        public OutcomePrepareResult TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared)
        {
            if (RejectWrite
                && reservation.ResultKey.ProducerId
                    == EnvironmentOutcomeIds.CropTreatmentProducer)
            {
                prepared = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.AdapterWriteFailed,
                    "injected-treatment-write-rejection");
            }
            return inner.TryWriteReserved(receipt, reservation, out prepared);
        }

        public void CancelReservation(
            in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);

        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            inner.CancelPrepared(prepared);

        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed) =>
            inner.CommitPrepared(
                prepared,
                expectedOwnerRevision,
                out committed);

        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] expectedOwnerRevisions,
            CommittedOutcomeToken[] committed) =>
            inner.CommitPreparedBatch(
                prepared,
                expectedOwnerRevisions,
                committed);

        public OutcomeDeliveryResult TryDeliver(
            in CommittedOutcomeToken committed) =>
            inner.TryDeliver(committed);

        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            inner.Acknowledge(committed);

        public int RetryPendingDeliveries(int maximumCount) =>
            inner.RetryPendingDeliveries(maximumCount);
    }
}
#endif
