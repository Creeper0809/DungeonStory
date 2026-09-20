#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using DungeonStory.Operation;
using UnityEditor;
using UnityEngine;

public static class CaptivityInteractionOutcomeDebugScenarios
{
    private const string IdPrefix = "character:qa-captivity-interaction-";
    private const string HousingAssetPath =
        "Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset";

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyCanonicalOutcome();
        VerifyAllAuthoredHandlers();
        VerifyProductionOwnerBoundaries();
        VerifyInterrogationComposition();
        VerifySaveCompatibility();
        if (logSuccess)
            Debug.Log("[Captivity Interaction Outcome] PASS");
        return true;
    }

    private static void VerifyCanonicalOutcome()
    {
        GameplayOutcomeRegistry registry = Registry();
        GameplayOutcomeLedger ledger = NewLedger(
            registry,
            "run:captivity-interaction-canonical");
        CaptivityInteractionGameplayOutcomeBridge bridge = new(
            new GameplayOutcomeRecorder(ledger, registry, new GameEventBus()),
            ledger);
        CaptivityInteractionOutcomeRuntime runtime = new(
            bridge,
            new FakePhysicalSource());
        CaptiveState state = PendingState("canonical");
        state.interactionTerminal = Terminal(
            state,
            attemptId: 1,
            outputItemId: CaptivityItemDefinitions.ExtractedBloodItemId,
            outputAmount: 1,
            bodyDamage: 12f,
            bodyBefore: 80f,
            bodyAfter: 68f);

        Require(
            runtime.TryPrepare(
                state,
                14,
                out PreparedCaptivityInteractionOutcome prepared,
                out long revision,
                out string failure)
            && revision == 1L
            && !prepared.IsReplay,
            "canonical interaction prepare failed: " + failure);
        OwnerOutcomeCommitResult committed = runtime.Commit(prepared);
        Require(
            committed.DurablyCommitted,
            "canonical interaction commit was not durable: "
            + committed.DetailCode);
        state.interactionOutcomeRevision = revision;
        state.interactionTerminal.outcomeRevision = revision;

        GameplayEntityId captive = new(
            CaptivityInteractionOutcomeIds.CharacterKind,
            state.captiveId);
        GameplayEntityId facility = new(
            CaptivityInteractionOutcomeIds.FacilityKind,
            state.housingBuildingId);
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage character = ledger.GetForEntity(
            captive,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage building = ledger.GetForEntity(
            facility,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeSnapshot exact = character.Items.Single().Exact;
        Require(
            global.Items.Count == 1
            && character.Items.Count == 1
            && building.Items.Count == 1
            && exact != null
            && exact.participants.Count == 3
            && exact.subjects.Count == 3
            && exact.metrics.Count == 11
            && Fact(exact, CaptivityInteractionOutcomeIds.InteractionIdFact.Value)
                == "captivity:persuasion"
            && Fact(exact, CaptivityInteractionOutcomeIds.OutputItemFact.Value)
                == CaptivityItemDefinitions.ExtractedBloodItemId,
            "canonical interaction query shape drifted");

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
            && view.Text.Contains("‘회유’를", StringComparison.Ordinal)
            && !view.Text.Contains("‘회유’을", StringComparison.Ordinal)
            && view.Text.Contains("가람", StringComparison.Ordinal),
            "canonical interaction Korean projection lost its josa contract");

        Require(
            runtime.TryPrepare(
                state,
                14,
                out PreparedCaptivityInteractionOutcome replay,
                out long replayRevision,
                out string replayFailure)
            && replayRevision == 1L
            && replay.IsReplay
            && runtime.Commit(replay).DurablyCommitted
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "canonical interaction replay failed or duplicated: "
            + replayFailure);

        CaptiveState drifted = state.Clone();
        drifted.interactionTerminal.message = "같은 키의 다른 결과";
        Require(
            !runtime.TryPrepare(
                drifted,
                14,
                out _,
                out _,
                out _),
            "same interaction result key accepted a drifted payload");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = NewLedger(
            registry,
            "run:captivity-interaction-restored");
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.GetGlobal(
                    OutcomeCursor.FirstPage(10),
                    OutcomeFilter.All)
                .Items.Single().Exact.immutablePayloadHash
            == exact.immutablePayloadHash,
            "interaction outcome changed across ledger save round trip");
    }

    private static void VerifyAllAuthoredHandlers()
    {
        ICaptivityInteractionHandler[] handlers = AuthoredHandlers();
        GameplayOutcomeRegistry registry = Registry();
        GameplayOutcomeLedger ledger = NewLedger(
            registry,
            "run:captivity-interaction-authored-handlers");
        CaptivityInteractionGameplayOutcomeBridge bridge = new(
            new GameplayOutcomeRecorder(ledger, registry, new GameEventBus()),
            ledger);
        CaptiveState state = PendingState("authored");
        state.status = CaptivityStatus.Confined;
        CaptivityInteractionContext context = new(
            state,
            subjectAvailable: true,
            wardenAvailable: true,
            facilityAvailable: true,
            new Vector2Int(7, 9));

        for (int index = 0; index < handlers.Length; index++)
        {
            ICaptivityInteractionHandler handler = handlers[index];
            Require(
                handler.CanExecute(context, out string reason),
                $"authored handler '{handler.InteractionId}' rejected a valid context: {reason}");
            CaptivityInteractionResult result = handler.Execute(context);
            CaptivityBodyDamageProjection damage = handler.BodyDamage.HasDamage
                ? handler.BodyDamage.Project(100f, 100f)
                : default;
            CaptivityInteractionOutcomeReceipt receipt = new(
                state.captiveId,
                Name("captive", state.captiveId, state.displayName),
                "character:qa-captivity-warden-authored",
                Name(
                    "warden",
                    "character:qa-captivity-warden-authored",
                    "도윤"),
                state.housingBuildingId,
                Name("facility", state.housingBuildingId, "감방 구속대"),
                7,
                9,
                handler.InteractionId,
                handler.Kind,
                handler.DisplayName,
                result.Success,
                result.Message,
                result.WillDelta,
                result.FearDelta,
                result.TrustDelta,
                result.GrudgeDelta,
                result.CorruptionDelta,
                damage.DamageAmount,
                handler.BodyDamage.HasDamage ? damage.CurrentHealth : 0f,
                handler.BodyDamage.HasDamage ? damage.ExpectedHealth : 0f,
                result.OutputItemId,
                result.OutputAmount,
                index + 1,
                index + 1L,
                20);
            Require(
                bridge.TryPrepare(receipt, out var prepared, out string failure)
                && bridge.Commit(prepared).DurablyCommitted,
                $"authored handler '{handler.InteractionId}' could not record: {failure}");
        }

        GameplayOutcomeQueryPage page = ledger.GetGlobal(
            OutcomeCursor.FirstPage(32),
            OutcomeFilter.All);
        Require(
            page.Items.Count == handlers.Length
            && page.Items.All(item => item.Exact != null
                && item.Exact.metrics.Count == 11)
            && page.Items.Select(item => Fact(
                    item.Exact,
                    CaptivityInteractionOutcomeIds.InteractionIdFact.Value))
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(handlers.Select(handler => handler.InteractionId)),
            "the ten authored interaction results were not all accepted exactly once");
    }

    private static void VerifyProductionOwnerBoundaries()
    {
        CaptivityInteractionResult effect = new(
            true,
            "경계 시험",
            willDelta: -8f,
            fearDelta: 7f,
            trustDelta: -4f,
            grudgeDelta: 5f);
        using (OwnerFixture rejected = new(
                   "rejected",
                   new FixedHandler(
                       "captivity:qa-rejected",
                       CaptiveInteractionKind.Coercion,
                       effect,
                       new CaptivityBodyDamagePolicy(20f)),
                   OwnerOutcomeCommitPhase.Rejected,
                   "qa-interaction-rejected"))
        {
            float will = rejected.State.will;
            float fear = rejected.State.fear;
            Require(
                rejected.Runtime.Advance(
                    rejected.State.captiveId,
                    rejected.Warden,
                    1f,
                    out string status)
                && status == "qa-interaction-rejected"
                && rejected.Handler.ExecuteCount == 1
                && Mathf.Approximately(rejected.State.will, will)
                && Mathf.Approximately(rejected.State.fear, fear)
                && Mathf.Approximately(rejected.Body.CurrentHealth, 100f)
                && rejected.State.interactionOutcomeRevision == 0L
                && rejected.State.interactionTerminal.HasOutcome
                && !rejected.State.interactionTerminal.HasCommittedOutcome
                && rejected.Body.ApplyCount == 1
                && rejected.Body.RestoreCount == 1
                && rejected.Body.CompleteCount == 0
                && rejected.Physical.SuccessfulPublicationCount == 0,
                "definite interaction rejection did not restore both owners");
            Require(
                rejected.Runtime.Advance(
                    rejected.State.captiveId,
                    rejected.Warden,
                    1f,
                    out _)
                && rejected.Handler.ExecuteCount == 1
                && rejected.Committer.CommitCount == 2,
                "a rejected frozen interaction reran its handler");
        }

        using (OwnerFixture pending = new(
                   "delivery-pending",
                   new FixedHandler(
                       "captivity:qa-delivery-pending",
                       CaptiveInteractionKind.Branding,
                       effect,
                       new CaptivityBodyDamagePolicy(8f)),
                   OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                   "qa-ledger-delivery-pending"))
        {
            Require(
                pending.Runtime.Advance(
                    pending.State.captiveId,
                    pending.Warden,
                    1f,
                    out string status)
                && status == effect.Message
                && pending.State.status == CaptivityStatus.Confined
                && pending.State.interactionOutcomeRevision == 1L
                && pending.Handler.ExecuteCount == 1
                && pending.Committer.CommitCount == 1
                && pending.Body.ApplyCount == 1
                && pending.Body.CompleteCount == 1
                && Mathf.Approximately(pending.Body.CurrentHealth, 92f),
                "delivery-pending interaction was not treated as durable");
        }

        CaptivityInteractionResult extraction = new(
            true,
            "혈액 확보",
            fearDelta: 10f,
            outputItemId: CaptivityItemDefinitions.ExtractedBloodItemId,
            outputAmount: 1);
        using (OwnerFixture retry = new(
                   "output-retry",
                   new FixedHandler(
                       "captivity:qa-output-retry",
                       CaptiveInteractionKind.BloodExtraction,
                       extraction,
                       new CaptivityBodyDamagePolicy(10f)),
                   OwnerOutcomeCommitPhase.PublishedAcknowledged,
                   string.Empty,
                   outputFailures: 1))
        {
            Require(
                retry.Runtime.Advance(
                    retry.State.captiveId,
                    retry.Warden,
                    1f,
                    out string firstStatus)
                && firstStatus.Contains("출력 전달을 재시도", StringComparison.Ordinal)
                && retry.State.status == CaptivityStatus.Interaction
                && retry.State.interactionTerminal.HasCommittedOutcome
                && !retry.State.interactionTerminal.outputPublished
                && retry.Handler.ExecuteCount == 1
                && retry.Committer.CommitCount == 1
                && retry.Body.ApplyCount == 1
                && retry.Body.CompleteCount == 0
                && Mathf.Approximately(retry.Body.CurrentHealth, 90f),
                "failed physical output did not retain a committed terminal");
            Require(
                retry.Runtime.Advance(
                    retry.State.captiveId,
                    retry.Warden,
                    1f,
                    out string secondStatus)
                && secondStatus == extraction.Message
                && retry.State.status == CaptivityStatus.Confined
                && retry.Handler.ExecuteCount == 1
                && retry.Committer.CommitCount == 1
                && retry.Physical.CallCount == 2
                && retry.Physical.SuccessfulPublicationCount == 1
                && retry.Body.ApplyCount == 1
                && retry.Body.CompleteCount == 1,
                "physical-output retry duplicated execution or owner mutation");
        }

        using (OwnerFixture lethal = new(
                   "lethal-output",
                   new FixedHandler(
                       "captivity:qa-lethal-output",
                       CaptiveInteractionKind.MemoryExtraction,
                       new CaptivityInteractionResult(
                           true,
                           "치명적 기억 추출",
                           outputItemId: CaptivityItemDefinitions.MemoryResidueItemId,
                           outputAmount: 1),
                       new CaptivityBodyDamagePolicy(100f)),
                   OwnerOutcomeCommitPhase.PublishedAcknowledged,
                   string.Empty,
                   outputFailures: 1,
                   startingHealth: 10f))
        {
            lethal.Committer.BeforeReturn = () => Require(
                lethal.Body.ApplyCount == 1
                && lethal.Body.CompleteCount == 0
                && !lethal.Captive.IsDead,
                "body observers ran before the ledger commit boundary");
            Require(
                lethal.Runtime.Advance(
                    lethal.State.captiveId,
                    lethal.Warden,
                    1f,
                    out _)
                && lethal.State.interactionTerminal.HasCommittedOutcome
                && lethal.Body.CompleteCount == 0,
                "lethal output fault did not retain the committed terminal");
            lethal.Captive.Die(
                CharacterDeathCauseCode.Unknown,
                "qa-external-death-before-output-retry");
            lethal.Runtime.HandleSubjectDeath(lethal.State);
            Require(
                lethal.Physical.SuccessfulPublicationCount == 1
                && lethal.Body.CompleteCount == 1
                && lethal.Handler.ExecuteCount == 1
                && lethal.State.currentInteractionId.Length == 0
                && !lethal.State.interactionTerminal.HasOutcome,
                "subject death discarded committed output or observer completion");
        }
    }

    private static void VerifyInterrogationComposition()
    {
        FixedHandler interrogation = new(
            CaptivityInterrogationAttemptIdentity.InteractionId,
            CaptiveInteractionKind.Interrogation,
            new CaptivityInteractionResult(
                true,
                "조각난 정보 하나를 확보했습니다.",
                willDelta: -8f,
                fearDelta: 9f,
                trustDelta: -4f,
                grudgeDelta: 7f),
            CaptivityBodyDamagePolicy.None,
            "심문");
        GameEventBus events = new();
        int notices = 0;
        using IDisposable subscription = events.Subscribe<EventAlertRequestedEvent>(
            _ => notices++);
        using OwnerFixture fixture = new(
            "interrogation",
            interrogation,
            OwnerOutcomeCommitPhase.PublishedAcknowledged,
            string.Empty,
            eventBus: events);

        Require(
            fixture.Runtime.Advance(
                fixture.State.captiveId,
                fixture.Warden,
                1f,
                out string status)
            && status == "심문 종료 — 추가 정보 없음"
            && fixture.Handler.ExecuteCount == 1
            && fixture.Committer.LastReceipt.InteractionKind
                == CaptiveInteractionKind.Interrogation
            && notices == 1
            && fixture.State.status == CaptivityStatus.Confined
            && fixture.State.currentInteractionId.Length == 0
            && !fixture.State.interactionTerminal.HasOutcome
            && fixture.State.currentInterrogationAttemptId == 0
            && fixture.State.interrogationTerminal.HasOutcome
            && fixture.State.interrogationTerminal.codexPublicationCompleted
            && fixture.State.interrogationTerminal.noticePublicationCompleted
            && !fixture.State.interrogationTerminal.HasPendingPublication,
            "interrogation outcome and publication were not composed exactly once");
    }

    private static void VerifySaveCompatibility()
    {
        CaptiveState committed = PendingState("save-roundtrip");
        committed.currentInteractionId = "captivity:memory-extraction";
        committed.interactionAttemptSequence = 3;
        committed.currentInteractionAttemptId = 3;
        committed.interactionOutcomeRevision = 2L;
        committed.completedInteractionWork = 10f;
        committed.requiredInteractionWork = 10f;
        committed.interactionTerminal = Terminal(
            committed,
            attemptId: 3,
            outcomeRevision: 2L,
            interactionId: committed.currentInteractionId,
            kind: CaptiveInteractionKind.MemoryExtraction,
            display: "기억 추출",
            outputItemId: CaptivityItemDefinitions.MemoryResidueItemId,
            outputAmount: 1,
            bodyDamage: 10f,
            bodyBefore: 100f,
            bodyAfter: 90f,
            bodyObserversPending: true);
        committed.will = committed.interactionTerminal.willAfter;
        committed.fear = committed.interactionTerminal.fearAfter;
        committed.trust = committed.interactionTerminal.trustAfter;
        committed.grudge = committed.interactionTerminal.grudgeAfter;
        committed.corruption = committed.interactionTerminal.corruptionAfter;

        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(Save(committed)));
        DungeonGameRestoreReport valid = new();
        CaptivitySaveValidation.Validate(roundTrip, valid);
        Require(
            valid.Success
            && roundTrip.captives.Single().interactionTerminal.attemptId == 3
            && roundTrip.captives.Single().interactionOutcomeRevision == 2L,
            "valid interaction terminal failed save round trip: "
            + string.Join(" | ", valid.Errors));

        CaptiveState tornRevision = committed.Clone();
        tornRevision.interactionTerminal.outcomeRevision = 1L;
        RequireInvalidSave(tornRevision, "torn interaction revision was accepted");

        CaptiveState tornOutput = committed.Clone();
        tornOutput.interactionTerminal.outputPublished = true;
        tornOutput.interactionTerminal.outputCommitId = string.Empty;
        RequireInvalidSave(tornOutput, "published output without a commit ID was accepted");

        CaptiveState driftedDamage = committed.Clone();
        driftedDamage.interactionTerminal.bodyHealthAfter = 80f;
        RequireInvalidSave(driftedDamage, "drifted frozen body projection was accepted");

        CaptiveState legacy = PendingState("legacy-active");
        legacy.interactionAttemptSequence = 0;
        legacy.currentInteractionAttemptId = 0;
        legacy.completedInteractionWork = 2f;
        legacy.requiredInteractionWork = 10f;
        legacy.interactionTerminal = new CaptivityInteractionTerminalState();
        DungeonGameRestoreReport legacyReport = new();
        CaptivitySaveValidation.Validate(Save(legacy), legacyReport);
        CaptiveState normalized = CaptivitySaveValidation
            .CreateRestoredCaptive(legacy);
        Require(
            legacyReport.Success
            && normalized.currentInteractionAttemptId == 1
            && normalized.interactionAttemptSequence == 1
            && !normalized.interactionTerminal.HasOutcome,
            "legacy active interaction was not normalized safely: "
            + string.Join(" | ", legacyReport.Errors));
    }

    private static void RequireInvalidSave(CaptiveState state, string message)
    {
        DungeonGameRestoreReport report = new();
        CaptivitySaveValidation.Validate(Save(state), report);
        Require(!report.Success, message);
    }

    private static GameplayOutcomeRegistry Registry() => new(
        new IGameplayOutcomeDescriptor[]
        {
            new CaptivityInteractionOutcomeDescriptor(new KoreanJosaFormatter())
        },
        new IGameplayOutcomeAdapterRegistration[]
        {
            new CaptivityInteractionOutcomeAdapter()
        });

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

    private static ICaptivityInteractionHandler[] AuthoredHandlers() =>
        new ICaptivityInteractionHandler[]
        {
            new CaptivityPersuasionHandler(),
            new CaptivityIsolationHandler(),
            new CaptivityCoercionHandler(),
            new CaptivityInterrogationHandler(),
            new CaptivityIndoctrinationHandler(),
            new CaptivityBrandingHandler(),
            new CaptivityBloodExtractionHandler(),
            new CaptivityMemoryExtractionHandler(),
            new CaptivityForcedModificationHandler(),
            new CaptivityCorruptionRitualHandler()
        };

    private static CaptiveState PendingState(string suffix)
    {
        string captiveId = IdPrefix + suffix;
        return new CaptiveState
        {
            captiveId = captiveId,
            displayName = "가람",
            status = CaptivityStatus.Interaction,
            policyId = CaptivityPolicyIds.Standard,
            reservedWardenId = "character:qa-captivity-warden-" + suffix,
            housingBuildingId = "building:qa-captivity-housing-" + suffix,
            housingPosition = new Vector2Int(7, 9),
            restrained = true,
            currentInteractionId = "captivity:persuasion",
            interactionMaterialDestinationId =
                "captivity-interaction-material:qa-" + suffix,
            interactionMaterialsConsumed = true,
            completedInteractionWork = 1f,
            requiredInteractionWork = 1f,
            interactionAttemptSequence = 1,
            currentInteractionAttemptId = 1,
            will = 70f,
            fear = 20f,
            trust = 30f,
            grudge = 40f,
            corruption = 10f
        };
    }

    private static CaptivityInteractionTerminalState Terminal(
        CaptiveState state,
        int attemptId,
        long outcomeRevision = 0L,
        string interactionId = "captivity:persuasion",
        CaptiveInteractionKind kind = CaptiveInteractionKind.Persuasion,
        string display = "회유",
        string outputItemId = "",
        int outputAmount = 0,
        float bodyDamage = 0f,
        float bodyBefore = 0f,
        float bodyAfter = 0f,
        bool bodyObserversPending = false) => new()
    {
        attemptId = attemptId,
        outcomeRevision = outcomeRevision,
        interactionId = interactionId,
        interactionKind = kind,
        interactionDisplayName = display,
        wardenId = state.reservedWardenId,
        wardenDisplayName = "도윤",
        facilityId = state.housingBuildingId,
        facilityDisplayName = "감방 구속대",
        resultGridX = state.housingPosition.x,
        resultGridY = state.housingPosition.y,
        success = true,
        message = display + " 결과",
        willBefore = 70f,
        willAfter = 67f,
        fearBefore = 20f,
        fearAfter = 22f,
        trustBefore = 30f,
        trustAfter = 35f,
        grudgeBefore = 40f,
        grudgeAfter = 38f,
        corruptionBefore = 10f,
        corruptionAfter = 11f,
        outputItemId = outputItemId,
        outputAmount = outputAmount,
        outputOperationId = outputAmount > 0
            ? CaptivityInteractionAttemptIdentity.FormatOutputOperationId(
                state.captiveId,
                attemptId)
            : string.Empty,
        bodyDamageAmount = bodyDamage,
        bodyHealthBefore = bodyBefore,
        bodyHealthAfter = bodyAfter,
        bodyMaximumHealth = bodyDamage > 0f ? 100f : 0f,
        bodyObserversPending = bodyObserversPending
    };

    private static CaptivitySaveData Save(CaptiveState state) => new()
    {
        captives = new List<CaptiveState> { state },
        policies = new List<CaptivePolicyData>
        {
            new()
            {
                policyId = CaptivityPolicyIds.Standard,
                displayName = "표준 수용"
            }
        }
    };

    private static KoreanNameSnapshot Name(
        string role,
        string id,
        string display) => CaptivityInteractionOutcomeNames.Snapshot(
        role,
        id,
        display);

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

        public OwnerFixture(
            string suffix,
            FixedHandler handler,
            OwnerOutcomeCommitPhase phase,
            string detail,
            int outputFailures = 0,
            float startingHealth = 100f,
            IGameEventBus eventBus = null)
        {
            Handler = handler;
            Captive = CreateActor(
                "Captive " + suffix,
                IdPrefix + suffix,
                "가람");
            Warden = CreateActor(
                "Warden " + suffix,
                "character:qa-captivity-warden-" + suffix,
                "도윤");
            Housing = CreateHousing(suffix);
            State = PendingState(suffix);
            State.captiveId = Captive.Identity.PersistentId;
            State.reservedWardenId = Warden.Identity.PersistentId;
            State.housingBuildingId = Housing.RequirePersistentInstanceId().Value;
            State.currentInteractionId = handler.InteractionId;
            State.interactionAttemptSequence = 1;
            State.currentInteractionAttemptId = 1;
            State.interactionTerminal = new CaptivityInteractionTerminalState();
            if (handler.Kind == CaptiveInteractionKind.Interrogation)
            {
                State.interrogationAttemptSequence = 1;
                State.currentInterrogationAttemptId = 1;
            }

            CaptivityActorAccess actors = new(
                new DungeonRuntimeAggregateRootStore(),
                _ => { });
            actors.AddState(State);
            CaptivityActorRuntimeLookup actorRuntime = new(id =>
                string.Equals(id, State.captiveId, StringComparison.Ordinal)
                    ? Captive
                    : null);
            Body = new FakeBodyHealth(
                new CharacterId(State.captiveId),
                startingHealth,
                startingHealth);
            Physical = new FakePhysicalSource(outputFailures);
            Committer = new ConfigurableCommitter(phase, detail);
            Materials = new FakeMaterials();
            IGameEventBus events = eventBus ?? new GameEventBus();
            CaptivityInterrogationInformationRuntime interrogation = new(
                new EmptyNarrativeQuery(),
                new EmptyEnemyCatalog(),
                new RecordingCodex(),
                events);
            Runtime = new CaptivityInteractionRuntime(
                actors,
                actorRuntime,
                new CaptivityInteractionRegistry(new[] { handler }),
                interrogation,
                Body,
                Body,
                new CaptivityInteractionOutcomeRuntime(Committer, Physical),
                new FakeClock(),
                Materials,
                TryGetHousing);
        }

        public FixedHandler Handler { get; }
        public CharacterActor Captive { get; }
        public CharacterActor Warden { get; }
        public BuildableObject Housing { get; }
        public CaptiveState State { get; }
        public FakeBodyHealth Body { get; }
        public FakePhysicalSource Physical { get; }
        public ConfigurableCommitter Committer { get; }
        public FakeMaterials Materials { get; }
        public CaptivityInteractionRuntime Runtime { get; }

        public void Dispose()
        {
            foreach (UnityEngine.Object value in cleanup.Where(value => value != null))
                UnityEngine.Object.DestroyImmediate(value);
        }

        private CharacterActor CreateActor(
            string objectName,
            string id,
            string display)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    display,
                    "human");
            cleanup.Add(data);
            GameObject actorObject = new(objectName);
            cleanup.Add(actorObject);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = data;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(id);
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

        private BuildableObject CreateHousing(string suffix)
        {
            BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                HousingAssetPath);
            Require(
                data?.GetCaptiveHousingAbility()?.IsValid == true,
                "authored captive housing asset is unavailable");
            GameObject buildingObject = new("Interaction Housing " + suffix);
            cleanup.Add(buildingObject);
            BuildableObject building =
                buildingObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.RestorePersistentIdentity(new BuildingInstanceId(
                "building:qa-captivity-interaction-" + suffix));
            building.Initialization(data, new Vector2Int(7, 9));
            return building;
        }

        private bool TryGetHousing(
            string captiveId,
            out BuildableObject housing)
        {
            housing = string.Equals(
                captiveId,
                State.captiveId,
                StringComparison.Ordinal)
                ? Housing
                : null;
            return housing != null;
        }
    }

    private sealed class FixedHandler : ICaptivityInteractionHandler
    {
        private static readonly IReadOnlyDictionary<StockCategory, int> Materials =
            new Dictionary<StockCategory, int>();
        private readonly CaptivityInteractionResult result;

        public FixedHandler(
            string interactionId,
            CaptiveInteractionKind kind,
            CaptivityInteractionResult result,
            CaptivityBodyDamagePolicy bodyDamage,
            string displayName = "시험 상호작용")
        {
            InteractionId = interactionId;
            Kind = kind;
            this.result = result;
            BodyDamage = bodyDamage;
            DisplayName = displayName;
        }

        public string InteractionId { get; }
        public string DisplayName { get; }
        public CaptiveInteractionKind Kind { get; }
        public float RequiredWork => 1f;
        public CaptivityBodyDamagePolicy BodyDamage { get; }
        public IReadOnlyDictionary<StockCategory, int> MaterialRequirements =>
            Materials;
        public int ExecuteCount { get; private set; }

        public bool CanExecute(
            CaptivityInteractionContext context,
            out string failureReason)
        {
            bool valid = context.Captive?.IsInCustody == true
                && context.SubjectAvailable
                && context.WardenAvailable
                && context.FacilityAvailable;
            failureReason = valid ? string.Empty : "qa-invalid-context";
            return valid;
        }

        public CaptivityInteractionResult Execute(CaptivityInteractionContext context)
        {
            ExecuteCount++;
            return result;
        }
    }

    private sealed class ConfigurableCommitter :
        ICaptivityInteractionOutcomeCommitter
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

        public CaptivityInteractionOutcomeReceipt LastReceipt { get; private set; }
        public int PrepareCount { get; private set; }
        public int CommitCount { get; private set; }
        public Action BeforeReturn { get; set; }

        public bool TryPrepare(
            in CaptivityInteractionOutcomeReceipt receipt,
            out PreparedCaptivityInteractionOutcome prepared,
            out string failureReason)
        {
            PrepareCount++;
            LastReceipt = receipt;
            prepared = new PreparedCaptivityInteractionOutcome(
                default,
                receipt.ResultKey,
                receipt.OwnerRevision,
                false);
            failureReason = string.Empty;
            return true;
        }

        public OwnerOutcomeCommitResult Commit(
            in PreparedCaptivityInteractionOutcome prepared)
        {
            CommitCount++;
            BeforeReturn?.Invoke();
            return new OwnerOutcomeCommitResult(
                phase,
                prepared.ResultKey,
                default,
                string.Empty,
                detail);
        }

        public void Cancel(in PreparedCaptivityInteractionOutcome prepared)
        {
        }
    }

    private sealed class FakePhysicalSource :
        IPhysicalItemSourcePublicationService
    {
        private int failuresRemaining;
        private readonly Dictionary<string, PhysicalItemSourcePublicationReceipt>
            committed = new(StringComparer.Ordinal);

        public FakePhysicalSource(int failuresRemaining = 0) =>
            this.failuresRemaining = Math.Max(0, failuresRemaining);

        public int CallCount { get; private set; }
        public int SuccessfulPublicationCount { get; private set; }

        public bool TryEnsureLooseOutputs(
            IReadOnlyDictionary<string, int> outputs,
            Vector2Int outputPosition,
            string operationId,
            string reasonCode,
            out PhysicalItemSourcePublicationReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            if (committed.TryGetValue(operationId, out receipt))
            {
                failureReason = string.Empty;
                return true;
            }
            if (failuresRemaining > 0)
            {
                failuresRemaining--;
                receipt = default;
                failureReason = "qa-physical-output-fault";
                return false;
            }
            int quantity = (outputs ?? new Dictionary<string, int>())
                .Sum(value => Math.Max(0, value.Value));
            receipt = new PhysicalItemSourcePublicationReceipt(
                operationId,
                reasonCode,
                new[] { "physical-output:qa-" + SuccessfulPublicationCount },
                quantity,
                Math.Max(1, quantity) * 100L);
            committed[operationId] = receipt;
            SuccessfulPublicationCount++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeBodyHealth :
        ICharacterBodyHealthQuery,
        ICharacterBodyHealthMutationTransaction
    {
        private readonly CharacterId id;
        private float capturedHealth;

        public FakeBodyHealth(
            CharacterId id,
            float currentHealth,
            float maximumHealth)
        {
            this.id = id;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
        }

        public float CurrentHealth { get; private set; }
        public float MaximumHealth { get; }
        public int ApplyCount { get; private set; }
        public int RestoreCount { get; private set; }
        public int CompleteCount { get; private set; }

        public CharacterVitalsSnapshot GetVitals(CharacterActor actor) =>
            new(CurrentHealth, MaximumHealth, 0f);
        public CharacterVitalsSnapshot GetVitals(string characterId) =>
            new(CurrentHealth, MaximumHealth, 0f);
        public CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor) =>
            EmptyBody();
        public CharacterBodyHealthSnapshot GetSnapshot(string characterId) =>
            EmptyBody();
        public float GetTotalBleeding(CharacterActor target) => 0f;
        public float GetMissingPartHealth(CharacterActor target) => 0f;

        public CharacterBodyHealthMutationSnapshot CaptureCombatMutation(
            CharacterActor actor)
        {
            capturedHealth = CurrentHealth;
            return default;
        }

        public void RestoreCombatMutation(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot snapshot,
            string reason)
        {
            RestoreCount++;
            CurrentHealth = capturedHealth;
        }

        public CharacterPreparedAggregateDamageReceipt
            ApplyPreparedAggregateDamage(
                CharacterActor actor,
                float amount,
                CharacterDeathCauseCode deathCause,
                string reasonCode,
                bool allowDeath)
        {
            ApplyCount++;
            float before = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, amount));
            return new CharacterPreparedAggregateDamageReceipt(
                id,
                amount,
                before,
                CurrentHealth,
                MaximumHealth,
                deathCause,
                reasonCode,
                allowDeath);
        }

        public void CompletePreparedAggregateDamage(
            CharacterActor actor,
            in CharacterPreparedAggregateDamageReceipt receipt)
        {
            CompleteCount++;
            if (receipt.ResultingHealth <= 0f && receipt.AllowDeath)
                actor.Die(receipt.DeathCause, receipt.ReasonCode);
        }

        public void ApplyPreparedCombatMutation(
            CharacterActor actor,
            CombatAttackResult result,
            string reason) => throw new NotSupportedException();
        public void CompletePreparedCombatMutation(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot before,
            CombatAttackResult result,
            string reason) => throw new NotSupportedException();
        public void ApplyPreparedSuppressionReduction(
            CharacterActor actor,
            float amount) => throw new NotSupportedException();
        public void CompletePreparedSuppressionReduction(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot before) =>
            throw new NotSupportedException();

        private static CharacterBodyHealthSnapshot EmptyBody() => new(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            1f,
            false);
    }

    private sealed class FakeMaterials : ICaptivityInteractionMaterialRuntime
    {
        public int CloseCount { get; private set; }

        public bool TryOpenAndRequest(
            CaptiveState state,
            ICaptivityInteractionHandler handler,
            BuildableObject facility,
            out string destinationId,
            out string failureReason)
        {
            destinationId = "captivity-interaction-material:qa";
            failureReason = string.Empty;
            return true;
        }

        public bool IsReady(
            CaptiveState state,
            ICaptivityInteractionHandler handler,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryCommitSink(
            CaptiveState state,
            ICaptivityInteractionHandler handler,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryClose(
            CaptiveState state,
            string reasonCode,
            out string failureReason)
        {
            CloseCount++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class EmptyNarrativeQuery : ICharacterNarrativeQuery
    {
        public int Version => 0;
        public IReadOnlyCollection<CharacterNarrativeSnapshot> All =>
            Array.Empty<CharacterNarrativeSnapshot>();
        public bool TryGet(
            CharacterId characterId,
            out CharacterNarrativeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
        public bool CanPerformPractice(
            CharacterId characterId,
            string practiceId,
            int absoluteDay,
            out int nextAllowedAbsoluteDay)
        {
            nextAllowedAbsoluteDay = 0;
            return false;
        }
        public bool TryPreviewAmbitionProgress(
            CharacterId characterId,
            int amount,
            out AmbitionProgressPreview preview)
        {
            preview = default;
            return false;
        }
    }

    private sealed class EmptyEnemyCatalog : IEnemyArchetypeCatalog
    {
        public IReadOnlyList<EnemyArchetypeDefinitionSO> All =>
            Array.Empty<EnemyArchetypeDefinitionSO>();
        public bool TryGet(
            string id,
            out EnemyArchetypeDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public EnemyArchetypeDefinitionSO Require(string id) =>
            throw new KeyNotFoundException(id);
    }

    private sealed class RecordingCodex : ICaptivityInterrogationCodexPort
    {
        public bool HasInformation(string entryId, string informationText) => false;
        public void PublishInformation(
            string entryId,
            string fallbackTitle,
            string informationText)
        {
        }
    }

    private sealed class FakeClock : IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => GameCalendarRules.SecondsPerDay * 20f;
        public int FrameCount => 200;
        public bool IsPaused => false;
    }
}
#endif
