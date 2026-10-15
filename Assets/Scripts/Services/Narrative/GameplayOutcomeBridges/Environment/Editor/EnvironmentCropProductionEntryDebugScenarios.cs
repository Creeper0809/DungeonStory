#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    private const string CropId = "crop:twilight-grain";
    internal const string CausalCropOwnerRestoreKind =
        "owner-family:environment-crop-plot";
    private const string CausalCropOwnerKind =
        "owner-kind:environment-crop-plot";
    private const string CausalCropDomainStateSchema =
        "dungeonstory-causal-crop-owner-state@1";
    private const string CertificationKitItemId =
        CertifiedSeedPhysicalTransformAuthority.CertificationKitItemId;
    private static JArray e08BatchObservations;

    public static void VerifyCertifiedSeedObserverFocused()
    {
        using (Fixture fixture = new())
        {
            VerifyCertifiedSeedCompletion(fixture);
            RequireOnlyCertifiedSeedCompletion(fixture);
        }

        ManualOutcomeFaultingRecorder fault = null;
        using Fixture recovery = new(
            ledgerRunId: "run:environment-certified-seed-observer-recovery",
            recorderDecorator: inner =>
                fault = new ManualOutcomeFaultingRecorder(inner));
        VerifyCertifiedSeedCompletion(
            recovery,
            verifyObserverRejectRecovery: true,
            recorderFault: fault);
        RequireOnlyCertifiedSeedCompletion(recovery);
    }

    private static void RequireOnlyCertifiedSeedCompletion(Fixture fixture)
    {
        GameplayOutcomeSnapshot[] exact = fixture.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Where(value => value != null)
            .ToArray();
        Require(
            exact.Length == 1
            && string.Equals(
                exact[0].producerId,
                EnvironmentOutcomeIds.CertifiedSeedProducer,
                StringComparison.Ordinal)
            && string.Equals(
                exact[0].outcomeTypeId,
                EnvironmentOutcomeIds.CertifiedSeedCompleted.Value,
                StringComparison.Ordinal),
            "Focused certified-seed observer verification did not publish "
            + "exactly its one completion outcome.");
    }

    public static bool RunAll(bool log = false)
    {
        try
        {
            using Fixture fixture = new(includeParallelPlot: true);
            VerifyCertifiedSeedCompletion(fixture);
            VerifyAutomaticIrrigationAndHarvest(fixture);

            fixture.RequirePublished(
                EnvironmentOutcomeIds.CertifiedSeedProducer,
                EnvironmentOutcomeIds.CertifiedSeedCompleted);
            fixture.RequirePublished(
                EnvironmentOutcomeIds.CropIrrigationProducer,
                EnvironmentOutcomeIds.CropIrrigationSupplied);
            fixture.RequirePublishedCount(
                EnvironmentOutcomeIds.CropSelectionProducer,
                EnvironmentOutcomeIds.CropSelectionChanged,
                2);
            fixture.RequirePublished(
                EnvironmentOutcomeIds.CropPlanProducer,
                EnvironmentOutcomeIds.CropPlanTerminal);
            fixture.RequirePublishedCount(
                EnvironmentOutcomeIds.CropTreatmentProducer,
                EnvironmentOutcomeIds.CropTreatmentApplied,
                3);
            Require(
                fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 8,
                "The shared ledger must contain the eight exact crop production-entry outcomes.");

            VerifyCropTreatmentRecoveryAndFailureBoundaries();
            VerifyManualWaterRefillRecoveryAndExactReceipt();
            VerifyGoldenHarvestOutcome(
                "jackpot",
                roll => roll < 0.12f,
                ExtremeRiskOutcome.Jackpot);
            VerifyGoldenHarvestOutcome(
                "loss",
                roll => roll >= 0.12f && roll < 0.30f,
                ExtremeRiskOutcome.Loss);

            if (log)
                Debug.Log("Environment crop production-entry scenarios passed.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Environment crop production-entry scenarios failed: "
                + exception);
            return false;
        }
    }

    public static void RunSuccessfulCycleOrThrow()
    {
        if (!RunAll(log: true))
            throw new InvalidOperationException(
                "Environment crop production-entry verification failed.");
    }

    public static void RunE03FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_E03_CROP_TREATMENT_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_E03_CROP_TREATMENT_REPORT_PATH is required.");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        string rawPath = fullPath + ".raw.json";
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath)
            || File.Exists(rawPath))
        {
            throw new InvalidOperationException(
                "E03 crop-treatment report must be a create-only path under Artifacts/QA.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        Require(e03BatchObservations == null,
            "E03 batch evidence collection is already active.");
        try
        {
            e03BatchObservations = new JArray();
            bool scenariosPassed = RunAll(log: true);
            string[] missingStages = MissingE03ObservationStages();
            bool passed = scenariosPassed && missingStages.Length == 0;
            using (FileStream raw = new(
                       rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter rawWriter = new(raw))
            {
                rawWriter.Write(new JObject
                {
                    ["schema"] = "dungeonstory-e03-crop-treatment-observations@1",
                    ["passed"] = passed,
                    ["scenariosPassed"] = scenariosPassed,
                    ["missingStages"] = new JArray(missingStages),
                    ["observations"] = e03BatchObservations,
                    ["scope"] = "Public crop owner with production physical sink/tare, ecology and ledger; controlled preparation/delivery/clock/fluid/fault ports.",
                    ["directPhysicalRestoreFixtureNormalization"] =
                        "Physical publication proof is restored unchanged; original pre-restore observations are retained. Not a whole-world save migration.",
                    ["brainAiExecuted"] = false,
                    ["wholeWorldRestoreExecuted"] = false,
                    ["rowCurrentEvidenceComplete"] = false,
                    ["fullAuditClosure"] = false,
                    ["humanApprovalClaimed"] = false,
                    ["trainingEligible"] = false
                }.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            Require(passed,
                "E03 crop-treatment scenarios or raw observation coverage failed: "
                + string.Join(",", missingStages));
        }
        finally
        {
            e03BatchObservations = null;
        }

        using FileStream report = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(report);
        writer.Write(
            "U0_E03_CROP_TREATMENT=PASS\n"
            + "runtimeEntry=CropPlotRuntime.TryScheduleTreatment,ApplyWork,Tick\n"
            + "treatments=pest-lure,botanical-pesticide,fungicide\n"
            + "recovery=reserve-reject,input-committed-restore,outcome-published-restore,cancel-no-publish\n"
            + "physicalConservation=receipt-input-mass,content-mass,tare-output-or-destruction,commit-join\n"
            + "ecology=pest-before-after,disease-before-after,cooldown\n"
            + "durability=partial-contribution,save-restore,terminal-once\n"
            + "rawEvidence=" + Path.GetFileName(rawPath) + "\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_E03_CROP_TREATMENT=PASS; report=" + fullPath);
    }

    public static void RunE08FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_E08_CROP_ACTION_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_E08_CROP_ACTION_REPORT_PATH is required.");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        string rawPath = fullPath + ".raw.json";
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath)
            || File.Exists(rawPath))
        {
            throw new InvalidOperationException(
                "E08 crop-action report must be a create-only path under Artifacts/QA.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        Require(e08BatchObservations == null,
            "E08 batch evidence collection is already active.");
        bool scenariosPassed = false;
        bool irrigationPassed = false;
        bool passed = false;
        string irrigationReport = "NOT_RUN";
        string[] missingStages = Array.Empty<string>();
        try
        {
            e08BatchObservations = new JArray();
            scenariosPassed = RunAll(log: true);
            if (scenariosPassed)
                irrigationPassed = Wim016IrrigationContractDebugScenarios.Run(
                    out irrigationReport);
            missingStages = MissingE08ObservationStages();
            passed = scenariosPassed && irrigationPassed
                && missingStages.Length == 0;
        }
        finally
        {
            try
            {
                using FileStream raw = new(
                    rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using StreamWriter rawWriter = new(raw);
                rawWriter.Write(new JObject
                {
                    ["schema"] = "dungeonstory-e08-crop-action-observations@1",
                    ["passed"] = passed,
                    ["scenariosPassed"] = scenariosPassed,
                    ["irrigationJointContractPassed"] = irrigationPassed,
                    ["irrigationJointContractReport"] = irrigationReport,
                    ["missingStages"] = new JArray(missingStages),
                    ["observations"] = e08BatchObservations,
                    ["scope"] = "Public crop commands/work with production physical items, ecology, deterministic golden roll and game ledger; controlled world, delivered materials, ready phase, clock, finite fluid and failure ports.",
                    ["restoreScope"] = "Existing JSON crop/ecology/identity owner restoration; new crop runtime reuses the same physical-item and game-ledger fixture. Not a fresh whole-world or cold-process restoration.",
                    ["observationCapture"] = "Capture DTOs without Tick or effect retries; crop Capture performs its existing input-owner reconciliation.",
                    ["brainAiExecuted"] = false,
                    ["wholeWorldRestoreExecuted"] = false,
                    ["coldProcessRestoreExecuted"] = false,
                    ["naturalNarrativeExample"] = false,
                    ["rowCurrentEvidenceComplete"] = false,
                    ["fullAuditClosure"] = false,
                    ["humanApprovalClaimed"] = false,
                    ["trainingEligible"] = false
                }.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            finally
            {
                e08BatchObservations = null;
            }
        }
        Require(passed,
            "E08 crop-action scenarios, irrigation joint contract or raw coverage failed: "
            + string.Join(",", missingStages) + "; irrigation=" + irrigationReport);
        using FileStream report = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(report);
        writer.Write(
            "U0_E08_CROP_ACTION=PASS\n"
            + "runtimeEntry=CropPlotRuntime.TrySetCrop,TryScheduleGoldenHarvest,ApplyWork,Tick\n"
            + "branches=same-value-no-op,schedule-vs-harvest,automatic-manual-water,special-harvest-save\n"
            + "persistence=plot-owner,identity-rule,ecology,terminal-receipt\n"
            + "exactlyOnce=golden-schedule,golden-terminal,harvest-output\n"
            + "irrigationJointContract=PASS\n"
            + "rawEvidence=" + Path.GetFileName(rawPath) + "\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_E08_CROP_ACTION=PASS; report=" + fullPath);
    }

    private static void ObserveE08Crop(
        string stage,
        Fixture fixture,
        CropPlotRuntime runtime)
    {
        if (e08BatchObservations == null)
            return;
        JObject observation = new()
        {
            ["stage"] = stage,
            ["plotId"] = fixture.Plot.RequirePersistentInstanceId().Value,
            ["sourceWater"] = fixture.Fluid.Water,
            ["sourceDebits"] = fixture.Fluid.Consumed,
            ["sourceVersion"] = fixture.Fluid.Version,
            ["waterItemStock"] = fixture.CountItem(
                CropCycleInputRequirementAuthority.CleanWaterItemId),
            ["harvestItemId"] = fixture.Crop.HarvestItemId,
            ["harvestItemStock"] = fixture.CountItem(fixture.Crop.HarvestItemId),
            ["seedItemId"] = fixture.Crop.SeedItemId,
            ["seedItemStock"] = fixture.CountItem(fixture.Crop.SeedItemId)
        };
        e08BatchObservations.Add(observation);
        try
        {
            observation["cropPlots"] = JObject.Parse(
                JsonUtility.ToJson(runtime.Capture()));
            observation["cropEcology"] = JObject.Parse(
                JsonUtility.ToJson(fixture.Ecology.Capture()));
            observation["identityStates"] = JArray.FromObject(
                fixture.CaptureIdentityStates());
            observation["physicalItems"] = JObject.Parse(
                JsonUtility.ToJson(fixture.WorldItems.Capture()));
            observation["ledger"] = JObject.Parse(
                JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()));
        }
        catch (Exception exception)
        {
            observation["captureFailure"] = exception.ToString();
            throw;
        }
    }

    private static string[] MissingE08ObservationStages()
    {
        string[] stages = e08BatchObservations.OfType<JObject>()
            .Select(value => (string)value["stage"]).ToArray();
        string[] required = new[]
        {
            "selection-before", "selection-changed", "selection-same-value",
            "selection-restored-choice", "automatic-dry", "automatic-supplied",
            "automatic-ready-harvest", "automatic-completed", "manual-waiting",
            "manual-ready", "manual-post-physical-pending", "manual-restored-pending",
            "manual-completed", "manual-replay"
        }.Concat(new[] { "jackpot", "loss" }.SelectMany(profile => new[]
        {
            "golden-ready:" + profile, "golden-scheduled:" + profile,
            "golden-second-worker-rejected:" + profile,
            "golden-restored-active:" + profile, "golden-active-replay:" + profile,
            "golden-mature:" + profile, "golden-wrong-worker:" + profile,
            "golden-completed:" + profile, "golden-terminal-restored:" + profile
        })).ToArray();
        return required.Where(stage => stages.Count(value => string.Equals(
            value, stage, StringComparison.Ordinal)) != 1).ToArray();
    }

    // Supplies a real committed crop child to the downstream identity fixture.
    // The crop completion port remains the existing controlled test port: this
    // does not certify the live crop-to-identity publication ordering.
    internal static void RunHarvestForWorkIdentity(
        GameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        string operationId,
        string plotId,
        CharacterActor harvester,
        int absoluteDay)
    {
        using Fixture fixture = new(
            plotGrowthRate: 100f,
            plotWaterRate: 0.01f,
            sharedRegistry: registry,
            sharedLedger: ledger);
        fixture.Clock.SetDateTime(absoluteDay, 0);
        fixture.Plot.RestorePersistentIdentity(new BuildingInstanceId(plotId));
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        Require(runtime.Plots.Single(value => value.PlotId == plotId).CropId == fixture.Crop.CropId,
            "The identity crop fixture did not start with its authored default crop.");
        int selectionCountBefore = ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
            value.outcomeTypeId == EnvironmentOutcomeIds.CropSelectionChanged.Value);
        PrepareGrowingCrop(fixture, runtime, plotId, operationId);
        Require(ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                    value.outcomeTypeId == EnvironmentOutcomeIds.CropSelectionChanged.Value)
                == selectionCountBefore,
            "Selecting the same default crop unexpectedly published a selection change.");
        int waterConsumedBefore = fixture.Fluid.Consumed;
        runtime.Tick();
        Require(fixture.Fluid.Consumed == waterConsumedBefore + 1,
            "Identity child setup did not consume its irrigation water exactly once.");
        fixture.Clock.AdvanceGameHours(1f);
        runtime.Tick();
        int productBefore = fixture.CountItem(fixture.Crop.HarvestItemId);
        Require(runtime.TryGetWork(fixture.Plot, BuiltInWorkTypeIds.Harvest,
                    out CropPlotWorkSnapshot harvest)
                && harvest.Available
                && runtime.ApplyWork(fixture.Plot, BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork, harvester, out bool completed)
                && completed
                && fixture.CountItem(fixture.Crop.HarvestItemId) > productBefore,
            "Identity child setup did not complete a real crop harvest and physical output.");
        Require(runtime.TryCaptureExecutionReceipt(operationId, out CropPlanExecutionReceipt receipt)
                && receipt.Succeeded && receipt.HarvesterId == harvester.Identity.PersistentId
                && receipt.HarvestContributions.Count == 1
                && Mathf.Approximately(receipt.HarvestContributions[0].contributedWork,
                    harvest.RequiredWork),
            "Identity child setup lost its actual harvester or approved work.");
        GameplayResultKey child = new(EnvironmentOutcomeIds.CropPlanProducer,
            new GameplayOperationId(operationId), 1L, 0);
        Require(ledger.TryGetResultIdentity(child, out GameplayOutcomeReplayIdentity identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash,
            "The real crop child was not committed to the shared identity ledger.");
    }

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalCropFixtures(
            string causalRunId,
            string ownerId = null,
            JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.",
                nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A crop owner checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }
        var context = new CausalCropFixtureContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                "action:environment-crop-irrigation-supplied",
                "environment",
                EnvironmentOutcomeIds.CropIrrigationSupplied,
                context.CaptureState,
                context.Irrigate,
                context.RequireIrrigationOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalCropOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                "action:environment-crop-plan-terminal",
                "environment",
                EnvironmentOutcomeIds.CropPlanTerminal,
                () => new Dictionary<string, object>(),
                context.GrowAndHarvest,
                context.RequireHarvestOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalCropOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    private sealed class CausalCropFixtureContext : IDisposable
    {
        private readonly Fixture fixture;
        private readonly CropPlotRuntime runtime;
        private readonly WorkerFixture harvestWorker;
        private readonly string plotId;
        private readonly string ownerId;
        private bool irrigated;
        private bool harvested;

        internal CausalCropFixtureContext(
            string causalRunId,
            string ownerId,
            JObject ownerCheckpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    CausalCropOwnerKind)
                : causalRunId + ":environment-crop-ledger";
            fixture = new Fixture(
                ledgerRunId,
                plotGrowthRate: 100f,
                plotWaterRate: 0.01f,
                fluidWater: 3f);
            try
            {
                harvestWorker = new WorkerFixture(
                    "character:causal-crop-harvester",
                    "Causal Crop Harvester",
                    6701);
                runtime = fixture.CreateCropRuntime();
                plotId = fixture.Plot.RequirePersistentInstanceId().Value;
                if (ownerCheckpoint == null)
                    PrepareGrowingCrop();
                else
                    RestoreOwnerCheckpoint(ownerCheckpoint);
            }
            catch
            {
                runtime?.Dispose();
                harvestWorker?.Dispose();
                fixture.Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string OwnerId => ownerId;
        internal string OwnerKind => HasPersistentOwner
            ? CausalCropOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            CropPlotSnapshot plot = Snapshot();
            return new Dictionary<string, object>
            {
                ["crop.plotId"] = plot.PlotId,
                ["crop.phase"] = plot.Phase.ToString(),
                ["crop.currentWater"] = plot.CurrentWater,
                ["crop.growthProgress"] = plot.GrowthProgress,
                ["crop.sourceWaterRemaining"] = fixture.Fluid.Water,
                ["crop.harvestItemCount"] = fixture.CountItem(
                    fixture.Crop.HarvestItemId)
            };
        }

        internal bool Irrigate()
        {
            if (irrigated || harvested || Snapshot().Phase != CropPlotPhase.Growing)
                return false;
            float waterBefore = Snapshot().CurrentWater;
            int consumedBefore = fixture.Fluid.Consumed;
            runtime.Tick();
            CropPlotSnapshot after = Snapshot();
            irrigated = after.CurrentWater > waterBefore
                && fixture.Fluid.Consumed == consumedBefore + 1;
            return irrigated;
        }

        internal bool GrowAndHarvest()
        {
            if (!irrigated || harvested)
                return false;
            fixture.Clock.AdvanceGameHours(1f);
            runtime.Tick();
            CropPlotSnapshot ready = Snapshot();
            if (ready.Phase != CropPlotPhase.ReadyToHarvest
                || !runtime.TryGetWork(
                    fixture.Plot,
                    BuiltInWorkTypeIds.Harvest,
                    out CropPlotWorkSnapshot harvest)
                || !harvest.Available
                || !runtime.ApplyWork(
                    fixture.Plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    harvestWorker.Actor,
                    out bool completed)
                || !completed)
            {
                return false;
            }
            harvested = true;
            return true;
        }

        internal GameplayOutcomeReplayIdentity RequireIrrigationOutcome() =>
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.CropIrrigationProducer,
                EnvironmentOutcomeIds.CropIrrigationSupplied);

        internal GameplayOutcomeReplayIdentity RequireHarvestOutcome() =>
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.CropPlanProducer,
                EnvironmentOutcomeIds.CropPlanTerminal);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(fixture.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This causal crop context has no persistent owner.");
            }
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalCropOwnerKind,
                CausalCropOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = CausalCropDomainStateSchema,
                    ["plotId"] = plotId,
                    ["cropPlots"] = Serialize(
                        runtime.Capture(),
                        "crop-plot state"),
                    ["cropEcology"] = Serialize(
                        fixture.Ecology.Capture(),
                        "crop-ecology state"),
                    ["physicalItems"] = Serialize(
                        fixture.WorldItems.Capture(),
                        "physical-item state"),
                    ["fluidWater"] = fixture.Fluid.Water,
                    ["fluidConsumed"] = fixture.Fluid.Consumed,
                    ["fluidVersion"] = fixture.Fluid.Version,
                    ["clockDay"] = fixture.Clock.Day,
                    ["clockHour"] = fixture.Clock.Hour,
                    ["clockTime"] = fixture.Clock.RawTime,
                    ["clockDeltaTime"] = fixture.Clock.RawDeltaTime,
                    ["clockRunning"] = fixture.Clock.IsRunning,
                    ["irrigated"] = irrigated,
                    ["harvested"] = harvested
                },
                fixture.Ledger);
        }

        public void Dispose()
        {
            runtime?.Dispose();
            harvestWorker?.Dispose();
            fixture.Dispose();
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CausalCropOwnerKind,
                CausalCropOwnerRestoreKind,
                fixture.Ledger);
            RequireFields(
                state,
                "causal crop owner state",
                "schemaVersion",
                "schema",
                "plotId",
                "cropPlots",
                "cropEcology",
                "physicalItems",
                "fluidWater",
                "fluidConsumed",
                "fluidVersion",
                "clockDay",
                "clockHour",
                "clockTime",
                "clockDeltaTime",
                "clockRunning",
                "irrigated",
                "harvested");
            Require(
                (int)state["schemaVersion"] == 1
                && string.Equals(
                    (string)state["schema"],
                    CausalCropDomainStateSchema,
                    StringComparison.Ordinal)
                && string.Equals(
                    (string)state["plotId"],
                    plotId,
                    StringComparison.Ordinal),
                "Causal crop owner state header or plot identity is invalid.");

            fixture.WorldItems.Restore(Deserialize<DungeonPhysicalItemSaveData>(
                state["physicalItems"],
                "physical-item state"));
            CropEcologyWorldSaveData ecology =
                Deserialize<CropEcologyWorldSaveData>(
                    state["cropEcology"],
                    "crop-ecology state");
            fixture.Ecology.PublishRestore(
                fixture.Ecology.PrepareRestore(ecology));
            DungeonCropPlotSaveData crops = Deserialize<DungeonCropPlotSaveData>(
                state["cropPlots"],
                "crop-plot state");
            runtime.Restore(runtime.BuildRestore(crops));
            fixture.Fluid.Restore(
                (float)state["fluidWater"],
                (int)state["fluidConsumed"],
                (int)state["fluidVersion"]);
            fixture.Clock.Restore(
                (int)state["clockDay"],
                (int)state["clockHour"],
                (float)state["clockTime"],
                (float)state["clockDeltaTime"],
                (bool)state["clockRunning"]);
            irrigated = (bool)state["irrigated"];
            harvested = (bool)state["harvested"];
            _ = Snapshot();
        }

        private static JObject Serialize(object value, string label)
        {
            try
            {
                return JObject.Parse(JsonUtility.ToJson(
                    value ?? throw new ArgumentNullException(nameof(value))));
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException
                or ArgumentException)
            {
                throw new InvalidOperationException(
                    "Could not serialize causal crop " + label + ".",
                    exception);
            }
        }

        private static T Deserialize<T>(JToken token, string label)
            where T : class
        {
            T value = JsonUtility.FromJson<T>(
                (token ?? throw new InvalidOperationException(
                    "Causal crop " + label + " is missing."))
                .ToString(Newtonsoft.Json.Formatting.None));
            return value ?? throw new InvalidOperationException(
                "Causal crop " + label + " is invalid.");
        }

        private static void RequireFields(
            JObject value,
            string label,
            params string[] expected)
        {
            string[] actual = (value
                    ?? throw new InvalidOperationException(label + " is missing."))
                .Properties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] wanted = expected
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    label + " fields mismatch. actual="
                    + string.Join(",", actual)
                    + "; expected="
                    + string.Join(",", wanted));
            }
        }

        private void PrepareGrowingCrop() =>
            EnvironmentCropProductionEntryDebugScenarios.PrepareGrowingCrop(
                fixture, runtime, plotId, "action:causal-environment-crop-cycle");

        private CropPlotSnapshot Snapshot() => runtime.Plots.Single(value =>
            string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
    }

    private static void PrepareGrowingCrop(
        Fixture fixture, CropPlotRuntime runtime, string plotId, string operationId)
    {
        CropPlotSnapshot Snapshot() => runtime.Plots.Single(value =>
            string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
        Require(
            runtime.TrySetCrop(
                fixture.Plot,
                fixture.Crop.CropId,
                out string cropFailure),
            "Causal crop setup failed: " + cropFailure);
        Require(
            runtime.TryBindNextCycle(
                operationId,
                plotId,
                fixture.Crop.CropId,
                out string bindFailure),
            "Causal crop correlation failed: " + bindFailure);
        runtime.Tick();
        CropPlotSnapshot waiting = Snapshot();
        foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials)
        {
            if (string.Equals(
                    material.Key,
                    fixture.Crop.SeedItemId,
                    StringComparison.Ordinal))
            {
                for (int index = 0; index < material.Value; index++)
                {
                    fixture.AddSeed(
                        WorldItemStackState.FacilityBuffer,
                        waiting.MaterialDestinationId,
                        fixture.Plot.centerPos,
                        pathogenLoad: 0f);
                }
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
        Require(
            runtime.TryGetWork(
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
            "Causal crop setup did not reach the growing phase.");
        DungeonCropPlotSaveData drySave = runtime.Capture();
        CropPlotSaveData dry = drySave.plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        dry.currentWater = 0f;
        runtime.Restore(runtime.BuildRestore(drySave));
        Require(
            Snapshot().Phase == CropPlotPhase.Growing,
            "Causal crop setup did not preserve its growing owner state.");
    }

    private static void VerifyCertifiedSeedCompletion(
        Fixture fixture,
        bool verifyObserverRejectRecovery = false,
        ManualOutcomeFaultingRecorder recorderFault = null,
        bool verifyObserverPendingRecovery = false)
    {
        string availableSeedStack = fixture.AddSeed(
            WorldItemStackState.Loose,
            string.Empty,
            fixture.CertifiedFacility.centerPos,
            pathogenLoad: 20f);
        WorldItemRepositoryEditorAccess.AddStack(
            fixture.Repository,
            CertificationKitItemId,
            1,
            WorldItemStackState.Loose,
            position: fixture.CertifiedFacility.centerPos);

        CertifiedSeedRuntime runtime = fixture.CreateCertifiedSeedRuntime();
        ICertifiedSeedCommand command = runtime;
        Require(
            command.TryPlan(
                "action:environment-certified-seed-production-entry",
                fixture.Crop.CropId,
                fixture.CertifiedFacility.RequirePersistentInstanceId().Value,
                out DomainFailure planFailure),
            "Certified-seed production planning failed: " + planFailure);

        CertifiedSeedOrderSaveData planned = runtime.Capture().orders.Single();
        string deliveredSeedStack = fixture.AddSeed(
            WorldItemStackState.FacilityBuffer,
            planned.destinationId,
            fixture.CertifiedFacility.centerPos,
            pathogenLoad: 20f);
        string deliveredKitStack = WorldItemRepositoryEditorAccess.AddStack(
            fixture.Repository,
            CertificationKitItemId,
            1,
            WorldItemStackState.FacilityBuffer,
            destinationId: planned.destinationId,
            position: fixture.CertifiedFacility.centerPos);
        int certifiedBefore = fixture.CountCertifiedSeedLots();

        int deliveredSeedQuantity = fixture.CountDelivered(
            fixture.Crop.SeedItemId,
            planned.destinationId);
        int deliveredKitQuantity = fixture.CountDelivered(
            CertificationKitItemId,
            planned.destinationId);
        bool mutable = ProductionFacilityMutationWorkPolicy.TryRequireMutable(
            fixture.FacilityMutations,
            fixture.CertifiedFacility.RequirePersistentInstanceId(),
            out DomainFailure mutationFailure);
        bool ready = runtime.IsPlanReadyForCompletion(planned.actionId);
        DomainFailure readinessFailure = mutable && ready
            ? DomainFailure.None
            : mutationFailure.IsFailure
                ? mutationFailure
                : new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    planned.destinationId,
                    "certified-seed-delivery-not-ready",
                    $"seed={deliveredSeedQuantity};kit={deliveredKitQuantity}");
        Require(
            mutable && ready,
            "Certified-seed completion preconditions failed: "
            + $"ready={ready}; deliveredSeed={deliveredSeedQuantity}; "
            + $"deliveredKit={deliveredKitQuantity}; "
            + $"DomainFailure={FormatFailure(readinessFailure)}.");

        if (verifyObserverPendingRecovery)
        {
            VerifyCertifiedSeedPendingRecovery(fixture, runtime, recorderFault,
                availableSeedStack, deliveredSeedStack, deliveredKitStack, certifiedBefore);
            return;
        }
        if (verifyObserverRejectRecovery)
        {
            VerifyCertifiedSeedObserverRejectRecovery(
                fixture,
                runtime,
                recorderFault,
                availableSeedStack,
                deliveredSeedStack,
                deliveredKitStack,
                certifiedBefore);
            return;
        }

        int completed = command.CompleteDeliveredPlans(fixture.OperatingDay);
        if (completed != 1)
        {
            CertifiedSeedWorldSaveData failed = runtime.Capture();
            CertifiedSeedOrderSaveData failedOrder = failed.orders
                .SingleOrDefault(value => string.Equals(
                    value.actionId,
                    planned.actionId,
                    StringComparison.Ordinal));
            throw new InvalidOperationException(
                "Certified-seed production owner did not complete its "
                + "delivered plan: "
                + $"completed={completed}; readyBefore={ready}; "
                + $"deliveredSeed={deliveredSeedQuantity}; "
                + $"deliveredKit={deliveredKitQuantity}; "
                + $"DomainFailure={FormatFailure(readinessFailure)}; "
                + $"lastProcessedDay={failed.lastProcessedOperatingDay}; "
                + $"orderPresent={failedOrder != null}; "
                + $"phase={failedOrder?.phase}; "
                + $"pendingInput={failedOrder?.pendingInput?.phase}; "
                + $"outputEmpty={failedOrder?.outputPublication?.IsEmpty}; "
                + $"publicationStatus={fixture.OutputPublicationStatus}; "
                + $"publicationFailure={fixture.OutputPublicationFailure}; "
                + $"publicationAttempt={failedOrder?.outputPublication?.publicationAttempt}; "
                + $"publicationOperation={failedOrder?.outputPublication?.publicationOperationId}; "
                + $"admissionToken={failedOrder?.outputPublication?.admissionTokenId}; "
                + $"plannedFingerprint={failedOrder?.outputPublication?.plannedOutputFingerprint}; "
                + $"batchCommit={failedOrder?.outputPublication?.batchCommitId}; "
                + $"outputMass={failedOrder?.outputPublication?.outputMassGrams}; "
                + $"maximumMass={failedOrder?.outputPublication?.maximumBatchMassGrams}; "
                + $"requiredCapacity={failedOrder?.outputPublication?.requiredMinimumCapacityGrams}; "
                + $"capacityRevision={failedOrder?.outputPublication?.capacityRevision}; "
                + $"publishedStackCount={failedOrder?.outputPublication?.stacks?.Count}; "
                + $"observationExpected={failedOrder?.outputPublication?.observationExpected}; "
                + $"outputPublished={failedOrder?.outputPublication?.outputPublished}; "
                + $"admissionCommitted={failedOrder?.outputPublication?.admissionCommitted}; "
                + $"outputAcknowledged={failedOrder?.outputPublication?.outputAcknowledged}.");
        }
        Require(
            runtime.Capture().orders.Count == 0,
            "Certified-seed production owner did not retire its completed order.");
        Require(
            fixture.Repository.GetEditorTestQuantity(deliveredSeedStack) == 0
            && fixture.Repository.GetEditorTestQuantity(deliveredKitStack) == 0,
            "Certified-seed production did not consume its exact physical inputs.");
        Require(
            fixture.Repository.GetEditorTestQuantity(availableSeedStack) == 1,
            "The planning-only source seed was mutated instead of the delivered input.");
        Require(
            fixture.CountCertifiedSeedLots() == certifiedBefore + 1,
            "Certified-seed production did not publish one transformed physical seed lot.");
        fixture.RequirePublished(
            EnvironmentOutcomeIds.CertifiedSeedProducer,
            EnvironmentOutcomeIds.CertifiedSeedCompleted);
    }

    private static void VerifyCertifiedSeedObserverRejectRecovery(
        Fixture fixture,
        CertifiedSeedRuntime runtime,
        ManualOutcomeFaultingRecorder fault,
        string availableSeedStack,
        string deliveredSeedStack,
        string deliveredKitStack,
        int certifiedBefore)
    {
        Require(
            fault != null,
            "Certified-seed observer rejection requires its recorder wrapper.");
        fault.RejectCertifiedSeedPrepare = true;
        int rejectedCompletion = runtime.CompleteDeliveredPlans(
            fixture.OperatingDay);
        CertifiedSeedWorldSaveData rejectedSave = runtime.Capture();
        CertifiedSeedOrderSaveData pending = rejectedSave.orders.Single();
        Require(
            rejectedCompletion == 0
            && fault.CertifiedSeedPrepareRejectionCount == 1
            && pending.phase == CertifiedSeedOrderPhase.InputCommitted
            && pending.pendingInput.phase
                == CropPhysicalCommitPhase.InputCommitted
            && pending.outputPublication is
            {
                outputPublished: false,
                admissionCommitted: false,
                outputAcknowledged: false,
                observationExpected: false
            }
            && !pending.outputPublication.IsEmpty
            && pending.outputPublication.publicationAttempt == 1
            && (pending.outputPublication.stacks?.Count ?? 0) == 0
            && string.IsNullOrEmpty(
                pending.outputPublication.admissionTokenId)
            && string.IsNullOrEmpty(
                pending.outputPublication.plannedOutputFingerprint)
            && string.Equals(
                fixture.OutputPublicationStatus,
                ProductionDomainOutputPublicationStatus.Pending.ToString(),
                StringComparison.Ordinal)
            && fixture.OutputPublicationFailure.Contains(
                "injected-certified-seed-observer-prepare-rejection",
                StringComparison.Ordinal),
            "Rejected certified-seed observer preparation did not retain "
            + "one committed input owner with a released output attempt.");
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.CertifiedSeedProducer,
            EnvironmentOutcomeIds.CertifiedSeedCompleted,
            0);
        Require(
            fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0
            && fixture.CountCertifiedSeedLots() == certifiedBefore
            && fixture.Repository.GetEditorTestQuantity(deliveredSeedStack) == 0
            && fixture.Repository.GetEditorTestQuantity(deliveredKitStack) == 0
            && fixture.Repository.GetEditorTestQuantity(availableSeedStack) == 1,
            "Rejected certified-seed observer preparation published output "
            + "or restored/reconsumed the already committed input batch.");

        fault.RejectCertifiedSeedPrepare = false;
        CertifiedSeedWorldSaveData roundTrip = JsonUtility.FromJson<
            CertifiedSeedWorldSaveData>(JsonUtility.ToJson(rejectedSave));
        CertifiedSeedRuntime resumed = fixture.CreateCertifiedSeedRuntime();
        resumed.Restore(resumed.BuildRestore(roundTrip));
        int resumedDay = fixture.OperatingDay + 1;
        Require(
            resumed.CompleteDeliveredPlans(resumedDay) == 1,
            "Restored certified-seed input owner did not complete once after "
            + "the observer rejection was removed.");
        Require(
            resumed.CompleteDeliveredPlans(resumedDay) == 0,
            "Certified-seed completion repeated on the same operating day.");
        CertifiedSeedWorldSaveData completedSave = resumed.Capture();
        Require(
            completedSave.orders.Count == 0
            && fault.CertifiedSeedPrepareRejectionCount == 1
            && fixture.CountCertifiedSeedLots() == certifiedBefore + 1
            && fixture.Repository.GetEditorTestQuantity(deliveredSeedStack) == 0
            && fixture.Repository.GetEditorTestQuantity(deliveredKitStack) == 0
            && fixture.Repository.GetEditorTestQuantity(availableSeedStack) == 1,
            "Recovered certified-seed completion duplicated input or physical "
            + "output, or retained its terminal owner.");
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.CertifiedSeedProducer,
            EnvironmentOutcomeIds.CertifiedSeedCompleted,
            1);

        CertifiedSeedWorldSaveData terminalRoundTrip = JsonUtility.FromJson<
            CertifiedSeedWorldSaveData>(JsonUtility.ToJson(completedSave));
        CertifiedSeedRuntime terminal = fixture.CreateCertifiedSeedRuntime();
        terminal.Restore(terminal.BuildRestore(terminalRoundTrip));
        Require(
            terminal.CompleteDeliveredPlans(resumedDay + 1) == 0
            && terminal.Capture().orders.Count == 0
            && fixture.CountCertifiedSeedLots() == certifiedBefore + 1
            && fixture.Repository.GetEditorTestQuantity(deliveredSeedStack) == 0
            && fixture.Repository.GetEditorTestQuantity(deliveredKitStack) == 0
            && fault.CertifiedSeedPrepareRejectionCount == 1,
            "Terminal certified-seed restore repeated its domain action, input "
            + "consumption, physical output, or observer preparation.");
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.CertifiedSeedProducer,
            EnvironmentOutcomeIds.CertifiedSeedCompleted,
            1);
    }

    private static void VerifyAutomaticIrrigationAndHarvest(Fixture fixture)
    {
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        BuildableObject plot = fixture.Plot;
        BuildableObject parallelPlot = fixture.ParallelPlot;
        using WorkerFixture sowWorkerA = new(
            "character:w06-sower-a",
            "W06 Sower A",
            6101);
        using WorkerFixture sowWorkerB = new(
            "character:w06-sower-b",
            "W06 Sower B",
            6102);
        using WorkerFixture harvestWorkerA = new(
            "character:w06-harvester-a",
            "W06 Harvester A",
            6103);
        using WorkerFixture harvestWorkerB = new(
            "character:w06-harvester-b",
            "W06 Harvester B",
            6104);
        string plotId = plot.RequirePersistentInstanceId().Value;
        string parallelPlotId = parallelPlot.RequirePersistentInstanceId().Value;
        CropDefinitionSO alternateCrop = fixture.RequireAlternateOutdoorCrop();
        ObserveE08Crop("selection-before", fixture, runtime);
        Require(
            runtime.TrySetCrop(
                plot,
                alternateCrop.CropId,
                out string alternateFailure),
            "Alternate crop selection failed: " + alternateFailure);
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.CropSelectionProducer,
            EnvironmentOutcomeIds.CropSelectionChanged,
            1);
        ObserveE08Crop("selection-changed", fixture, runtime);
        Require(
            runtime.TrySetCrop(
                plot,
                alternateCrop.CropId,
                out string sameCropMessage),
            "Same-crop no-op was rejected: " + sameCropMessage);
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.CropSelectionProducer,
            EnvironmentOutcomeIds.CropSelectionChanged,
            1);
        ObserveE08Crop("selection-same-value", fixture, runtime);
        Require(
            runtime.TrySetCrop(plot, fixture.Crop.CropId, out string cropFailure),
            "Crop production setup failed: " + cropFailure);
        ObserveE08Crop("selection-restored-choice", fixture, runtime);
        Require(
            runtime.TryBindNextCycle(
                "action:environment-crop-production-entry",
                plotId,
                fixture.Crop.CropId,
                out string bindFailure),
            "Crop production correlation failed: " + bindFailure);
        Require(
            runtime.TrySetCrop(
                parallelPlot,
                fixture.Crop.CropId,
                out string parallelCropFailure),
            "Parallel crop production setup failed: " + parallelCropFailure);
        Require(
            runtime.TryBindNextCycle(
                "action:environment-crop-production-entry:parallel",
                parallelPlotId,
                fixture.Crop.CropId,
                out string parallelBindFailure),
            "Parallel crop correlation failed: " + parallelBindFailure);

        runtime.Tick();
        CropPlotSnapshot waiting = runtime.Plots.Single(value =>
            string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
        CropPlotSnapshot parallelWaiting = runtime.Plots.Single(value =>
            string.Equals(value.PlotId, parallelPlotId, StringComparison.Ordinal));
        foreach (CropPlotSnapshot target in new[] { waiting, parallelWaiting })
        foreach (KeyValuePair<string, int> material in target.RequiredMaterials)
        {
            if (string.Equals(
                    material.Key,
                    fixture.Crop.SeedItemId,
                    StringComparison.Ordinal))
            {
                for (int index = 0; index < material.Value; index++)
                {
                    fixture.AddSeed(
                        WorldItemStackState.FacilityBuffer,
                        target.MaterialDestinationId,
                        string.Equals(target.PlotId, plotId, StringComparison.Ordinal)
                            ? plot.centerPos
                            : parallelPlot.centerPos,
                        pathogenLoad: 0f);
                }
            }
            else
            {
                WorldItemRepositoryEditorAccess.AddStack(
                    fixture.Repository,
                    material.Key,
                    material.Value,
                    WorldItemStackState.FacilityBuffer,
                    destinationId: target.MaterialDestinationId,
                    position: string.Equals(
                            target.PlotId,
                            plotId,
                            StringComparison.Ordinal)
                        ? plot.centerPos
                        : parallelPlot.centerPos);
            }
        }

        runtime.Tick();
        Require(
            runtime.TryGetWork(
                plot,
                BuiltInWorkTypeIds.Sow,
                out CropPlotWorkSnapshot sow)
            && sow.Available,
            "Crop sow production entry was unavailable: " + sow.UnavailableReason);
        Require(
            runtime.TryGetWork(
                parallelPlot,
                BuiltInWorkTypeIds.Sow,
                out CropPlotWorkSnapshot parallelSow)
            && parallelSow.Available
            && runtime.ApplyWork(
                parallelPlot,
                BuiltInWorkTypeIds.Sow,
                parallelSow.RequiredWork * 0.3f,
                sowWorkerA.Actor,
                out bool parallelCompleted)
            && !parallelCompleted,
            "Parallel same-day crop contribution was not retained as partial work.");
        Require(runtime.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Sow,
                sow.RequiredWork * 0.4f,
                sowWorkerA.Actor,
                out bool firstSow)
            && !firstSow
            && runtime.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Sow,
                sow.RequiredWork * 0.6f,
                sowWorkerB.Actor,
                out bool sowed)
            && sowed,
            "Crop sow production entry did not commit its physical cycle.");

        DungeonCropPlotSaveData drySave = runtime.Capture();
        CropPlotSaveData dry = drySave.plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        dry.currentWater = 0f;
        runtime.Restore(runtime.BuildRestore(drySave));

        ObserveE08Crop("automatic-dry", fixture, runtime);
        float sourceWaterBefore = fixture.Fluid.Water;
        int consumedBefore = fixture.Fluid.Consumed;
        int irrigationSequenceBefore = dry.nextWaterRefillOperationSequence;
        runtime.Tick();
        CropPlotSaveData irrigated = runtime.Capture().plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        Require(
            Mathf.Approximately(
                fixture.Fluid.Water,
                sourceWaterBefore - CropWaterRules.RefillQuantity)
            && fixture.Fluid.Consumed == consumedBefore + 1,
            "Automatic irrigation did not debit exactly one finite physical water unit.");
        Require(
            Mathf.Approximately(
                irrigated.currentWater,
                CropWaterRules.RefillQuantity)
            && irrigated.waterRefill.phase == CropWaterRefillPhase.None
            && irrigated.nextWaterRefillOperationSequence
                == irrigationSequenceBefore + 1,
            "Crop Tick did not publish the exact irrigation credit and owner sequence.");
        GameplayOutcomeReplayIdentity irrigationIdentity =
            fixture.RequirePublishedIdentity(
            EnvironmentOutcomeIds.CropIrrigationProducer,
            EnvironmentOutcomeIds.CropIrrigationSupplied);
        Require(
            fixture.Ledger.TryGetExact(
                irrigationIdentity.OutcomeId,
                out GameplayOutcomeSnapshot irrigationOutcome)
            && irrigationOutcome.metrics.Any(value =>
                string.Equals(
                    value.metricId,
                    EnvironmentOutcomeIds.NetworkWaterBeforeMetric.Value,
                    StringComparison.Ordinal)
                && Math.Abs(value.value - sourceWaterBefore) < 0.0001d)
            && irrigationOutcome.metrics.Any(value =>
                string.Equals(
                    value.metricId,
                    EnvironmentOutcomeIds.NetworkWaterAfterMetric.Value,
                    StringComparison.Ordinal)
                && Math.Abs(
                    value.value
                    - (sourceWaterBefore - CropWaterRules.RefillQuantity))
                    < 0.0001d)
            && irrigationOutcome.facts.Any(value =>
                string.Equals(
                    value.factId,
                    EnvironmentOutcomeIds.NetworkIdFact.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.value,
                    "network:environment-crop-fixture",
                    StringComparison.Ordinal)),
            "Automatic irrigation lost its exact source-network before/after evidence.");
        ObserveE08Crop("automatic-supplied", fixture, runtime);

        VerifyCropTreatmentOutcomes(fixture, runtime, plot, plotId);

        DungeonCropPlotSaveData readySave = runtime.Capture();
        CropPlotSaveData ready = readySave.plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        ready.phase = CropPlotPhase.ReadyToHarvest;
        ready.growthHours = fixture.Crop.GrowthHours;
        runtime.Restore(runtime.BuildRestore(readySave));

        ObserveE08Crop("automatic-ready-harvest", fixture, runtime);
        int harvestBefore = fixture.CountItem(fixture.Crop.HarvestItemId);
        int seedBefore = fixture.CountItem(fixture.Crop.SeedItemId);
        Require(
            runtime.TryGetWork(
                plot,
                BuiltInWorkTypeIds.Harvest,
                out CropPlotWorkSnapshot harvest)
            && harvest.Available,
            "Crop harvest production entry was unavailable: "
            + harvest.UnavailableReason);
        Require(runtime.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Harvest,
                harvest.RequiredWork * 0.25f,
                harvestWorkerA.Actor,
                out bool firstHarvest)
            && !firstHarvest
            && runtime.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Harvest,
                harvest.RequiredWork * 0.75f,
                harvestWorkerB.Actor,
                out bool harvested)
            && harvested,
            "Crop harvest production entry did not reach terminal publication.");

        CropPlotSaveData terminal = runtime.Capture().plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        Require(
            terminal.pendingHarvest.phase == CropHarvestOutputPhase.None
            && terminal.nextHarvestOperationSequence == 1,
            "Crop harvest did not retire exactly one pending physical-output owner.");
        Require(
            runtime.TryCaptureExecutionReceipt(
                "action:environment-crop-production-entry",
                out CropPlanExecutionReceipt receipt)
            && receipt.Succeeded
            && receipt.Outputs.Count == 2
            && receipt.SowContributions.Count == 2
            && receipt.HarvestContributions.Count == 2
            && string.Equals(
                receipt.HarvesterId,
                harvestWorkerB.Actor.Identity.PersistentId,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(receipt.EcologyBeforeFingerprint)
            && !string.IsNullOrWhiteSpace(receipt.EcologyAfterFingerprint)
            && !receipt.GoldenPrepared
            && receipt.GoldenOutcome == ExtremeRiskOutcome.Normal
            && Mathf.Approximately(receipt.GoldenPrimaryMultiplier, 1f)
            && Mathf.Approximately(receipt.GoldenSecondaryMultiplier, 1f)
            && receipt.GoldenRollHash == 0UL
            && receipt.IrrigationChildResultKeys.Count == 1
            && receipt.WaterBefore >= 0f
            && receipt.WaterAfter >= 0f,
            "Crop harvest did not retain its authoritative terminal execution receipt.");
        CropPlotSaveData parallel = runtime.Capture().plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                parallelPlotId,
                StringComparison.Ordinal));
        Require(
            parallel.cycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.Active
            && parallel.cycleExecutionReceipt.sowContributions.Count == 1
            && string.Equals(
                parallel.cycleExecutionReceipt.sowContributions[0].characterId,
                sowWorkerA.Actor.Identity.PersistentId,
                StringComparison.Ordinal)
            && !string.Equals(
                parallel.cycleExecutionReceipt.sowOperationId,
                receipt.SowOperationId,
                StringComparison.Ordinal),
            "Same-day crop plots did not retain distinct operation/contributor owners.");
        CropPlotSnapshot nextCycle = runtime.Plots.Single(value =>
            string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
        Require(
            nextCycle.Phase == CropPlotPhase.Empty
            && nextCycle.GrowthProgress == 0f,
            "Crop harvest did not expose a readable empty next-cycle snapshot.");
        Require(
            fixture.CountItem(fixture.Crop.HarvestItemId)
                == harvestBefore + receipt.Outputs
                    .Where(value => string.Equals(
                        value.ItemId,
                        fixture.Crop.HarvestItemId,
                        StringComparison.Ordinal))
                    .Sum(value => value.Quantity)
            && fixture.CountItem(fixture.Crop.SeedItemId)
                == seedBefore + receipt.Outputs
                    .Where(value => string.Equals(
                        value.ItemId,
                        fixture.Crop.SeedItemId,
                        StringComparison.Ordinal))
                    .Sum(value => value.Quantity),
            "Crop harvest physical quantities drifted from its terminal receipt.");
        fixture.RequirePublished(
            EnvironmentOutcomeIds.CropPlanProducer,
            EnvironmentOutcomeIds.CropPlanTerminal);
        ObserveE08Crop("automatic-completed", fixture, runtime);
    }

    private static void VerifyManualWaterRefillRecoveryAndExactReceipt(bool hiddenAcknowledgement = false)
    {
        ManualOutcomeFaultingRecorder fault = null;
        using Fixture fixture = new(
            ledgerRunId: "run:environment-crop-manual-water-recovery:" + hiddenAcknowledgement,
            fluidWater: 0f,
            recordPhysicalConsumption: true,
            recorderDecorator: inner =>
                fault = new ManualOutcomeFaultingRecorder(inner));
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        BuildableObject plot = fixture.Plot;
        using WorkerFixture worker = new(
            "character:e08-manual-water-worker",
            "E08 Manual Water Worker",
            6801);
        PrepareSownPlot(fixture, runtime, plot, worker.Actor);

        string plotId = plot.RequirePersistentInstanceId().Value;
        DungeonCropPlotSaveData drySave = runtime.Capture();
        CropPlotSaveData dry = drySave.plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        dry.currentWater = 0f;
        runtime.Restore(runtime.BuildRestore(drySave));
        runtime.Tick();

        CropPlotSaveData waiting = runtime.Capture().plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        Require(
            waiting.waterRefill.phase
                == CropWaterRefillPhase.WaitingForDelivery
            && waiting.waterRefill.quantity == 1
            && waiting.waterRefill.outcomeSchemaVersion
                == CropWaterRefillSaveData.CurrentOutcomeSchemaVersion,
            "Manual crop refill did not create a current-format one-unit owner.");
        ObserveE08Crop("manual-waiting", fixture, runtime);
        fixture.AddItem(
            CropCycleInputRequirementAuthority.CleanWaterItemId,
            1,
            WorldItemStackState.FacilityBuffer,
            waiting.waterRefill.destinationId,
            plot.centerPos);
        runtime.Tick();
        Require(
            runtime.TryGetWork(
                plot,
                BuiltInWorkTypeIds.Treat,
                out CropPlotWorkSnapshot refillWork)
            && refillWork.Available,
            "Delivered manual crop refill did not become workable: "
                + refillWork.UnavailableReason);
        ObserveE08Crop("manual-ready", fixture, runtime);

        int waterItemsBefore = fixture.CountItem(
            CropCycleInputRequirementAuthority.CleanWaterItemId);
        CropCommandProbe probe = new(fixture.domainFacts) { RejectReservation = true };
        runtime.ConstructDomainFacts(probe);
        Require(!runtime.ApplyWork(plot, BuiltInWorkTypeIds.Treat, refillWork.RequiredWork,
                worker.Actor, out bool reserveCompleted) && !reserveCompleted
            && fixture.CountItem(CropCycleInputRequirementAuthority.CleanWaterItemId) == waterItemsBefore
            && runtime.Capture().plots.Single().currentWater == 0f,
            "Rejected manual-water observation reservation consumed material or applied water.");
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropManualWaterProducer,
            EnvironmentOutcomeIds.CropManualWaterRefilled, 0);
        probe.RejectReservation = false;
        probe.HideNextAcknowledgement = hiddenAcknowledgement;
        fault.RejectManualWrite = !hiddenAcknowledgement;
        bool firstAttempt = runtime.ApplyWork(
            plot,
            BuiltInWorkTypeIds.Treat,
            refillWork.RequiredWork,
            worker.Actor,
            out bool firstCompleted);
        DungeonCropPlotSaveData interruptedSave = runtime.Capture();
        CropPlotSaveData interrupted = interruptedSave.plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        Require(
            !firstAttempt
            && !firstCompleted
            && interrupted.waterRefill.phase
                == CropWaterRefillPhase.OutcomePublished
            && Mathf.Approximately(interrupted.currentWater, 1f)
            && Mathf.Approximately(interrupted.waterRefill.waterBefore, 0f)
            && Mathf.Approximately(interrupted.waterRefill.waterAfter, 1f)
            && interrupted.waterRefill.inputQuantity == 1
            && interrupted.waterRefill.inputMassGrams > 0L
            && interrupted.waterRefill.physicalOutcomeOwnerRevision > 0L
            && !string.IsNullOrWhiteSpace(
                interrupted.waterRefill.publicationKey)
            && fixture.CountItem(
                CropCycleInputRequirementAuthority.CleanWaterItemId)
                == waterItemsBefore - 1,
            "Injected parent-write failure did not retain the exact post-physical refill owner.");
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.CropManualWaterProducer,
            EnvironmentOutcomeIds.CropManualWaterRefilled,
            hiddenAcknowledgement ? 1 : 0);
        ObserveE08Crop("manual-post-physical-pending", fixture, runtime);

        CropWaterRefillFact immutableFact = new(plot.RequirePersistentInstanceId(), interrupted.waterRefill);
        CropWaterRefillSaveData returnedSnapshot = immutableFact.Snapshot();
        returnedSnapshot.sourceStackIds.Clear();
        returnedSnapshot.plotDisplayName = "changed outside";
        Require(immutableFact.Snapshot().sourceStackIds.Count > 0
            && immutableFact.Snapshot().plotDisplayName == interrupted.waterRefill.plotDisplayName,
            "Manual-water fact exposes mutable physical or presentation fields.");
        DungeonCropPlotSaveData missingKey = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(interruptedSave));
        missingKey.plots.Single().waterRefill.publicationKey = string.Empty;
        RequireThrows(() => runtime.BuildRestore(missingKey), "Manual-water pending owner accepted a missing publication key.");
        VerifyIrrigationRawSaveFields(runtime, interruptedSave);

        string savedJson = JsonUtility.ToJson(interruptedSave);
        DungeonCropPlotSaveData restoredSave =
            JsonUtility.FromJson<DungeonCropPlotSaveData>(savedJson);
        CropEcologyWorldSaveData ecologySave = fixture.Ecology.Capture();
        fault.RejectManualWrite = false;
        CropPlotRuntime resumed = fixture.CreateCropRuntime(
            allowReplacement: true);
        resumed.ConstructDomainFacts(probe);
        fixture.Ecology.PublishRestore(
            fixture.Ecology.PrepareRestore(ecologySave));
        resumed.Restore(resumed.BuildRestore(restoredSave));
        ObserveE08Crop("manual-restored-pending", fixture, resumed);
        resumed.Tick();

        CropPlotSaveData completed = resumed.Capture().plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        Require(
            completed.waterRefill.phase == CropWaterRefillPhase.None
            && completed.nextWaterRefillOperationSequence
                == interrupted.waterRefill.operationSequence + 1
            && completed.currentWater > 0f
            && completed.currentWater <= 1f
            && fixture.CountItem(
                CropCycleInputRequirementAuthority.CleanWaterItemId)
                == waterItemsBefore - 1
            && completed.cycleExecutionReceipt.irrigationChildResultKeys.Count == 1
            && completed.cycleExecutionReceipt.irrigationChildResultKeys[0]
                .StartsWith(
                    EnvironmentOutcomeIds.CropManualWaterProducer + "|",
                    StringComparison.Ordinal),
            "Restored manual refill did not finish once without a second physical debit: "
                + $"phase={completed.waterRefill.phase}; "
                + $"next={completed.nextWaterRefillOperationSequence}; "
                + $"expectedNext={interrupted.waterRefill.operationSequence + 1}; "
                + $"water={completed.currentWater:R}; "
                + $"items={fixture.CountItem(CropCycleInputRequirementAuthority.CleanWaterItemId)}; "
                + $"children={string.Join(",", completed.cycleExecutionReceipt.irrigationChildResultKeys)}; "
                + $"expectedParent={interrupted.waterRefill.publicationKey}; "
                + $"failure={completed.waterRefill.failureReason}");
        GameplayOutcomeReplayIdentity identity = fixture.RequirePublishedIdentity(
            EnvironmentOutcomeIds.CropManualWaterProducer,
            EnvironmentOutcomeIds.CropManualWaterRefilled);
        Require(identity.ResultKey.ToString() == interrupted.waterRefill.publicationKey
            && probe.LastReplayPublicationKey == interrupted.waterRefill.publicationKey,
            "Manual-water restore changed the saved observer identity.");
        RequireReplayRejected(fixture.domainFacts, interrupted.waterRefill.publicationKey + ":changed",
            immutableFact, "manual-water wrong key");
        CropWaterRefillSaveData changedPayload = immutableFact.Snapshot();
        changedPayload.itemDisplayName += " changed";
        RequireReplayRejected(fixture.domainFacts, interrupted.waterRefill.publicationKey,
            new CropWaterRefillFact(plot.RequirePersistentInstanceId(), changedPayload), "manual-water changed payload");
        Require(
            fixture.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && exact.metrics.Any(value =>
                string.Equals(
                    value.metricId,
                    EnvironmentOutcomeIds.InputQuantityMetric.Value,
                    StringComparison.Ordinal)
                && Math.Abs(value.value - 1d) < 0.0001d)
            && exact.metrics.Any(value =>
                string.Equals(
                    value.metricId,
                    EnvironmentOutcomeIds.WaterBeforeMetric.Value,
                    StringComparison.Ordinal)
                && Math.Abs(value.value) < 0.0001d)
            && exact.metrics.Any(value =>
                string.Equals(
                    value.metricId,
                    EnvironmentOutcomeIds.WaterAfterMetric.Value,
                    StringComparison.Ordinal)
                && Math.Abs(value.value - 1d) < 0.0001d)
            && exact.facts.Any(value =>
                string.Equals(
                    value.factId,
                    EnvironmentOutcomeIds.ChildResultKeyFact.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.value,
                    CropWaterRefillFactMapping.PhysicalChildKey(interrupted.waterRefill.operationId,
                        interrupted.waterRefill.physicalOutcomeOwnerRevision).ToString(),
                    StringComparison.Ordinal))
            && exact.facts.Any(value =>
                string.Equals(
                    value.factId,
                    EnvironmentOutcomeIds.WorkContributorsFact.Value,
                    StringComparison.Ordinal)
                && value.value.Contains(
                    worker.Actor.Identity.PersistentId,
                    StringComparison.Ordinal)),
            "Manual refill parent lost exact water, physical-child or worker evidence.");
        ObserveE08Crop("manual-completed", fixture, resumed);
        resumed.Tick();
        fixture.RequirePublishedCount(EnvironmentOutcomeIds.CropManualWaterProducer,
            EnvironmentOutcomeIds.CropManualWaterRefilled, 1);
        Require(fixture.CountItem(CropCycleInputRequirementAuthority.CleanWaterItemId) == waterItemsBefore - 1,
            "Repeated manual-refill continuation duplicated its physical debit.");
        ObserveE08Crop("manual-replay", fixture, resumed);
    }

    private static void VerifyGoldenHarvestOutcome(
        string scenarioId,
        Func<float, bool> acceptsRoll,
        ExtremeRiskOutcome expectedOutcome)
    {
        using Fixture fixture = new(
            ledgerRunId: "run:environment-crop-golden-" + scenarioId,
            fluidWater: 3f);
        CropPlotRuntime runtime = fixture.CreateCropRuntime();
        BuildableObject plot = fixture.Plot;
        string plotId = plot.RequirePersistentInstanceId().Value;
        string goldenWorkerId = Enumerable.Range(0, 100_000)
            .Select(index =>
                $"character:e08-golden-{scenarioId}:{index:D5}")
            .First(candidate => acceptsRoll(
                GoldenHarvestDeterministicOutcomeAuthority.CaptureRoll01(
                    unchecked((ulong)(uint)fixture.RunSeed),
                    plotId,
                    0,
                    candidate)));
        using WorkerFixture goldenWorker = new(
            goldenWorkerId,
            "E08 Golden " + scenarioId,
            6901,
            new[] { 304 });
        using WorkerFixture otherWorker = new(
            "character:e08-golden-other:" + scenarioId,
            "E08 Other " + scenarioId,
            6902);
        string actionId = "action:e08-golden-" + scenarioId;
        runtime.Tick();
        Require(
            runtime.TryBindNextCycle(
                actionId,
                plotId,
                fixture.Crop.CropId,
                out string bindFailure),
            "Golden harvest cycle binding failed: " + bindFailure);
        PrepareSownPlot(fixture, runtime, plot, goldenWorker.Actor);

        DungeonCropPlotSaveData readySave = runtime.Capture();
        CropPlotSaveData ready = readySave.plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        ready.phase = CropPlotPhase.ReadyToHarvest;
        ready.growthHours = fixture.Crop.GrowthHours;
        runtime.Restore(runtime.BuildRestore(readySave));
        ObserveE08Crop("golden-ready:" + scenarioId, fixture, runtime);
        Require(
            runtime.TryScheduleGoldenHarvest(
                plot,
                goldenWorker.Actor,
                out string scheduleReason),
            "Golden harvest schedule failed: " + scheduleReason);
        ObserveE08Crop("golden-scheduled:" + scenarioId, fixture, runtime);
        Require(
            !runtime.TryScheduleGoldenHarvest(
                plot,
                otherWorker.Actor,
                out _),
            "A second worker replaced an active golden-harvest reservation.");
        ObserveE08Crop("golden-second-worker-rejected:" + scenarioId,
            fixture, runtime);
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.GoldenHarvestScheduleProducer,
            EnvironmentOutcomeIds.GoldenHarvestScheduled,
            1);

        GoldenHarvestRestorePayload activePayload = RoundTripGoldenHarvest(
            new GoldenHarvestRestorePayload
            {
                cropPlots = runtime.Capture(),
                cropEcology = fixture.Ecology.Capture(),
                identityStates = fixture.CaptureIdentityStates().ToList()
            });
        CropPlotRuntime resumed = fixture.CreateCropRuntime(
            allowReplacement: true);
        fixture.Ecology.PublishRestore(
            fixture.Ecology.PrepareRestore(activePayload.cropEcology));
        fixture.RestoreIdentityStates(
            activePayload.identityStates,
            goldenWorker.Actor.Progression.ResolveSelectedTraits());
        resumed.Restore(resumed.BuildRestore(activePayload.cropPlots));
        runtime = resumed;
        CropPlotSaveData restoredActive = runtime.Capture().plots.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                plotId,
                StringComparison.Ordinal));
        Require(
            string.Equals(
                restoredActive.goldenHarvestHarvesterId,
                goldenWorkerId,
                StringComparison.Ordinal)
            && restoredActive.goldenHarvestAttemptSequence == 1
            && runtime.TryGetGoldenHarvestDelay(
                plot,
                goldenWorker.Actor,
                out float restoredRemaining)
            && restoredRemaining > 0f,
            "Active golden-harvest plot and identity owners did not survive JSON restore.");
        ObserveE08Crop("golden-restored-active:" + scenarioId, fixture, runtime);
        runtime.Tick();
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.GoldenHarvestScheduleProducer,
            EnvironmentOutcomeIds.GoldenHarvestScheduled,
            1);
        ObserveE08Crop("golden-active-replay:" + scenarioId, fixture, runtime);

        fixture.Clock.AdvanceGameHours(25f);
        Require(
            !runtime.TryGetGoldenHarvestDelay(
                plot,
                goldenWorker.Actor,
                out float remaining)
            && remaining <= 0f,
            "Golden harvest did not mature after the authored delay.");
        ObserveE08Crop("golden-mature:" + scenarioId, fixture, runtime);
        Require(
            runtime.TryGetWork(
                plot,
                BuiltInWorkTypeIds.Harvest,
                out CropPlotWorkSnapshot harvest)
            && harvest.Available,
            "Golden harvest work was unavailable: "
                + harvest.UnavailableReason);
        int harvestBefore = fixture.CountItem(fixture.Crop.HarvestItemId);
        Require(
            runtime.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Harvest,
                harvest.RequiredWork,
                otherWorker.Actor,
                out bool wrongWorkerCompleted)
            && !wrongWorkerCompleted
            && fixture.CountItem(fixture.Crop.HarvestItemId) == harvestBefore,
            "A worker different from the scheduled golden harvester completed the roll.");
        ObserveE08Crop("golden-wrong-worker:" + scenarioId, fixture, runtime);
        Require(
            runtime.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Harvest,
                0.1f,
                goldenWorker.Actor,
                out bool completed)
            && completed,
            "The scheduled golden harvester did not complete the mature roll.");
        Require(
            runtime.TryCaptureExecutionReceipt(
                actionId,
                out CropPlanExecutionReceipt receipt)
            && receipt.Succeeded
            && receipt.GoldenPrepared
            && receipt.GoldenOutcome == expectedOutcome
            && string.Equals(
                receipt.HarvesterId,
                goldenWorkerId,
                StringComparison.Ordinal)
            && receipt.GoldenRollHash
                == GoldenHarvestDeterministicOutcomeAuthority.CaptureRollHash(
                    unchecked((ulong)(uint)fixture.RunSeed),
                    plotId,
                    0,
                    goldenWorkerId)
            && !string.IsNullOrWhiteSpace(
                receipt.GoldenTraitDefinitionId)
            && !string.IsNullOrWhiteSpace(
                receipt.GoldenOutcomeFingerprint)
            && !string.IsNullOrWhiteSpace(
                receipt.EcologyBeforeFingerprint)
            && !string.IsNullOrWhiteSpace(
                receipt.EcologyAfterFingerprint)
            && (expectedOutcome != ExtremeRiskOutcome.Loss
                || receipt.GoldenPrimaryMultiplier < 1f
                    && receipt.GoldenSecondaryMultiplier < 1f),
            "Golden harvest terminal receipt lost its exact actor, ecology or deterministic roll evidence.");
        GameplayOutcomeReplayIdentity terminalIdentity =
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.CropPlanProducer,
                EnvironmentOutcomeIds.CropPlanTerminal);
        Require(
            fixture.Ledger.TryGetExact(
                terminalIdentity.OutcomeId,
                out GameplayOutcomeSnapshot terminal)
            && terminal.facts.Any(value =>
                string.Equals(
                    value.factId,
                    EnvironmentOutcomeIds.HarvesterFact.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.value,
                    goldenWorkerId,
                    StringComparison.Ordinal))
            && terminal.facts.Any(value =>
                string.Equals(
                    value.factId,
                    EnvironmentOutcomeIds.GoldenOutcomeFact.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.value,
                    expectedOutcome.ToString(),
                    StringComparison.Ordinal)),
            "Golden harvest gameplay outcome lost its scheduled harvester or roll result.");
        ObserveE08Crop("golden-completed:" + scenarioId, fixture, runtime);

        int harvestAfter = fixture.CountItem(fixture.Crop.HarvestItemId);
        GoldenHarvestRestorePayload terminalPayload = RoundTripGoldenHarvest(
            new GoldenHarvestRestorePayload
            {
                cropPlots = runtime.Capture(),
                cropEcology = fixture.Ecology.Capture(),
                identityStates = fixture.CaptureIdentityStates().ToList()
            });
        CropPlotRuntime replayed = fixture.CreateCropRuntime(
            allowReplacement: true);
        fixture.Ecology.PublishRestore(
            fixture.Ecology.PrepareRestore(terminalPayload.cropEcology));
        fixture.RestoreIdentityStates(
            terminalPayload.identityStates,
            goldenWorker.Actor.Progression.ResolveSelectedTraits());
        replayed.Restore(replayed.BuildRestore(terminalPayload.cropPlots));
        replayed.Tick();
        Require(
            replayed.TryCaptureExecutionReceipt(
                actionId,
                out CropPlanExecutionReceipt restoredReceipt)
            && restoredReceipt.Succeeded
            && restoredReceipt.GoldenPrepared
            && restoredReceipt.GoldenOutcome == expectedOutcome
            && restoredReceipt.GoldenRollHash == receipt.GoldenRollHash
            && string.Equals(
                restoredReceipt.HarvesterId,
                goldenWorkerId,
                StringComparison.Ordinal)
            && fixture.CountItem(fixture.Crop.HarvestItemId) == harvestAfter,
            "Terminal golden-harvest restore lost its receipt or duplicated physical output.");
        fixture.RequirePublishedCount(
            EnvironmentOutcomeIds.CropPlanProducer,
            EnvironmentOutcomeIds.CropPlanTerminal,
            1);
        ObserveE08Crop("golden-terminal-restored:" + scenarioId, fixture, replayed);
    }

    private static GoldenHarvestRestorePayload RoundTripGoldenHarvest(
        GoldenHarvestRestorePayload source)
    {
        string json = JsonUtility.ToJson(
            source ?? throw new ArgumentNullException(nameof(source)));
        GoldenHarvestRestorePayload restored =
            JsonUtility.FromJson<GoldenHarvestRestorePayload>(json);
        return restored ?? throw new InvalidOperationException(
            "Golden-harvest save payload did not survive JSON round trip.");
    }

    private static void PrepareSownPlot(
        Fixture fixture,
        CropPlotRuntime runtime,
        BuildableObject plot,
        CharacterActor worker)
    {
        string plotId = plot.RequirePersistentInstanceId().Value;
        runtime.Tick();
        CropPlotSnapshot waiting = runtime.Plots.Single(value =>
            string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
        foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials)
        {
            if (string.Equals(
                    material.Key,
                    fixture.Crop.SeedItemId,
                    StringComparison.Ordinal))
            {
                for (int index = 0; index < material.Value; index++)
                {
                    fixture.AddSeed(
                        WorldItemStackState.FacilityBuffer,
                        waiting.MaterialDestinationId,
                        plot.centerPos,
                        pathogenLoad: 0f);
                }
            }
            else
            {
                fixture.AddItem(
                    material.Key,
                    material.Value,
                    WorldItemStackState.FacilityBuffer,
                    waiting.MaterialDestinationId,
                    plot.centerPos);
            }
        }
        runtime.Tick();
        Require(
            runtime.TryGetWork(
                plot,
                BuiltInWorkTypeIds.Sow,
                out CropPlotWorkSnapshot sow)
            && sow.Available
            && runtime.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Sow,
                sow.RequiredWork,
                worker,
                out bool sown)
            && sown,
            "Crop sow setup for the E08 focused scenario failed.");
    }

    private sealed class WorkerFixture : IDisposable
    {
        private readonly GameObject host;
        private readonly CharacterSO data;

        internal WorkerFixture(
            string persistentId,
            string displayName,
            int generationSeed,
            IReadOnlyList<int> traitIds = null)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                "human");
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            host = new GameObject("W06 Crop Worker " + persistentId);
            host.AddComponent<SpriteRenderer>();
            Actor = host.AddComponent<CharacterActor>();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(host);
            Actor.Identity.SetPersistentId(persistentId);
            Actor.Initialization(data);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                "w06-crop-worker",
                traitIds?.ToArray() ?? Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                generationSeed,
                autoChooseDrafts: false,
                maximumTraitCount: 4,
                ensureInitialDrafts: false);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            Actor.Stats.Stats[CharacterCondition.MOOD] = 70f;
            _ = Actor.Mood;
        }

        internal CharacterActor Actor { get; }

        public void Dispose()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
            if (data != null)
                UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Serializable]
    private sealed class GoldenHarvestRestorePayload
    {
        public DungeonCropPlotSaveData cropPlots = new();
        public CropEcologyWorldSaveData cropEcology = new();
        public List<CharacterIdentityRuntimeStateSaveData> identityStates = new();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string FormatFailure(in DomainFailure failure) =>
        failure.Code + (failure.Parameters.Length == 0
            ? string.Empty
            : ":" + string.Join("|", failure.Parameters.ToArray()));

    private static T Proxy<T>() where T : class =>
        DispatchProxy.Create<T, DefaultInterfaceProxy>();

    public class DefaultInterfaceProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class DiagnosticOutputPublication :
        IProductionDomainOutputPublicationService,
        IObservedProductionDomainOutputPublicationService
    {
        private readonly ProductionDomainOutputPublicationService inner;

        internal DiagnosticOutputPublication(
            ProductionDomainOutputPublicationService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal ProductionDomainOutputPublicationStatus? LastStatus
        {
            get;
            private set;
        }

        internal string LastFailure { get; private set; } = string.Empty;

        public ProductionDomainOutputPublicationResult EnsureCommitted(
            ProductionDomainOutputPublicationSaveData owner,
            ProductionDomainOutputPublicationPlan plan) => Record(
            inner.EnsureCommitted(owner, plan));

        public ProductionDomainOutputPublicationResult EnsureCommitted<TFact>(
            ProductionDomainOutputPublicationSaveData owner,
            ProductionDomainOutputPublicationPlan plan,
            Func<ProductionDomainOutputFactPreview, TFact> capture) =>
            Record(inner.EnsureCommitted(owner, plan, capture));

        public bool TryRecover<TFact>(
            ProductionDomainOutputPublicationSaveData owner,
            string ownerId,
            Func<ProductionDomainOutputFactPreview, TFact> capture,
            out string failureReason)
        {
            bool recovered = inner.TryRecover(
                owner,
                ownerId,
                capture,
                out failureReason);
            if (!recovered)
                LastFailure = failureReason ?? string.Empty;
            return recovered;
        }

        public bool TryAcknowledge(
            ProductionDomainOutputPublicationSaveData owner,
            out string failureReason)
        {
            bool acknowledged = inner.TryAcknowledge(
                owner,
                out failureReason);
            if (!acknowledged)
                LastFailure = failureReason ?? string.Empty;
            return acknowledged;
        }

        private ProductionDomainOutputPublicationResult Record(
            ProductionDomainOutputPublicationResult result)
        {
            LastStatus = result.Status;
            LastFailure = result.FailureReason;
            return result;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly IResourceEconomyContentCatalog economy;
        private readonly IStockQuery stock;
        private readonly IProductionItemGateway productionItems;
        private readonly IPhysicalSeedLotGateway seedLots;
        private readonly IPhysicalFacilityItemSinkGateway itemSink;
        private readonly IPackagedLotTareDispositionService packagedTare;
        private readonly IProductionOutputCapabilityRegistry outputCapabilities;
        private readonly DiagnosticOutputPublication outputPublication;
        internal readonly FacilityBufferMassAdmissionService outputAdmission;
        private readonly ICropPlotInputOwnerRuntime cropInputOwners;
        private readonly ICertifiedSeedInputOwnerRuntime certifiedInputOwners;
        internal readonly DomainFactDispatcher domainFacts;
        internal readonly WorkCompletionIdentityDeliveryLedger CompletionDeliveries = new();
        internal readonly IWorkCompletionIdentityDeliveryCommand CompletionCommand;
        private readonly ProductionFacilityMutationEpochRuntime facilityMutations = new();
        private readonly IGameContentCatalog content;
        private readonly FixedClock clock = new(23);
        private readonly FixedFacilities certifiedFacilities;
        private readonly FixedFacilities irrigationFacilities;
        private readonly BlueprintResearchRuntime research;
        private readonly CharacterIdentityStateStore identityStates;
        private readonly ExtremeTraitRuntime extremeTraits;
        private readonly FixedRunSeedProvider runSeedProvider;

        private readonly float plotGrowthRate;
        private readonly float plotWaterRate;
        private readonly float outdoorTemperature;

        internal Fixture(
            string ledgerRunId = "run:environment-crop-production-entry",
            float plotGrowthRate = 1f,
            float plotWaterRate = 1f,
            float fluidWater = 3f,
            Func<IGameplayOutcomeRecorder, IGameplayOutcomeRecorder>
                recorderDecorator = null,
            Func<IPackagedLotTareDispositionService,
                IPackagedLotTareDispositionService> tareDecorator = null,
            float outdoorTemperature = 20f,
            bool includeParallelPlot = false,
            GameplayOutcomeRegistry sharedRegistry = null,
            GameplayOutcomeLedger sharedLedger = null,
            bool recordPhysicalConsumption = false,
            Func<GameplayOutcomeLedger, IGameplayOutcomeRecorder, GameEventBus,
                WorkCompletionIdentityDeliveryLedger, IWorkCompletionIdentityDeliveryCommand> completionFactory = null)
        {
            if ((sharedRegistry == null) != (sharedLedger == null))
                throw new ArgumentException("A shared crop ledger requires its matching registry.");
            identityStates = new CharacterIdentityStateStore();
            extremeTraits = new ExtremeTraitRuntime(identityStates);
            runSeedProvider = new FixedRunSeedProvider(67211);
            this.plotGrowthRate = plotGrowthRate;
            this.plotWaterRate = plotWaterRate;
            this.outdoorTemperature = outdoorTemperature;
            content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            economy = new ResourceEconomyContentCatalog(content);
            Require(economy.TryGetCrop(CropId, out CropDefinitionSO crop),
                "Authored crop is missing: " + CropId);
            Crop = crop;

            Events = new GameEventBus();
            IKoreanJosaFormatter josa = new KoreanJosaFormatter();
            GameplayOutcomeRegistry outcomeRegistry = sharedRegistry ?? new(
                new IGameplayOutcomeDescriptor[]
                {
                    new CertifiedSeedCompletionOutcomeDescriptor(josa),
                    new CropPlanOutcomeDescriptor(josa),
                    new CropIrrigationSupplyOutcomeDescriptor(josa),
                    new CropSelectionCommandOutcomeDescriptor(josa),
                    new GoldenHarvestScheduleOutcomeDescriptor(josa),
                    new CropManualWaterRefillOutcomeDescriptor(josa),
                    new CropTreatmentOutcomeDescriptor(josa),
                    new WorkCompletionIdentityOutcomeDescriptor(josa),
                    new TradeInventoryOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CertifiedSeedCompletionOutcomeAdapter(),
                    new CropPlanGameplayOutcomeAdapter(),
                    new CropIrrigationSupplyOutcomeAdapter(),
                    new CropSelectionCommandOutcomeAdapter(),
                    new GoldenHarvestScheduleOutcomeAdapter(),
                    new CropManualWaterRefillOutcomeAdapter(),
                    new CropTreatmentOutcomeAdapter(),
                    new WorkCompletionIdentityOutcomeAdapter(),
                    new TradeInventoryOutcomeAdapter()
                });
            Ledger = sharedLedger ?? new GameplayOutcomeLedger(
                outcomeRegistry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            IGameplayOutcomeRecorder recorder = new GameplayOutcomeRecorder(
                Ledger,
                outcomeRegistry,
                Events);
            if (recorderDecorator != null)
                recorder = recorderDecorator(recorder);
            GameplayDomainFactCommitGroup factCommits = new(
                recorder,
                Ledger);
            domainFacts = new DomainFactDispatcher();
            if (recordPhysicalConsumption)
            {
                domainFacts.Subscribe(
                    "physical-disposition",
                    new GameplayDomainFactObserver<
                        PhysicalItemDisposedFact,
                        TradeInventoryOutcomeReceipt>(
                        PhysicalItemDispositionFactObservation.Capture,
                        outcomeRegistry,
                        Ledger,
                        recorder,
                        factCommits));
            }

            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            WorldItems = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    itemCatalog,
                    new DungeonRuntimeAggregateRootStore(),
                    out WorldItemRepository repository,
                    out _,
                    out ItemQuantityReservationService reservations,
                    out _,
                    out _,
                    out _,
                    out _,
                    batchDispositionFactory: !recordPhysicalConsumption ? null : (r, m, q) =>
                        new PhysicalItemBatchDispositionService(r, m, EditorNullItemMarkerPresenter.Instance,
                            q, itemCatalog, domainFacts, domainFacts,
                            new GameplayDomainFactDeliveryQuery(recorder, Ledger), clock));
            Repository = repository;
            stock = new PhysicalStockQuery(
                Repository,
                itemCatalog,
                WorldItems.MassQuery);

            IItemTransferService planningTransfers =
                DispatchProxy.Create<IItemTransferService, PlanningTransferPort>();
            IFacilityBufferDestinationReleaseService releases =
                new FacilityBufferDestinationReleaseService(
                    WorldItems,
                    planningTransfers,
                    EmptyCharacterWorld.Instance);
            seedLots = new PhysicalSeedLotGateway(
                stock,
                planningTransfers,
                WorldItems,
                ReachableDeliveryQuery.Instance,
                releases);
            productionItems = new ProductionItemGateway(
                stock,
                planningTransfers,
                WorldItems,
                itemCatalog,
                releases);
            itemSink = new PhysicalFacilityItemSinkGateway(
                stock,
                new PhysicalItemBatchDispositionService(
                    Repository,
                    WorldItems.MassQuery,
                    EditorNullItemMarkerPresenter.Instance,
                    reservations));
            IPackagedLotTareDispositionService realTare =
                new PackagedLotTareDispositionService(
                    (IPackagedLotDefinitionQuery)WorldItems.MassQuery,
                    new PackagedLotTareOutputGateway(WorldItems));
            packagedTare = tareDecorator != null
                ? tareDecorator(realTare)
                : realTare;

            FacilityBufferDestinationClaimRegistry claims = new();
            FacilityBufferPhysicalOccupancyQuery occupancy = new(
                Repository,
                WorldItems.MassQuery,
                reservations);
            FacilityBufferMassAdmissionService admission = new(
                claims,
                occupancy,
                WorldItems.MassQuery);
            outputAdmission = admission;
            FacilityBufferDestinationLifecycleService lifecycle = new(
                claims,
                claims,
                admission,
                admission);
            certifiedInputOwners = new CertifiedSeedInputOwnerRuntime(
                WorldItems.MassQuery,
                claims,
                admission,
                lifecycle,
                releases);
            cropInputOwners = new CropPlotInputOwnerRuntime(
                WorldItems.MassQuery,
                claims,
                admission,
                lifecycle,
                releases);

            ProductionPreparedOutputComponentCodec componentCodec = new();
            StandardDefinitionProductionOutputCapability standard = new(
                economy,
                componentCodec);
            CertifiedSeedOutputCapability certified = new(economy);
            CropHarvestSeedLotOutputCapability returnedSeed = new(economy);
            IProductionOutputCapability[] capabilities =
            {
                standard,
                certified,
                returnedSeed
            };
            outputCapabilities = new ProductionOutputHandlerRegistry(capabilities);
            ProductionOutputMaximumMassRegistry maximumMass = new(
                capabilities.Cast<IProductionOutputMaximumMassCapability>(),
                WorldItems.MassQuery);

            Bridge = new EnvironmentGameplayOutcomeBridge(
                recorder,
                Ledger,
                Ledger);
            var workIdentityMapping = new WorkIdentityAppliedFactMapping(
                Ledger,
                Ledger);
            CompletionCommand = completionFactory == null
                ? new UnavailableRecipientCompletion(CompletionDeliveries)
                : completionFactory(Ledger, recorder, Events, CompletionDeliveries);
            domainFacts.Subscribe<CropHarvestCompletedFact>("fixture:crop-completion",
                new WorkCompletionDomainFactObserver<CropHarvestCompletedFact>(
                    CropHarvestCompletionObservation.CreateRequest, CompletionCommand,
                    CompletionDeliveries, new CharacterIdentityEventPublisher(Events),
                    WorkCompletionNotificationMode.ApplyOnly,
                    workIdentityMapping));
            domainFacts.Subscribe<CropPlotRetiredFact>("fixture:crop-retired", new CropPlotRetirementObserver(CompletionCommand));
            if (outcomeRegistry.TryGetAdapter<CropPlanGameplayOutcomeReceipt>(out _))
                domainFacts.Subscribe<CropCycleTerminalFact>("fixture:crop-cycle-terminal",
                    new GameplayDomainFactObserver<CropCycleTerminalFact, CropPlanGameplayOutcomeReceipt>(
                        CropCycleTerminalFactMapping.Capture, outcomeRegistry, Ledger, recorder, factCommits));
            if (outcomeRegistry.TryGetAdapter<CropTreatmentOutcomeReceipt>(out _))
                domainFacts.Subscribe<CropTreatmentFact>("fixture:crop-treatment",
                    new GameplayDomainFactObserver<CropTreatmentFact, CropTreatmentOutcomeReceipt>(
                        CropTreatmentFactMapping.Capture, outcomeRegistry, Ledger, recorder, factCommits,
                        CropTreatmentFactMapping.Reserve));
            if (outcomeRegistry.TryGetAdapter<CropManualWaterRefillOutcomeReceipt>(out _))
                domainFacts.Subscribe<CropWaterRefillFact>(
                    "fixture:crop-manual-water",
                    new GameplayDomainFactObserver<CropWaterRefillFact, CropManualWaterRefillOutcomeReceipt>(
                        new CropWaterRefillFactMapping(Ledger).Capture,
                        outcomeRegistry, Ledger, recorder, factCommits, CropWaterRefillFactMapping.Reserve));
            if (outcomeRegistry.TryGetAdapter<CropIrrigationSupplyOutcomeReceipt>(out _))
                domainFacts.Subscribe<CropIrrigationSuppliedFact>(
                    "fixture:crop-irrigation",
                    new GameplayDomainFactObserver<CropIrrigationSuppliedFact, CropIrrigationSupplyOutcomeReceipt>(
                        CropIrrigationFactMapping.Capture, outcomeRegistry, Ledger, recorder, factCommits));
            if (outcomeRegistry.TryGetAdapter<CropSelectionCommandReceipt>(out _))
                domainFacts.Subscribe<CropSelectionChangedFact>(
                    "fixture:crop-selection",
                    new GameplayDomainFactObserver<CropSelectionChangedFact, CropSelectionCommandReceipt>(
                        CropCommandFactMapping.Capture, outcomeRegistry, Ledger, recorder, factCommits));
            if (outcomeRegistry.TryGetAdapter<GoldenHarvestScheduleReceipt>(out _))
                domainFacts.Subscribe<GoldenHarvestScheduledFact>(
                    "fixture:golden-harvest-schedule",
                    new GameplayDomainFactObserver<GoldenHarvestScheduledFact, GoldenHarvestScheduleReceipt>(
                        CropCommandFactMapping.Capture, outcomeRegistry, Ledger, recorder, factCommits));
            if (outcomeRegistry.TryGetAdapter<
                CertifiedSeedCompletionOutcomeReceipt>(out _))
            {
                domainFacts.Subscribe<CertifiedSeedCompletedFact>(
                    "fixture:environment-certified-seed-completed",
                    new GameplayDomainFactObserver<
                        CertifiedSeedCompletedFact,
                        CertifiedSeedCompletionOutcomeReceipt>(
                            CertifiedSeedFactObserverMapping.Capture,
                            outcomeRegistry,
                            Ledger,
                            recorder,
                            factCommits));
            }
            FacilityBufferPlannedOutputPublicationService physicalPublication =
                new(
                    Repository,
                    itemCatalog,
                    WorldItems.MassQuery,
                    admission);
            ProductionOutputDestinationAuthorityRuntime destinations = new(
                claims,
                admission,
                claims,
                admission,
                lifecycle);
            ProductionDomainOutputPublicationService realOutputPublication = new(
                new ProductionFacilityHandleQueryAdapter(),
                destinations,
                new FixedCapacityProjector(),
                maximumMass,
                admission,
                physicalPublication,
                WorldItems.MassQuery,
                physicalPublication,
                admission,
                domainFacts,
                domainFacts,
                new GameplayDomainFactDeliveryQuery(recorder, Ledger));
            outputPublication = new DiagnosticOutputPublication(
                realOutputPublication);

            Grid grid = new(24, 3);
            for (int y = 0; y < grid.height; y++)
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, y),
                    GridCellAreaType.ExteriorPath);
            }

            CertifiedFacility = CreateCertifiedFacility(grid);
            Plot = CreateCropPlot(
                grid,
                97102,
                "EnvironmentCropPlot",
                "생산 진입 경작지",
                "building:environment-crop-plot",
                new Vector2Int(10, 1));
            if (includeParallelPlot)
            {
                ParallelPlot = CreateCropPlot(
                    grid,
                    97104,
                    "EnvironmentCropParallelPlot",
                    "생산 진입 병렬 경작지",
                    "building:environment-crop-plot-parallel",
                    new Vector2Int(12, 1));
            }
            BuildableObject irrigator = CreateIrrigator(grid);
            certifiedFacilities = new FixedFacilities(CertifiedFacility);
            irrigationFacilities = new FixedFacilities(irrigator);

            GameObject researchObject = new(
                "EnvironmentCropProductionEntryResearch");
            cleanup.Add(researchObject);
            research = researchObject.AddComponent<BlueprintResearchRuntime>();
            if (!string.IsNullOrWhiteSpace(Crop.RequiredResearchId))
            {
                research.State.Projects.Complete(
                    new ResearchProjectId(Crop.RequiredResearchId));
            }
            Fluid = new ControlledFluid { Water = fluidWater };
        }

        internal WorldItemRepository Repository { get; }
        internal WorldItemStackRuntime WorldItems { get; }
        internal IResourceEconomyContentCatalog Economy => economy;
        internal CropDefinitionSO Crop { get; }
        internal BuildableObject CertifiedFacility { get; }
        internal BuildableObject Plot { get; }
        internal BuildableObject ParallelPlot { get; }
        internal ControlledFluid Fluid { get; }
        internal GameEventBus Events { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal CropEcologyRuntime Ecology { get; private set; }
        internal EnvironmentGameplayOutcomeBridge Bridge { get; }
        internal string OutputPublicationStatus =>
            outputPublication.LastStatus?.ToString() ?? string.Empty;
        internal string OutputPublicationFailure =>
            outputPublication.LastFailure;
        internal int OperatingDay => clock.Day;
        internal FixedClock Clock => clock;
        internal IProductionFacilityMutationEpochQuery FacilityMutations =>
            facilityMutations;
        internal int RunSeed => runSeedProvider.RunSeed;

        internal IReadOnlyList<CharacterIdentityRuntimeStateSaveData>
            CaptureIdentityStates() => identityStates.Capture();

        internal void RestoreIdentityStates(
            IEnumerable<CharacterIdentityRuntimeStateSaveData> source,
            IEnumerable<CharacterTraitSO> traitCatalog) =>
            identityStates.Restore(source, traitCatalog);

        internal CropDefinitionSO RequireAlternateOutdoorCrop()
        {
            CropDefinitionSO alternate = economy.Crops.FirstOrDefault(value =>
                value != null
                && !string.Equals(
                    value.CropId,
                    Crop.CropId,
                    StringComparison.Ordinal));
            Require(alternate != null,
                "The crop fixture requires a second authored crop.");
            if (!string.IsNullOrWhiteSpace(alternate.RequiredResearchId))
            {
                research.State.Projects.Complete(
                    new ResearchProjectId(alternate.RequiredResearchId));
            }
            return alternate;
        }

        internal CertifiedSeedRuntime CreateCertifiedSeedRuntime() => new(
            certifiedFacilities,
            new FixedBuildingWorld(CertifiedFacility),
            economy,
            stock,
            productionItems,
            DispatchProxy.Create<IItemTransferService, PlanningTransferPort>(),
            seedLots,
            outputCapabilities,
            outputPublication,
            outputPublication,
            clock,
            new DungeonRuntimeAggregateRootStore(),
            facilityMutations,
            certifiedInputOwners);

        internal CropPlotRuntime CreateCropRuntime(bool allowReplacement = false)
        {
            if (Ecology != null && !allowReplacement)
            {
                throw new InvalidOperationException(
                    "The crop fixture can compose only one crop runtime.");
            }
            Ecology = new CropEcologyRuntime(
                new DungeonRuntimeAggregateRootStore(),
                content,
                new RandomStreamProvider(67211),
                stock);
            CropIrrigationRuntime irrigation = new(
                irrigationFacilities,
                new ControlledResearch(),
                Fluid,
                Fluid,
                Fluid,
                clock);
            CropPlotWorldDependencies world = new(
                new FixedBuildingWorld(Plot, ParallelPlot),
                economy,
                new CropCycleInputRequirementAuthority(economy),
                productionItems,
                seedLots,
                itemSink,
                packagedTare,
                Ecology,
                Ecology,
                outputCapabilities,
                outputPublication,
                cropInputOwners,
                FixedPerformanceMaximum.Instance,
                FixedEffectBounds.Instance,
                irrigationFacilities,
                irrigation,
                Proxy<IFacilityCandidateCache>(),
                Proxy<IWorkforceReplanService>());
            CropPlotSimulationDependencies simulation = new(
                clock,
                clock,
                new ProgressionSceneRuntimeReferences(null, research, null),
                Proxy<IGameSessionStateProvider>(),
                new FixedSurvivalEnvironment(outdoorTemperature),
                FixedEnvironmentalField.Instance,
                Proxy<IGrandProjectBenefitQuery>(),
                Events,
                Proxy<ISeasonalEventQuery>(),
                new V20StoryContentCatalog(content),
                extremeTraits,
                runSeedProvider);
            CropPlotRuntime runtime = new(
                world,
                simulation,
                new DungeonRuntimeAggregateRootStore(),
                facilityMutations,
                NeutralMilestoneGameplayModifierQuery.Instance,
                FixedCharacterPerformance.Instance);
            runtime.ConstructDomainFacts(domainFacts);
            return runtime;
        }

        internal string AddSeed(
            WorldItemStackState state,
            string destinationId,
            Vector2Int position,
            float pathogenLoad) => WorldItemRepositoryEditorAccess.AddStack(
            Repository,
            Crop.SeedItemId,
            1,
            state,
            destinationId: destinationId,
            position: position,
            components: new[]
            {
                SeedLotItemStateCodec.Encode(new SeedLotState
                {
                    cropId = Crop.CropId,
                    cultivarGenomeId = Crop.BaseGenome.GenomeId,
                    generation = 0,
                    pathogenLoad = pathogenLoad
                })
            });

        internal int CountItem(string itemId) => WorldItems.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .Sum(value => value.Quantity);

        internal bool TryGetPendingTreatmentReceipt(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            itemSink.TryGetPending(operationId, out receipt);

        internal bool TryGetPackagedLot(
            string itemId,
            out PackagedLotDefinitionSnapshot packagedLot) =>
            ((IPackagedLotDefinitionQuery)WorldItems.MassQuery).TryGetPackagedLot(
                (ItemDefinitionId)itemId,
                out packagedLot);

        internal PhysicalMassGrams GetDefinitionUnitMass(string itemId) =>
            WorldItems.MassQuery.GetDefinitionUnitMass((ItemDefinitionId)itemId);

        internal bool EnsureTreatmentTareReceipt(
            string itemId,
            int quantity,
            Vector2Int position,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason) =>
            packagedTare.EnsureTerminalSinkOutputs(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [itemId] = quantity
                },
                position,
                parentCommitId,
                out receipt,
                out failureReason);

        internal int CountDelivered(string itemId, string destinationId) =>
            productionItems.CountDelivered(itemId, destinationId);

        internal int CountCertifiedSeedLots() => WorldItems.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    Crop.SeedItemId,
                    StringComparison.Ordinal))
            .Sum(value =>
            {
                try
                {
                    SeedLotState seed = SeedLotItemStateCodec.Decode(value.Components);
                    return seed.generation == 0
                        && Mathf.Approximately(seed.pathogenLoad, 0f)
                            ? value.Quantity
                            : 0;
                }
                catch (InvalidOperationException)
                {
                    return 0;
                }
            });

        internal string AddItem(
            string itemId,
            int quantity,
            WorldItemStackState state,
            string destinationId,
            Vector2Int position) => WorldItemRepositoryEditorAccess.AddStack(
            Repository,
            itemId,
            quantity,
            state,
            destinationId: destinationId,
            position: position);

        internal void RequirePublished(
            string producerId,
            GameplayOutcomeTypeId outcomeTypeId) =>
            _ = RequirePublishedIdentity(producerId, outcomeTypeId);

        internal void RequirePublishedCount(
            string producerId,
            GameplayOutcomeTypeId outcomeTypeId,
            int expected)
        {
            GameplayOutcomeSnapshot[] matches = Ledger.CaptureGameplayOutcomes()
                .exactOutcomes
                .Where(value => value != null
                    && string.Equals(
                        value.producerId,
                        producerId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.outcomeTypeId,
                        outcomeTypeId.Value,
                        StringComparison.Ordinal))
                .ToArray();
            Require(matches.Length == expected,
                $"Expected {expected} exact {producerId}/{outcomeTypeId.Value} outcomes, found {matches.Length}.");
            Require(matches.All(value => value.lifecycle
                        == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
                    && value.status == GameplayOutcomeStatus.Succeeded),
                $"{producerId}/{outcomeTypeId.Value} contains a non-terminal result.");
        }

        internal GameplayOutcomeReplayIdentity RequirePublishedIdentity(
            string producerId,
            GameplayOutcomeTypeId outcomeTypeId)
        {
            GameplayOutcomeSnapshot[] matches = Ledger.CaptureGameplayOutcomes()
                .exactOutcomes
                .Where(value => value != null
                    && string.Equals(
                        value.producerId,
                        producerId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.outcomeTypeId,
                        outcomeTypeId.Value,
                        StringComparison.Ordinal))
                .ToArray();
            Require(
                matches.Length == 1,
                $"Expected one exact {producerId}/{outcomeTypeId.Value} outcome, found {matches.Length}.");
            GameplayOutcomeSnapshot snapshot = matches[0];
            Require(
                snapshot.lifecycle
                    == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
                && snapshot.status == GameplayOutcomeStatus.Succeeded,
                $"{producerId}/{outcomeTypeId.Value} was not PublishedAcknowledged/Succeeded.");
            GameplayResultKey key = new(
                snapshot.producerId,
                new GameplayOperationId(snapshot.operationId),
                snapshot.commitRevision,
                snapshot.localResultIndex);
            Require(
                Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && Ledger.TryGetExact(identity.OutcomeId, out _),
                $"{producerId}/{outcomeTypeId.Value} replay identity is not canonical.");
            return identity;
        }

        private BuildableObject CreateCertifiedFacility(Grid grid)
        {
            BuildingSO definition = CreateDefinition(
                97101,
                "EnvironmentCropCertifiedSeedFacility",
                "종자 인증 작업대");
            definition.AbilityModules.Add(new BuildingProductionWorkstationAbility
            {
                workstationTag = CertifiedSeedFacilityEligibility.WorkstationTag,
                lanePolicy = ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors,
                manualWorkLaneCount = 1,
                automaticWorkLaneCount = 0
            });
            definition.AbilityModules.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 2,
                physicalOutputBufferCycleCapacity = 4,
                allowOverflowDump = false
            });
            return CreateBuilding(
                grid,
                definition,
                "building:environment-crop-certified-seed",
                new Vector2Int(4, 1));
        }

        private BuildableObject CreateCropPlot(
            Grid grid,
            int definitionId,
            string assetName,
            string displayName,
            string persistentId,
            Vector2Int position)
        {
            BuildingSO definition = CreateDefinition(
                definitionId,
                assetName,
                displayName);
            BuildingCropPlotAbility ability = new();
            ability.Configure(
                isIndoor: false,
                growthRate: plotGrowthRate,
                waterRate: plotWaterRate,
                compost: 0,
                fuel: 0);
            definition.AbilityModules.Add(ability);
            BuildingWorkAmountAbility work = new()
            {
                operateWorkRequired = 1f
            };
            work.SetConstructionMaterials(new[]
            {
                new ItemAmountDefinition(
                    CropCycleInputRequirementAuthority.CleanWaterItemId,
                    1)
            });
            definition.AbilityModules.Add(work);
            return CreateBuilding(
                grid,
                definition,
                persistentId,
                position);
        }

        private BuildableObject CreateIrrigator(Grid grid)
        {
            BuildingSO definition = CreateDefinition(
                97103,
                "EnvironmentCropIrrigator",
                "생산 진입 관개기");
            definition.AbilityModules.Add(new BuildingCropIrrigationAbility
            {
                rangeManhattan = 4,
                waterPerRefill = CropWaterRules.RefillQuantity,
                minimumRefillIntervalSeconds = 1f
            });
            definition.AbilityModules.Add(new BuildingUtilityConnectionAbility
            {
                channels = UtilityChannel.CleanWater,
                maxThroughput = 1f
            });
            return CreateBuilding(
                grid,
                definition,
                "building:environment-crop-irrigator",
                new Vector2Int(8, 1));
        }

        private BuildingSO CreateDefinition(
            int id,
            string assetName,
            string displayName)
        {
            BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
            cleanup.Add(definition);
            definition.id = id;
            definition.name = assetName;
            definition.objectName = displayName;
            definition.width = 1;
            definition.height = 1;
            definition.unlocked = true;
            definition.layer = GridLayer.Building;
            definition.category = BuildingCategory.Resource;
            definition.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
            definition.ReplaceAbilities(new BuildingAbilityCollection());
            return definition;
        }

        private BuildableObject CreateBuilding(
            Grid grid,
            BuildingSO definition,
            string persistentId,
            Vector2Int position)
        {
            GameObject host = new(definition.name + "Host");
            cleanup.Add(host);
            BuildableObject building = host.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.RestorePersistentIdentity((BuildingInstanceId)persistentId);
            building.SetGrid(grid);
            building.Initialization(definition, position);
            return building;
        }

        public void Dispose()
        {
            domainFacts.Dispose();
            (CompletionCommand as IDisposable)?.Dispose();
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
        }
    }

    private sealed class FixedBuildingWorld : IBuildingWorldQuery
    {
        private readonly BuildableObject[] buildings;

        internal FixedBuildingWorld(params BuildableObject[] buildings) =>
            this.buildings = (buildings ?? Array.Empty<BuildableObject>())
                .Where(value => value != null)
                .ToArray();

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
    }

    private sealed class FixedFacilities : IFacilityCapabilityQuery
    {
        private readonly BuildableObject[] facilities;

        internal FixedFacilities(params BuildableObject[] facilities) =>
            this.facilities = (facilities ?? Array.Empty<BuildableObject>())
                .Where(value => value != null)
                .ToArray();

        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") => facilities;

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) =>
            Array.Empty<BuildableObject>();
    }

    private sealed class FixedCapacityProjector :
        IProductionOutputBufferCapacityProjector
    {
        public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
            ProductionFacilityHandle facility,
            ProductionOutputBatchMaximumMassProof maximumMassProof)
        {
            long maximum = maximumMassProof.MaximumBatchMassGrams;
            long portfolio = Math.Max(10_000L, checked(maximum * 2L));
            long batchMinimum = checked(maximum * 4L);
            long required = Math.Max(portfolio, batchMinimum);
            return new ProductionOutputBufferCapacitySourceSnapshot(
                4,
                maximum,
                portfolio,
                batchMinimum,
                required,
                new string('b', 64),
                new string('c', 64),
                new string('d', 64),
                new string('e', 64));
        }
    }

    public class PlanningTransferPort : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name is "TryRequestItemDelivery"
                or "TryRequestStackDelivery")
            {
                args[4] = args[1];
                args[5] = DomainFailure.None;
                return true;
            }
            if (targetMethod.Name == "ReleaseDestination")
                return 0;
            if (targetMethod.ReturnType == typeof(void))
                return null;
            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private sealed class ReachableDeliveryQuery :
        IWorldItemDeliveryReachabilityQuery
    {
        internal static ReachableDeliveryQuery Instance { get; } = new();

        public WorldItemDeliveryReachabilityStatus AssessExactStackDelivery(
            ItemStackId stackId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out string failureReason)
        {
            failureReason = string.Empty;
            return WorldItemDeliveryReachabilityStatus.Reachable;
        }
    }

    private sealed class EmptyCharacterWorld : ICharacterWorldQuery
    {
        internal static EmptyCharacterWorld Instance { get; } = new();
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters =>
            Array.Empty<CharacterActor>();
    }

    private sealed class ControlledResearch : IBlueprintResearchStateService
    {
        private readonly BlueprintResearchState state = new();
        public BlueprintResearchState GetState() => state;
    }

    private sealed class FixedClock : IGameClock, IGameCalendar
    {
        private float time;
        private float deltaTime = 1f;

        internal FixedClock(int day)
        {
            Day = Math.Max(1, day);
            time = (Day - 1) * GameCalendarRules.SecondsPerDay;
        }

        public float Time => time;
        public float DeltaTime => deltaTime;
        public int FrameCount => 1;
        public bool IsPaused => false;
        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => Time;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        internal float RawTime => time;
        internal float RawDeltaTime => deltaTime;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;

        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, GameCalendarRules.HoursPerDay - 1);
            time = ((Day - 1) * GameCalendarRules.HoursPerDay + Hour)
                * GameSimulationTimeRules.SecondsPerGameHour;
            deltaTime = 1f;
        }

        internal void AdvanceGameHours(float hours)
        {
            if (!float.IsFinite(hours) || hours <= 0f)
                throw new ArgumentOutOfRangeException(nameof(hours));
            deltaTime = hours * GameSimulationTimeRules.SecondsPerGameHour;
            time += deltaTime;
            int absoluteHour = Mathf.FloorToInt(
                time / GameSimulationTimeRules.SecondsPerGameHour);
            Day = absoluteHour / GameCalendarRules.HoursPerDay + 1;
            Hour = absoluteHour % GameCalendarRules.HoursPerDay;
        }

        internal void Restore(
            int day,
            int hour,
            float restoredTime,
            float restoredDeltaTime,
            bool running)
        {
            if (day <= 0
                || hour < 0
                || hour >= GameCalendarRules.HoursPerDay
                || !float.IsFinite(restoredTime)
                || restoredTime < 0f
                || !float.IsFinite(restoredDeltaTime)
                || restoredDeltaTime < 0f)
            {
                throw new InvalidOperationException(
                    "Causal crop clock checkpoint is invalid.");
            }
            int projectedAbsoluteHour = Mathf.FloorToInt(
                restoredTime / GameSimulationTimeRules.SecondsPerGameHour);
            if (projectedAbsoluteHour / GameCalendarRules.HoursPerDay + 1 != day
                || projectedAbsoluteHour % GameCalendarRules.HoursPerDay != hour)
            {
                throw new InvalidOperationException(
                    "Causal crop clock checkpoint calendar drifted.");
            }
            Day = day;
            Hour = hour;
            time = restoredTime;
            deltaTime = restoredDeltaTime;
            IsRunning = running;
        }
    }

    private sealed class ControlledFluid :
        IFluidInfrastructureQuery,
        IFluidInfrastructureTransaction,
        IFluidInfrastructureMutationTransaction
    {
        private readonly Dictionary<FluidNetworkAggregateState, MutationSnapshot>
            mutations = new();
        private float water;
        private int version;

        internal float Water
        {
            get => water;
            set
            {
                if (water.Equals(value))
                    return;
                water = value;
                version = checked(version + 1);
            }
        }

        internal int Consumed { get; private set; }
        public int Version => version;
        public IReadOnlyList<FluidNetworkSnapshot> Networks =>
            Array.Empty<FluidNetworkSnapshot>();
        public IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers =>
            Array.Empty<WaterTransferFacilitySnapshot>();

        public bool TryGetNetwork(
            BuildableObject building,
            out FluidNetworkSnapshot snapshot)
        {
            snapshot = new FluidNetworkSnapshot
            {
                NetworkId = "network:environment-crop-fixture",
                Channel = UtilityChannel.CleanWater,
                CleanWater = Water,
                Capacity = Math.Max(3f, Water)
            };
            return building != null;
        }

        public bool TryGetMaintenance(
            BuildableObject building,
            out float blockage,
            out float leak)
        {
            blockage = 0f;
            leak = 0f;
            return false;
        }

        public bool TryGetWaterCondition(
            BuildableObject building,
            out BuildingWaterConditionSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public bool CanConsume(
            BuildableObject consumer,
            WorldWaterQuality quality,
            float amount,
            out DomainFailure failure)
        {
            failure = Water >= amount
                ? DomainFailure.None
                : new DomainFailure(FailureCode.FluidInsufficientWater);
            return !failure.IsFailure;
        }

        public bool TryConsume(
            BuildableObject consumer,
            WorldWaterQuality quality,
            float amount,
            out WorldWaterQuality consumedQuality,
            out DomainFailure failure)
        {
            consumedQuality = WorldWaterQuality.Clean;
            if (!CanConsume(consumer, quality, amount, out failure))
                return false;
            Water -= amount;
            Consumed++;
            return true;
        }

        public bool TryAdd(
            BuildableObject producer,
            WorldWaterQuality quality,
            float amount,
            out float accepted)
        {
            accepted = 0f;
            return false;
        }

        public bool TryConsumeManualContainer(
            BuildableObject consumer,
            string destination,
            float amount,
            out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.FluidInsufficientWater);
            return false;
        }

        public FluidInfrastructureMutationToken CaptureMutation()
        {
            FluidNetworkAggregateState marker = new() { Version = version };
            mutations.Add(marker, new MutationSnapshot(water, Consumed, version));
            return new FluidInfrastructureMutationToken(
                marker,
                checked(version + 1));
        }

        internal void Restore(float restoredWater, int consumed, int restoredVersion)
        {
            if (!float.IsFinite(restoredWater)
                || restoredWater < 0f
                || consumed < 0
                || restoredVersion < 0
                || mutations.Count != 0)
            {
                throw new InvalidOperationException(
                    "Causal crop fluid checkpoint is invalid.");
            }
            water = restoredWater;
            Consumed = consumed;
            version = restoredVersion;
        }

        public void RestoreMutation(in FluidInfrastructureMutationToken token)
        {
            Require(
                token.IsValid
                && token.ExpectedMutatedVersion == version,
                "Irrigation fluid rollback token drifted.");
            Require(
                mutations.TryGetValue(
                    token.Before,
                    out MutationSnapshot snapshot),
                "Irrigation fluid rollback token was not issued by this fixture.");
            water = snapshot.Water;
            Consumed = snapshot.Consumed;
            version = snapshot.Version;
            mutations.Remove(token.Before);
        }

        private readonly struct MutationSnapshot
        {
            internal MutationSnapshot(float water, int consumed, int version)
            {
                Water = water;
                Consumed = consumed;
                Version = version;
            }

            internal float Water { get; }
            internal int Consumed { get; }
            internal int Version { get; }
        }
    }

    private sealed class FixedPerformanceMaximum :
        ICharacterPerformanceDefinitionMaximumQuery
    {
        internal static FixedPerformanceMaximum Instance { get; } = new();

        public CharacterPerformanceDefinitionMaximumSnapshot Capture(
            string formulaId) => new(
            formulaId,
            maximumValue: 2d,
            functionalCapacityMaximum: 2d,
            proficiencyMaximum: 2d,
            gameplayEffectMaximum: 1d,
            sourceDigest: new string('c', 64));
    }

    private sealed class FixedCharacterPerformance : ICharacterPerformanceQuery
    {
        internal static FixedCharacterPerformance Instance { get; } = new();

        public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(
            CharacterActor actor) => default;

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            float contextFactor = 1f,
            GameplayEffectContext effectContext = null) =>
            Create(formulaId, contextFactor);

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            CharacterPerformanceEvaluationContext context) =>
            Create(formulaId, 1f);

        public CharacterPerformanceSnapshot EvaluateWork(
            CharacterActor actor,
            WorkTypeId workTypeId,
            CharacterPerformanceResultChannel resultChannel,
            CharacterPerformanceEvaluationContext context) =>
            new()
            {
                FormulaId = workTypeId.Value,
                ResultChannel = resultChannel,
                Value = 1f
            };

        public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
            CharacterActor actor,
            CharacterPerformanceFormulaDomain domain) =>
            Array.Empty<CharacterPerformanceSnapshot>();

        private static CharacterPerformanceSnapshot Create(
            string formulaId,
            float contextFactor) => new()
        {
            FormulaId = formulaId ?? string.Empty,
            ContextFactor = contextFactor,
            Value = Mathf.Max(0f, contextFactor),
            IsApplicable = true
        };
    }

    // Crop-only fixtures do not own a character identity runtime. They model an
    // unavailable recipient, never claim identity effects or manufacture a log.
    // Tests of real identity effects supply completionFactory explicitly.
    private sealed class UnavailableRecipientCompletion :
        IWorkCompletionIdentityDeliveryCommand
    {
        private readonly WorkCompletionIdentityDeliveryLedger deliveries;

        internal UnavailableRecipientCompletion(WorkCompletionIdentityDeliveryLedger deliveries)
        {
            this.deliveries = deliveries
                ?? throw new ArgumentNullException(nameof(deliveries));
        }

        public WorkCompletionIdentityDeliveryResult EnsureApplied(
            WorkCompletionIdentityDeliveryRequest request)
        {
            WorkCompletionIdentityDeliveryStatus state = deliveries.Inspect(
                request,
                out string failure);
            if (state != WorkCompletionIdentityDeliveryStatus.Applied)
                return new WorkCompletionIdentityDeliveryResult(state, failure);

            return new WorkCompletionIdentityDeliveryResult(
                deliveries.Commit(request, out failure,
                    WorkCompletionIdentityDeliveryDisposition.TerminalRecipientUnavailable), failure);
        }

        public bool RetireProducerStream(string producerStreamId) => deliveries.RetireProducerStream(producerStreamId);
    }

    private sealed class FixedRunSeedProvider : IRunSeedProvider
    {
        internal FixedRunSeedProvider(int runSeed) => RunSeed = runSeed;
        public int RunSeed { get; }
    }

    private sealed class ManualOutcomeFaultingRecorder :
        IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;

        internal ManualOutcomeFaultingRecorder(
            IGameplayOutcomeRecorder inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectManualWrite { get; set; }
        internal bool RejectCertifiedSeedPrepare { get; set; }
        internal int CertifiedSeedPrepareRejectionCount { get; private set; }
        internal bool FailCertifiedSeedDelivery { get; set; }

        public OutcomePrepareResult TryPrepare<TReceipt>(
            in TReceipt receipt,
            out PreparedOutcomeToken prepared)
        {
            if (RejectCertifiedSeedPrepare
                && typeof(TReceipt)
                    == typeof(CertifiedSeedCompletionOutcomeReceipt))
            {
                CertifiedSeedPrepareRejectionCount++;
                prepared = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "injected-certified-seed-observer-prepare-rejection");
            }
            return inner.TryPrepare(receipt, out prepared);
        }

        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation) =>
            inner.TryReserve(requirements, out reservation);

        public OutcomePrepareResult TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared)
        {
            if (RejectManualWrite
                && typeof(TReceipt)
                    == typeof(CropManualWaterRefillOutcomeReceipt))
            {
                prepared = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "injected-e08-manual-water-parent-write-rejection");
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
            FailCertifiedSeedDelivery
                ? new OutcomeDeliveryResult(OutcomeDeliveryCode.PendingDeliveryFault,
                    committed.OutcomeId, "injected-certified-seed-pending-delivery")
                : inner.TryDeliver(committed);

        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            inner.Acknowledge(committed);

        public int RetryPendingDeliveries(int maximumCount) =>
            inner.RetryPendingDeliveries(maximumCount);
    }

    private sealed class FixedEffectBounds : IGameplayEffectResultBoundsQuery
    {
        internal static FixedEffectBounds Instance { get; } = new();
        public float RequireFiniteMaximum(string targetId) => 2f;

        public GameplayEffectResultBoundsSnapshot CaptureFiniteBounds(
            string targetId) => new(
            targetId,
            minimum: 0f,
            maximum: 2f,
            sourceDigest: new string('d', 64));
    }

    private sealed class FixedSurvivalEnvironment : ISurvivalEnvironmentQuery
    {
        internal static FixedSurvivalEnvironment Instance { get; } = new();
        private readonly float outdoorTemperature;
        internal FixedSurvivalEnvironment(float outdoorTemperature = 20f) =>
            this.outdoorTemperature = outdoorTemperature;
        public bool HasFuelSupply(BuildableObject building) => true;
        public float GetRemainingFuelGameSeconds(BuildableObject building) =>
            float.MaxValue;
        public SurvivalEnvironmentSnapshot GetEnvironmentSnapshot() => new(
            SurvivalWeatherType.Clear,
            outdoorTemperature: outdoorTemperature,
            exteriorNightDanger: 0f,
            sanitationRisk: 0f,
            diseaseRisk: 0f);
    }

    private sealed class FixedEnvironmentalField : IEnvironmentalFieldQuery
    {
        internal static FixedEnvironmentalField Instance { get; } = new();

        public int Version => 1;
        public bool IsInitialized => true;

        public bool TryGetCell(
            Vector2Int position,
            out EnvironmentalCellSnapshot snapshot)
        {
            snapshot = new EnvironmentalCellSnapshot(
                position,
                temperatureC: 20f,
                airQuality: 100f,
                lightLevel: 100f);
            return true;
        }

        public bool TryGetAverage(
            IReadOnlyList<Vector2Int> positions,
            out EnvironmentalCellSnapshot snapshot) =>
            TryGetCell(
                positions != null && positions.Count > 0
                    ? positions[0]
                    : Vector2Int.zero,
                out snapshot);

        public float GetFoodSpoilageMultiplier(Vector2Int position) => 1f;
        public bool IsOrganPreservationSafe(Vector2Int position) => false;

        public bool TryGetTargetTemperature(
            Vector2Int buildingPosition,
            out float targetTemperatureC)
        {
            targetTemperatureC = 0f;
            return false;
        }
    }
}
#endif
