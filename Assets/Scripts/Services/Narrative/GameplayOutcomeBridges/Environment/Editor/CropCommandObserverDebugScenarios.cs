#if UNITY_EDITOR
using System;
using DungeonStory.Foundation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    public static void RunCropAndFastingObserversFromBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        RunCropCommandObserversFromBatch();
        try
        {
            Environment.SetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT", Path.Combine(output, "ritual-fasting"));
            RitualFastingA08DebugScenarios.RunBatch();
        }
        finally { Environment.SetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT", output); }
    }

    // Deliberately separate from the large E08/E09 suites: these crop commands
    // do not need a whole-world session or the unrelated salience/history tests.
    public static void RunCropCommandObserversFromBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string persistence = DungeonPersistencePaths.ValidateVerificationRoot(
            Path.Combine(output ?? string.Empty, "persistent-data"), output,
            Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
        string prior = Environment.GetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable);
        List<string> diagnostics = new();
        int errors = 0;
        void Capture(string message, string trace, LogType type)
        {
            if (type is LogType.Warning or LogType.Error or LogType.Exception or LogType.Assert)
                diagnostics.Add(type + ": " + message + "\n" + trace);
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors++;
        }
        Directory.CreateDirectory(output);
        string reportPath = Path.Combine(output, "crop-command-observers.txt");
        using FileStream file = new(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(file) { AutoFlush = true };
        Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable, persistence);
        Application.logMessageReceived += Capture;
        try
        {
            VerifyCropSelectionObserverRecovery();
            writer.WriteLine("crop-selection/reserve-reject/no-mutation/hidden-ack/json-fresh-owner/exact-key/no-duplicate=PASS");
            VerifyGoldenScheduleObserverRecovery();
            writer.WriteLine("golden-schedule/reserve-reject/trait-cancel/hidden-ack/json-fresh-owner/exact-key/no-duplicate=PASS");
            VerifyIrrigationObserverRecovery(hiddenCommittedAck: false);
            VerifyIrrigationObserverRecovery(hiddenCommittedAck: true);
            writer.WriteLine("automatic-irrigation/reserve-and-publish-reject-rollback/unknown-before-and-after-commit/json-fresh-owner/same-key/single-debit-and-credit=PASS");
            VerifyManualWaterRefillRecoveryAndExactReceipt();
            VerifyManualWaterRefillRecoveryAndExactReceipt(hiddenAcknowledgement: true);
            writer.WriteLine("manual-water-refill/reserve-reject-no-effects/write-reject-after-consumption/hidden-ack/json-fresh-owner/same-key/single-debit/exact-physical-child-and-worker/no-duplicate=PASS");
            VerifyCropTreatmentRecoveryAndFailureBoundaries();
            VerifyTreatmentEcologyAppliedRestore(hiddenAcknowledgement: true);
            writer.WriteLine("crop-treatment/pest-lure-pesticide-fungicide/reserve-reject/input-and-ecology-pending-restore/hidden-ack/fresh-owner/same-key/single-consumption-tare-effect/cancel/no-duplicate=PASS");
            VerifyHarvestTerminalObserverRecovery(hiddenAcknowledgement: false);
            VerifyHarvestTerminalObserverRecovery(hiddenAcknowledgement: true);
            writer.WriteLine("crop-harvest-terminal/reserve-reject/unknown-before-and-after-commit/frozen-terminal/json-fresh-owner/exact-key/single-product-and-seed/no-duplicate=PASS");
            VerifyHarvestCompletionObserverRecovery(false);
            VerifyHarvestCompletionObserverRecovery(true);
            writer.WriteLine("crop-harvest-completion/actual-identity-owner/terminal-child-first/reserve-reject/unknown-before-and-after-commit/json-fresh-crop-and-identity-owner/exact-key/effects-once/no-extra-notification=PASS");
            writer.WriteLine("crop-plot-retirement/cancel-no-effects/unknown-before-and-after-commit/json-fresh-owner/exact-key/stream-retired-once=PASS");
            writer.WriteLine("husbandry-completion-notification-regression="
                + WorkCompletionIdentityCausalProductionEntryDebugScenarios.VerifyHusbandryCompletionFactFocused());
            RunOrThrow();
            writer.WriteLine("crop-failed-terminal/natural-death/active-and-pre-sow-plot-destruction/reserve-reject/delivery-and-ack-fault/json-restore/retire-once=PASS");
            Require(errors == 0, "Crop command observer verification emitted Unity errors.");
            writer.WriteLine("fact-immutability/payload-drift/wrong-key/strict-pending-restore=PASS");
            writer.WriteLine("command-and-ledger=PRODUCTION;completion-specific-tests-identity-owner-and-effects=PRODUCTION;other-fixtures-completion=CONTROLLED;world-clock-actors-ready-crop-fluid-network=CONTROLLED;natural-ai-and-cold-whole-world=NOT-RUN");
            writer.WriteLine("warnings=" + diagnostics.Count + ";errors=" + errors);
            writer.WriteLine("result=PASS");
        }
        catch (Exception exception)
        {
            writer.WriteLine("result=FAIL\n" + exception);
            throw;
        }
        finally
        {
            foreach (string diagnostic in diagnostics) writer.WriteLine(diagnostic);
            Application.logMessageReceived -= Capture;
            Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable, prior);
        }
    }

    private static void VerifyCropSelectionObserverRecovery()
    {
        using Fixture fixture = new(ledgerRunId: "run:crop-command-selection-observer");
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        CropDefinitionSO alternate = fixture.RequireAlternateOutdoorCrop();
        runtime.Tick();
        CropCommandProbe probe = new(fixture.domainFacts) { RejectReservation = true };
        runtime.ConstructDomainFacts(probe);
        string before = JsonUtility.ToJson(runtime.Capture());
        Require(!runtime.TrySetCrop(fixture.Plot, alternate.CropId, out _)
            && before == JsonUtility.ToJson(runtime.Capture()),
            "Rejected crop selection changed authoritative state.");
        probe.RejectReservation = false;
        probe.HideNextAcknowledgement = true;
        Require(!runtime.TrySetCrop(fixture.Plot, alternate.CropId, out _),
            "Unknown crop selection ACK was reported complete.");
        DungeonCropPlotSaveData saved = JsonUtility.FromJson<DungeonCropPlotSaveData>(
            JsonUtility.ToJson(runtime.Capture()));
        CropPlotSaveData plot = saved.plots.Single();
        CropSelectionCommandSaveData pending = plot.cropSelectionOutcome;
        Require(plot.cropId == alternate.CropId && !pending.IsEmpty
            && plot.nextCropSelectionOperationSequence == 0
            && !string.IsNullOrEmpty(pending.publicationKey),
            "Selection pending fact/key or applied crop was lost after hidden ACK.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropSelectionProducer,
            EnvironmentOutcomeIds.CropSelectionChanged, 1);
        BuildingInstanceId plotId = fixture.Plot.RequirePersistentInstanceId();
        CropSelectionChangedFact fact = new(plotId, pending);
        string frozenName = pending.cropDisplayName;
        pending.cropDisplayName = "mutated source";
        CropSelectionCommandSaveData copy = fact.Snapshot();
        copy.cropDisplayName = "mutated returned copy";
        Require(fact.Snapshot().cropDisplayName == frozenName,
            "Crop selection fact was not immutable.");
        RequireReplayRejected(fixture.domainFacts, pending.publicationKey,
            new CropSelectionChangedFact(plotId, pending), "changed selection payload");
        RequireReplayRejected(fixture.domainFacts, pending.publicationKey + ":wrong", fact,
            "wrong selection publication key");
        pending.cropDisplayName = frozenName;
        string savedKey = pending.publicationKey;
        pending.publicationKey = string.Empty;
        RequireThrows(() => runtime.BuildRestore(saved), "Missing selection publication key was accepted.");
        pending.publicationKey = savedKey;
        CropPlotRuntime resumed = fixture.CreateCropRuntime(allowReplacement: true);
        resumed.ConstructDomainFacts(probe);
        resumed.Restore(resumed.BuildRestore(saved));
        Require(resumed.TrySetCrop(fixture.Plot, alternate.CropId, out _),
            "Restored selection did not acknowledge its exact pending fact.");
        CropPlotSaveData completed = resumed.Capture().plots.Single();
        Require(completed.cropSelectionOutcome.IsEmpty
            && completed.nextCropSelectionOperationSequence == 1
            && probe.LastReplayPublicationKey == savedKey,
            "Selection replay did not clear exactly one original operation.");
        Require(resumed.TrySetCrop(fixture.Plot, alternate.CropId, out _),
            "Repeated same-crop selection failed.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropSelectionProducer,
            EnvironmentOutcomeIds.CropSelectionChanged, 1);
    }

    private static void VerifyGoldenScheduleObserverRecovery()
    {
        using Fixture fixture = new(ledgerRunId: "run:crop-command-golden-observer", fluidWater: 3f);
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        BuildableObject plot = fixture.Plot;
        string plotId = plot.RequirePersistentInstanceId().Value;
        using WorkerFixture worker = new("character:crop-command-golden", "Golden observer", 6951, new[] { 304 });
        using WorkerFixture other = new("character:crop-command-no-trait", "No golden trait", 6952);
        runtime.Tick();
        Require(runtime.TryBindNextCycle("action:crop-command-golden", plotId,
            fixture.Crop.CropId, out string bindFailure), "Golden bind failed: " + bindFailure);
        PrepareSownPlot(fixture, runtime, plot, worker.Actor);
        DungeonCropPlotSaveData ready = runtime.Capture();
        ready.plots.Single().phase = CropPlotPhase.ReadyToHarvest;
        ready.plots.Single().growthHours = fixture.Crop.GrowthHours;
        runtime.Restore(runtime.BuildRestore(ready));
        CropCommandProbe probe = new(fixture.domainFacts) { RejectReservation = true };
        runtime.ConstructDomainFacts(probe);
        string before = JsonUtility.ToJson(runtime.Capture());
        Require(!runtime.TryScheduleGoldenHarvest(plot, worker.Actor, out _)
            && before == JsonUtility.ToJson(runtime.Capture()),
            "Rejected golden reservation changed authoritative state.");
        probe.RejectReservation = false;
        Require(!runtime.TryScheduleGoldenHarvest(plot, other.Actor, out _)
            && probe.CancelCount == 1 && before == JsonUtility.ToJson(runtime.Capture()),
            "Failed golden trait command did not cancel its observer reservation.");
        probe.HideNextAcknowledgement = true;
        Require(!runtime.TryScheduleGoldenHarvest(plot, worker.Actor, out _),
            "Unknown golden ACK was reported complete.");
        GoldenHarvestRestorePayload saved = RoundTripGoldenHarvest(new GoldenHarvestRestorePayload
        {
            cropPlots = runtime.Capture(), cropEcology = fixture.Ecology.Capture(),
            identityStates = fixture.CaptureIdentityStates().ToList()
        });
        CropPlotSaveData pendingPlot = saved.cropPlots.plots.Single();
        CropGoldenHarvestScheduleSaveData pending = pendingPlot.goldenHarvestScheduleOutcome;
        Require(!pending.IsEmpty && pendingPlot.goldenHarvestAttemptSequence == 1
            && pendingPlot.goldenHarvestHarvesterId == worker.Actor.Identity.PersistentId,
            "Golden applied state or pending fact was lost after hidden ACK.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.GoldenHarvestScheduleProducer,
            EnvironmentOutcomeIds.GoldenHarvestScheduled, 1);
        GoldenHarvestScheduledFact fact = new(plot.RequirePersistentInstanceId(), pending);
        string name = pending.harvesterDisplayName;
        pending.harvesterDisplayName = "mutated source";
        RequireReplayRejected(fixture.domainFacts, pending.publicationKey,
            new GoldenHarvestScheduledFact(plot.RequirePersistentInstanceId(), pending), "changed golden payload");
        CropGoldenHarvestScheduleSaveData copy = fact.Snapshot();
        copy.harvesterDisplayName = "mutated returned copy";
        Require(fact.Snapshot().harvesterDisplayName == name, "Golden fact was not immutable.");
        pending.harvesterDisplayName = name;
        RequireReplayRejected(fixture.domainFacts, pending.publicationKey + ":wrong", fact,
            "wrong golden publication key");
        string savedKey = pending.publicationKey;
        pending.publicationKey = null;
        RequireThrows(() => runtime.BuildRestore(saved.cropPlots), "Null golden publication key was accepted.");
        pending.publicationKey = savedKey;
        CropPlotRuntime resumed = fixture.CreateCropRuntime(allowReplacement: true);
        resumed.ConstructDomainFacts(probe);
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(saved.cropEcology));
        fixture.RestoreIdentityStates(saved.identityStates, worker.Actor.Progression.ResolveSelectedTraits());
        resumed.Restore(resumed.BuildRestore(saved.cropPlots));
        resumed.Tick();
        CropPlotSaveData completed = resumed.Capture().plots.Single();
        Require(completed.goldenHarvestScheduleOutcome.IsEmpty
            && completed.goldenHarvestAttemptSequence == 1
            && probe.LastReplayPublicationKey == savedKey
            && resumed.TryGetGoldenHarvestDelay(plot, worker.Actor, out float remaining) && remaining > 0f,
            "Golden exact replay did not preserve its single active schedule.");
        Require(!resumed.TryScheduleGoldenHarvest(plot, other.Actor, out _),
            "A second worker replaced the active golden schedule.");
        resumed.Tick();
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.GoldenHarvestScheduleProducer,
            EnvironmentOutcomeIds.GoldenHarvestScheduled, 1);
    }

    private static void VerifyIrrigationObserverRecovery(bool hiddenCommittedAck)
    {
        using Fixture fixture = new(ledgerRunId: "run:crop-irrigation-observer:" + hiddenCommittedAck);
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        using WorkerFixture worker = new("character:crop-irrigation-observer", "Irrigation observer", 6961);
        string plotId = fixture.Plot.RequirePersistentInstanceId().Value;
        runtime.Tick();
        Require(runtime.TryBindNextCycle("action:crop-irrigation-observer", plotId,
            fixture.Crop.CropId, out string failure), "Irrigation cycle bind failed: " + failure);
        PrepareSownPlot(fixture, runtime, fixture.Plot, worker.Actor);
        DungeonCropPlotSaveData dry = runtime.Capture();
        dry.plots.Single().currentWater = 0f;
        runtime.Restore(runtime.BuildRestore(dry));
        CropCommandProbe probe = new(fixture.domainFacts) { RejectReservation = true };
        runtime.ConstructDomainFacts(probe);
        float networkBefore = fixture.Fluid.Water;
        int consumedBefore = fixture.Fluid.Consumed;
        string before = JsonUtility.ToJson(runtime.Capture());
        RequireThrows(runtime.Tick, "Irrigation observer reservation rejection was hidden.");
        Require(before == JsonUtility.ToJson(runtime.Capture())
            && fixture.Fluid.Water == networkBefore && fixture.Fluid.Consumed == consumedBefore,
            "Irrigation reservation rejection failed to roll back both owners.");
        probe.RejectReservation = false;
        probe.RejectPublication = true;
        RequireThrows(runtime.Tick, "Irrigation publication rejection was hidden.");
        Require(before == JsonUtility.ToJson(runtime.Capture())
            && fixture.Fluid.Water == networkBefore && fixture.Fluid.Consumed == consumedBefore,
            "Irrigation publication rejection failed to restore water, sequence and cycle receipt.");
        probe.RejectPublication = false;
        probe.ThrowBeforePublication = !hiddenCommittedAck;
        probe.HideNextAcknowledgement = hiddenCommittedAck;
        runtime.Tick();
        DungeonCropPlotSaveData saved = JsonUtility.FromJson<DungeonCropPlotSaveData>(
            JsonUtility.ToJson(runtime.Capture()));
        CropEcologyWorldSaveData ecology = JsonUtility.FromJson<CropEcologyWorldSaveData>(
            JsonUtility.ToJson(fixture.Ecology.Capture()));
        CropPlotSaveData plot = saved.plots.Single();
        CropIrrigationPublicationSaveData pending = plot.irrigationPublication;
        string key = pending.publicationKey;
        Require(pending.pending && pending.operationSequence == dry.plots.Single().nextWaterRefillOperationSequence
            && plot.nextWaterRefillOperationSequence == pending.operationSequence + 1
            && plot.currentWater == CropWaterRules.RefillQuantity
            && plot.cycleExecutionReceipt.irrigationChildResultKeys.Count(value => value == key) == 1
            && fixture.Fluid.Water == networkBefore - CropWaterRules.RefillQuantity
            && fixture.Fluid.Consumed == consumedBefore + 1,
            "Uncertain irrigation lost its exact applied debit/credit or pending identity.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropIrrigationProducer,
            EnvironmentOutcomeIds.CropIrrigationSupplied, hiddenCommittedAck ? 1 : 0);
        probe.DeferRecovery = true;
        runtime.Tick();
        Require(runtime.Capture().plots.Single().irrigationPublication.pending
            && fixture.Fluid.Water == networkBefore - CropWaterRules.RefillQuantity
            && fixture.Fluid.Consumed == consumedBefore + 1,
            "Blocked irrigation recovery repeated a water mutation or dropped pending state.");

        CropIrrigationSuppliedFact fact = new(pending);
        string frozenName = pending.irrigatorDisplayName;
        pending.irrigatorDisplayName = "changed-source";
        CropIrrigationPublicationSaveData snapshot = fact.Snapshot();
        snapshot.irrigatorDisplayName = "changed-copy";
        Require(fact.Snapshot().irrigatorDisplayName == frozenName,
            "Irrigation fact was mutable through its source or snapshot.");
        if (hiddenCommittedAck)
            RequireReplayRejected(fixture.domainFacts, key, new CropIrrigationSuppliedFact(pending),
                "changed irrigation payload");
        pending.irrigatorDisplayName = frozenName;
        RequireReplayRejected(fixture.domainFacts, key + ":wrong", fact, "wrong irrigation key");
        pending.publicationKey = null;
        RequireThrows(() => runtime.BuildRestore(saved), "Null irrigation identity was accepted.");
        pending.publicationKey = key;
        plot.nextWaterRefillOperationSequence++;
        RequireThrows(() => runtime.BuildRestore(saved), "Irrigation owner sequence drift was accepted.");
        plot.nextWaterRefillOperationSequence--;
        plot.currentWater = 0f;
        RequireThrows(() => runtime.BuildRestore(saved), "Irrigation pending fact disagreed with actual plot water but was accepted.");
        plot.currentWater = pending.waterAfter;
        plot.cycleExecutionReceipt.waterAfter = 0f;
        RequireThrows(() => runtime.BuildRestore(saved), "Irrigation pending fact disagreed with its cycle water but was accepted.");
        plot.cycleExecutionReceipt.waterAfter = pending.waterAfter;
        VerifyIrrigationRawSaveFields(runtime, saved);
        CropPlotRuntime resumed = fixture.CreateCropRuntime(allowReplacement: true);
        resumed.ConstructDomainFacts(probe);
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(ecology));
        resumed.Restore(resumed.BuildRestore(saved));
        // No elapsed simulation step: isolate replay from normal plant uptake.
        fixture.Clock.Restore(fixture.Clock.Day, fixture.Clock.Hour, fixture.Clock.RawTime, 0f, false);
        probe.DeferRecovery = false;
        resumed.Tick();
        resumed.Tick();
        CropPlotSaveData completed = resumed.Capture().plots.Single();
        Require(completed.irrigationPublication.IsEmpty
            && completed.nextWaterRefillOperationSequence == pending.operationSequence + 1
            && completed.currentWater == CropWaterRules.RefillQuantity
            && completed.cycleExecutionReceipt.irrigationChildResultKeys.Count(value => value == key) == 1
            && probe.LastReplayPublicationKey == key
            && fixture.Fluid.Water == networkBefore - CropWaterRules.RefillQuantity
            && fixture.Fluid.Consumed == consumedBefore + 1,
            "Fresh-owner irrigation replay changed its once-only debit/credit/sequence.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropIrrigationProducer,
            EnvironmentOutcomeIds.CropIrrigationSupplied, 1);
    }

    private static void VerifyIrrigationRawSaveFields(CropPlotRuntime runtime, DungeonCropPlotSaveData saved)
    {
        var section = new CropPlotSaveSection(runtime, Proxy<ICropPlotInputOwnerDescriptorSource>(),
            Proxy<ICropPlotInputOwnerRuntime>(), Proxy<IProductionDomainOutputRestoreJoin>(),
            Proxy<IProductionOutputMaximumMassRegistry>(), Proxy<IProductionOutputDetachedFacilityCapacityRestoreGuard>(),
            Proxy<IRestoreWorldCandidateQuery>(), Proxy<IFacilityBufferPlannedOutputRestoreCandidateQuery>(),
            Proxy<IFacilityBufferAcknowledgedOutputRestoreCandidateQuery>());
        var method = typeof(CropPlotSaveSection).GetMethod("ValidateRawPayload",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        void Validate(string json)
        {
            try { method.Invoke(section, new object[] { json }); }
            catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            { throw exception.InnerException; }
        }
        string source = JsonUtility.ToJson(saved);
        Validate(source);
        var missing = Newtonsoft.Json.Linq.JObject.Parse(source);
        ((Newtonsoft.Json.Linq.JObject)missing["plots"][0]["irrigationPublication"]).Remove("pending");
        RequireThrows(() => Validate(missing.ToString()), "Missing irrigation pending flag was accepted.");
        var nullKey = Newtonsoft.Json.Linq.JObject.Parse(source);
        nullKey["plots"][0]["irrigationPublication"]["publicationKey"] = Newtonsoft.Json.Linq.JValue.CreateNull();
        RequireThrows(() => Validate(nullKey.ToString()), "Null raw irrigation publication key was accepted.");
        var missingWaterName = Newtonsoft.Json.Linq.JObject.Parse(source);
        ((Newtonsoft.Json.Linq.JObject)missingWaterName["plots"][0]["waterRefill"]).Remove("itemDisplayName");
        RequireThrows(() => Validate(missingWaterName.ToString()), "Missing manual-water frozen name was accepted.");
        var nullWaterKey = Newtonsoft.Json.Linq.JObject.Parse(source);
        nullWaterKey["plots"][0]["waterRefill"]["publicationKey"] = Newtonsoft.Json.Linq.JValue.CreateNull();
        RequireThrows(() => Validate(nullWaterKey.ToString()), "Null raw manual-water key was accepted.");
        var missingTreatmentName = Newtonsoft.Json.Linq.JObject.Parse(source);
        ((Newtonsoft.Json.Linq.JObject)missingTreatmentName["plots"][0]["treatment"]).Remove("itemDisplayName");
        RequireThrows(() => Validate(missingTreatmentName.ToString()), "Missing treatment frozen name was accepted.");
        var nullTreatmentKey = Newtonsoft.Json.Linq.JObject.Parse(source);
        nullTreatmentKey["plots"][0]["treatment"]["publicationKey"] = Newtonsoft.Json.Linq.JValue.CreateNull();
        RequireThrows(() => Validate(nullTreatmentKey.ToString()), "Null raw treatment key was accepted.");
        var missingTerminal = Newtonsoft.Json.Linq.JObject.Parse(source);
        ((Newtonsoft.Json.Linq.JObject)missingTerminal["plots"][0]["pendingHarvest"]).Remove("terminalReceipt");
        RequireThrows(() => Validate(missingTerminal.ToString()), "Missing harvest terminal snapshot was accepted.");
        var nullFailureKey = Newtonsoft.Json.Linq.JObject.Parse(source);
        nullFailureKey["plots"][0]["failedOutcome"]["publicationKey"] = Newtonsoft.Json.Linq.JValue.CreateNull();
        RequireThrows(() => Validate(nullFailureKey.ToString()), "Null failed-terminal key was accepted.");
        var missingCompletion = Newtonsoft.Json.Linq.JObject.Parse(source);
        ((Newtonsoft.Json.Linq.JObject)missingCompletion["plots"][0]["pendingHarvest"]).Remove("completionPublicationKey");
        RequireThrows(() => Validate(missingCompletion.ToString()), "Missing completion publication key was accepted.");
        var nullRetirement = Newtonsoft.Json.Linq.JObject.Parse(source);
        nullRetirement["plots"][0]["retirementPublicationKey"] = Newtonsoft.Json.Linq.JValue.CreateNull();
        RequireThrows(() => Validate(nullRetirement.ToString()), "Null retirement publication key was accepted.");
    }

    private static void VerifyHarvestTerminalObserverRecovery(bool hiddenAcknowledgement)
    {
        using Fixture fixture = new(ledgerRunId: "run:crop-harvest-terminal-" + hiddenAcknowledgement);
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        using WorkerFixture worker = new("character:crop-terminal-worker", "Terminal worker", 6971);
        BuildingInstanceId plotId = fixture.Plot.RequirePersistentInstanceId();
        const string actionId = "action:crop-terminal-observer";
        runtime.Tick();
        Require(runtime.TryBindNextCycle(actionId, plotId.Value, fixture.Crop.CropId, out string failure),
            "Terminal cycle binding failed: " + failure);
        PrepareSownPlot(fixture, runtime, fixture.Plot, worker.Actor);
        DungeonCropPlotSaveData ready = runtime.Capture();
        ready.plots.Single().phase = CropPlotPhase.ReadyToHarvest;
        ready.plots.Single().growthHours = fixture.Crop.GrowthHours;
        runtime.Restore(runtime.BuildRestore(ready));
        Require(runtime.TryGetWork(fixture.Plot, BuiltInWorkTypeIds.Harvest, out CropPlotWorkSnapshot work)
            && work.Available, "Terminal harvest work is unavailable.");
        int productBefore = fixture.CountItem(fixture.Crop.HarvestItemId);
        int seedsBefore = fixture.CountItem(fixture.Crop.SeedItemId);
        CropCommandProbe probe = new(fixture.domainFacts) { RejectReservation = true };
        runtime.ConstructDomainFacts(probe);
        Require(runtime.ApplyWork(fixture.Plot, BuiltInWorkTypeIds.Harvest, work.RequiredWork, worker.Actor,
            out bool completed) && !completed, "Rejected terminal observation completed the harvest owner.");
        CropHarvestOutputSaveData rejected = runtime.Capture().plots.Single().pendingHarvest;
        Require(rejected.phase == CropHarvestOutputPhase.OutputRestoredAwaitingFinalization
            && rejected.terminalReceipt.status == CropCycleExecutionReceiptStatus.Completed
            && string.IsNullOrEmpty(rejected.terminalPublicationKey),
            "Rejected terminal reservation lost the frozen terminal or advanced its owner.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropPlanProducer, EnvironmentOutcomeIds.CropPlanTerminal, 0);
        int productAfter = fixture.CountItem(fixture.Crop.HarvestItemId);
        int seedsAfter = fixture.CountItem(fixture.Crop.SeedItemId);
        Require(productAfter - productBefore == rejected.harvestQuantity
            && seedsAfter - seedsBefore == rejected.seedQuantity,
            "Harvest terminal observation changed the exact physical output quantities.");
        string digest = rejected.terminalReceipt.sourceDigest;
        probe.RejectReservation = false;
        probe.HideNextAcknowledgement = hiddenAcknowledgement;
        probe.ThrowBeforePublication = !hiddenAcknowledgement;
        runtime.Tick();
        DungeonCropPlotSaveData saved = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(runtime.Capture()));
        var ecology = fixture.Ecology.Capture();
        CropHarvestOutputSaveData pending = saved.plots.Single().pendingHarvest;
        string key = pending.terminalPublicationKey;
        Require(!string.IsNullOrEmpty(key) && pending.terminalReceipt.sourceDigest == digest
            && !pending.cropOutcomeAcknowledged && saved.plots.Single().nextHarvestOperationSequence == 0,
            "Uncertain terminal response lost its frozen payload/key or advanced the cycle.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropPlanProducer, EnvironmentOutcomeIds.CropPlanTerminal,
            hiddenAcknowledgement ? 1 : 0);
        CropCycleTerminalFact fact = new(plotId, pending.terminalReceipt, pending.terminalPlotDisplayName,
            pending.terminalCropDisplayName, pending.completionAbsoluteDay, pending.operationSequence + 1L);
        string outputLine = pending.terminalReceipt.outputs[0].outputLineId;
        pending.terminalReceipt.outputs[0].outputLineId = "changed-input";
        var copy = fact.Snapshot();
        copy.outputs[0].outputLineId = "changed-returned-copy";
        Require(fact.Snapshot().outputs[0].outputLineId == outputLine, "Crop terminal fact is not deeply immutable.");
        pending.terminalReceipt.outputs[0].outputLineId = outputLine;
        pending.terminalPublicationKey = null;
        RequireThrows(() => runtime.BuildRestore(saved), "Null terminal key was accepted.");
        pending.terminalPublicationKey = key;
        pending.terminalReceipt.sourceDigest = "changed";
        RequireThrows(() => runtime.BuildRestore(saved), "Changed terminal snapshot was accepted.");
        pending.terminalReceipt.sourceDigest = digest;
        VerifyIrrigationRawSaveFields(runtime, saved);
        CropPlotRuntime resumed = fixture.CreateCropRuntime(allowReplacement: true);
        resumed.ConstructDomainFacts(probe);
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(ecology));
        resumed.Restore(resumed.BuildRestore(saved));
        probe.DeferRecovery = true;
        resumed.Tick();
        Require(!resumed.Capture().plots.Single().pendingHarvest.terminalReceipt.IsEmpty
            && fixture.CountItem(fixture.Crop.HarvestItemId) == productAfter
            && fixture.CountItem(fixture.Crop.SeedItemId) == seedsAfter,
            "Deferred terminal recovery republished physical output or cleared the owner.");
        probe.DeferRecovery = false;
        resumed.Tick();
        CropPlotSaveData settled = resumed.Capture().plots.Single();
        Require(settled.pendingHarvest.phase == CropHarvestOutputPhase.None
            && settled.nextHarvestOperationSequence == 1 && probe.LastReplayPublicationKey == key
            && resumed.TryCaptureExecutionReceipt(actionId, out CropPlanExecutionReceipt receipt) && receipt.Succeeded,
            "Fresh crop owner did not settle exactly its saved terminal fact.");
        var identity = fixture.RequirePublishedIdentity(EnvironmentOutcomeIds.CropPlanProducer, EnvironmentOutcomeIds.CropPlanTerminal);
        Require(identity.ResultKey.ToString() == key, "Terminal replay changed the original publication identity.");
        RequireReplayRejected(fixture.domainFacts, key + ":wrong", fact, "wrong terminal publication key");
        RequireReplayRejected(fixture.domainFacts, key,
            new CropCycleTerminalFact(plotId, fact.Snapshot(), fact.PlotDisplayName + " changed",
                fact.CropDisplayName, fact.AbsoluteDay, fact.OwnerRevision), "changed terminal presentation");
        resumed.Tick();
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropPlanProducer, EnvironmentOutcomeIds.CropPlanTerminal, 1);
        Require(fixture.CountItem(fixture.Crop.HarvestItemId) == productAfter
            && fixture.CountItem(fixture.Crop.SeedItemId) == seedsAfter,
            "Terminal retries duplicated harvest output.");
    }

    private static void VerifyHarvestCompletionObserverRecovery(bool hiddenAcknowledgement)
    {
        using WorkerFixture worker = new("character:crop-completion-worker", "완료 작업자", 6973);
        RestorableCropCompletion command = null;
        using Fixture fixture = new(ledgerRunId: "run:crop-completion-" + hiddenAcknowledgement,
            completionFactory: (ledger, recorder, bus, deliveries) =>
                command = new RestorableCropCompletion(worker.Actor, ledger, recorder, bus, deliveries));
        int notifications = 0;
        using var subscription = fixture.Events.Subscribe<WorkCompletedIdentityEvent>(_ => notifications++);
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        BuildingInstanceId plotId = fixture.Plot.RequirePersistentInstanceId();
        const string actionId = "action:crop-completion-observer";
        runtime.Tick();
        Require(runtime.TryBindNextCycle(actionId, plotId.Value, fixture.Crop.CropId, out string failure), failure);
        PrepareSownPlot(fixture, runtime, fixture.Plot, worker.Actor);
        var ready = runtime.Capture();
        ready.plots.Single().phase = CropPlotPhase.ReadyToHarvest;
        ready.plots.Single().growthHours = fixture.Crop.GrowthHours;
        runtime.Restore(runtime.BuildRestore(ready));
        Require(runtime.TryGetWork(fixture.Plot, BuiltInWorkTypeIds.Harvest, out var work) && work.Available,
            "Completion harvest work unavailable.");
        CropCommandProbe probe = new(fixture.domainFacts)
        { FaultFactType = typeof(CropHarvestCompletedFact), RejectReservation = true };
        runtime.ConstructDomainFacts(probe);
        Require(runtime.ApplyWork(fixture.Plot, BuiltInWorkTypeIds.Harvest, work.RequiredWork, worker.Actor,
            out bool completed) && !completed, "Rejected completion observer completed the crop owner.");
        var pending = runtime.Capture().plots.Single().pendingHarvest;
        Require(pending.cropOutcomeAcknowledged && pending.completionPublicationKey == string.Empty
            && fixture.CompletionDeliveries.Capture().Count == 0 && notifications == 0,
            "Completion reserved/applied before its terminal or changed identity on rejection.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropPlanProducer, EnvironmentOutcomeIds.CropPlanTerminal, 1);
        int product = fixture.CountItem(fixture.Crop.HarvestItemId), seed = fixture.CountItem(fixture.Crop.SeedItemId);
        probe.RejectReservation = false;
        probe.ThrowBeforePublication = !hiddenAcknowledgement;
        probe.HideNextAcknowledgement = hiddenAcknowledgement;
        runtime.Tick();
        var saved = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(runtime.Capture()));
        pending = saved.plots.Single().pendingHarvest;
        string key = pending.completionPublicationKey;
        Require(!string.IsNullOrEmpty(key) && pending.cropOutcomeAcknowledged && !pending.completionEventPublished,
            "Unconfirmed completion lost its parent acknowledgement or exact key.");
        Require(notifications == 0, "Crop completion emitted an extra legacy identity event.");
        var fact = new CropHarvestCompletedFact(plotId, pending, saved.plots.Single().cycleExecutionReceipt);
        var invalidNotification = new WorkCompletionDomainFactObserver<CropHarvestCompletedFact>(
            CropHarvestCompletionObservation.CreateRequest, command, fixture.CompletionDeliveries,
            new CharacterIdentityEventPublisher(fixture.Events), WorkCompletionNotificationMode.ReferencedEvent,
            new WorkIdentityAppliedFactMapping(fixture.Ledger, fixture.Ledger));
        string beforeInvalidNotification = JsonUtility.ToJson(new CharacterNarrativeWorldSaveData
        { identityStates = command.States.Capture().ToList(), workCompletionDeliveries = fixture.CompletionDeliveries.Capture().ToList() });
        RequireThrows(() => invalidNotification.TryReserve(fact, out _, out _),
            "An observer accepted an event that changes the crop completion identity.");
        Require(beforeInvalidNotification == JsonUtility.ToJson(new CharacterNarrativeWorldSaveData
            { identityStates = command.States.Capture().ToList(), workCompletionDeliveries = fixture.CompletionDeliveries.Capture().ToList() })
            && notifications == 0, "Invalid notification reservation mutated identity state.");
        float workApproved = fact.ContributionWork;
        saved.plots.Single().cycleExecutionReceipt.harvestContributions[0].contributedWork += 3f;
        Require(fact.ContributionWork == workApproved, "Completion fact retained mutable work evidence.");
        saved = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(runtime.Capture()));
        var ecology = fixture.Ecology.Capture();
        var narrative = JsonUtility.FromJson<CharacterNarrativeWorldSaveData>(JsonUtility.ToJson(
            new CharacterNarrativeWorldSaveData { identityStates = command.States.Capture().ToList(),
                workCompletionDeliveries = fixture.CompletionDeliveries.Capture().ToList() }));
        string statsAfterAttempt = WorkCompletionIdentityCausalProductionEntryDebugScenarios.CaptureCropCompletionStats(worker.Actor).ToString();
        command.Rebuild(narrative);
        CropPlotRuntime resumed = fixture.CreateCropRuntime(allowReplacement: true);
        resumed.ConstructDomainFacts(probe);
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(ecology));
        resumed.Restore(resumed.BuildRestore(saved));
        // Check canonical payload/key joining before resuming a committed response.
        CropHarvestCompletionDeliveryRestoreJoin.Validate(saved.plots, fixture.CompletionDeliveries.Capture(), fixture.Ledger.CaptureGameplayOutcomes());
        var wrongKey = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(saved));
        wrongKey.plots.Single().pendingHarvest.completionPublicationKey += ":changed";
        RequireThrows(() => CropHarvestCompletionDeliveryRestoreJoin.Validate(wrongKey.plots,
            fixture.CompletionDeliveries.Capture(), fixture.Ledger.CaptureGameplayOutcomes()), "Changed completion key joined restore.");
        resumed.Tick();
        Require(resumed.Capture().plots.Single().pendingHarvest.phase == CropHarvestOutputPhase.None
            && notifications == 0 && probe.LastReplayPublicationKey == key,
            "Restored completion failed or repeated notification.");
        var cursor = fixture.CompletionDeliveries.Capture().Single();
        Require(cursor.operationSequence == 0 && !cursor.notificationAttempted
            && cursor.disposition == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome,
            "Actual completion did not retain its effect/outcome cursor or attempted an extra event.");
        fixture.RequirePublishedCount(WorkCompletionIdentityOutcomeIds.ProducerId, WorkCompletionIdentityOutcomeIds.Applied, 1);
        if (hiddenAcknowledgement)
            Require(statsAfterAttempt == WorkCompletionIdentityCausalProductionEntryDebugScenarios.CaptureCropCompletionStats(worker.Actor).ToString(),
                "Restored hidden completion ACK repeated identity effects.");
        string settledStats = WorkCompletionIdentityCausalProductionEntryDebugScenarios.CaptureCropCompletionStats(worker.Actor).ToString();
        RequireReplayRejected(fixture.domainFacts, key + ":wrong", fact, "wrong completion key");
        var results = new DomainFactDelivery[1];
        Require(fixture.domainFacts.ReplayBatch(new[] { DomainFactReplay.Create(key, fact) }, results, out failure)
            && results[0].Completed, "Exact completion replay failed: " + failure);
        resumed.Tick();
        Require(notifications == 0 && product == fixture.CountItem(fixture.Crop.HarvestItemId)
            && seed == fixture.CountItem(fixture.Crop.SeedItemId)
            && settledStats == WorkCompletionIdentityCausalProductionEntryDebugScenarios.CaptureCropCompletionStats(worker.Actor).ToString(),
            "Completion replay duplicated effects, notification or harvest outputs.");
        var retiredFact = new CropPlotRetiredFact(plotId);
        Require(fixture.domainFacts.TryReserve(retiredFact, out var cancelled, out failure), failure);
        fixture.domainFacts.Cancel(cancelled);
        Require(fixture.domainFacts.Publish(cancelled, retiredFact).Phase == DomainFactDeliveryPhase.Rejected
            && fixture.CompletionDeliveries.Capture().Count == 1, "Cancelled retirement removed the completion cursor.");
        Require(resumed.TryCaptureExecutionReceipt(actionId, out var receipt)
            && resumed.TryAcknowledgeExecutionReceipt(actionId, receipt.RuntimeReceiptDigest, out failure),
            "Completed crop receipt could not be acknowledged: " + failure);
        fixture.Plot.isDestroy = true;
        probe.FaultFactType = typeof(CropPlotRetiredFact);
        probe.ThrowBeforePublication = !hiddenAcknowledgement;
        probe.HideNextAcknowledgement = hiddenAcknowledgement;
        resumed.Tick();
        var retiring = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(resumed.Capture()));
        string retirementKey = retiring.plots.Single().retirementPublicationKey;
        Require(!resumed.TryBindNextCycle("action:retiring-plot-invalid", plotId.Value, fixture.Crop.CropId, out _),
            "Retiring crop owner accepted a new cycle.");
        Require(!string.IsNullOrEmpty(retirementKey)
            && fixture.CompletionDeliveries.Capture().Count == (hiddenAcknowledgement ? 0 : 1),
            "Unconfirmed plot retirement lost its pending owner or changed its effect unexpectedly.");
        var retiredEcology = fixture.Ecology.Capture();
        CropPlotRuntime retirementOwner = fixture.CreateCropRuntime(allowReplacement: true);
        retirementOwner.ConstructDomainFacts(probe);
        fixture.Ecology.PublishRestore(fixture.Ecology.PrepareRestore(retiredEcology));
        retirementOwner.Restore(retirementOwner.BuildRestore(retiring));
        CropHarvestCompletionDeliveryRestoreJoin.Validate(retiring.plots,
            fixture.CompletionDeliveries.Capture(), fixture.Ledger.CaptureGameplayOutcomes());
        var changedRetirement = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(retiring));
        changedRetirement.plots.Single().retirementPublicationKey += ":wrong";
        RequireThrows(() => CropHarvestCompletionDeliveryRestoreJoin.Validate(changedRetirement.plots,
            fixture.CompletionDeliveries.Capture(), fixture.Ledger.CaptureGameplayOutcomes()),
            "Changed retirement key passed canonical restore join.");
        RequireReplayRejected(fixture.domainFacts, retirementKey + ":wrong", retiredFact, "wrong retirement key");
        retirementOwner.Tick();
        retirementOwner.Tick();
        Require(retirementOwner.Capture().plots.Count == 0 && fixture.CompletionDeliveries.Capture().Count == 0
            && notifications == 0 && probe.LastReplayPublicationKey == retirementKey,
            "Saved retirement did not settle exactly once without completion notifications.");
    }

    private sealed class RestorableCropCompletion : IWorkCompletionIdentityDeliveryCommand, IDisposable
    {
        private readonly CharacterActor actor;
        private readonly GameplayOutcomeLedger ledger;
        private readonly IGameplayOutcomeRecorder recorder;
        private readonly GameEventBus bus;
        private readonly WorkCompletionIdentityDeliveryLedger deliveries;
        private WorkIdentityEventAdapter current;
        internal CharacterIdentityStateStore States;
        internal RestorableCropCompletion(CharacterActor actor, GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder, GameEventBus bus, WorkCompletionIdentityDeliveryLedger deliveries)
        { this.actor = actor; this.ledger = ledger; this.recorder = recorder; this.bus = bus; this.deliveries = deliveries; Rebuild(null); }
        internal void Rebuild(CharacterNarrativeWorldSaveData saved)
        {
            current?.Dispose();
            current = WorkCompletionIdentityCausalProductionEntryDebugScenarios.CreateCropCompletionCommand(
                actor, ledger, recorder, bus, deliveries, out States);
            if (saved == null) return;
            States.RestoreTrustedTransactionSnapshot(saved.identityStates);
            deliveries.Restore(saved.workCompletionDeliveries);
        }
        public WorkCompletionIdentityDeliveryResult EnsureApplied(WorkCompletionIdentityDeliveryRequest request) => current.EnsureApplied(request);
        public bool RetireProducerStream(string stream) => current.RetireProducerStream(stream);
        public void Dispose() => current.Dispose();
    }

    private static void RequireReplayRejected<TFact>(DomainFactDispatcher dispatcher,
        string key, TFact fact, string scenario)
    {
        Require(!dispatcher.ReplayBatch(new[] { DomainFactReplay.Create(key, fact) },
            new DomainFactDelivery[1], out _), "Observer accepted " + scenario + ".");
    }

    private static void RequireThrows(Action operation, string failure)
    {
        try { operation(); }
        catch (ArgumentException) { return; }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(failure);
    }

    private sealed class CropCommandProbe : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal CropCommandProbe(DomainFactDispatcher inner) => this.inner = inner;
        internal bool RejectReservation;
        internal bool HideNextAcknowledgement;
        internal bool RejectPublication;
        internal bool ThrowBeforePublication;
        internal bool DeferRecovery;
        internal Type FaultFactType;
        internal int CancelCount;
        internal string LastReplayPublicationKey;
        public bool TryReserve<TFact>(in TFact fact, out DomainFactLease lease, out string failure)
        {
            if (!RejectReservation || FaultFactType != null && FaultFactType != typeof(TFact))
                return inner.TryReserve(fact, out lease, out failure);
            lease = default; failure = "qa:reject-crop-command-reservation"; return false;
        }
        public DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact fact)
        {
            if (FaultFactType != null && FaultFactType != typeof(TFact)) return inner.Publish(lease, fact);
            if (RejectPublication)
            {
                inner.Cancel(lease);
                return DomainFactDelivery.Reject("qa:reject-crop-publication");
            }
            if (ThrowBeforePublication)
            {
                ThrowBeforePublication = false;
                inner.Cancel(lease);
                throw new InvalidOperationException("qa:irrigation-unknown-before-commit");
            }
            DomainFactDelivery result = inner.Publish(lease, fact);
            if (!HideNextAcknowledgement || !result.Completed) return result;
            HideNextAcknowledgement = false;
            return DomainFactDelivery.Uncertain("qa:hidden-crop-command-ack");
        }
        public void Cancel(in DomainFactLease lease) { CancelCount++; inner.Cancel(lease); }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure)
        {
            LastReplayPublicationKey = facts[0].PublicationKey;
            if (DeferRecovery)
            {
                failure = "qa:defer-irrigation-recovery";
                return false;
            }
            return inner.ReplayBatch(facts, results, out failure);
        }
        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            throw new InvalidOperationException("Crop commands must replay their saved exact key.");
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
    }
}
#endif
