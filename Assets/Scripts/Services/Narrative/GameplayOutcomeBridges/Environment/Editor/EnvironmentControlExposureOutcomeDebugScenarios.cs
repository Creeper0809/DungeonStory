#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class EnvironmentControlExposureOutcomeDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/LedgerB1E09EnvironmentOutcome-20260925-r4/report.txt";
    private static Action<JObject> observationSink;

    public static bool RunAll(
        bool log = false,
        bool writeReport = true,
        Action<JObject> observe = null)
    {
        Require(observationSink == null, "E09 observation collection cannot be nested.");
        if (observe != null)
        {
            Require(Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification
                && Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_E09_ENVIRONMENT_EXPOSURE_VERIFY") == "1"
                && !writeReport,
                "E09 raw observations require explicit isolated batch and no legacy report writes.");
        }
        observationSink = observe;
        try
        {
            VerifyThermostatProductionEntry();
            VerifyThermostatPresenterButtons();
            VerifyThermostatReservationRejection();
            VerifyThermostatPendingRoundTrip();
            VerifyExposureReceiptContracts();
            if (writeReport)
                WriteReport("PASS", string.Empty);
            if (log)
                Debug.Log("Environment control/exposure outcome scenarios passed.");
            return true;
        }
        catch (Exception exception)
        {
            if (writeReport)
                WriteReport("FAIL", exception.ToString());
            if (log)
                Debug.LogError(exception);
            return false;
        }
        finally
        {
            observationSink = null;
        }
    }

    public static void RunAnchoredEvidenceRestore()
    {
        VerifyThermostatProductionEntry();
        VerifyExposureReceiptContracts();
    }

    private static void VerifyThermostatProductionEntry()
    {
        using OutcomeFixture outcomes = new("run:e09-control-success");
        using EnvironmentalFieldDebugScenarios.TestWorld world =
            new(6, 2, 20f, outcomes.Facts);
        Vector2Int position = new(2, 0);
        world.Add(world.CreateBuilding(
            "e09-hvac",
            position,
            CreateThermostat()));
        world.Advance(1);

        Observe("control-before", world, outcomes, position);

        Require(world.Runtime.TrySetTargetTemperature(
                position,
                6f,
                out DomainFailure firstFailure)
            && !firstFailure.IsFailure,
            "Thermostat production command failed.");
        Observe("control-set", world, outcomes, position);
        Require(Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 1,
            "Thermostat change did not publish exactly one result.");
        DungeonEnvironmentalFieldSaveData afterFirst = world.Runtime.Capture();
        Require(afterFirst.nextControlOutcomeSequence == 1
            && afterFirst.controlOutcomes.Count == 0,
            "Acknowledged thermostat result retained a pending owner outbox.");

        Require(world.Runtime.TrySetTargetTemperature(position, 6f, out _),
            "Idempotent thermostat command failed.");
        Observe("control-same-value", world, outcomes, position);
        Require(Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 1
            && world.Runtime.Capture().nextControlOutcomeSequence == 1,
            "No-op thermostat command created a result or consumed a sequence.");

        Require(world.Runtime.TrySetTargetTemperature(position, -20f, out _)
            && world.Runtime.TryGetTargetTemperature(position, out float clamped)
            && Mathf.Approximately(clamped, 2f),
            "Thermostat command did not clamp to the authored minimum.");
        Observe("control-clamped", world, outcomes, position);
        Require(Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 2,
            "Clamped thermostat change did not publish exactly one additional result.");
        GameplayOutcomeSnapshot latest = outcomes.Ledger.CaptureGameplayOutcomes()
            .exactOutcomes
            .Where(value => value != null
                && value.outcomeTypeId
                    == EnvironmentOutcomeIds.EnvironmentalControlChanged.Value)
            .OrderBy(value => value.sequence)
            .Last();
        Require(Near(FindMetric(latest, EnvironmentOutcomeIds.RequestedTargetMetric), -20d)
            && Near(FindMetric(latest, EnvironmentOutcomeIds.AppliedTargetMetric), 2d)
            && Near(FindMetric(latest, EnvironmentOutcomeIds.TargetBeforeMetric), 6d)
            && Near(FindMetric(latest, EnvironmentOutcomeIds.TargetAfterMetric), 2d),
            "Thermostat result omitted requested/applied/before/after values.");

        world.Advance(20);
        Observe("control-continuous", world, outcomes, position);
        Require(Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 2,
            "Continuous field simulation emitted a gameplay result without a command.");
        VerifyAnchoredOutcome(outcomes, latest, "facility-evolution");
    }

    private static void VerifyThermostatReservationRejection()
    {
        using OutcomeFixture outcomes = new("run:e09-control-reserve-reject");
        FaultingFactPublisher fault = new(outcomes.Facts) { RejectReserve = true };
        using EnvironmentalFieldDebugScenarios.TestWorld world =
            new(6, 2, 20f, fault);
        Vector2Int position = new(2, 0);
        world.Add(world.CreateBuilding(
            "e09-hvac",
            position,
            CreateThermostat()));
        world.Advance(1);

        Observe("control-reserve-before", world, outcomes, position);

        Require(!world.Runtime.TrySetTargetTemperature(
                position,
                6f,
                out DomainFailure failure)
            && failure.Code == FailureCode.EnvironmentOutcomeUnavailable,
            "Reservation rejection did not surface the typed outcome failure.");
        Observe("control-reserve-rejected", world, outcomes, position,
            new JObject { ["failureCode"] = failure.Code.ToString() });
        Require(world.Runtime.TryGetTargetTemperature(position, out float target)
            && Mathf.Approximately(target, 22f),
            "Reservation rejection mutated the thermostat target.");
        DungeonEnvironmentalFieldSaveData save = world.Runtime.Capture();
        Require(save.nextControlOutcomeSequence == 0
            && save.controlOutcomes.Count == 0
            && Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 0,
            "Reservation rejection consumed sequence/outbox/ledger state.");
    }

    private static void VerifyThermostatPresenterButtons()
    {
        using OutcomeFixture outcomes = new("run:e09-control-presenter");
        using EnvironmentalFieldDebugScenarios.TestWorld world =
            new(6, 2, 20f, outcomes.Facts);
        Vector2Int position = new(2, 0);
        BuildableObject building = world.CreateBuilding(
            "e09-hvac-ui",
            position,
            CreateThermostat());
        world.Add(building);
        world.Advance(1);
        GameObject root = new("E09 Environmental Presenter Root", typeof(RectTransform));
        try
        {
            string status = string.Empty;
            int refreshes = 0;
            EnvironmentalBuildingPanelPresenter presenter = new(
                world.Runtime,
                world.Runtime,
                EmptyFluid.Instance,
                PassthroughFailureLocalizer.Instance);
            IReadOnlyList<GameObject> rendered = presenter.Render(
                root.transform,
                building,
                null,
                value => status = value,
                () => refreshes++);
            UnityEngine.UI.Button plus = rendered
                .SelectMany(value => value.GetComponentsInChildren<
                    UnityEngine.UI.Button>(includeInactive: true))
                .Single(value => value.GetComponentInChildren<TMPro.TMP_Text>()
                    ?.text == "+2°C");
            Observe("control-ui-before", world, outcomes, position);
            plus.onClick.Invoke();
            Observe("control-ui-set", world, outcomes, position,
                new JObject { ["refreshes"] = refreshes, ["statusText"] = status });
            Require(world.Runtime.TryGetTargetTemperature(position, out float raised)
                && Mathf.Approximately(raised, 24f)
                && refreshes == 1
                && status.Contains("24", StringComparison.Ordinal)
                && Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 1,
                "Generated +2°C button did not invoke the actual thermostat command.");
            plus.onClick.Invoke();
            Observe("control-ui-replay", world, outcomes, position,
                new JObject { ["refreshes"] = refreshes, ["statusText"] = status });
            Require(Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 1,
                "Repeated generated button invocation duplicated a no-op result.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyThermostatPendingRoundTrip()
    {
        using OutcomeFixture outcomes = new("run:e09-control-roundtrip");
        FaultingFactPublisher fault = new(outcomes.Facts)
        {
            HideAcknowledgementCount = 1
        };
        Vector2Int position = new(2, 0);
        string json;
        using (EnvironmentalFieldDebugScenarios.TestWorld source =
               new(6, 2, 20f, fault))
        {
            source.Add(source.CreateBuilding(
                "e09-hvac",
                position,
                CreateThermostat()));
            source.Advance(1);
            Observe("control-pending-before", source, outcomes, position);
            Require(source.Runtime.TrySetTargetTemperature(position, 7f, out _),
                "Thermostat command failed before pending round trip.");
            Observe("control-pending", source, outcomes, position);
            DungeonEnvironmentalFieldSaveData pending = source.Runtime.Capture();
            Require(pending.controlOutcomes.Count == 1
                && pending.nextControlOutcomeSequence == 1,
                "Hidden acknowledgement did not retain the owner pending record.");
            json = JsonUtility.ToJson(pending);
        }

        using EnvironmentalFieldDebugScenarios.TestWorld target =
            new(6, 2, 20f, outcomes.Facts);
        target.Add(target.CreateBuilding(
            "e09-hvac",
            position,
            CreateThermostat()));
        target.Advance(1);
        target.Runtime.Restore(target.Runtime.PrepareRestore(
            JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(json)));
        Observe("control-restored-pending", target, outcomes, position,
            new JObject { ["serializedEnvironment"] = JObject.Parse(json) });
        Require(target.Runtime.Capture().controlOutcomes.Count == 1,
            "Restore discarded the unacknowledged thermostat owner record.");
        target.Advance(1);
        Observe("control-reconciled", target, outcomes, position);
        Require(target.Runtime.Capture().controlOutcomes.Count == 0
            && target.Runtime.TryGetTargetTemperature(position, out float restored)
            && Mathf.Approximately(restored, 7f),
            "Reconciliation did not clear the restored thermostat owner record.");
        Require(Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 1,
            "Restore/reconciliation duplicated the thermostat result.");
        if (observationSink != null)
        {
            target.Advance(1);
            Observe("control-reconciled-replay", target, outcomes, position);
            Require(Count(outcomes, EnvironmentOutcomeIds.EnvironmentalControlChanged) == 1
                && target.Runtime.Capture().controlOutcomes.Count == 0
                && target.Runtime.Capture().nextControlOutcomeSequence == 1,
                "Replayed thermostat reconciliation changed completion identity or result count.");
        }
    }

    private static void Observe(
        string stage,
        EnvironmentalFieldDebugScenarios.TestWorld world,
        OutcomeFixture outcomes,
        Vector2Int position,
        JObject details = null)
    {
        if (observationSink == null)
            return;
        JObject record = new()
        {
            ["stage"] = stage,
            ["kind"] = "thermostat-production-command",
            ["testPreparationExcludedFromTraining"] = true,
            ["naturalGameplayClaimed"] = false,
            ["details"] = details
        };
        observationSink(record);
        try
        {
            record["environment"] = JObject.Parse(JsonUtility.ToJson(world.Runtime.Capture()));
            record["ledger"] = JObject.Parse(JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()));
            bool hasTarget = world.Runtime.TryGetTargetTemperature(position, out float target);
            record["hasTarget"] = hasTarget;
            record["targetTemperatureC"] = target;
            record["x"] = position.x;
            record["y"] = position.y;
        }
        catch (Exception exception)
        {
            record["captureFailure"] = exception.ToString();
            throw;
        }
    }

    private static void VerifyExposureReceiptContracts()
    {
        using OutcomeFixture outcomes = new("run:e09-exposure-receipts");
        EnvironmentalExposureOutcomeSaveData band = Exposure(
            "environment-exposure:character:e09:000000:band-transition",
            "band-transition",
            24f,
            26f,
            EnvironmentalExposureBand.Stable,
            EnvironmentalExposureBand.Burden,
            0f,
            100f,
            100f,
            1L,
            "physiological-band-transition");
        Commit(outcomes.Bridge,
            EnvironmentOutcomeReceiptFactory.CreateEnvironmentalExposureThreshold(band),
            band.ownerRevision);
        EnvironmentalExposureOutcomeSaveData damage = Exposure(
            "environment-exposure:character:e09:000001:damage-tick",
            "damage-tick",
            76f,
            78f,
            EnvironmentalExposureBand.Critical,
            EnvironmentalExposureBand.Critical,
            1.5f,
            100f,
            98.5f,
            2L,
            "critical-environment");
        Commit(outcomes.Bridge,
            EnvironmentOutcomeReceiptFactory.CreateEnvironmentalExposureThreshold(damage),
            damage.ownerRevision);

        GameplayOutcomeSnapshot[] rows = outcomes.Ledger.CaptureGameplayOutcomes()
            .exactOutcomes
            .Where(value => value != null
                && value.outcomeTypeId
                    == EnvironmentOutcomeIds.EnvironmentalExposureThreshold.Value)
            .OrderBy(value => value.sequence)
            .ToArray();
        Require(rows.Length == 2
            && rows[0].status == GameplayOutcomeStatus.Succeeded
            && rows[1].status == GameplayOutcomeStatus.Failed,
            "Exposure band/damage receipts did not retain distinct statuses.");
        Require(Near(FindMetric(rows[0], EnvironmentOutcomeIds.BandBeforeMetric), 0d)
            && Near(FindMetric(rows[0], EnvironmentOutcomeIds.BandAfterMetric), 1d)
            && Near(FindMetric(rows[1], EnvironmentOutcomeIds.DamageMetric), 1.5d)
            && Near(FindMetric(rows[1], EnvironmentOutcomeIds.HealthAfterMetric), 98.5d),
            "Exposure receipt omitted band, damage, or health evidence.");
        foreach (GameplayOutcomeSnapshot row in rows)
            VerifyAnchoredOutcome(outcomes, row, "acquired-trait");
    }

    private static void VerifyAnchoredOutcome(OutcomeFixture fixture,
        GameplayOutcomeSnapshot outcome, string anchorType)
    {
        GameplayOutcomeSubjectSnapshot subject = outcome.subjects[0];
        GameplayOutcomeLedgerDebugScenarios.VerifyTypedEvidenceAnchorRoundTrip(
            fixture.Ledger, fixture.Registry,
            new GameplayEntityId(new GameplayEntityKindId(subject.entityKindId),
                subject.entityId),
            new GameplayOutcomeId(new GameplayOutcomeRunId(outcome.runId),
                outcome.sequence), outcome.immutablePayloadHash, anchorType);
    }

    private static EnvironmentalExposureOutcomeSaveData Exposure(
        string operationId,
        string eventKind,
        float coldBefore,
        float coldAfter,
        EnvironmentalExposureBand bandBefore,
        EnvironmentalExposureBand bandAfter,
        float damage,
        float healthBefore,
        float healthAfter,
        long ownerRevision,
        string reason) => new()
    {
        operationId = operationId,
        eventKind = eventKind,
        characterId = "character:e09",
        characterDisplayName = "환경 검증자",
        x = 3,
        y = 1,
        absoluteDay = 11,
        ownerRevision = ownerRevision,
        temperatureC = -12f,
        airQuality = 82f,
        lightLevel = 44f,
        coldBefore = coldBefore,
        coldAfter = coldAfter,
        heatBefore = 0f,
        heatAfter = 0f,
        airborneBefore = 3f,
        airborneAfter = 4f,
        bandBefore = bandBefore,
        bandAfter = bandAfter,
        damage = damage,
        healthBefore = healthBefore,
        healthAfter = healthAfter,
        reasonCode = reason
    };

    private static void Commit<TReceipt>(
        IEnvironmentGameplayOutcomeCommitter bridge,
        in TReceipt receipt,
        long ownerRevision)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        Require(bridge.TryPrepare(receipt, out PreparedEnvironmentOutcome prepared,
                out string failure),
            "Receipt preparation failed: " + failure);
        Require(bridge.Commit(prepared, ownerRevision).CanClearOwnerPending,
            "Receipt did not reach published acknowledgement.");
    }

    private static BuildingThermalEmitterAbility CreateThermostat() => new()
    {
        mode = ThermalEmitterMode.Thermostat,
        targetTemperatureC = 22f,
        playerConfigurable = true,
        minimumTargetTemperatureC = 2f,
        maximumTargetTemperatureC = 30f,
        degreesPerSecond = 2f,
        radius = 2
    };

    private static int Count(OutcomeFixture fixture, GameplayOutcomeTypeId type) =>
        fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
            value != null
            && value.outcomeTypeId == type.Value);

    private static double FindMetric(
        GameplayOutcomeSnapshot outcome,
        GameplayMetricId id) => outcome.metrics.Single(value =>
            value.metricId == id.Value).value;

    private static bool Near(double actual, double expected) =>
        Math.Abs(actual - expected) < 0.0001d;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("E09: " + message);
    }

    private static void WriteReport(string result, string detail)
    {
        string directory = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllLines(ReportPath, new[]
        {
            "workItem=E09",
            "thermostat.actual-command.clamp.no-op.continuous-zero=covered",
            "thermostat.generated-presenter-button.command-and-no-duplicate=covered",
            "thermostat.reserve-rejection.atomic=covered",
            "thermostat.pending-save-restore-reconcile.exactly-once=covered",
            "exposure.typed-band-and-damage-receipts=covered",
            "character-runtime.threshold-and-damage-entry=covered-by-playmode-companion",
            "humanApprovalClaimed=false",
            "result=" + result,
            detail ?? string.Empty
        });
    }

    private sealed class OutcomeFixture : IDisposable
    {
        internal OutcomeFixture(string runId)
        {
            IKoreanJosaFormatter josa = new KoreanJosaFormatter();
            GameplayOutcomeRegistry registry = Registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new EnvironmentalControlCommandOutcomeDescriptor(josa),
                    new EnvironmentalExposureThresholdOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new EnvironmentalControlCommandOutcomeAdapter(),
                    new EnvironmentalExposureThresholdOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(runId),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                new GameEventBus());
            Facts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(recorder, Ledger);
            Facts.Subscribe(
                "domain-fact:environmental-control:e09",
                new GameplayDomainFactObserver<
                    EnvironmentalControlFact,
                    EnvironmentalControlCommandReceipt>(
                        fact => EnvironmentOutcomeReceiptFactory
                            .CreateEnvironmentalControl(fact.Snapshot()),
                        registry,
                        Ledger,
                        recorder,
                        commits));
            Bridge = new EnvironmentGameplayOutcomeBridge(
                recorder,
                Ledger,
                Ledger);
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal DomainFactDispatcher Facts { get; }
        internal EnvironmentGameplayOutcomeBridge Bridge { get; }
        public void Dispose() => Facts.Dispose();
    }

    private sealed class FaultingFactPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;

        internal FaultingFactPublisher(IDomainFactPublisher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectReserve { get; set; }
        internal int HideAcknowledgementCount { get; set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure)
        {
            if (RejectReserve)
            {
                lease = default;
                failure = "e09-injected-reserve-rejection";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failure);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual) => HideAcknowledgement(inner.Publish(lease, actual));

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            for (int i = 0; i < results.Length; i++)
                results[i] = HideAcknowledgement(results[i]);
            return accepted;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        private DomainFactDelivery HideAcknowledgement(
            DomainFactDelivery result)
        {
            if (HideAcknowledgementCount <= 0 || !result.Completed)
                return result;
            HideAcknowledgementCount--;
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.AcceptedPendingDelivery,
                result.OperationKey,
                result.PublicationId,
                result.Fingerprint,
                "e09-injected-hidden-acknowledgement");
        }
    }

    private sealed class EmptyFluid : IFluidInfrastructureQuery
    {
        internal static readonly EmptyFluid Instance = new();
        public int Version => 0;
        public IReadOnlyList<FluidNetworkSnapshot> Networks =>
            Array.Empty<FluidNetworkSnapshot>();
        public IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers =>
            Array.Empty<WaterTransferFacilitySnapshot>();
        public bool TryGetNetwork(
            BuildableObject building,
            out FluidNetworkSnapshot snapshot)
        {
            snapshot = default;
            return false;
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
    }

    private sealed class PassthroughFailureLocalizer : IDomainFailureLocalizer
    {
        internal static readonly PassthroughFailureLocalizer Instance = new();
        public string Localize(DomainFailure failure) => failure.ToString();
        public string Localize(InfrastructureStatus status) => status.ToString();
        public string Localize(
            CharacterConsumablesFailureCode code,
            IReadOnlyList<string> parameters) => code.ToString();
    }
}
#endif
