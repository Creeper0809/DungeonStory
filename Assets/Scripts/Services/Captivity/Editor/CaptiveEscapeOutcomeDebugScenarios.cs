#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class CaptiveEscapeOutcomeDebugScenarios
{
    private const string CaptiveIdPrefix = "character:qa-captive-escape-";

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyCanonicalKinds();
        VerifyProductionOwners();
        VerifyCommitBoundaries();
        VerifyCaptivitySaveCompatibility();
        if (logSuccess)
            Debug.Log("[Captive Escape Outcome] PASS");
        return true;
    }

    public static bool RunCanonicalSaveRoundTrip()
    {
        VerifyCanonicalKinds();
        return true;
    }

    private static void VerifyCanonicalKinds()
    {
        using EscapeFactAuthority authority = new("run:captive-escape");
        GameplayOutcomeRegistry registry = authority.Registry;
        GameplayOutcomeLedger ledger = authority.Ledger;
        GameEventBus eventBus = new();
        int legacyEventCount = 0;
        using IDisposable subscription = eventBus.Subscribe<CaptiveEscapedEvent>(
            _ => legacyEventCount++);
        CaptiveEscapeOutcomeRuntime runtime = new(
            authority.Facts,
            authority.Facts,
            eventBus);
        EscapeCase[] cases =
        {
            new(
                "physical",
                CaptiveEscapeKind.PhysicalEscape,
                CaptivityStatus.EscapeAttempt,
                "감방 외벽 통과",
                42f),
            new(
                "arrival",
                CaptiveEscapeKind.ArrivalCustodyLoss,
                CaptivityStatus.AwaitingCapture,
                "귀환 수용 전 이탈",
                37f),
            new(
                "betrayal",
                CaptiveEscapeKind.FalseComplianceBetrayal,
                CaptivityStatus.Labor,
                "침공의 혼란",
                66f),
            new(
                "minion",
                CaptiveEscapeKind.MinionControlBreak,
                CaptivityStatus.Minion,
                "정착지 통제 거부",
                58f)
        };

        foreach (EscapeCase escapeCase in cases)
        {
            CaptiveState state = State(escapeCase.Suffix, escapeCase.PreviousStatus);
            Require(
                runtime.TryPrepare(
                    state,
                    escapeCase.Kind,
                    "  " + escapeCase.Trigger + "  ",
                    escapeCase.PreviousStatus,
                    escapeCase.Pressure,
                    12,
                    out CaptiveEscapePublication prepared,
                    out long revision,
                    out string trigger,
                    out string failure),
                escapeCase.Suffix + " prepare failed: " + failure);
            using (prepared)
            {
                Require(
                    revision == 1L
                    && trigger == escapeCase.Trigger
                    && !prepared.IsReplay,
                    escapeCase.Suffix + " prepare did not freeze canonical inputs");
                state.status = CaptivityStatus.Escaped;
                state.escapeOutcomeRevision = revision;
                state.retaliationPressure = escapeCase.Pressure;
                DomainFactDelivery commit = runtime.Commit(prepared);
                Require(
                    commit.Completed,
                    escapeCase.Suffix + " commit was not delivered: " + commit.Detail);
                runtime.NotifyCommitted(prepared);
            }
        }

        Require(legacyEventCount == cases.Length, "legacy escape observer count drifted");
        // This detached ledger has no PlayerLoop/day-boundary scheduler.
        // Run the real bounded retention pipeline before expecting its policy
        // to promote the mutable subject tier; the initial receipt stays Recent.
        Require(ledger.TrySchedule(new GameplayOutcomeConsolidationRequest(
                12, cases.Length, registry.ConsolidationPolicyVersion,
                ledger.CurrentWorldEpoch), out string scheduleFailure),
            "escape memory schedule failed: " + scheduleFailure);
        int memorySlices = 0;
        while (ledger.PendingJobCount > 0 && memorySlices++ < 8)
            ledger.ProcessSlice(cases.Length, 0L);
        Require(ledger.PendingJobCount == 0,
            "four escape memory records did not finish bounded consolidation");
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == cases.Length
            && global.Items.All(item => item.Exact != null),
            "global query did not expose four exact escape outcomes");
        foreach (EscapeCase escapeCase in cases)
        {
            GameplayEntityId captive = new(
                CaptiveEscapeOutcomeIds.CharacterKind,
                Id(escapeCase.Suffix));
            GameplayOutcomeQueryPage character = ledger.GetForEntity(
                captive,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All);
            Require(
                character.Items.Count == 1
                && character.Items[0].Exact.participants.Single().displayText == "가람"
                && Fact(
                    character.Items[0].Exact,
                    CaptiveEscapeOutcomeIds.KindFact.Value)
                    == CaptiveEscapeOutcomeIds.KindId(escapeCase.Kind)
                && Fact(
                    character.Items[0].Exact,
                    CaptiveEscapeOutcomeIds.TriggerFact.Value)
                    == escapeCase.Trigger,
                escapeCase.Suffix + " exact receipt shape drifted");
            GameplayOutcomeSnapshot exact = character.Items[0].Exact;
            Require(exact.subjects.Single().tier == NarrativeMemoryTier.Episodic
                && exact.initialSubjects.Single().tier == NarrativeMemoryTier.Recent,
                escapeCase.Suffix + " initial receipt or published memory tier differs from ledger/policy contract");
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
                && view.Text.Contains("가람이", StringComparison.Ordinal)
                && view.Text.Contains(escapeCase.Trigger, StringComparison.Ordinal),
                escapeCase.Suffix + " Korean projection was not deterministic");
        }

        EscapeCase replayCase = cases[0];
        CaptiveState replayState = State(
            replayCase.Suffix,
            replayCase.PreviousStatus);
        Require(
            runtime.TryPrepare(
                replayState,
                replayCase.Kind,
                replayCase.Trigger,
                replayCase.PreviousStatus,
                replayCase.Pressure,
                12,
                out CaptiveEscapePublication replay,
                out long replayRevision,
                out _,
                out string replayFailure),
            "canonical escape replay prepare failed: " + replayFailure);
        using (replay)
        {
            replayState.status = CaptivityStatus.Escaped;
            replayState.escapeOutcomeRevision = replayRevision;
            replayState.retaliationPressure = replayCase.Pressure;
            Require(
                replay.IsReplay
                && runtime.Commit(replay).Completed
                && ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10),
                    OutcomeFilter.All).Items.Count == cases.Length,
                "canonical escape replay failed or duplicated: " + replayFailure);
            runtime.NotifyCommitted(replay);
        }

        CaptiveEscapedFact drifted = new(new CaptiveEscapedFactData
        {
            captiveId = Id(replayCase.Suffix),
            displayName = "가람",
            kind = replayCase.Kind,
            trigger = "다른 탈출 원인",
            previousStatus = replayCase.PreviousStatus,
            retaliationPressure = replayCase.Pressure,
            revision = 1L,
            absoluteDay = 12
        });
        Require(
            !authority.Facts.Replay(drifted).MayBeCommitted,
            "same escape result key accepted a drifted receipt");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = NewLedger(
            registry,
            "run:captive-escape-restore");
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        string[] beforeHashes = global.Items
            .Select(item => item.Exact.immutablePayloadHash)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] afterHashes = restored.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All)
            .Items.Select(item => item.Exact.immutablePayloadHash)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(
            beforeHashes.SequenceEqual(afterHashes),
            "escape outcomes changed across ledger save round trip");

        GameplayOutcomeLedgerSaveData wrongTier = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(saved));
        wrongTier.exactOutcomes[0].initialSubjects[0].tier = NarrativeMemoryTier.Episodic;
        string beforeRejectedRestore = JsonUtility.ToJson(restored.CaptureGameplayOutcomes());
        bool wrongTierRejected = false;
        string wrongTierFailure = string.Empty;
        try
        {
            restored.PrepareGameplayOutcomeRestore(wrongTier);
        }
        catch (InvalidOperationException exception)
        {
            wrongTierFailure = exception.Message;
            wrongTierRejected = wrongTierFailure.Contains(
                "invalid immutable initial subject link", StringComparison.Ordinal);
        }
        Require(wrongTierRejected,
            "escape ledger did not reject mutation at the canonical initial-row guard: "
                + wrongTierFailure);
        Require(beforeRejectedRestore == JsonUtility.ToJson(restored.CaptureGameplayOutcomes()),
            "rejected escape tier restore changed the live ledger");
    }

    private static void VerifyCommitBoundaries()
    {
        CaptiveState prepareFailed = State(
            "prepare-failed",
            CaptivityStatus.EscapeAttempt);
        using (EscapeFactAuthority authority = new(
                   "run:captive-escape-prepare-reject"))
        {
            ConfigurablePublisher publisher = new(
                authority.Facts,
                DomainFactDeliveryPhase.Delivered,
                string.Empty,
                prepareSucceeds: false);
            CaptiveEscapeOutcomeRuntime prepareRuntime = new(
                publisher,
                authority.Facts,
                new GameEventBus());
            Require(
                !prepareRuntime.TryPrepare(
                    prepareFailed,
                    CaptiveEscapeKind.PhysicalEscape,
                    "외벽 통과",
                    CaptivityStatus.EscapeAttempt,
                    20f,
                    3,
                    out _,
                    out _,
                    out _,
                    out string prepareFailure)
                && prepareFailure == "qa-prepare-rejected"
                && prepareFailed.status == CaptivityStatus.EscapeAttempt
                && prepareFailed.escapeOutcomeRevision == 0L,
                "prepare failure changed captive state");
        }

        CaptiveState rejected = State("rejected", CaptivityStatus.Labor);
        using (EscapeFactAuthority authority = new(
                   "run:captive-escape-commit-reject"))
        {
            ConfigurablePublisher publisher = new(
                authority.Facts,
                DomainFactDeliveryPhase.Rejected,
                "qa-definite-rejection");
            CaptiveEscapeOutcomeRuntime rejectRuntime = new(
                publisher,
                authority.Facts,
                new GameEventBus());
            Require(
                rejectRuntime.TryPrepare(
                    rejected,
                    CaptiveEscapeKind.FalseComplianceBetrayal,
                    "배신 기회",
                    CaptivityStatus.Labor,
                    55f,
                    4,
                    out CaptiveEscapePublication rejectedPrepared,
                    out long rejectedRevision,
                    out _,
                    out string rejectPrepareFailure),
                "rejection scenario prepare failed: " + rejectPrepareFailure);
            using (rejectedPrepared)
            {
                rejected.status = CaptivityStatus.Escaped;
                rejected.escapeOutcomeRevision = rejectedRevision;
                rejected.retaliationPressure = 55f;
                DomainFactDelivery rejectedCommit = rejectRuntime.Commit(
                    rejectedPrepared);
                Require(
                    !rejectedCommit.MayBeCommitted
                    && rejectedCommit.Detail == "qa-definite-rejection"
                    && !rejected.escapeObservation.HasFact,
                    "definite rejection was reported as durable or retained observation state");
                rejected.status = CaptivityStatus.Labor;
                rejected.escapeOutcomeRevision = 0L;
                rejected.retaliationPressure = 0f;
            }
            Require(
                rejected.status == CaptivityStatus.Labor
                && rejected.escapeOutcomeRevision == 0L,
                "definite rejection owner rollback was not exact");
        }

        GameEventBus pendingBus = new();
        int pendingEvents = 0;
        using IDisposable pendingSubscription =
            pendingBus.Subscribe<CaptiveEscapedEvent>(_ => pendingEvents++);
        CaptiveState pending = State("pending", CaptivityStatus.Minion);
        using (EscapeFactAuthority authority = new(
                   "run:captive-escape-delivery-pending"))
        {
            ConfigurablePublisher publisher = new(
                authority.Facts,
                DomainFactDeliveryPhase.AcceptedPendingDelivery,
                "qa-delivery-pending");
            CaptiveEscapeOutcomeRuntime pendingRuntime = new(
                publisher,
                authority.Facts,
                pendingBus);
            Require(
                pendingRuntime.TryPrepare(
                    pending,
                    CaptiveEscapeKind.MinionControlBreak,
                    "통제 거부",
                    CaptivityStatus.Minion,
                    48f,
                    5,
                    out CaptiveEscapePublication pendingPrepared,
                    out long pendingRevision,
                    out _,
                    out string pendingFailure),
                "pending scenario prepare failed: " + pendingFailure);
            using (pendingPrepared)
            {
                pending.status = CaptivityStatus.Escaped;
                pending.escapeOutcomeRevision = pendingRevision;
                pending.retaliationPressure = 48f;
                DomainFactDelivery pendingCommit = pendingRuntime.Commit(
                    pendingPrepared);
                Require(
                    pendingCommit.Phase
                        == DomainFactDeliveryPhase.AcceptedPendingDelivery
                    && pending.escapeObservation.pending,
                    "durable pending delivery was treated as rejection");
                pendingRuntime.NotifyCommitted(pendingPrepared);
                Require(
                    pendingEvents == 0,
                    "pending delivery notified observers before recovery");
            }
            Require(
                pendingRuntime.Recover(pending, out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !pending.escapeObservation.pending
                && pendingEvents == 1
                && pendingRuntime.Recover(pending, out _)
                && pendingEvents == 1,
                "pending delivery did not recover and notify exactly once");
        }

        CaptiveState observerFault = State(
            "observer-fault",
            CaptivityStatus.AwaitingCapture);
        using (EscapeFactAuthority authority = new(
                   "run:captive-escape-observer-fault"))
        {
            CaptiveEscapeOutcomeRuntime observerRuntime = new(
                authority.Facts,
                authority.Facts,
                new ThrowingEventBus());
            Require(
                observerRuntime.TryPrepare(
                    observerFault,
                    CaptiveEscapeKind.ArrivalCustodyLoss,
                    "귀환 수용 전 이탈",
                    CaptivityStatus.AwaitingCapture,
                    33f,
                    6,
                    out CaptiveEscapePublication observerPrepared,
                    out long observerRevision,
                    out _,
                    out string observerPrepareFailure),
                "observer scenario prepare failed: " + observerPrepareFailure);
            using (observerPrepared)
            {
                observerFault.status = CaptivityStatus.Escaped;
                observerFault.escapeOutcomeRevision = observerRevision;
                observerFault.retaliationPressure = 33f;
                Require(
                    observerRuntime.Commit(observerPrepared).Completed,
                    "observer scenario did not commit");
                observerRuntime.NotifyCommitted(observerPrepared);
            }
            Require(
                observerFault.escapeOutcomeRevision == 1L
                && !observerFault.escapeObservation.observersPending,
                "observer exception escaped or rolled back a durable result");
        }
    }

    private static void VerifyProductionOwners()
    {
        using (OwnerFixture physical = new(
                   "owner-physical",
                   CaptivityStatus.EscapeAttempt,
                   DomainFactDeliveryPhase.Rejected,
                   "qa-physical-rejected"))
        {
            physical.State.restrained = true;
            physical.State.grudge = 25f;
            physical.Actor.SetAiPaused(true);
            physical.Doors.SetCaptive(physical.Id, true);
            string before = JsonUtility.ToJson(physical.State);
            Require(
                !physical.Completion.CompleteEscape(
                    physical.Id,
                    physical.Actor,
                    "외벽 통과",
                    out string failure)
                && failure == "qa-physical-rejected"
                && JsonUtility.ToJson(physical.State) == before
                && physical.Actor.characterType == CharacterType.NPC
                && physical.Actor.IsAiPaused()
                && physical.Doors.IsCaptive(physical.Id)
                && physical.EventCount == 0,
                "physical escape rejection did not restore exact owner state");

            ConfigurablePublisher pending = physical.NewPublisher(
                DomainFactDeliveryPhase.AcceptedPendingDelivery,
                "qa-physical-pending");
            CaptivityEscapeCompletionRuntime retry = physical.NewCompletion(pending);
            Require(
                retry.CompleteEscape(
                    physical.Id,
                    physical.Actor,
                    "외벽 통과",
                    out string retryFailure)
                && retryFailure.StartsWith(
                    "captive-escape-delivery-pending:",
                    StringComparison.Ordinal)
                && physical.State.status == CaptivityStatus.Escaped
                && physical.State.escapeOutcomeRevision == 1L
                && physical.Actor.characterType == CharacterType.Intruder
                && !physical.Actor.IsAiPaused()
                && !physical.Doors.IsCaptive(physical.Id)
                && physical.State.escapeObservation.pending
                && physical.EventCount == 0
                && pending.LastFact.Snapshot().kind
                    == CaptiveEscapeKind.PhysicalEscape,
                "pending physical escape did not retain its applied owner state");
            Require(
                physical.Recover(out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !physical.State.escapeObservation.pending
                && physical.EventCount == 1,
                "persisted physical escape did not recover one durable result");
        }

        using (OwnerFixture arrival = new(
                   "owner-arrival",
                   CaptivityStatus.AwaitingCapture,
                   DomainFactDeliveryPhase.Delivered,
                   string.Empty))
        {
            Require(
                arrival.Completion.CompleteEscape(
                    arrival.Id,
                    arrival.Actor,
                    "귀환 수용 전 이탈",
                    out string failure)
                && failure.Length == 0
                && arrival.Publisher.LastFact.Snapshot().kind
                    == CaptiveEscapeKind.ArrivalCustodyLoss,
                "arrival custody loss did not use its distinct receipt kind");
        }

        using (OwnerFixture betrayalRejected = new(
                   "owner-betrayal-rejected",
                   CaptivityStatus.Labor,
                   DomainFactDeliveryPhase.Rejected,
                   "qa-betrayal-rejected"))
        {
            betrayalRejected.State.falseCompliance = true;
            betrayalRejected.State.restrained = true;
            betrayalRejected.State.grudge = 40f;
            betrayalRejected.Actor.SetAiPaused(true);
            betrayalRejected.Doors.SetCaptive(betrayalRejected.Id, true);
            string before = JsonUtility.ToJson(betrayalRejected.State);
            Require(
                !betrayalRejected.Defection.TryTriggerBetrayal(
                    betrayalRejected.Id,
                    "침공의 혼란",
                    out string failure)
                && failure == "qa-betrayal-rejected"
                && JsonUtility.ToJson(betrayalRejected.State) == before
                && betrayalRejected.Actor.characterType == CharacterType.NPC
                && betrayalRejected.Actor.IsAiPaused()
                && betrayalRejected.Doors.IsCaptive(betrayalRejected.Id)
                && betrayalRejected.EventCount == 0,
                "betrayal rejection did not restore captive/actor/door owners");
        }

        using (OwnerFixture betrayalPending = new(
                   "owner-betrayal-pending",
                   CaptivityStatus.Performer,
                   DomainFactDeliveryPhase.AcceptedPendingDelivery,
                   "qa-betrayal-pending"))
        {
            betrayalPending.State.falseCompliance = true;
            betrayalPending.State.restrained = true;
            Require(
                betrayalPending.Defection.TryTriggerBetrayal(
                    betrayalPending.Id,
                    "무대 뒤 혼란",
                    out string failure)
                && failure.StartsWith(
                    "captive-escape-delivery-pending:",
                    StringComparison.Ordinal)
                && betrayalPending.State.status == CaptivityStatus.Escaped
                && betrayalPending.State.escapeOutcomeRevision == 1L
                && betrayalPending.Publisher.LastFact.Snapshot().kind
                    == CaptiveEscapeKind.FalseComplianceBetrayal
                && betrayalPending.State.escapeObservation.pending
                && betrayalPending.EventCount == 0,
                "durable pending betrayal did not retain applied state");
            Require(
                betrayalPending.Recover(out string recoveryFailure)
                && recoveryFailure.Length == 0
                && betrayalPending.EventCount == 1,
                "durable pending betrayal did not recover exactly once");
        }

        using (OwnerFixture minionRejected = new(
                   "owner-minion-rejected",
                   CaptivityStatus.Minion,
                   DomainFactDeliveryPhase.Rejected,
                   "qa-minion-rejected"))
        {
            minionRejected.Population.Standing = CharacterSettlementStanding.Minion;
            minionRejected.Employment.Standing = CharacterSettlementStanding.Minion;
            minionRejected.State.rehabilitationInProgress = true;
            minionRejected.State.reservedWardenId = "character:qa-warden";
            string before = JsonUtility.ToJson(minionRejected.State);
            Require(
                !minionRejected.Defection.TryBreakMinionControl(
                    minionRejected.Id,
                    "통제 거부",
                    out string failure)
                && failure == "qa-minion-rejected"
                && JsonUtility.ToJson(minionRejected.State) == before
                && minionRejected.Actor.characterType == CharacterType.NPC
                && minionRejected.Actor.Identity.CharacterType == CharacterType.NPC
                && minionRejected.Population.Standing
                    == CharacterSettlementStanding.Minion
                && minionRejected.Population.RollbackCount == 1
                && minionRejected.Population.CompleteCount == 0
                && minionRejected.Employment.Standing
                    == CharacterSettlementStanding.Minion
                && minionRejected.EventCount == 0,
                "minion rejection did not restore every reversible owner");
        }

        using (OwnerFixture minionPending = new(
                   "owner-minion-pending",
                   CaptivityStatus.Minion,
                   DomainFactDeliveryPhase.AcceptedPendingDelivery,
                   "qa-minion-pending"))
        {
            minionPending.Population.Standing = CharacterSettlementStanding.Minion;
            minionPending.Employment.Standing = CharacterSettlementStanding.Minion;
            Require(
                minionPending.Defection.TryBreakMinionControl(
                    minionPending.Id,
                    "통제에서 벗어남",
                    out string failure)
                && failure.StartsWith(
                    "captive-escape-delivery-pending:",
                    StringComparison.Ordinal)
                && minionPending.State.status == CaptivityStatus.Escaped
                && minionPending.State.escapeOutcomeRevision == 1L
                && minionPending.Actor.characterType == CharacterType.Intruder
                && minionPending.Actor.Identity.CharacterType
                    == CharacterType.Intruder
                && minionPending.Population.Standing
                    == CharacterSettlementStanding.PreparedCandidate
                && minionPending.Population.CompleteCount == 1
                && minionPending.Population.RollbackCount == 0
                && minionPending.Employment.Standing
                    == CharacterSettlementStanding.PreparedCandidate
                && minionPending.Publisher.LastFact.Snapshot().kind
                    == CaptiveEscapeKind.MinionControlBreak
                && minionPending.State.escapeObservation.pending
                && minionPending.EventCount == 0,
                "durable pending minion defection did not commit every owner");
            Require(
                minionPending.Recover(out string recoveryFailure)
                && recoveryFailure.Length == 0
                && minionPending.EventCount == 1,
                "durable pending minion defection did not recover exactly once");
        }
    }

    private static void VerifyCaptivitySaveCompatibility()
    {
        CaptiveState committed;
        using (OwnerFixture fixture = new(
                   "save",
                   CaptivityStatus.AwaitingCapture,
                   DomainFactDeliveryPhase.Delivered,
                   string.Empty))
        {
            Require(
                fixture.Completion.CompleteEscape(
                    fixture.Id,
                    fixture.Actor,
                    "귀환 수용 전 이탈",
                    out string failure),
                "escape save fixture failed: " + failure);
            committed = fixture.State.Clone();
        }
        CaptivitySaveData source = Save(committed);
        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(source));
        DungeonGameRestoreReport roundTripReport = new();
        CaptivitySaveValidation.Validate(roundTrip, roundTripReport);
        Require(
            roundTripReport.Success
            && roundTrip.captives.Single().escapeOutcomeRevision == 1L
            && roundTrip.captives.Single().escapeObservation.HasFact
            && !roundTrip.captives.Single().escapeObservation.pending,
            "captivity save round trip lost escape revision: "
            + string.Join(" | ", roundTripReport.Errors));

        CaptiveState missingFact = State("missing-fact", CaptivityStatus.Escaped);
        DungeonGameRestoreReport missingFactReport = new();
        CaptivitySaveValidation.Validate(Save(missingFact), missingFactReport);
        Require(
            !missingFactReport.Success,
            "escaped state without its frozen transition fact was accepted");

        CaptiveState negative = committed.Clone();
        negative.escapeOutcomeRevision = -1L;
        DungeonGameRestoreReport negativeReport = new();
        CaptivitySaveValidation.Validate(Save(negative), negativeReport);
        Require(!negativeReport.Success, "negative escape revision was accepted");

        CaptiveState torn = committed.Clone();
        torn.status = CaptivityStatus.Confined;
        DungeonGameRestoreReport tornReport = new();
        CaptivitySaveValidation.Validate(Save(torn), tornReport);
        Require(
            !tornReport.Success,
            "positive escape revision on a non-escaped state was accepted");
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
            or CaptivityStatus.Labor
            or CaptivityStatus.Performer
            or CaptivityStatus.EscapeAttempt
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

    private static string Id(string suffix) => CaptiveIdPrefix + suffix;

    private static string Fact(GameplayOutcomeSnapshot snapshot, string factId) =>
        snapshot.facts.Single(value => value.factId == factId).value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct EscapeCase
    {
        public EscapeCase(
            string suffix,
            CaptiveEscapeKind kind,
            CaptivityStatus previousStatus,
            string trigger,
            float pressure)
        {
            Suffix = suffix;
            Kind = kind;
            PreviousStatus = previousStatus;
            Trigger = trigger;
            Pressure = pressure;
        }

        public string Suffix { get; }
        public CaptiveEscapeKind Kind { get; }
        public CaptivityStatus PreviousStatus { get; }
        public string Trigger { get; }
        public float Pressure { get; }
    }

    private sealed class OwnerFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly IDisposable subscription;
        private readonly CaptivityActorAccess actors;
        private readonly CaptivityActorRuntimeLookup actorRuntime;
        private readonly FakeCalendar calendar = new();
        private readonly EscapeFactAuthority authority;

        public OwnerFixture(
            string suffix,
            CaptivityStatus status,
            DomainFactDeliveryPhase phase,
            string detail)
        {
            Id = CaptiveIdPrefix + suffix;
            Actor = CreateActor(suffix);
            State = State(suffix, status);
            actors = new CaptivityActorAccess(
                new DungeonRuntimeAggregateRootStore(),
                _ => { });
            actors.AddState(State);
            actorRuntime = new CaptivityActorRuntimeLookup(id =>
                string.Equals(id, Id, StringComparison.Ordinal) ? Actor : null);
            Doors = new FakeDoorSubjects();
            Doors.SetCaptive(Id, State.IsInCustody);
            Population = new FakePopulationStandingPort();
            Employment = new FakeEmploymentStanding();
            Events = new GameEventBus();
            authority = new EscapeFactAuthority(
                "run:captive-escape:" + suffix);
            Publisher = NewPublisher(phase, detail);
            subscription = Events.Subscribe<CaptiveEscapedEvent>(_ => EventCount++);
            Completion = NewCompletion(Publisher);
            Defection = new CaptivityDefectionRuntime(
                actors,
                actorRuntime,
                new AlwaysChanceRandom(),
                Doors,
                Population,
                Employment,
                calendar,
                new CaptiveEscapeOutcomeRuntime(
                    Publisher,
                    authority.Facts,
                    Events));
        }

        public string Id { get; }
        public CharacterActor Actor { get; }
        public CaptiveState State { get; }
        public FakeDoorSubjects Doors { get; }
        public FakePopulationStandingPort Population { get; }
        public FakeEmploymentStanding Employment { get; }
        public ConfigurablePublisher Publisher { get; }
        public GameEventBus Events { get; }
        public CaptivityEscapeCompletionRuntime Completion { get; }
        public CaptivityDefectionRuntime Defection { get; }
        public int EventCount { get; private set; }

        public CaptivityEscapeCompletionRuntime NewCompletion(
            IDomainFactStagingPublisher publisher) => new(
            actors,
            actorRuntime,
            Doors,
            calendar,
            new CaptiveEscapeOutcomeRuntime(
                publisher,
                authority.Facts,
                Events));

        public ConfigurablePublisher NewPublisher(
            DomainFactDeliveryPhase phase,
            string detail) => new(authority.Facts, phase, detail);

        public bool Recover(out string failureReason) =>
            new CaptiveEscapeOutcomeRuntime(
                authority.Facts,
                authority.Facts,
                Events).Recover(State, out failureReason);

        public void Dispose()
        {
            subscription?.Dispose();
            authority.Dispose();
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
            GameObject actorObject = new("Captive Escape " + suffix);
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

    private sealed class FakeCalendar : IGameCalendar
    {
        public int Day => 10;
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

    private sealed class AlwaysChanceRandom : IRandomStream
    {
        public ulong State => 1UL;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
        public bool Chance(float probability) => true;
        public void Restore(ulong state)
        {
        }
    }

    private sealed class FakePopulationStandingPort :
        ICaptivityPopulationStandingPort
    {
        public CharacterSettlementStanding Standing { get; set; }
        public int RollbackCount { get; private set; }
        public int CompleteCount { get; private set; }

        public ICaptivityPopulationStandingTransition Begin(
            CharacterActor actor,
            CharacterSettlementStanding standing)
        {
            CharacterSettlementStanding previous = Standing;
            Standing = standing;
            return new Transition(this, previous);
        }

        private sealed class Transition : ICaptivityPopulationStandingTransition
        {
            private readonly FakePopulationStandingPort owner;
            private readonly CharacterSettlementStanding previous;
            private bool active = true;

            public Transition(
                FakePopulationStandingPort owner,
                CharacterSettlementStanding previous)
            {
                this.owner = owner;
                this.previous = previous;
            }

            public void Rollback()
            {
                if (!active)
                    return;
                owner.Standing = previous;
                owner.RollbackCount++;
                active = false;
            }

            public void Complete()
            {
                if (!active)
                    throw new InvalidOperationException("qa-transition-inactive");
                owner.CompleteCount++;
                active = false;
            }
        }
    }

    private sealed class FakeEmploymentStanding : IEmploymentStandingCommand
    {
        private readonly Dictionary<string, CharacterSettlementStanding> captured =
            new(StringComparer.Ordinal);

        public CharacterSettlementStanding Standing { get; set; }

        public EmploymentStandingState CaptureStandingState(string characterId)
        {
            captured[characterId] = Standing;
            return new EmploymentStandingState(characterId, null, null);
        }

        public void ApplyStanding(
            string characterId,
            CharacterSettlementStanding standing) => Standing = standing;

        public void RestoreStandingState(EmploymentStandingState snapshot)
        {
            if (captured.TryGetValue(snapshot.CharacterId, out var standing))
                Standing = standing;
        }
    }

    private sealed class EscapeFactAuthority : IDisposable
    {
        private readonly IDisposable observation;

        public EscapeFactAuthority(string runId)
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptiveEscapeOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptiveEscapeOutcomeAdapter()
                });
            Ledger = NewLedger(Registry, runId);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                Registry,
                new GameEventBus());
            Facts = new DomainFactDispatcher();
            observation = Facts.Subscribe(
                "captive-escape:fixture",
                new GameplayDomainFactObserver<
                    CaptiveEscapedFact,
                    CaptiveEscapeOutcomeReceipt>(
                    CaptiveEscapeFactMapping.Capture,
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

    private sealed class ConfigurablePublisher :
        IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly DomainFactDeliveryPhase phase;
        private readonly string detail;
        private readonly bool prepareSucceeds;

        public ConfigurablePublisher(
            IDomainFactStagingPublisher inner,
            DomainFactDeliveryPhase phase,
            string detail,
            bool prepareSucceeds = true)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.phase = phase;
            this.detail = detail ?? string.Empty;
            this.prepareSucceeds = prepareSucceeds;
        }

        public CaptiveEscapedFact LastFact { get; private set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            if (fact is CaptiveEscapedFact escaped)
                LastFact = escaped;
            if (!prepareSucceeds)
            {
                stage = null;
                failureReason = "qa-prepare-rejected";
                return false;
            }
            if (!inner.TryStage(fact, out stage, out failureReason))
                return false;
            stage = new ConfigurableStage(stage, phase, detail);
            return true;
        }
    }

    private sealed class ConfigurableStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;
        private readonly DomainFactDeliveryPhase phase;
        private readonly string detail;

        public ConfigurableStage(
            IDomainFactStage inner,
            DomainFactDeliveryPhase phase,
            string detail)
        {
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
            if (phase == DomainFactDeliveryPhase.Rejected)
            {
                inner.Cancel();
                return DomainFactDelivery.Reject(detail);
            }
            DomainFactDelivery delivery = inner.Commit();
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
