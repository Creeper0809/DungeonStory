#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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

    public static void RunPresentationForBatch()
    {
        VerifyInterrogationComposition();
        VerifyInterrogationFrozenInformation();
        VerifyInterrogationOwnerDeliveryRestore();
        Debug.Log("B129_INTERROGATION_FACT_DELIVERY=PASS");
    }

    private static void VerifyInterrogationFrozenInformation()
    {
        var events = new GameEventBus();
        var output = new List<EventAlertRequest>();
        var facts = new List<CaptivityInterrogationInformationResolvedFact>();
        using var capture = events.Subscribe<EventAlertRequestedEvent>(value => output.Add(value.request));
        using var factCapture = events.Subscribe<CaptivityInterrogationInformationResolvedFact>(facts.Add);
        using var observer = new CaptivityInterrogationPresentationObserver(events);
        observer.Initialize(); observer.Initialize();
        var terminal = new CaptivityInterrogationTerminalState
        {
            attemptId = 2, subjectDisplayName = "검증 포로", hasInformation = true,
            codexEntryId = "monster:b129", codexEntryTitle = "검증 부대",
            originEnemyArchetypeId = "enemy:b129", originFactionId = "faction:b129",
            enemyDisplayName = "검증 부대", formationTag = "pike-line",
            informationText = CaptivityInterrogationAttemptIdentity.FormatInformationText(
                "enemy:b129", "faction:b129", "pike-line"),
            highFearCaution = true, codexPublicationCompleted = true
        };
        var frozen = new CaptivityInterrogationInformationResolvedFact("character:b129", terminal);
        terminal.subjectDisplayName = "changed"; terminal.formationTag = "changed";
        events.Publish(frozen);
        Require(output.Count == 1 && facts.Count == 1
            && output[0].Title == "심문 성공" && output[0].Category == "포로"
            && output[0].Importance == EventAlertImportance.Medium
            && output[0].SourceId == CaptivityInterrogationAttemptIdentity.FormatNoticeSourceId("character:b129", 2)
            && output[0].Detail.Contains("검증 포로")
            && output[0].Detail.Contains("'pike-line'")
            && output[0].Detail.Contains("미확인 진술")
            && output[0].Detail.Contains("진실 또는 거짓으로 판정되지 않았습니다.")
            && !output[0].Detail.Contains("changed"),
            "Interrogation fact was mutable or its knowledge/caution projection changed.");
        observer.Dispose(); observer.Dispose();
        events.Publish(frozen);
        Require(output.Count == 1 && facts.Count == 2, "Disposed presentation lost domain delivery or emitted.");
        observer.Initialize();
        events.Publish(frozen);
        Require(output.Count == 2, "Reinitialized interrogation observer did not receive facts.");

        var codex = new RecordingCodex();
        var owner = new CaptivityInterrogationInformationRuntime(
            new EmptyNarrativeQuery(), new EmptyEnemyCatalog(), codex, events);
        var pending = new CaptivityInterrogationTerminalState
        {
            attemptId = 3, subjectDisplayName = "검증 포로", hasInformation = true,
            codexEntryId = frozen.CodexEntryId, codexEntryTitle = frozen.CodexEntryTitle,
            originEnemyArchetypeId = frozen.OriginEnemyArchetypeId,
            originFactionId = frozen.OriginFactionId, enemyDisplayName = frozen.EnemyDisplayName,
            formationTag = frozen.FormationTag, informationText = frozen.InformationText,
            highFearCaution = true
        };
        using (events.Subscribe<EventAlertRequestedEvent>(_ =>
            throw new InvalidOperationException("b129-deliberate-required-information-delivery-failure")))
        {
            Require(!owner.TryPublish("character:b129", pending, out string failure)
                && failure.Contains("b129-deliberate") && pending.codexPublicationCompleted
                && !pending.factPublicationCompleted && pending.HasPendingPublication
                && codex.PublishCount == 1, "Required delivery failure discarded frozen knowledge or acknowledged it.");
        }
        var restored = JsonUtility.FromJson<CaptivityInterrogationTerminalState>(JsonUtility.ToJson(pending));
        var freshOwner = new CaptivityInterrogationInformationRuntime(
            new EmptyNarrativeQuery(), new EmptyEnemyCatalog(), codex, events);
        Require(freshOwner.TryPublish("character:b129", restored, out _)
            && !restored.HasPendingPublication && restored.factPublicationCompleted
            && codex.PublishCount == 1 && facts.Last().InformationText == frozen.InformationText,
            "Fresh information owner replay duplicated knowledge or changed the frozen attempt.");
        int count = facts.Count;
        Require(freshOwner.TryPublish("character:b129", restored, out _) && facts.Count == count,
            "Completed information delivery replayed a fact.");
    }

    private static void VerifyInterrogationOwnerDeliveryRestore()
    {
        FixedHandler Handler() => new(CaptivityInterrogationAttemptIdentity.InteractionId,
            CaptiveInteractionKind.Interrogation,
            new CaptivityInteractionResult(true, "기존 심문 효과", willDelta: -8f,
                fearDelta: 9f, trustDelta: -4f, grudgeDelta: 7f),
            CaptivityBodyDamagePolicy.None, "심문");
        var events = new GameEventBus();
        var facts = new List<CaptivityInterrogationInformationResolvedFact>();
        using var factCapture = events.Subscribe<CaptivityInterrogationInformationResolvedFact>(facts.Add);
        using var observer = new CaptivityInterrogationPresentationObserver(events);
        observer.Initialize();
        using var fixture = new OwnerFixture("interrogation-replay", Handler(),
            DomainFactDeliveryPhase.Delivered, string.Empty, eventBus: events);
        using (events.Subscribe<EventAlertRequestedEvent>(_ =>
            throw new InvalidOperationException("b129-deliberate-required-owner-delivery-failure")))
        {
            Require(fixture.Runtime.Advance(fixture.State.captiveId, fixture.Warden, 1f, out _)
                && fixture.State.interrogationTerminal.HasPendingPublication
                && fixture.State.status == CaptivityStatus.Interaction
                && fixture.Handler.ExecuteCount == 1 && fixture.Publisher.CommitCount == 1,
                "Actual interrogation owner did not retain pending delivery after one effect commit.");
        }
        CaptivitySaveData payload = Save(fixture.State.Clone());
        string json = JsonUtility.ToJson(payload);
        var section = new CaptivitySaveSection(new UnusedCaptivityPersistence());
        section.ValidatePayload(json, CaptivitySaveData.CurrentVersion, new DungeonGameRestoreReport());
        var missing = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)missing["captives"][0]["interrogationTerminal"])
            .Remove("factPublicationCompleted");
        RequireFails(() => section.ValidatePayload(missing.ToString(),
            CaptivitySaveData.CurrentVersion, new DungeonGameRestoreReport()),
            "Missing interrogation delivery acknowledgement passed preflight.");
        RequireFails(() => section.ValidatePayload(json, CaptivitySaveData.CurrentVersion - 1,
            new DungeonGameRestoreReport()), "Previous captivity schema was silently migrated.");
        CaptivitySaveData restored = JsonUtility.FromJson<CaptivitySaveData>(json);
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(restored.captives.Single()), fixture.State);
        Require(fixture.Runtime.Advance(fixture.State.captiveId, fixture.Warden, 0f, out _)
            && fixture.State.status == CaptivityStatus.Confined
            && !fixture.State.interrogationTerminal.HasPendingPublication
            && fixture.Handler.ExecuteCount == 1 && fixture.Publisher.CommitCount == 1
            && fixture.Materials.CloseCount == 1
            && Mathf.Approximately(fixture.State.will, 62f)
            && Mathf.Approximately(fixture.State.fear, 29f)
            && Mathf.Approximately(fixture.State.trust, 26f)
            && Mathf.Approximately(fixture.State.grudge, 47f)
            && facts.Count == 2 && facts[0].AttemptId == facts[1].AttemptId,
            "Restored required-delivery retry reran effects, consumed again or changed attempt identity.");

        observer.Dispose();
        using var independent = new OwnerFixture("interrogation-observer-off", Handler(),
            DomainFactDeliveryPhase.Delivered, string.Empty, eventBus: events);
        int before = facts.Count;
        Require(independent.Runtime.Advance(independent.State.captiveId, independent.Warden, 1f, out _)
            && independent.State.status == CaptivityStatus.Confined
            && independent.State.interrogationTerminal.factPublicationCompleted
            && independent.Handler.ExecuteCount == 1 && facts.Count == before + 1,
            "Interrogation gameplay required the presentation observer.");
        Debug.Log("B129_INTERROGATION_ACTUAL_OWNER_RESTORE=PASS");
    }

    private static void RequireFails(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }

    private sealed class UnusedCaptivityPersistence : ICaptivityPersistence
    {
        public CaptivitySaveData Capture() => throw new NotSupportedException("Preflight must not capture live state.");
        public CaptivityRestoreCandidate BuildRestore(CaptivitySaveData data) =>
            throw new NotSupportedException("DTO preflight must not build live state.");
        public void PublishRestoreCandidate(CaptivityRestoreCandidate candidate) =>
            throw new NotSupportedException("Preflight must not publish state.");
    }

    private static void VerifyCanonicalOutcome()
    {
        using InteractionFactAuthority authority = new(
            "run:captivity-interaction-canonical");
        GameplayOutcomeRegistry registry = authority.Registry;
        GameplayOutcomeLedger ledger = authority.Ledger;
        CaptivityInteractionOutcomeRuntime runtime = new(
            authority.Facts,
            authority.Facts,
            new FakePhysicalSource());
        CaptiveState state = PendingState("canonical");
        state.interactionTerminal = Terminal(
            state,
            attemptId: 1,
            outputItemId: CaptivityItemDefinitions.ExtractedBloodItemId,
            outputAmount: 1,
            bodyDamage: 12f,
            bodyBefore: 80f,
            bodyAfter: 68f,
            absoluteDay: 14);

        Require(
            runtime.TryPrepare(
                state,
                out CaptivityInteractionPublication prepared,
                out long revision,
                out string failure)
            && revision == 1L
            && !prepared.IsReplay,
            "canonical interaction prepare failed: " + failure);
        using (prepared)
        {
            ApplyTerminalState(state, revision);
            DomainFactDelivery committed = runtime.Commit(prepared);
            Require(
                committed.Completed,
                "canonical interaction commit was not delivered: "
                + committed.Detail);
        }
        Require(
            runtime.TryEnsureDelivered(state, out string deliveryFailure),
            "canonical interaction delivery proof was not retained: "
            + deliveryFailure);

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

        CaptiveState replayState = PendingState("canonical");
        replayState.interactionTerminal = Terminal(
            replayState,
            attemptId: 1,
            outputItemId: CaptivityItemDefinitions.ExtractedBloodItemId,
            outputAmount: 1,
            bodyDamage: 12f,
            bodyBefore: 80f,
            bodyAfter: 68f,
            absoluteDay: 14);
        Require(
            !runtime.TryPrepare(
                replayState,
                out CaptivityInteractionPublication replay,
                out _,
                out string replayFailure)
            && replay == null
            && replayFailure
                == "captivity-interaction-unapplied-replay-conflict",
            "an unapplied stale interaction was allowed to replay: "
            + replayFailure);

        CaptiveState recoveryState = state.Clone();
        string recoveryKey = recoveryState.interactionTerminal.publicationKey;
        recoveryState.interactionTerminal.deliveryPending = true;
        Require(
            runtime.TryEnsureDelivered(
                recoveryState,
                out string recoveryFailure)
            && recoveryState.interactionTerminal.HasDeliveredOutcome
            && recoveryState.interactionTerminal.publicationKey == recoveryKey
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "applied interaction recovery failed or duplicated its row: "
            + recoveryFailure);

        CaptivityInteractionTerminalState drifted = state.interactionTerminal.Clone();
        drifted.message = "같은 키의 다른 결과";
        Require(
            !authority.Facts.Replay(new CaptivityInteractionResolvedFact(
                state.captiveId,
                drifted,
                1L)).MayBeCommitted,
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
        using InteractionFactAuthority authority = new(
            "run:captivity-interaction-authored-handlers");
        GameplayOutcomeLedger ledger = authority.Ledger;
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
            int attempt = index + 1;
            string outputItem = result.Success
                ? result.OutputItemId?.Trim() ?? string.Empty
                : string.Empty;
            int outputAmount = result.Success && outputItem.Length > 0
                ? result.OutputAmount
                : 0;
            string message = result.Message?.Trim() ?? string.Empty;
            if (message.Length == 0)
            {
                message = handler.DisplayName
                    + (result.Success ? " 완료" : " 실패");
            }
            CaptivityInteractionTerminalState terminal = new()
            {
                captiveDisplayName = state.displayName,
                absoluteDay = 20,
                attemptId = attempt,
                interactionId = handler.InteractionId,
                interactionKind = handler.Kind,
                interactionDisplayName = handler.DisplayName,
                wardenId = "character:qa-captivity-warden-authored",
                wardenDisplayName = "도윤",
                facilityId = state.housingBuildingId,
                facilityDisplayName = "감방 구속대",
                resultGridX = 7,
                resultGridY = 9,
                success = result.Success,
                message = message,
                willBefore = state.will,
                willAfter = result.Success
                    ? Mathf.Clamp(state.will + result.WillDelta, 0f, 100f)
                    : state.will,
                fearBefore = state.fear,
                fearAfter = result.Success
                    ? Mathf.Clamp(state.fear + result.FearDelta, 0f, 100f)
                    : state.fear,
                trustBefore = state.trust,
                trustAfter = result.Success
                    ? Mathf.Clamp(state.trust + result.TrustDelta, 0f, 100f)
                    : state.trust,
                grudgeBefore = state.grudge,
                grudgeAfter = result.Success
                    ? Mathf.Clamp(state.grudge + result.GrudgeDelta, 0f, 100f)
                    : state.grudge,
                corruptionBefore = state.corruption,
                corruptionAfter = result.Success
                    ? Mathf.Clamp(
                        state.corruption + result.CorruptionDelta,
                        0f,
                        100f)
                    : state.corruption,
                outputItemId = outputItem,
                outputAmount = outputAmount,
                outputOperationId = outputAmount > 0
                    ? CaptivityInteractionAttemptIdentity.FormatOutputOperationId(
                        state.captiveId,
                        attempt)
                    : string.Empty,
                bodyDamageAmount = result.Success && handler.BodyDamage.HasDamage
                    ? damage.DamageAmount
                    : 0f,
                bodyHealthBefore = result.Success && handler.BodyDamage.HasDamage
                    ? damage.CurrentHealth
                    : 0f,
                bodyHealthAfter = result.Success && handler.BodyDamage.HasDamage
                    ? damage.ExpectedHealth
                    : 0f,
                bodyMaximumHealth = result.Success && handler.BodyDamage.HasDamage
                    ? damage.MaximumHealth
                    : 0f
            };
            CaptivityInteractionResolvedFact fact = new(
                state.captiveId,
                terminal,
                attempt);
            Require(authority.Facts.TryStage(
                    fact,
                    out IDomainFactStage stage,
                    out string stageFailure),
                $"authored handler '{handler.InteractionId}' could not stage: "
                + stageFailure);
            try
            {
                Require(
                    stage.Commit().Completed,
                    $"authored handler '{handler.InteractionId}' could not record");
            }
            finally
            {
                stage.Cancel();
            }
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
                   DomainFactDeliveryPhase.Rejected,
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
                && rejected.State.interactionTerminal.publicationKey.Length == 0
                && !rejected.State.interactionTerminal.deliveryPending
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
                && rejected.Publisher.CommitCount == 2,
                "a rejected frozen interaction reran its handler");
        }

        using (OwnerFixture pending = new(
                   "delivery-pending",
                   new FixedHandler(
                       "captivity:qa-delivery-pending",
                       CaptiveInteractionKind.Branding,
                       effect,
                       new CaptivityBodyDamagePolicy(8f)),
                   DomainFactDeliveryPhase.AcceptedPendingDelivery,
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
                && pending.State.currentInteractionId.Length == 0
                && !pending.State.interactionTerminal.HasOutcome
                && pending.Handler.ExecuteCount == 1
                && pending.Publisher.CommitCount == 1
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
                   DomainFactDeliveryPhase.Delivered,
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
                && retry.State.interactionTerminal.HasDeliveredOutcome
                && !retry.State.interactionTerminal.outputPublished
                && retry.Handler.ExecuteCount == 1
                && retry.Publisher.CommitCount == 1
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
                && retry.Publisher.CommitCount == 1
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
                   DomainFactDeliveryPhase.Delivered,
                   string.Empty,
                   outputFailures: 1,
                   startingHealth: 10f))
        {
            lethal.Publisher.BeforeReturn = () => Require(
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
                && lethal.State.interactionTerminal.HasDeliveredOutcome
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
        using var observer = new CaptivityInterrogationPresentationObserver(events);
        observer.Initialize();
        int notices = 0;
        using IDisposable subscription = events.Subscribe<EventAlertRequestedEvent>(
            _ => notices++);
        using OwnerFixture fixture = new(
            "interrogation",
            interrogation,
            DomainFactDeliveryPhase.Delivered,
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
            && fixture.Publisher.LastFact.Snapshot().interactionKind
                == CaptiveInteractionKind.Interrogation
            && notices == 1
            && fixture.State.status == CaptivityStatus.Confined
            && fixture.State.currentInteractionId.Length == 0
            && !fixture.State.interactionTerminal.HasOutcome
            && fixture.State.currentInterrogationAttemptId == 0
            && fixture.State.interrogationTerminal.HasOutcome
            && fixture.State.interrogationTerminal.codexPublicationCompleted
            && fixture.State.interrogationTerminal.factPublicationCompleted
            && !fixture.State.interrogationTerminal.HasPendingPublication,
            "interrogation outcome and publication were not composed exactly once");
    }

    private static void VerifySaveCompatibility()
    {
        CaptiveState committed = PublishedInteractionState();

        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(Save(committed)));
        DungeonGameRestoreReport valid = new();
        CaptivitySaveValidation.Validate(roundTrip, valid);
        Require(
            valid.Success
            && roundTrip.captives.Single().interactionTerminal.attemptId == 3
            && roundTrip.captives.Single().interactionOutcomeRevision == 2L
            && roundTrip.captives.Single().interactionTerminal.HasDeliveredOutcome,
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

        CaptiveState missingPublication = committed.Clone();
        missingPublication.interactionTerminal.publicationKey = string.Empty;
        RequireInvalidSave(
            missingPublication,
            "committed interaction without its V9 publication key was accepted");
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
        string interactionId = "captivity:persuasion",
        CaptiveInteractionKind kind = CaptiveInteractionKind.Persuasion,
        string display = "회유",
        string outputItemId = "",
        int outputAmount = 0,
        float bodyDamage = 0f,
        float bodyBefore = 0f,
        float bodyAfter = 0f,
        bool bodyObserversPending = false,
        int absoluteDay = 21) => new()
    {
        captiveDisplayName = state.displayName,
        absoluteDay = absoluteDay,
        attemptId = attemptId,
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

    private static void ApplyTerminalState(CaptiveState state, long revision)
    {
        CaptivityInteractionTerminalState terminal = state.interactionTerminal;
        state.will = terminal.willAfter;
        state.fear = terminal.fearAfter;
        state.trust = terminal.trustAfter;
        state.grudge = terminal.grudgeAfter;
        state.corruption = terminal.corruptionAfter;
        state.interactionOutcomeRevision = revision;
        terminal.outcomeRevision = revision;
    }

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

    private static CaptiveState PublishedInteractionState()
    {
        using InteractionFactAuthority authority = new(
            "run:captivity-interaction-save-roundtrip");
        CaptivityInteractionOutcomeRuntime runtime = new(
            authority.Facts,
            authority.Facts,
            new FakePhysicalSource());
        CaptiveState state = PendingState("save-roundtrip");
        state.currentInteractionId = "captivity:memory-extraction";
        state.interactionAttemptSequence = 3;
        state.currentInteractionAttemptId = 3;
        state.interactionOutcomeRevision = 1L;
        state.completedInteractionWork = 10f;
        state.requiredInteractionWork = 10f;
        state.interactionTerminal = Terminal(
            state,
            attemptId: 3,
            interactionId: state.currentInteractionId,
            kind: CaptiveInteractionKind.MemoryExtraction,
            display: "기억 추출",
            outputItemId: CaptivityItemDefinitions.MemoryResidueItemId,
            outputAmount: 1,
            bodyDamage: 10f,
            bodyBefore: 100f,
            bodyAfter: 90f,
            bodyObserversPending: true);
        Require(
            runtime.TryPrepare(
                state,
                out CaptivityInteractionPublication prepared,
                out long revision,
                out string prepareFailure),
            "interaction save fact prepare failed: " + prepareFailure);
        using (prepared)
        {
            ApplyTerminalState(state, revision);
            state.interactionTerminal.bodyObserversPending = true;
            Require(
                runtime.Commit(prepared).Completed,
                "interaction save fact was not delivered");
        }
        return state.Clone();
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
        private readonly InteractionFactAuthority authority;

        public OwnerFixture(
            string suffix,
            FixedHandler handler,
            DomainFactDeliveryPhase phase,
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
            authority = new InteractionFactAuthority(
                "run:captivity-interaction:" + suffix);
            Publisher = new ConfigurablePublisher(
                authority.Facts,
                phase,
                detail);
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
                new CaptivityInteractionOutcomeRuntime(
                    Publisher,
                    authority.Facts,
                    Physical),
                new FakeCalendar(),
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
        public ConfigurablePublisher Publisher { get; }
        public FakeMaterials Materials { get; }
        public CaptivityInteractionRuntime Runtime { get; }

        public void Dispose()
        {
            authority.Dispose();
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

    private sealed class InteractionFactAuthority : IDisposable
    {
        private readonly IDisposable observation;

        public InteractionFactAuthority(string runId)
        {
            Registry = CaptivityInteractionOutcomeDebugScenarios.Registry();
            Ledger = NewLedger(Registry, runId);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                Registry,
                new GameEventBus());
            Facts = new DomainFactDispatcher();
            observation = Facts.Subscribe(
                "captivity-interaction:fixture",
                new GameplayDomainFactObserver<
                    CaptivityInteractionResolvedFact,
                    CaptivityInteractionOutcomeReceipt>(
                    CaptivityInteractionFactMapping.Capture,
                    Registry,
                    Ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, Ledger)));
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public DomainFactDispatcher Facts { get; }

        public void Dispose()
        {
            observation.Dispose();
            Facts.Dispose();
        }
    }

    private sealed class ConfigurablePublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly DomainFactDeliveryPhase phase;
        private readonly string detail;

        public ConfigurablePublisher(
            IDomainFactStagingPublisher inner,
            DomainFactDeliveryPhase phase,
            string detail)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.phase = phase;
            this.detail = detail ?? string.Empty;
        }

        public CaptivityInteractionResolvedFact LastFact { get; private set; }
        public int PrepareCount { get; private set; }
        public int CommitCount { get; set; }
        public Action BeforeReturn { get; set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            PrepareCount++;
            if (fact is CaptivityInteractionResolvedFact interaction)
                LastFact = interaction;
            if (!inner.TryStage(fact, out stage, out failureReason))
                return false;
            stage = new ConfigurableStage(this, stage, phase, detail);
            return true;
        }
    }

    private sealed class ConfigurableStage : IDomainFactStage
    {
        private readonly ConfigurablePublisher owner;
        private readonly IDomainFactStage inner;
        private readonly DomainFactDeliveryPhase phase;
        private readonly string detail;

        public ConfigurableStage(
            ConfigurablePublisher owner,
            IDomainFactStage inner,
            DomainFactDeliveryPhase phase,
            string detail)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.phase = phase;
            this.detail = detail ?? string.Empty;
        }

        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;

        public DomainFactDelivery Commit()
        {
            owner.CommitCount++;
            if (phase == DomainFactDeliveryPhase.Rejected)
            {
                inner.Cancel();
                owner.BeforeReturn?.Invoke();
                return DomainFactDelivery.Reject(detail);
            }
            DomainFactDelivery delivery = inner.Commit();
            owner.BeforeReturn?.Invoke();
            return phase == DomainFactDeliveryPhase.Delivered
                || !delivery.MayBeCommitted
                    ? delivery
                    : new DomainFactDelivery(
                        phase,
                        delivery.OperationKey,
                        delivery.PublicationId,
                        delivery.Fingerprint,
                        detail);
        }

        public void Cancel() => inner.Cancel();
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
        public void ApplyPreparedMedicalStabilization(
            CharacterActor actor) => throw new NotSupportedException();
        public void ApplyPreparedMedicalTreatment(
            CharacterActor actor,
            float partHealthAmount,
            float bloodLossReduction) => throw new NotSupportedException();
        public void CompletePreparedMedicalMutation(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot before) =>
            throw new NotSupportedException();
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
        public int PublishCount { get; private set; }
        public bool HasInformation(string entryId, string informationText) => false;
        public void PublishInformation(
            string entryId,
            string fallbackTitle,
            string informationText)
        {
            PublishCount++;
        }
    }

    private sealed class FakeCalendar : IGameCalendar
    {
        public int Day => 21;
        public int Hour => 0;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour)
        {
        }
    }
}
#endif
