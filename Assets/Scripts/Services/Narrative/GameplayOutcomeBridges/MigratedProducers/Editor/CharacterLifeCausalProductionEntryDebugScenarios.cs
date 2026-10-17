#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class CharacterLifeCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:character-life-progression";
    internal const string AgeConditionActionId =
        "action:character-age-condition-changed";
    internal const string LifeStageActionId =
        "action:character-life-stage-changed";

    private const string OwnerKind = "owner-kind:character-life-progression";
    private const string DomainStateSchema =
        "dungeonstory-causal-character-life-owner-state@1";
    private const string CharacterIdValue =
        "character:world:causal:life-progression";
    private const string SpeciesIdValue = "species:causal:life-progression";
    private const string ConditionId = "age:causal:life-progression";
    private const int InitialChronologicalAgeDays = 100;
    private const int FirstAbsoluteDay = 2;
    private const int SecondAbsoluteDay = 3;

    private static DomainFactDispatcher CreateCharacterLifeFacts(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        var dispatcher = new DomainFactDispatcher();
        try
        {
            dispatcher.Subscribe(
                "fixture:character-life-transition",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterLifeTransitionFact>(
                    CharacterLifeTransitionFactMapping.Capture,
                    outcomes.Transaction,
                    new GameplayDomainFactCommitGroup(
                        outcomes.Recorder,
                        outcomes.Ledger),
                    probe));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A character-life checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                AgeConditionActionId,
                "life",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent)
                    .OutcomeTypeId,
                context.CaptureState,
                context.AdvanceAgeCondition,
                context.RequireAgeConditionOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                LifeStageActionId,
                "life",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent)
                    .OutcomeTypeId,
                context.CaptureState,
                context.AdvanceLifeStage,
                context.RequireLifeStageOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue = "life-owner:causal-progression:focused";
        JObject ageCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-character-life:age-condition",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture age = fixtures.Get(AgeConditionActionId);
            IReadOnlyDictionary<string, object> before = age.CaptureState();
            Require((int)before["characterLife.stage"] == 0
                    && (int)before["characterLife.chronologicalAgeDays"] == 100
                    && (int)before["characterLife.conditionSeverity"] == 0
                    && (int)before["characterLife.exactOutcomeCount"] == 0,
                "Character-life owner did not start at the causal boundary.");
            Require(age.Execute(), "The real age-condition progression did not complete.");
            Require(age.RequireExactOutcome().HasCanonicalPayloadHash,
                "The age-condition outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = age.CaptureState();
            Require((int)after["characterLife.stage"] == 1
                    && (int)after["characterLife.chronologicalAgeDays"] == 101
                    && (int)after["characterLife.conditionSeverity"] == 1
                    && string.Equals(
                        (string)after["characterLife.lifeStage"],
                        CharacterLifeStage.Adolescent.ToString(),
                        StringComparison.Ordinal)
                    && (int)after["characterLife.exactOutcomeCount"] == 1,
                "The first daily advance lost its age-condition state.");
            ageCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)age)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-character-life:life-stage",
                   OwnerIdValue,
                   ageCheckpoint)))
        {
            IProductionCausalActionFixture age = fixtures.Get(AgeConditionActionId);
            IProductionCausalActionFixture stage = fixtures.Get(LifeStageActionId);
            IReadOnlyDictionary<string, object> restored = stage.CaptureState();
            Require((int)restored["characterLife.stage"] == 1 && !age.Execute(),
                "Fresh composition restore lost or replayed the age-condition day.");
            Require(stage.Execute(), "The restored character did not reach adulthood.");
            Require(stage.RequireExactOutcome().HasCanonicalPayloadHash,
                "The life-stage outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = stage.CaptureState();
            Require((int)after["characterLife.stage"] == 2
                    && (int)after["characterLife.chronologicalAgeDays"] == 102
                    && (int)after["characterLife.conditionSeverity"] == 1
                    && string.Equals(
                        (string)after["characterLife.lifeStage"],
                        CharacterLifeStage.Adult.ToString(),
                        StringComparison.Ordinal)
                    && (int)after["characterLife.exactOutcomeCount"] == 2,
                "The second daily advance did not preserve illness history and adulthood.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)stage)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-character-life:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture age = fixtures.Get(AgeConditionActionId);
            IProductionCausalActionFixture stage = fixtures.Get(LifeStageActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)age)
                .CaptureOwnerCheckpoint();
            Require(!age.Execute() && !stage.Execute(),
                "Terminal character-life owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)stage)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected character-life replay mutated the owner checkpoint.");
        }

        JObject ageTamper = (JObject)ageCheckpoint.DeepClone();
        ((JObject)ageTamper["domainState"])["characterLife"]!["characters"]![0]![
            "biologicalAgeDayUnits"] = Catalog.Instance.History.AdultAgeDayUnits;
        Require(RejectCheckpoint(OwnerIdValue, ageTamper, "age-tamper"),
            "A character-life checkpoint with changed biological age was accepted.");

        JObject identityTamper = (JObject)ageCheckpoint.DeepClone();
        identityTamper["ownerId"] = "life-owner:causal-progression:tampered";
        Require(RejectCheckpoint(OwnerIdValue, identityTamper, "identity-tamper"),
            "A character-life checkpoint with changed owner identity was accepted.");

        if (log)
        {
            Debug.Log(
                "Age condition -> restored life-stage transition causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Character-life causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    internal static void VerifyAmbiguousObservationRecovery()
    {
        var aggregateRoot = new DungeonRuntimeAggregateRootStore();
        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "run:causal-character-life:ambiguous",
            aggregateRoot);
        using var dispatcher = new DomainFactDispatcher();
        IDisposable binding = CharacterLifeTransitionFactMapping
            .CreateBinding(
                outcomes.Transaction,
                new GameplayDomainFactCommitGroup(
                    outcomes.Recorder,
                    outcomes.Ledger))
            .Connect(dispatcher);
        try
        {
            CharacterLifeRuntime life = CreateInitialLife(aggregateRoot);
            var ambiguous = new CommittedAmbiguousFactPublisher(
                dispatcher);
            var events = new GameEventBus();
            using (var adapter = new CharacterLifeApplicationAdapter(
                       life,
                       events,
                       NeutralHeritableTraitEffects.Instance,
                       ambiguous,
                       ambiguous))
            {
                adapter.Start();
                bool uncertain = false;
                try
                {
                    events.Publish(new OperatingDayEndedEvent(1));
                }
                catch (InvalidOperationException exception)
                {
                    uncertain = exception.Message.Contains(
                        "life-observation-delivery-pending",
                        StringComparison.Ordinal);
                }
                CharacterLifeRecord onceAged = RequireRecord(life);
                Require(uncertain
                        && ambiguous.PublishCount == 1
                        && onceAged.ChronologicalAgeDays
                            == InitialChronologicalAgeDays + 1
                        && life.LastAdvancedAbsoluteDay == FirstAbsoluteDay
                        && life.PendingDailyObservation.entries.Count == 1
                        && outcomes.Ledger.CaptureGameplayOutcomes()
                            .exactOutcomes.Count == 1,
                    "Ambiguous character-life commit did not retain its once-aged state and pending replay.");

                bool recoveryBlocked = false;
                try
                {
                    events.Publish(new OperatingDayEndedEvent(1));
                }
                catch (InvalidOperationException exception)
                {
                    recoveryBlocked = exception.Message.Contains(
                        CommittedAmbiguousFactPublisher.RecoveryFailure,
                        StringComparison.Ordinal);
                }
                Require(recoveryBlocked
                        && ambiguous.RecoveryCount == 1
                        && RequireRecord(life).ChronologicalAgeDays
                            == InitialChronologicalAgeDays + 1
                        && outcomes.Ledger.CaptureGameplayOutcomes()
                            .exactOutcomes.Count == 1,
                    "Blocked character-life recovery repeated its daily aging or ledger result.");
            }

            string saved = JsonUtility.ToJson(life.Capture());
            var restoredRoot = new DungeonRuntimeAggregateRootStore();
            var restoredLife = new CharacterLifeRuntime(
                restoredRoot,
                Catalog.Instance,
                new RandomStreamProvider(104729));
            restoredLife.PublishRestore(restoredLife.PrepareRestore(
                JsonUtility.FromJson<CharacterLifeWorldSaveData>(saved)));
            var restoredEvents = new GameEventBus();
            using (var restoredAdapter = new CharacterLifeApplicationAdapter(
                       restoredLife,
                       restoredEvents,
                       NeutralHeritableTraitEffects.Instance,
                       dispatcher,
                       dispatcher))
            {
                restoredAdapter.Start();
                Require(restoredLife.PendingDailyObservation.entries.Count == 0
                        && RequireRecord(restoredLife).ChronologicalAgeDays
                            == InitialChronologicalAgeDays + 1
                        && outcomes.Ledger.CaptureGameplayOutcomes()
                            .exactOutcomes.Count == 1,
                    "Fresh character-life adapter did not replay its pending observation exactly once.");
                restoredEvents.Publish(new OperatingDayEndedEvent(1));
                Require(RequireRecord(restoredLife).ChronologicalAgeDays
                            == InitialChronologicalAgeDays + 1
                        && restoredLife.LastAdvancedAbsoluteDay
                            == FirstAbsoluteDay
                        && outcomes.Ledger.CaptureGameplayOutcomes()
                            .exactOutcomes.Count == 1,
                    "Character-life same-day guard repeated aging after recovery.");
            }
        }
        finally
        {
            binding.Dispose();
        }
    }

    private static CharacterLifeRuntime CreateInitialLife(
        DungeonRuntimeAggregateRootStore aggregateRoot)
    {
        var life = new CharacterLifeRuntime(
            aggregateRoot,
            Catalog.Instance,
            new RandomStreamProvider(104729));
        double adult = Catalog.Instance.History.AdultAgeDayUnits;
        var data = new CharacterLifeWorldSaveData();
        data.characters.Add(new CharacterLifeRecordSaveData
        {
            characterId = CharacterIdValue,
            phenotypeSpeciesId = SpeciesIdValue,
            chronologicalAgeDays = InitialChronologicalAgeDays,
            biologicalAgeDayUnits = adult - 8d,
            birthdayDayOfYear = 1,
            lifeStage = CharacterLifeStage.Adolescent,
            ageConditions = new List<CharacterAgeConditionSaveData>
            {
                new()
                {
                    conditionId = ConditionId,
                    severity = AgeConditionSeverity.Mild,
                    onsetBiologicalAgeDayUnits = 1d,
                    nextProgressBiologicalAgeDayUnits = adult - 4d
                }
            }
        });
        life.PublishRestore(life.PrepareRestore(data));
        return life;
    }

    private static CharacterLifeRecord RequireRecord(
        CharacterLifeRuntime life) =>
        life.TryGet(new CharacterId(CharacterIdValue), out CharacterLifeRecord value)
            ? value
            : throw new InvalidOperationException(
                "Character-life ambiguity fixture record disappeared.");

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-character-life:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly CharacterLifeRuntime life;
        private readonly GameEventBus events;
        private readonly DomainFactDispatcher facts;
        private readonly CharacterLifeApplicationAdapter adapter;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":character-life-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            facts = CreateCharacterLifeFacts(outcomes, probe);
            life = new CharacterLifeRuntime(
                aggregateRootStore,
                Catalog.Instance,
                new RandomStreamProvider(104729));
            events = new GameEventBus();
            adapter = new CharacterLifeApplicationAdapter(
                life,
                events,
                NeutralHeritableTraitEffects.Instance,
                facts,
                facts);
            try
            {
                if (checkpoint == null)
                    RestoreInitialLifeState();
                else
                    RestoreOwnerCheckpoint(checkpoint);
                adapter.Start();
                probe.ClearObservations();
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            CharacterLifeRecord record = Record;
            CharacterAgeConditionState condition = Condition;
            return new Dictionary<string, object>
            {
                ["characterLife.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "life-owner:ephemeral-progression",
                ["characterLife.stage"] = Stage,
                ["characterLife.absoluteDay"] = Stage == 0
                    ? 1
                    : Stage == 1 ? FirstAbsoluteDay : SecondAbsoluteDay,
                ["characterLife.characterId"] = CharacterIdValue,
                ["characterLife.chronologicalAgeDays"] =
                    record.ChronologicalAgeDays,
                ["characterLife.biologicalAgeDayUnits"] =
                    record.BiologicalAgeDayUnits,
                ["characterLife.lifeStage"] = record.LifeStage.ToString(),
                ["characterLife.conditionId"] = condition.ConditionId,
                ["characterLife.conditionSeverity"] = (int)condition.Severity,
                ["characterLife.conditionNextProgressDayUnits"] =
                    condition.NextProgressBiologicalAgeDayUnits,
                ["characterLife.ageConditionOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent),
                ["characterLife.lifeStageOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent),
                ["characterLife.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool AdvanceAgeCondition() => AdvanceAtStage(0, 1, 1);
        internal bool AdvanceLifeStage() => AdvanceAtStage(1, 2, 2);

        internal GameplayOutcomeReplayIdentity RequireAgeConditionOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent,
                expectedSequence: 1,
                expectedAbsoluteDay: FirstAbsoluteDay,
                expectedSummary: string.Join("|", new[]
                {
                    "age-condition-change@1",
                    CharacterIdValue,
                    ConditionId,
                    "0",
                    "1",
                    "progressed",
                    "active"
                }));

        internal GameplayOutcomeReplayIdentity RequireLifeStageOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent,
                expectedSequence: 2,
                expectedAbsoluteDay: SecondAbsoluteDay,
                expectedSummary: string.Join("|", new[]
                {
                    "life-stage-change@1",
                    CharacterIdValue,
                    ((int)CharacterLifeStage.Adolescent).ToString(),
                    ((int)CharacterLifeStage.Adult).ToString()
                }));

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral character-life fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["characterId"] = CharacterIdValue,
                    ["conditionId"] = ConditionId,
                    ["characterLife"] = JObject.Parse(
                        JsonUtility.ToJson(life.Capture())),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture()))
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            adapter.Dispose();
            facts.Dispose();
        }

        private CharacterLifeRecord Record
        {
            get
            {
                if (!life.TryGet(new CharacterId(CharacterIdValue), out var record))
                    throw new InvalidOperationException("Character-life owner disappeared.");
                return record;
            }
        }

        private CharacterAgeConditionState Condition => Record.AgeConditions.Single(
            value => string.Equals(
                value.ConditionId,
                ConditionId,
                StringComparison.Ordinal));

        private int Stage
        {
            get
            {
                CharacterLifeRecord record = Record;
                CharacterAgeConditionState condition = Condition;
                int ages = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent);
                int stages = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent);
                double adult = Catalog.Instance.History.AdultAgeDayUnits;
                if (ages == 0 && stages == 0
                    && record.ChronologicalAgeDays == InitialChronologicalAgeDays
                    && Math.Abs(record.BiologicalAgeDayUnits - (adult - 8d)) < 0.001d
                    && record.LifeStage == CharacterLifeStage.Adolescent
                    && condition.Severity == AgeConditionSeverity.Mild
                    && Math.Abs(condition.NextProgressBiologicalAgeDayUnits
                        - (adult - 4d)) < 0.001d)
                    return 0;
                if (ages == 1 && stages == 0
                    && record.ChronologicalAgeDays == InitialChronologicalAgeDays + 1
                    && Math.Abs(record.BiologicalAgeDayUnits - (adult - 4d)) < 0.001d
                    && record.LifeStage == CharacterLifeStage.Adolescent
                    && condition.Severity == AgeConditionSeverity.Moderate
                    && Math.Abs(condition.NextProgressBiologicalAgeDayUnits
                        - (adult - 4d + GameCalendarRules.DaysPerYear)) < 0.001d)
                    return 1;
                if (ages == 1 && stages == 1
                    && record.ChronologicalAgeDays == InitialChronologicalAgeDays + 2
                    && Math.Abs(record.BiologicalAgeDayUnits - adult) < 0.001d
                    && record.LifeStage == CharacterLifeStage.Adult
                    && condition.Severity == AgeConditionSeverity.Moderate)
                    return 2;
                return -1;
            }
        }

        private bool AdvanceAtStage(int expectedStage, int expectedAfter, int endedDay)
        {
            ThrowIfDisposed();
            if (Stage != expectedStage)
                return false;
            events.Publish(new OperatingDayEndedEvent(endedDay));
            bool valid = Stage == expectedAfter;
            if (valid)
                ValidateState();
            return valid;
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedSequence,
            int expectedAbsoluteDay,
            string expectedSummary)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.absoluteDay != expectedAbsoluteDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || !string.Equals(exact.producerId, definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.participants == null
                || exact.participants.Count != 1
                || exact.subjects == null
                || exact.subjects.Count != 1
                || !string.Equals(exact.participants[0].entityKindId,
                    MigratedProducerOutcomeIds.CharacterKind.Value,
                    StringComparison.Ordinal)
                || !string.Equals(exact.participants[0].entityId,
                    CharacterIdValue,
                    StringComparison.Ordinal)
                || !string.Equals(exact.participants[0].roleId,
                    MigratedProducerOutcomeIds.ActorRole.Value,
                    StringComparison.Ordinal)
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                    value.factId,
                    "summary",
                    StringComparison.Ordinal)
                    && string.Equals(value.value, expectedSummary,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Character-life production entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(value.outcomeTypeId, type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes.Count;

        private void RestoreInitialLifeState()
        {
            double adult = Catalog.Instance.History.AdultAgeDayUnits;
            var data = new CharacterLifeWorldSaveData();
            data.characters.Add(new CharacterLifeRecordSaveData
            {
                characterId = CharacterIdValue,
                phenotypeSpeciesId = SpeciesIdValue,
                chronologicalAgeDays = InitialChronologicalAgeDays,
                biologicalAgeDayUnits = adult - 8d,
                birthdayDayOfYear = 1,
                lifeStage = CharacterLifeStage.Adolescent,
                ageConditions = new List<CharacterAgeConditionSaveData>
                {
                    new()
                    {
                        conditionId = ConditionId,
                        severity = AgeConditionSeverity.Mild,
                        onsetBiologicalAgeDayUnits = 1d,
                        nextProgressBiologicalAgeDayUnits = adult - 4d
                    }
                }
            });
            life.PublishRestore(life.PrepareRestore(data));
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "character-life owner state",
                "schemaVersion",
                "schema",
                "stage",
                "characterId",
                "conditionId",
                "characterLife",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals((string)state["schema"], DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["characterId"], CharacterIdValue,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["conditionId"], ConditionId,
                    StringComparison.Ordinal)
                || state["characterLife"] is not JObject lifeJson
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Character-life checkpoint identity changed.");
            }

            DungeonMigratedProducerOutcomeSaveData ownerData =
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None));
            outcomes.Transaction.Restore(
                outcomes.Transaction.PrepareRestore(ownerData));
            CharacterLifeWorldSaveData lifeData =
                JsonUtility.FromJson<CharacterLifeWorldSaveData>(
                    lifeJson.ToString(Formatting.None));
            life.PublishRestore(life.PrepareRestore(lifeData));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Character-life checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            bool valid = stage is >= 0 and <= 2
                && life.Records.Count == 1
                && Record.AgeConditions.Count == 1
                && ExactOutcomeCount() == stage
                && OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent)
                    == (stage == 0 ? 0 : 1)
                && OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent)
                    == (stage == 2 ? 1 : 0);
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Character life, outcome owner and exact ledger diverged.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class CommittedAmbiguousFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        internal const string RecoveryFailure =
            "qa-character-life-recovery-unavailable";
        private const string DeliveryFailure =
            "qa-character-life-delivery-ambiguous";
        private readonly IDomainFactPublisher inner;

        internal CommittedAmbiguousFactPublisher(
            IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal int PublishCount { get; private set; }
        internal int RecoveryCount { get; private set; }

        public bool TryReserve<TFact>(in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (!delivered.Completed)
            {
                throw new InvalidOperationException(
                    "Character-life ambiguity fixture requires a committed delivery.");
            }
            PublishCount++;
            return InDoubt(delivered);
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (!accepted || results.Any(value => !value.Completed))
            {
                throw new InvalidOperationException(
                    "Character-life ambiguity fixture requires a committed cohort.");
            }
            PublishCount += results.Length;
            for (int index = 0; index < results.Length; index++)
                results[index] = InDoubt(results[index]);
            failure = DeliveryFailure;
            return true;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact)
        {
            _ = fact;
            RecoveryCount++;
            return DomainFactDelivery.Reject(RecoveryFailure);
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (facts == null || results == null || facts.Count == 0
                || facts.Count != results.Length)
            {
                throw new ArgumentException(
                    "Invalid blocked character-life replay shape.");
            }
            RecoveryCount++;
            for (int index = 0; index < results.Length; index++)
                results[index] = DomainFactDelivery.Reject(RecoveryFailure);
            failure = RecoveryFailure;
            return false;
        }

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        private static DomainFactDelivery InDoubt(
            in DomainFactDelivery delivered) => new(
            DomainFactDeliveryPhase.InDoubt,
            delivered.OperationKey,
            string.Empty,
            string.Empty,
            DeliveryFailure);
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures ?? throw new ArgumentNullException(nameof(fixtures));

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private sealed class Catalog : ICharacterLifeDefinitionCatalog
    {
        internal static readonly Catalog Instance = new();
        internal SpeciesLifeHistoryDefinition History { get; } = new(
            new CharacterSpeciesId(SpeciesIdValue),
            1,
            5,
            10,
            70,
            80f,
            false);
        private readonly AgeConditionDefinition condition = new(
            ConditionId,
            false,
            new[] { "body:core" });

        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) => speciesId.Equals(History.SpeciesId)
                ? History
                : throw new KeyNotFoundException(speciesId.Value);

        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            string.Equals(conditionId, condition.ConditionId,
                StringComparison.Ordinal)
                ? condition
                : throw new KeyNotFoundException(conditionId);

        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => construct == condition.ConstructCondition
                ? new[] { condition }
                : Array.Empty<AgeConditionDefinition>();
    }

    private sealed class NeutralHeritableTraitEffects :
        IHeritableTraitEffectQuery
    {
        internal static readonly NeutralHeritableTraitEffects Instance = new();

        public float GetMultiplier(
            CharacterId characterId,
            HeritableTraitConsequenceKind kind,
            string targetId) => 1f;
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
