#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class CaptiveRansomOutcomeDebugScenarios
{
    private const string IdPrefix = "character:qa-captive-ransom-";

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyCanonicalOutcome();
        VerifyProductionOwnerBoundaries();
        VerifySaveCompatibility();
        if (logSuccess)
            Debug.Log("[Captive Ransom Outcome] PASS");
        return true;
    }

    private static void VerifyCanonicalOutcome()
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new CaptiveRansomOutcomeDescriptor(new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new CaptiveRansomOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = NewLedger(registry, "run:captive-ransom");
        CaptiveRansomGameplayOutcomeBridge bridge = new(
            new GameplayOutcomeRecorder(ledger, registry, new GameEventBus()),
            ledger);
        CaptiveRansomOutcomeRuntime runtime = new(
            bridge,
            new GameEventBus(),
            new FakeWorldRegistry(),
            new FakeClock());
        CaptiveState state = State("canonical", CaptivityStatus.Confined);

        Require(
            runtime.TryPrepare(
                state,
                CaptivityStatus.Confined,
                120,
                43f,
                12,
                out PreparedCaptiveRansomOutcome prepared,
                out long revision,
                out string failure)
            && revision == 1L
            && !prepared.IsReplay,
            "canonical ransom prepare failed: " + failure);
        state.status = CaptivityStatus.Ransom;
        state.ransomOutcomeRevision = revision;
        state.ransomAcceptedAmount = 120;
        Require(
            runtime.Commit(prepared).DurablyCommitted,
            "canonical ransom commit was not durable");

        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayEntityId captive = new(
            CaptiveRansomOutcomeIds.CharacterKind,
            state.captiveId);
        GameplayOutcomeQueryPage character = ledger.GetForEntity(
            captive,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == 1
            && character.Items.Count == 1
            && character.Items[0].Exact != null
            && character.Items[0].Exact.participants.Single().displayText == "가람"
            && Fact(
                character.Items[0].Exact,
                CaptiveRansomOutcomeIds.PreviousStatusFact.Value)
                == CaptivityStatus.Confined.ToString(),
            "ransom global/entity query shape drifted");
        GameplayOutcomeSnapshot exact = character.Items[0].Exact;
        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(exact.runId),
            exact.sequence);
        Require(
            ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    captive,
                    NarrativePerspectiveKind.Character,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("가람은", StringComparison.Ordinal)
            && view.Text.Contains("120", StringComparison.Ordinal),
            "ransom Korean projection was not deterministic");

        CaptiveState replayState = State("canonical", CaptivityStatus.Confined);
        Require(
            runtime.TryPrepare(
                replayState,
                CaptivityStatus.Confined,
                120,
                43f,
                12,
                out PreparedCaptiveRansomOutcome replay,
                out _,
                out string replayFailure)
            && replay.IsReplay
            && runtime.Commit(replay).DurablyCommitted
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "canonical ransom replay failed or duplicated: " + replayFailure);

        CaptiveRansomOutcomeReceipt drifted = new(
            state.captiveId,
            Name("canonical"),
            CaptivityStatus.Confined,
            121,
            43f,
            1L,
            12);
        Require(
            !bridge.TryPrepare(drifted, out _, out _),
            "same ransom result key accepted a drifted amount");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = NewLedger(
            registry,
            "run:captive-ransom-restore");
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Single().Exact.immutablePayloadHash
            == exact.immutablePayloadHash,
            "ransom outcome changed across ledger save round trip");
    }

    private static void VerifyProductionOwnerBoundaries()
    {
        using (OwnerFixture rejected = new(
                   "rejected",
                   OwnerOutcomeCommitPhase.Rejected,
                   "qa-ransom-rejected"))
        {
            Require(
                !rejected.Runtime.TryRansom(
                    rejected.Id,
                    out int paid,
                    out string failure)
                && paid == 0
                && failure == "qa-ransom-rejected"
                && rejected.State.status == CaptivityStatus.Confined
                && rejected.State.ransomOutcomeRevision == 0L
                && rejected.State.ransomAcceptedAmount == 0
                && !rejected.State.ransomIncomeCredited
                && rejected.Money.CreditCount == 0
                && rejected.Terminal.AttemptCount == 0
                && rejected.Actor.characterType == CharacterType.NPC
                && rejected.Doors.IsCaptive(rejected.Id)
                && rejected.EventCount == 0,
                "definite ransom rejection did not restore every owner");
        }

        using (OwnerFixture pending = new(
                   "pending",
                   OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                   "qa-delivery-pending"))
        {
            Require(
                pending.Runtime.TryRansom(
                    pending.Id,
                    out int paid,
                    out string failure)
                && string.IsNullOrEmpty(failure)
                && paid == pending.State.ransomAcceptedAmount
                && paid > 0
                && pending.State.status == CaptivityStatus.Released
                && pending.State.ransomOutcomeRevision == 1L
                && pending.State.ransomIncomeCredited
                && !pending.State.ransomObserversPending
                && pending.Money.CreditCount == 1
                && pending.Terminal.AttemptCount == 1
                && pending.Actor.characterType == CharacterType.Intruder
                && !pending.Doors.IsCaptive(pending.Id)
                && pending.EventCount == 1,
                "delivery-pending ransom did not durably finalize: " + failure);
        }

        using (OwnerFixture creditRetry = new(
                   "credit-retry",
                   OwnerOutcomeCommitPhase.PublishedAcknowledged,
                   string.Empty,
                   creditFailures: 1))
        {
            Require(
                !creditRetry.Runtime.TryRansom(
                    creditRetry.Id,
                    out int firstPaid,
                    out string firstFailure)
                && firstPaid == 0
                && firstFailure.Contains("마무리를 재시도", StringComparison.Ordinal)
                && creditRetry.State.status == CaptivityStatus.Ransom
                && creditRetry.State.ransomOutcomeRevision == 1L
                && !creditRetry.State.ransomIncomeCredited
                && creditRetry.Terminal.AttemptCount == 0
                && creditRetry.EventCount == 0,
                "failed ransom credit did not remain durably pending");
            Require(
                creditRetry.Runtime.TryFinalizeNextPending(
                    new[] { creditRetry.State },
                    out bool attempted,
                    out string retryFailure)
                && attempted
                && string.IsNullOrEmpty(retryFailure)
                && creditRetry.State.status == CaptivityStatus.Released
                && creditRetry.Money.CreditCount == 1
                && creditRetry.EventCount == 1,
                "pending ransom credit did not recover exactly once: "
                + retryFailure);
        }

        using (OwnerFixture physicalRetry = new(
                   "physical-retry",
                   OwnerOutcomeCommitPhase.PublishedAcknowledged,
                   string.Empty,
                   terminalFailures: 1))
        {
            Require(
                !physicalRetry.Runtime.TryRansom(
                    physicalRetry.Id,
                    out _,
                    out _)
                && physicalRetry.State.status == CaptivityStatus.Ransom
                && physicalRetry.State.ransomIncomeCredited
                && physicalRetry.Money.CreditCount == 1
                && physicalRetry.Terminal.AttemptCount == 1
                && physicalRetry.EventCount == 0,
                "failed physical ransom finalization did not retain credit state");
            Require(
                physicalRetry.Runtime.TryFinalizeNextPending(
                    new[] { physicalRetry.State },
                    out bool attempted,
                    out string retryFailure)
                && attempted
                && string.IsNullOrEmpty(retryFailure)
                && physicalRetry.State.status == CaptivityStatus.Released
                && physicalRetry.Money.CreditCount == 1
                && physicalRetry.Terminal.AttemptCount == 2
                && physicalRetry.EventCount == 1,
                "physical ransom retry duplicated credit or observer: "
                + retryFailure);
        }

        using (OwnerFixture observerFault = new(
                   "observer-fault",
                   OwnerOutcomeCommitPhase.PublishedAcknowledged,
                   string.Empty,
                   eventBus: new ThrowingEventBus()))
        {
            Require(
                observerFault.Runtime.TryRansom(
                    observerFault.Id,
                    out int paid,
                    out string failure)
                && paid > 0
                && string.IsNullOrEmpty(failure)
                && observerFault.State.status == CaptivityStatus.Released
                && observerFault.State.ransomOutcomeRevision == 1L
                && observerFault.State.ransomIncomeCredited,
                "observer fault rolled back a committed ransom");
        }
    }

    private static void VerifySaveCompatibility()
    {
        CaptiveState committed = State("save", CaptivityStatus.Released);
        committed.ransomOutcomeRevision = 1L;
        committed.ransomAcceptedAmount = 120;
        committed.ransomIncomeCredited = true;
        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(Save(committed)));
        DungeonGameRestoreReport roundTripReport = new();
        CaptivitySaveValidation.Validate(roundTrip, roundTripReport);
        Require(
            roundTripReport.Success
            && roundTrip.captives.Single().ransomOutcomeRevision == 1L
            && roundTrip.captives.Single().ransomAcceptedAmount == 120
            && roundTrip.captives.Single().ransomIncomeCredited,
            "captivity save round trip lost ransom state: "
            + string.Join(" | ", roundTripReport.Errors));

        CaptiveState pending = State("save-pending", CaptivityStatus.Ransom);
        pending.ransomOutcomeRevision = 1L;
        pending.ransomAcceptedAmount = 90;
        pending.ransomObserversPending = true;
        DungeonGameRestoreReport pendingReport = new();
        CaptivitySaveValidation.Validate(Save(pending), pendingReport);
        Require(
            pendingReport.Success,
            "durable pending ransom state was rejected: "
            + string.Join(" | ", pendingReport.Errors));

        CaptiveState legacy = State("legacy", CaptivityStatus.Released);
        DungeonGameRestoreReport legacyReport = new();
        CaptivitySaveValidation.Validate(Save(legacy), legacyReport);
        Require(
            legacyReport.Success,
            "legacy released state with revision zero was rejected: "
            + string.Join(" | ", legacyReport.Errors));

        CaptiveState torn = committed.Clone();
        torn.ransomIncomeCredited = false;
        DungeonGameRestoreReport tornReport = new();
        CaptivitySaveValidation.Validate(Save(torn), tornReport);
        Require(!tornReport.Success, "released ransom without credit was accepted");

        CaptiveState invented = legacy.Clone();
        invented.ransomAcceptedAmount = 12;
        DungeonGameRestoreReport inventedReport = new();
        CaptivitySaveValidation.Validate(Save(invented), inventedReport);
        Require(!inventedReport.Success, "revision-zero ransom data was invented");

        CaptiveState negative = committed.Clone();
        negative.ransomOutcomeRevision = -1L;
        DungeonGameRestoreReport negativeReport = new();
        CaptivitySaveValidation.Validate(Save(negative), negativeReport);
        Require(!negativeReport.Success, "negative ransom revision was accepted");
    }

    private static GameplayOutcomeLedger NewLedger(
        IGameplayOutcomeRegistry registry,
        string runId) => new(
        registry,
        new GameplayOutcomeBufferLimits(
            smallPageCount: 8,
            largePageCount: 0,
            knownResultKeyCapacity: 128),
        new GameplayOutcomeRunId(runId),
        1L);

    private static CaptiveState State(
        string suffix,
        CaptivityStatus status) => new()
    {
        captiveId = Id(suffix),
        displayName = "가람",
        status = status,
        policyId = CaptivityPolicyIds.Standard,
        restrained = status is CaptivityStatus.Confined
            or CaptivityStatus.Labor,
        grudge = 40f,
        retaliationPressure = 10f
    };

    private static CaptivitySaveData Save(CaptiveState state) => new()
    {
        captives = new List<CaptiveState> { state },
        policies = new List<CaptivePolicyData>
        {
            new()
            {
                policyId = CaptivityPolicyIds.Standard,
                displayName = "표준 수용",
                allowRansom = true
            }
        }
    };

    private static string Id(string suffix) => IdPrefix + suffix;

    private static KoreanNameSnapshot Name(string suffix)
    {
        string id = Id(suffix);
        return new KoreanNameSnapshot(
            "가람",
            "captivity-ransom-display-v1:"
                + NarrativeInferenceHash.ComputeSha256Utf8(id + "|가람"),
            KoreanPronunciationHint.AutoHangulDisplay(
                "captivity-ransom-pronunciation-v1:" + id),
            "ko-KR");
    }

    private static string Fact(GameplayOutcomeSnapshot snapshot, string factId) =>
        snapshot.facts.Single(value => value.factId == factId).value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class OwnerFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly IDisposable subscription;

        public OwnerFixture(
            string suffix,
            OwnerOutcomeCommitPhase phase,
            string detail,
            int creditFailures = 0,
            int terminalFailures = 0,
            IGameEventBus eventBus = null)
        {
            Id = IdPrefix + suffix;
            Actor = CreateActor(suffix);
            State = State(suffix, CaptivityStatus.Confined);
            CaptivityActorAccess actors = new(
                new DungeonRuntimeAggregateRootStore(),
                _ => { });
            actors.AddState(State);
            CaptivityActorRuntimeLookup actorRuntime = new(id =>
                string.Equals(id, Id, StringComparison.Ordinal) ? Actor : null);
            CaptivityPolicyRuntime policies = new(
                actors,
                new NoOpPolicyActorPort());
            Doors = new FakeDoorSubjects();
            Doors.SetCaptive(Id, true);
            Money = new FakeMoney(creditFailures);
            Terminal = new FakeTerminal(terminalFailures);
            Committer = new ConfigurableCommitter(phase, detail);
            Events = eventBus ?? new GameEventBus();
            if (Events is GameEventBus concrete)
            {
                subscription = concrete.Subscribe<CaptiveRansomedEvent>(
                    _ => EventCount++);
            }
            FakeClock clock = new();
            CaptiveRansomOutcomeRuntime outcomes = new(
                Committer,
                Events,
                new FakeWorldRegistry(Actor),
                clock);
            Runtime = new CaptivityRansomRuntime(
                actors,
                actorRuntime,
                policies,
                new FixedBodyHealthQuery(),
                Money,
                Terminal,
                Doors,
                clock,
                outcomes);
        }

        public string Id { get; }
        public CharacterActor Actor { get; }
        public CaptiveState State { get; }
        public FakeDoorSubjects Doors { get; }
        public FakeMoney Money { get; }
        public FakeTerminal Terminal { get; }
        public ConfigurableCommitter Committer { get; }
        public IGameEventBus Events { get; }
        public CaptivityRansomRuntime Runtime { get; }
        public int EventCount { get; private set; }

        public void Dispose()
        {
            subscription?.Dispose();
            foreach (UnityEngine.Object value in cleanup.Where(value => value != null))
                UnityEngine.Object.DestroyImmediate(value);
        }

        private CharacterActor CreateActor(string suffix)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "가람",
                    "human");
            cleanup.Add(data);
            GameObject actorObject = new("Captive Ransom " + suffix);
            cleanup.Add(actorObject);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = data;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(Id);
            actor.Identity.SetCharacterType(CharacterType.NPC);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats = new Dictionary<CharacterCondition, float>
            {
                { CharacterCondition.SLEEP, 100f },
                { CharacterCondition.HUNGER, 100f },
                { CharacterCondition.FUN, 100f },
                { CharacterCondition.MOOD, 100f },
                { CharacterCondition.EXCRETION, 100f },
                { CharacterCondition.HYGIENE, 100f }
            };
            return actor;
        }
    }

    private sealed class ConfigurableCommitter : ICaptiveRansomOutcomeCommitter
    {
        private readonly OwnerOutcomeCommitPhase phase;
        private readonly string detail;

        public ConfigurableCommitter(
            OwnerOutcomeCommitPhase phase,
            string detail)
        {
            this.phase = phase;
            this.detail = detail ?? string.Empty;
        }

        public CaptiveRansomOutcomeReceipt LastReceipt { get; private set; }

        public bool TryPrepare(
            in CaptiveRansomOutcomeReceipt receipt,
            out PreparedCaptiveRansomOutcome prepared,
            out string failureReason)
        {
            LastReceipt = receipt;
            prepared = new PreparedCaptiveRansomOutcome(
                default,
                receipt.ResultKey,
                receipt.OwnerRevision,
                false);
            failureReason = string.Empty;
            return true;
        }

        public OwnerOutcomeCommitResult Commit(
            in PreparedCaptiveRansomOutcome prepared) => new(
            phase,
            prepared.ResultKey,
            default,
            string.Empty,
            detail);

        public void Cancel(in PreparedCaptiveRansomOutcome prepared)
        {
        }
    }

    private sealed class FakeTerminal : ICaptivityRansomTerminalPhysicalPort
    {
        private int failuresRemaining;

        public FakeTerminal(int failuresRemaining) =>
            this.failuresRemaining = Math.Max(0, failuresRemaining);

        public int AttemptCount { get; private set; }

        public bool TryFinalize(CaptiveState state, out string failureReason)
        {
            AttemptCount++;
            if (failuresRemaining > 0)
            {
                failuresRemaining--;
                failureReason = "qa-terminal-physical-fault";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeMoney : IIdempotentGameMoneyAccount
    {
        private int failuresRemaining;
        private string creditedSource = string.Empty;
        private string creditedTarget = string.Empty;
        private int creditedAmount;

        public FakeMoney(int failuresRemaining) =>
            this.failuresRemaining = Math.Max(0, failuresRemaining);

        public int Balance { get; private set; }
        public int CreditCount { get; private set; }
        public bool CanSpend(int amount) => Balance >= Math.Max(0, amount);

        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            int cost = Math.Max(0, amount);
            if (Balance < cost)
            {
                reason = "qa-insufficient";
                return false;
            }
            Balance -= cost;
            reason = string.Empty;
            return true;
        }

        public void Add(int amount) => Balance += Math.Max(0, amount);
        public void Add(int amount, EconomyTransactionContext context) => Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            Balance = Math.Max(0, amount);

        public bool TrySpendOnce(
            int amount,
            EconomyTransactionContext context,
            out EconomyTransactionRecord receipt,
            out string reason)
        {
            receipt = null;
            reason = "qa-not-supported";
            return false;
        }

        public bool TryCreditOnce(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (failuresRemaining > 0)
            {
                failuresRemaining--;
                reason = "qa-credit-fault";
                return false;
            }
            if (creditedSource.Length > 0)
            {
                bool same = creditedSource == context.sourceId
                    && creditedTarget == context.targetId
                    && creditedAmount == amount;
                reason = same ? string.Empty : "qa-credit-drift";
                return same;
            }
            creditedSource = context.sourceId;
            creditedTarget = context.targetId;
            creditedAmount = amount;
            Balance += amount;
            CreditCount++;
            reason = string.Empty;
            return true;
        }
    }

    private sealed class FixedBodyHealthQuery : ICharacterBodyHealthQuery
    {
        private static readonly CharacterVitalsSnapshot Vitals = new(100f, 100f, 0f);
        private static readonly CharacterBodyHealthSnapshot Body = new(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            1f,
            false);

        public CharacterVitalsSnapshot GetVitals(CharacterActor actor) => Vitals;
        public CharacterVitalsSnapshot GetVitals(string characterId) => Vitals;
        public CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor) => Body;
        public CharacterBodyHealthSnapshot GetSnapshot(string characterId) => Body;
        public float GetTotalBleeding(CharacterActor target) => 0f;
        public float GetMissingPartHealth(CharacterActor target) => 0f;
    }

    private sealed class NoOpPolicyActorPort : ICaptivityPolicyActorPort
    {
        public void ConfineLaborer(string captiveId)
        {
        }
    }

    private sealed class FakeDoorSubjects : IDoorAccessSubjectRegistry
    {
        private readonly HashSet<string> captives = new(StringComparer.Ordinal);

        public bool IsCaptive(string id) => captives.Contains(id);

        public void SetCaptive(string persistentId, bool captive)
        {
            if (captive)
                captives.Add(persistentId);
            else
                captives.Remove(persistentId);
        }

        public void SetCapturedWildlife(string wildlifeId, bool captured)
        {
        }

        public void ReplaceCaptiveSubjects(IEnumerable<string> persistentIds)
        {
            captives.Clear();
            foreach (string id in persistentIds ?? Array.Empty<string>())
                captives.Add(id);
        }

        public void ReplaceCapturedWildlifeSubjects(IEnumerable<string> wildlifeIds)
        {
        }
    }

    private sealed class FakeClock : IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => GameCalendarRules.SecondsPerDay * 12f;
        public int FrameCount => 100;
        public bool IsPaused => false;
    }

    private sealed class FakeWorldRegistry : ICharacterAiWorldRegistry
    {
        private readonly List<CharacterActor> characters;

        public FakeWorldRegistry(params CharacterActor[] characters) =>
            this.characters = (characters ?? Array.Empty<CharacterActor>()).ToList();

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => characters;
        public int WildlifeVersion => 0;
        public IReadOnlyList<WildlifeActor> Wildlife => Array.Empty<WildlifeActor>();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings => Array.Empty<BuildableObject>();
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
        public int RetailVersion => 0;
        public IReadOnlyList<IRetailFacility> RetailFacilities =>
            Array.Empty<IRetailFacility>();
        public int Version => 1;
        public void RegisterCharacter(CharacterActor actor) => characters.Add(actor);
        public void UnregisterCharacter(CharacterActor actor) => characters.Remove(actor);
        public void RegisterCharacterLifetime(CharacterActor actor) { }
        public void UnregisterCharacterLifetime(CharacterActor actor) { }
        public void RegisterWildlife(WildlifeActor actor) { }
        public void UnregisterWildlife(WildlifeActor actor) { }
        public void RegisterBuilding(BuildableObject building) { }
        public void UnregisterBuilding(BuildableObject building) { }
        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor,
            string reason) => 0;
        public int GetTransientBuildingOwnershipCount(CharacterId characterId) => 0;
        public void RegisterWarehouse(IWarehouseFacility warehouse) { }
        public void UnregisterWarehouse(IWarehouseFacility warehouse) { }
        public void SetGrid(Grid grid) { }
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
        public bool TryGetSessionState(out GameSessionState data)
        {
            data = null;
            return false;
        }
        public void Clear() => characters.Clear();
    }

    private sealed class ThrowingEventBus : IGameEventBus
    {
        public IDisposable Subscribe<TEvent>(Action<TEvent> listener) =>
            throw new NotSupportedException();
        public void Publish<TEvent>(TEvent gameEvent) =>
            throw new InvalidOperationException("qa-event-observer-fault");
        public void Clear()
        {
        }
    }
}
#endif
