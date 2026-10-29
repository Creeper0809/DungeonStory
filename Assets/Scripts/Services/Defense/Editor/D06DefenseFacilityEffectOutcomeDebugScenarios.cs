#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    public static void RunR30DefensePreparationFocused()
    {
        foreach (var test in new[] { (Speed: 1f, Level: 0), (Speed: 1.2f, Level: 0), (Speed: 1.2f, Level: 2) })
        {
            var clock = new R30DefenseClock();
            using var scenario = new D06Scenario("r30-preparation", gameClock: clock,
                projectBenefits: new R30DefensePreparationBenefits(test.Speed), cooldownSeconds: 6f);
            scenario.SetEffects(scenario.CreateDamage(1f));
            var initial = scenario.Runtime.CaptureState();
            initial.facilities.Single().growth.resetSpeedLevel = test.Level;
            scenario.Runtime.PublishRestoreState(scenario.Runtime.PrepareRestoreState(initial));
            Require(scenario.Trigger() != null, "Defense preparation activation failed.");
            float expected = 6f / (1f + test.Level * .1f) / test.Speed;
            var saved = scenario.Runtime.CaptureState();
            Require(Mathf.Abs(saved.facilities.Single().cooldownUntil - expected) < .0001f
                && Mathf.Abs(scenario.Facility.CooldownRemaining - expected) < .0001f,
                "Defense owner deadline and presented cooldown diverged.");
            clock.Now = expected - .001f;
            string before = JsonUtility.ToJson(scenario.Runtime.CaptureState());
            int rows = scenario.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;
            Require(!scenario.Facility.CanTrigger(DefenseTriggerTiming.OnEnter, out var failure)
                && failure.Code == FailureCode.DefenseCooldownActive && scenario.Trigger() == null
                && before == JsonUtility.ToJson(scenario.Runtime.CaptureState())
                && rows == scenario.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count,
                "Early retry bypassed cooldown or changed owner/receipt state.");
            var fresh = new DefenseFacilityRuntime(UnavailableEquipmentPhysicalItemGateway.Instance,
                new NoopDefensePhysicalGateway(), new AlwaysReadyDefenseInputOwner(), clock,
                new GameEventBus(), new AlwaysPoweredQuery(), new NoopDefenseFacilityNetwork(),
                new EmptyFacilityCapabilities(), new DungeonRuntimeAggregateRootStore(),
                new R30DefensePreparationBenefits(1f));
            fresh.PublishRestoreState(fresh.PrepareRestoreState(
                JsonUtility.FromJson<DefenseFacilitySaveData>(JsonUtility.ToJson(saved))));
            Require(Mathf.Abs(fresh.CaptureState().facilities.Single().cooldownUntil - expected) < .0001f
                && Mathf.Abs(fresh.GetSnapshot(scenario.Facility).CooldownRemaining - .001f) < .0001f,
                "Fresh JSON restore recalculated a started cooldown using current benefits.");
            clock.Now = expected;
            Require(scenario.Facility.CanTrigger(DefenseTriggerTiming.OnEnter, out _)
                && scenario.Trigger() != null && scenario.FacilityAfter.ActivationCount == 2,
                "Defense did not accept exactly one activation at the cooldown boundary.");
        }
        Debug.Log("R30_DEFENSE_PREPARATION=PASS actual Trigger/readiness/UI owner timer;6->5;upgrade composition;JSON fresh owner;early rejection and retry");
    }

    internal sealed class R30DefensePreparationBenefits : IGrandProjectBenefitQuery
    {
        private readonly float speed;
        internal R30DefensePreparationBenefits(float speed) { this.speed = speed; }
        public bool IsCompleted(string projectId) => false;
        public float GetProductionOutputMultiplier(string facilityTag) => 1f;
        public float ContractRewardMultiplier => 1f;
        public float DefensePreparationMultiplier => speed;
        public int ExpeditionSupplyCapacityBonus => 0;
    }

    private sealed class R30DefenseClock : IGameClock
    {
        internal float Now;
        public float Time => Now;
        public float DeltaTime => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    public const string D06FocusedReportPath =
        "Artifacts/QA/d06-defense-facility-effect-focused.txt";

    [MenuItem("DungeonStory/QA/Run D06 Defense Facility Effect Focused")]
    public static void RunD06DefenseFacilityEffectFocusedFromMenu()
    {
        string result = RunD06DefenseFacilityEffectFocused();
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(D06FocusedReportPath, result + Environment.NewLine);
        Debug.Log(result);
    }

    public static string RunD06DefenseFacilityEffectFocused()
    {
        var checks = new List<string>();
        VerifyD06SuccessTargetMetricsAndCausation(checks);
        VerifyD06JammedActivationWithFailedEffect(checks);
        VerifyD06MisfireProducesPartialEffect(checks);
        VerifyD06PerEffectFailureKeepsOnlySuccessfulDelta(checks);
        VerifyD06EffectCommitRejectionKeepsActivationOnly(checks);
        VerifyD06SaveRestorePreservesExactEffect(checks);
        VerifyD06ProductionEntryNoLongerUsesLegacyEffectPath(checks);
        return "PASS | D06 defense facility effect outcome | checks="
            + checks.Count + " | " + string.Join("; ", checks);
    }

    private static void VerifyD06SuccessTargetMetricsAndCausation(
        ICollection<string> checks)
    {
        using var scenario = new D06Scenario("success");
        scenario.SetEffects(
            scenario.CreateDamage(9f),
            scenario.CreateCorrosion(0.2f, 6f));
        DefenseActivationReport report = scenario.Trigger();
        GameplayOutcomeSnapshot effect = scenario.RequireEffect();
        GameplayOutcomeSnapshot activation = scenario.RequireActivation();
        Require(report != null
                && effect.status == GameplayOutcomeStatus.Succeeded
                && effect.absoluteDay
                    == CharacterAiEditorTestDependencies.GameCalendar.Day,
            "D06 success did not commit a succeeded effect result.");
        Require(Approximately(
                    Metric(effect, DefenseFacilityEffectOutcomeIds.Damage),
                    report.TotalDamage)
                && Approximately(
                    Metric(effect, DefenseFacilityEffectOutcomeIds.HealthBefore)
                    - Metric(effect, DefenseFacilityEffectOutcomeIds.HealthAfter),
                    report.TotalDamage)
                && Metric(
                    effect,
                    DefenseFacilityEffectOutcomeIds.StatusCountAfter) == 1d,
            "D06 success metrics drifted from the applied report/body/status state.");
        scenario.RequireTypedFacilityAndTarget(effect);
        Require(effect.parentRunId == activation.runId
                && effect.parentSequence == activation.sequence
                && effect.rootOperationId == activation.operationId
                && effect.causationRelationId == "direct-effect",
            "D06 effect did not preserve activation parent/root causation.");
        checks.Add("success-target-metrics-causation");
    }

    private static void VerifyD06JammedActivationWithFailedEffect(
        ICollection<string> checks)
    {
        using var scenario = new D06Scenario("jammed");
        scenario.ConfigureFaultChances(jam: 1f, misfire: 0f);
        scenario.SetEffects(
            scenario.CreateDamage(10f),
            scenario.CreateCorrosion(0.3f, 8f));
        float healthBefore = scenario.TargetHealth;
        DefenseActivationReport report = scenario.Trigger();
        GameplayOutcomeSnapshot effect = scenario.RequireEffect();
        Require(report != null
                && scenario.RequireActivation().status
                    == GameplayOutcomeStatus.Succeeded
                && scenario.RequireStateChange().status
                    == GameplayOutcomeStatus.Succeeded
                && effect.status == GameplayOutcomeStatus.Failed
                && Approximately(healthBefore, scenario.TargetHealth)
                && scenario.StatusCount == 0
                && Fact(effect, DefenseFacilityEffectOutcomeIds.Jammed) == "true"
                && Fact(effect, DefenseFacilityEffectOutcomeIds.Failure)
                    == "mechanical-jam",
            "D06 jam did not keep activation success separate from failed effect.");
        checks.Add("jam-activation-success-effect-failed");
    }

    private static void VerifyD06MisfireProducesPartialEffect(
        ICollection<string> checks)
    {
        using var scenario = new D06Scenario("misfire");
        scenario.ConfigureFaultChances(jam: 0f, misfire: 1f);
        scenario.SetEffects(scenario.CreateDamage(10f));
        float healthBefore = scenario.TargetHealth;
        DefenseActivationReport report = scenario.Trigger();
        GameplayOutcomeSnapshot effect = scenario.RequireEffect();
        Require(report != null
                && effect.status == GameplayOutcomeStatus.PartiallySucceeded
                && Fact(effect, DefenseFacilityEffectOutcomeIds.Misfired) == "true"
                && Fact(effect, DefenseFacilityEffectOutcomeIds.Failure)
                    == "partial-misfire"
                && Approximately(report.TotalDamage, 5f)
                && Approximately(healthBefore - scenario.TargetHealth, 5f),
            "D06 misfire did not record and apply an exact partial effect.");
        checks.Add("misfire-partial");
    }

    private static void VerifyD06PerEffectFailureKeepsOnlySuccessfulDelta(
        ICollection<string> checks)
    {
        using var scenario = new D06Scenario("partial-asset-failure");
        scenario.SetEffects(
            scenario.CreateDamage(7f),
            scenario.CreateThrowingStatus());
        float healthBefore = scenario.TargetHealth;
        DefenseActivationReport report = scenario.Trigger();
        GameplayOutcomeSnapshot effect = scenario.RequireEffect();
        Require(report != null
                && effect.status == GameplayOutcomeStatus.PartiallySucceeded
                && Approximately(healthBefore - scenario.TargetHealth, 7f)
                && Approximately(report.TotalDamage, 7f)
                && scenario.StatusCount == 0
                && Fact(effect, DefenseFacilityEffectOutcomeIds.Failure)
                    .StartsWith("effect:defense.test-throw:",
                        StringComparison.Ordinal),
            "D06 partial failure did not roll back only the failed asset delta.");
        checks.Add("per-effect-partial-rollback");
    }

    private static void VerifyD06EffectCommitRejectionKeepsActivationOnly(
        ICollection<string> checks)
    {
        using var scenario = new D06Scenario(
            "effect-commit-rejected",
            rejectEffectCommit: true);
        scenario.SetEffects(
            scenario.CreateDamage(8f),
            scenario.CreateCorrosion(0.25f, 9f));
        float healthBefore = scenario.TargetHealth;
        DefenseFacilitySnapshot facilityBefore = scenario.FacilityBefore;
        DefenseActivationReport report = scenario.Trigger();
        DefenseFacilitySnapshot facilityAfter = scenario.FacilityAfter;
        Require(report != null
                && scenario.RequireActivation().status
                    == GameplayOutcomeStatus.Succeeded
                && scenario.RequireStateChange().status
                    == GameplayOutcomeStatus.Succeeded
                && scenario.EffectRows.Count == 0
                && Approximately(healthBefore, scenario.TargetHealth)
                && scenario.StatusCount == 0
                && Approximately(report.TotalDamage, 0f)
                && facilityAfter.ActivationCount
                    == facilityBefore.ActivationCount + 1
                && facilityAfter.Condition < facilityBefore.Condition,
            "D06 effect commit rejection rolled back activation or leaked effect delta.");
        checks.Add("effect-rejection-keeps-activation");
    }

    private static void VerifyD06SaveRestorePreservesExactEffect(
        ICollection<string> checks)
    {
        using var scenario = new D06Scenario("save-restore");
        scenario.SetEffects(
            scenario.CreateDamage(6f),
            scenario.CreateCorrosion(0.15f, 5f));
        _ = scenario.Trigger();
        GameplayOutcomeSnapshot source = scenario.RequireEffect();
        GameplayOutcomeLedgerSaveData saved =
            scenario.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = scenario.CreateEmptyLedger();
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        GameplayOutcomeSnapshot replay = restored.CaptureGameplayOutcomes()
            .exactOutcomes.Single(value => string.Equals(
                value.outcomeTypeId,
                DefenseFacilityEffectOutcomeIds.Result.Value,
                StringComparison.Ordinal));
        Require(replay.immutablePayloadHash == source.immutablePayloadHash
                && replay.status == source.status
                && replay.parentRunId == source.parentRunId
                && replay.parentSequence == source.parentSequence
                && replay.rootOperationId == source.rootOperationId
                && replay.subjects.Any(value =>
                    value.entityKindId == "character"
                    && value.entityId == scenario.TargetId),
            "D06 effect result did not survive an exact ledger save/restore round trip.");
        checks.Add("save-restore-exact");
    }

    private static void VerifyD06ProductionEntryNoLongerUsesLegacyEffectPath(
        ICollection<string> checks)
    {
        string path = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Assets/Scripts/Services/Defense/DefenseFacilitySystem.cs");
        string source = File.ReadAllText(path);
        int triggerStart = source.IndexOf(
            "public DefenseActivationReport Trigger(",
            StringComparison.Ordinal);
        int resolverStart = source.IndexOf(
            "public static class DefenseFacilityResolver",
            triggerStart,
            StringComparison.Ordinal);
        string trigger = source.Substring(triggerStart, resolverStart - triggerStart);
        Require(trigger.Contains(
                    "DefenseEffectResolver.ApplyEffectsTransactional(",
                    StringComparison.Ordinal)
                && !trigger.Contains(
                    "DefenseEffectResolver.ApplyEffects(",
                    StringComparison.Ordinal)
                && trigger.Contains(
                    "new DefenseEffectFact(",
                    StringComparison.Ordinal)
                && !trigger.Contains(
                    "DefenseFacilityEffectOutcomeReceipt(",
                    StringComparison.Ordinal),
            "D06 production Trigger still bypasses the neutral transactional effect fact.");
        checks.Add("production-entry-transactional");
    }

    private static double Metric(
        GameplayOutcomeSnapshot outcome,
        GameplayMetricId metricId) => outcome.metrics.Single(value =>
            string.Equals(
                value.metricId,
                metricId.Value,
                StringComparison.Ordinal)).value;

    private static string Fact(
        GameplayOutcomeSnapshot outcome,
        GameplayOutcomeFactId factId) => outcome.facts.Single(value =>
            string.Equals(
                value.factId,
                factId.Value,
                StringComparison.Ordinal)).value;

    private static bool Approximately(double left, double right) =>
        Math.Abs(left - right) <= 0.001d;

    internal sealed class D06Scenario : IDisposable
    {
        private readonly List<Object> cleanup = new();
        private readonly GameplayOutcomeBufferLimits limits;
        private readonly GameplayOutcomeRegistry registry;
        private readonly D06FaultingRecorder recorder;
        private readonly MigratedProducerOutcomeRuntime migrated;
        private readonly BuildingFixture building;
        private readonly ActorFixture target;
        private readonly DefenseFacility facility;
        private readonly DefenseFacilityRuntime runtime;
        private readonly DefenseStatusRuntimeService statuses;
        private readonly DomainFactDispatcher facts;
        private readonly IDisposable activationBinding;
        private readonly IDisposable effectBinding;
        private readonly DefenseEffectSO[] originalEffects;
        private readonly float originalJam;
        private readonly float originalMisfire;

        internal D06Scenario(string scope, bool rejectEffectCommit = false, bool hideActivationAck = false,
            bool hideEffectAck = false, bool rejectActivation = false,
            IGameClock gameClock = null, IGrandProjectBenefitQuery projectBenefits = null,
            float? cooldownSeconds = null)
        {
            string suffix = scope + ":" + Guid.NewGuid().ToString("N");
            var josa = new KoreanJosaFormatter();
            registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new DefenseFacilityEffectOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new MigratedProducerOutcomeAdapter(),
                    new DefenseFacilityEffectOutcomeAdapter()
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new MigratedProducerOutcomeDescriptorCatalog(josa)
                });
            limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 64,
                largePageCount: 8,
                knownResultKeyCapacity: 256,
                retainedSmallPageCount: 64,
                retainedLargePageCount: 8);
            Ledger = new GameplayOutcomeLedger(
                registry,
                limits,
                new GameplayOutcomeRunId("run:d06:" + suffix),
                1L);
            recorder = new D06FaultingRecorder(
                new GameplayOutcomeRecorder(Ledger, registry, new GameEventBus()))
            {
                RejectEffectCommit = rejectEffectCommit
            };
            var migratedStore = new DungeonRuntimeAggregateRootStore();
            migrated = new MigratedProducerOutcomeRuntime(
                migratedStore,
                Ledger,
                recorder,
                Ledger);
            facts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                recorder,
                Ledger);
            activationBinding = new DefenseActivationFactObserver(
                    migrated,
                    commits)
                .Connect(facts);
            effectBinding = DefenseEffectFactMapping.CreateBinding(
                    registry,
                    Ledger,
                    recorder,
                    commits)
                .Connect(facts);
            var delivery = new B94PublicationFault(facts)
            { HideActivation = hideActivationAck, HideEffect = hideEffectAck, RejectActivation = rejectActivation };
            Publications = new DefenseActivationPublicationRuntime(delivery, delivery, facts);
            var grid = new Grid(5, 1);
            FacilityId = "building:d06:" + suffix;
            building = new BuildingFixture(
                grid,
                "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset",
                new Vector2Int(2, 0),
                FacilityId, cloneDefinition: true);
            facility = building.Building as DefenseFacility
                ?? throw new InvalidOperationException(
                    "D06 authored facility is not a defense facility.");
            target = new ActorFixture(
                CharacterType.Intruder,
                "D06 Target",
                suffix);
            TargetId = CharacterPersistentIdentity.Require(target.Actor).Value;
            var events = new GameEventBus();
            runtime = new DefenseFacilityRuntime(
                UnavailableEquipmentPhysicalItemGateway.Instance,
                new NoopDefensePhysicalGateway(),
                new AlwaysReadyDefenseInputOwner(),
                gameClock ?? new UnityGameClock(),
                events,
                new AlwaysPoweredQuery(),
                new NoopDefenseFacilityNetwork(),
                new EmptyFacilityCapabilities(),
                new DungeonRuntimeAggregateRootStore(),
                projectBenefits ?? new R30DefensePreparationBenefits(1f));
            statuses = new DefenseStatusRuntimeService(
                new DefenseStatusRuntimeFactory());
            facility.ConstructDefenseFacilityEventBus(events, null, runtime);
            facility.ConstructDefenseActivation(
                Publications,
                CharacterAiEditorTestDependencies.GameCalendar,
                target.Health);
            if (cooldownSeconds.HasValue)
            {
                facility.Defense.cooldownSeconds = cooldownSeconds.Value;
                int twoActivations = checked(facility.Defense.supplyPerActivation * 2);
                Require(facility.Defense.supplyCapacity >= twoActivations,
                    "Cooldown fixture cannot supply two real activations.");
                facility.Defense.initialSupply = twoActivations;
            }
            originalEffects = facility.Defense.effectAssets;
            originalJam = facility.Defense.baseJamChance;
            originalMisfire = facility.Defense.baseMisfireChance;
            facility.Defense.baseJamChance = 0f;
            facility.Defense.baseMisfireChance = 0f;
            FacilityBefore = runtime.GetSnapshot(facility);
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal DefenseActivationPublicationRuntime Publications { get; }
        internal DomainFactDispatcher Dispatcher => facts;
        internal GameplayOutcomeRegistry Registry => registry;
        internal MigratedProducerOutcomeRuntime Migrated => migrated;
        internal DefenseFacilityRuntime Runtime => runtime;
        internal CharacterActor Target => target.Actor;
        internal DefenseFacility Facility => facility;
        internal string FacilityId { get; }
        internal string TargetId { get; }
        internal DefenseFacilitySnapshot FacilityBefore { get; }
        internal DefenseFacilitySnapshot FacilityAfter => runtime.GetSnapshot(facility);
        internal float TargetHealth => target.Health.GetVitals(target.Actor).CurrentHealth;
        internal int StatusCount => statuses.Get(target.Actor)?.ActiveStatuses.Count ?? 0;
        internal IReadOnlyList<GameplayOutcomeSnapshot> EffectRows =>
            Ledger.CaptureGameplayOutcomes().exactOutcomes.Where(value =>
                string.Equals(
                    value.outcomeTypeId,
                    DefenseFacilityEffectOutcomeIds.Result.Value,
                    StringComparison.Ordinal)).ToArray();

        internal void ConfigureFaultChances(float jam, float misfire)
        {
            facility.Defense.baseJamChance = jam;
            facility.Defense.baseMisfireChance = misfire;
        }

        internal DefenseDamageEffectSO CreateDamage(float amount)
        {
            DefenseDamageEffectSO value =
                ScriptableObject.CreateInstance<DefenseDamageEffectSO>();
            value.Configure(amount, 0f, 1, "피해");
            cleanup.Add(value);
            return value;
        }

        internal DefenseCorrosionEffectSO CreateCorrosion(
            float amount,
            float duration)
        {
            DefenseCorrosionEffectSO value =
                ScriptableObject.CreateInstance<DefenseCorrosionEffectSO>();
            value.Configure(amount, duration, 1, "부식");
            cleanup.Add(value);
            return value;
        }

        internal DefenseEffectSO CreateThrowingStatus()
        {
            D06ThrowingEffect value =
                ScriptableObject.CreateInstance<D06ThrowingEffect>();
            value.Configure(0.4f, 10f, 1, "실패 상태");
            cleanup.Add(value);
            return value;
        }

        internal void SetEffects(params DefenseEffectSO[] effects) =>
            facility.Defense.effectAssets = effects ?? Array.Empty<DefenseEffectSO>();

        internal DefenseActivationReport Trigger()
        {
            var result = facility.Trigger(target.Actor, DefenseTriggerTiming.OnEnter, statuses);
            DefenseActivationFactPreflight.ValidateOwners(Publications.Capture(), runtime.CaptureState());
            DefenseActivationFactPreflight.ValidateJournal(Publications.Capture(), Ledger.CaptureGameplayOutcomes(), migrated.Capture());
            return result;
        }

        internal GameplayOutcomeSnapshot RequireEffect() =>
            EffectRows.Single();

        internal GameplayOutcomeSnapshot RequireActivation() =>
            RequireMigrated(
                MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent);

        internal GameplayOutcomeSnapshot RequireStateChange() =>
            RequireMigrated(
                MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent);

        internal void RequireTypedFacilityAndTarget(
            GameplayOutcomeSnapshot effect)
        {
            Require(effect.participants.Count == 2
                    && effect.participants.Any(value =>
                        value.entityKindId == "facility"
                        && value.entityId == FacilityId
                        && value.roleId == "source-facility")
                    && effect.participants.Any(value =>
                        value.entityKindId == "character"
                        && value.entityId == TargetId
                        && value.roleId == "target")
                    && effect.subjects.Any(value =>
                        value.entityKindId == "facility"
                        && value.entityId == FacilityId)
                    && effect.subjects.Any(value =>
                        value.entityKindId == "character"
                        && value.entityId == TargetId),
                "D06 effect did not expose typed facility/target participants and subjects.");
        }

        internal GameplayOutcomeLedger CreateEmptyLedger() => new(
            registry,
            limits,
            new GameplayOutcomeRunId(Ledger.CaptureGameplayOutcomes().runId),
            Ledger.CaptureGameplayOutcomes().worldEpoch);

        private GameplayOutcomeSnapshot RequireMigrated(
            MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return Ledger.CaptureGameplayOutcomes().exactOutcomes.Single(value =>
                string.Equals(value.outcomeTypeId, type, StringComparison.Ordinal));
        }

        public void Dispose()
        {
            facility.Defense.effectAssets = originalEffects;
            facility.Defense.baseJamChance = originalJam;
            facility.Defense.baseMisfireChance = originalMisfire;
            effectBinding.Dispose();
            activationBinding.Dispose();
            facts.Dispose();
            target.Dispose();
            building.Dispose();
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
        }
    }

    private sealed class D06ThrowingEffect : DefenseEffectSO
    {
        public override string EffectId => "defense.test-throw";
        public override string DisplayName => "실패 상태";

        public override void Apply(IDefenseEffectCommands context)
        {
            context.ApplyStatus(
                DefenseStatusKind.Corrosion,
                Amount,
                Duration,
                Stacks);
            context.AddEffectTag(DisplayName);
            throw new InvalidOperationException("injected-d06-effect-failure");
        }
    }

    private sealed class D06FaultingRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;

        internal D06FaultingRecorder(IGameplayOutcomeRecorder inner) =>
            this.inner = inner;

        internal bool RejectEffectCommit { get; set; }

        public OutcomePrepareResult TryPrepare<T>(
            in T receipt,
            out PreparedOutcomeToken prepared) =>
            inner.TryPrepare(receipt, out prepared);

        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation) =>
            inner.TryReserve(requirements, out reservation);

        public OutcomePrepareResult TryWriteReserved<T>(
            in T receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared) =>
            inner.TryWriteReserved(receipt, reservation, out prepared);

        public void CancelReservation(
            in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);

        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            inner.CancelPrepared(prepared);

        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed)
        {
            if (RejectEffectCommit
                && string.Equals(
                    prepared.ResultKey.ProducerId,
                    DefenseFacilityEffectOutcomeIds.ProducerId,
                    StringComparison.Ordinal))
            {
                committed = default;
                return new OutcomeCommitResult(
                    OutcomeCommitCode.OwnerRevisionMismatch,
                    "injected-d06-effect-commit-rejection");
            }
            return inner.CommitPrepared(
                prepared,
                expectedOwnerRevision,
                out committed);
        }

        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] expectedOwnerRevisions,
            CommittedOutcomeToken[] committed)
        {
            if (RejectEffectCommit
                && prepared != null
                && prepared.Any(value => string.Equals(
                    value.ResultKey.ProducerId,
                    DefenseFacilityEffectOutcomeIds.ProducerId,
                    StringComparison.Ordinal)))
            {
                if (committed != null)
                    Array.Clear(committed, 0, committed.Length);
                return new OutcomeCommitResult(
                    OutcomeCommitCode.OwnerRevisionMismatch,
                    "injected-d06-effect-commit-rejection");
            }
            return inner.CommitPreparedBatch(
                prepared,
                expectedOwnerRevisions,
                committed);
        }

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
