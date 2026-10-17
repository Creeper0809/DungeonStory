#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using DungeonStory.ServiceRooms;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class ServiceSessionOutcomeDebugScenarios
{
    private static JObject lastPartialProvisionEvidence;
    private static JArray lastRawSnapshots;
    private static bool collectingRawEvidence;

    public static JObject LastPartialProvisionEvidence =>
        lastPartialProvisionEvidence?.DeepClone() as JObject;

    public static JObject LastRawEvidence => lastRawSnapshots == null ? null : new JObject
    {
        ["schema"] = "e05-service-terminal-raw@1",
        ["scope"] = "Production service owner and gameplay ledger; original controlled-money cases plus actual GameMoneyAccount/EconomyTransactionLedgerRuntime cases. Actor/grid, clock, links, power, research and outcome faults remain controlled. Canonical finance/session DTO restores create fresh owners in the same process and preserve the gameplay ledger; not Brain, complete world or cold-process restore.",
        ["financeGameplayChildExecuted"] = false,
        ["productionIncomeLedgerExecuted"] = lastRawSnapshots.Any(value =>
            (string)value["moneyAuthority"] == "production" && value["successfulCredits"].HasValues),
        ["brainGameplayExecuted"] = false,
        ["wholeWorldRestoreExecuted"] = false,
        ["snapshots"] = lastRawSnapshots.DeepClone(),
        ["trainingEligible"] = false
    };

    [MenuItem("DungeonStory/QA/Service Rooms/Run Terminal Outcome Scenarios")]
    public static void RunFromMenu()
    {
        RunAll(log: true);
    }

    public static bool RunAll(bool log)
    {
        lastPartialProvisionEvidence = null;
        lastRawSnapshots = new JArray();
        collectingRawEvidence = true;
        try
        {
            List<string> contractFailures = ServiceRoomDebugScenarios.Validate();
            Require(contractFailures.Count == 0,
                "Existing service-room contract regression failed: "
                + string.Join(" | ", contractFailures));
            VerifyMode(ServiceOperationMode.Direct);
            VerifyMode(ServiceOperationMode.Managed);
            VerifyMode(ServiceOperationMode.Automated);
            VerifyInternalUnpaid();
            VerifyEveryStageSaveRestoreCancellation();
            lastPartialProvisionEvidence = VerifyPartialProvisionCancellation();
            VerifyReservationRejectionIsAtomic();
            VerifyCreditRejectionKeepsPreparedPaymentOwner();
            VerifyReceiptLookupRejectionKeepsPreparedPaymentOwner();
            VerifyPaymentCommittedParentFailureRestore();
            VerifyCancelledParentFailureRestore();
            VerifyColdClockRestore();
            VerifyClockCandidateRollback();
            VerifyMedicalOwnerSectionRestore();
            VerifyProductionIncomeRestore();
            VerifyProductionCreditRejection();
            if (log)
            {
                Debug.Log(
                    "SERVICE_SESSION_TERMINAL_OUTCOME=PASS; modes=3; "
                    + "internal-unpaid=1; stage-restores=5; partial-provision=1; "
                    + "reserve-rollback=1; credit-reject-restore=1; "
                    + "receipt-query-reject-restore=1; paid-parent-restore=1; "
                    + "cancel-parent-restore=1; cold-clock-terminal=2; clock-candidate-rollback=1; medical-owner-section=2; production-income-restore=1; production-credit-overflow=1");
            }
            return true;
        }
        finally
        {
            collectingRawEvidence = false;
        }
    }

    public static string RunObserverFocused()
    {
        VerifyMode(ServiceOperationMode.Direct);
        VerifyReservationRejectionIsAtomic();
        VerifyPaymentCommittedParentFailureRestore();
        VerifyCancelledParentFailureRestore();
        VerifyUnknownTerminalReply(false);
        VerifyUnknownTerminalReply(true);
        return "service-terminal-observer/normal/reserve-reject/paid-and-cancel-recovery/unknown-before-after/json-new-owner/exact-payment/canonical-tamper=PASS";
    }

    private static void VerifyUnknownTerminalReply(bool afterCommit)
    {
        using Fixture fixture = new("run:service-terminal-unknown-" + afterCommit);
        fixture.SetMode(ServiceOperationMode.Direct);
        var session = fixture.Begin(false, true);
        fixture.Outcomes.UnknownReply = afterCommit;
        fixture.Clock.Advance(session.Contract.serviceSeconds);
        int before = fixture.Money.Balance;
        Require(fixture.Runtime.TryCompleteSession(session.SessionId, out var pending, out _)
            && pending.TerminalPhase == ServiceSessionTerminalPhase.PaymentCommitted
            && fixture.Money.Balance == before + session.Contract.price
            && fixture.Money.SuccessfulCredits.Count == 1
            && fixture.CountTerminalOutcomes() == (afterCommit ? 1 : 0),
            "Unknown terminal response did not preserve the exact one-time payment.");
        var saved = fixture.RoundTrip(fixture.Runtime.Capture());
        var originalLedger = fixture.Ledger.CaptureGameplayOutcomes();
        Validate(saved, originalLedger, true);
        string key = pending.TerminalPublicationKey;
        string name = pending.TerminalHubName;
        var badKey = fixture.RoundTrip(saved);
        badKey.sessions[0].terminalPublicationKey += ":tampered";
        Validate(badKey, originalLedger, false);
        var raw = JObject.Parse(JsonUtility.ToJson(saved));
        ((JObject)raw["sessions"][0]).Remove("terminalHubName");
        bool rejected = false;
        try { ServiceRoomsSaveSection.ValidateTerminalFactJson(raw.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Raw terminal name omission was accepted.");
        if (afterCommit)
        {
            var changedName = fixture.RoundTrip(saved);
            changedName.sessions[0].terminalHubName += " 변경";
            Validate(changedName, originalLedger, false);
            var erasedBinding = fixture.RoundTrip(saved);
            var row = erasedBinding.sessions[0];
            row.terminalPublicationKey = row.terminalHubName = row.economyTransactionId = row.economyTransactionSourceId = string.Empty;
            row.paymentCommitted = false; row.paymentAmount = row.balanceBefore = row.balanceAfter = 0;
            row.terminalPhase = ServiceSessionTerminalPhase.Prepared;
            Validate(erasedBinding, originalLedger, false);
        }
        fixture.ReplaceRuntime(saved, viaSection: true);
        fixture.Outcomes.UnknownReply = null;
        fixture.Runtime.Tick();
        var restored = fixture.RequireSession(session.SessionId);
        Require(restored.TerminalPhase == ServiceSessionTerminalPhase.Acknowledged
            && restored.TerminalPublicationKey == key && restored.TerminalHubName == name
            && fixture.Money.Balance == before + session.Contract.price && fixture.Money.SuccessfulCredits.Count == 1,
            "Recovered terminal changed the frozen source or repeated payment.");
        VerifySingleTerminal(fixture, restored, GameplayOutcomeStatus.Succeeded);
        var finalLedger = fixture.Ledger.CaptureGameplayOutcomes();
        Validate(saved, finalLedger, true);
        var badPayload = fixture.RoundTrip(saved);
        badPayload.sessions[0].contract.satisfaction += 1;
        Validate(badPayload, finalLedger, false);
        fixture.Runtime.Tick();
        Require(fixture.CountTerminalOutcomes() == 1 && fixture.Money.SuccessfulCredits.Count == 1,
            "Acknowledged terminal emitted a duplicate.");

        static void Validate(ServiceRoomsSaveData save, GameplayOutcomeLedgerSaveData ledger, bool expected)
        {
            var report = new DungeonGameRestoreReport();
            ServiceSessionTerminalFactPreflight.Validate(save, ledger, report);
            Require(report.Success == expected, "Service terminal canonical join unexpected: " + string.Join(" | ", report.Errors));
        }
    }

    private static JObject VerifyPartialProvisionCancellation()
    {
        using Fixture fixture = new(
            "run:service-partial-provision",
            medical: true);
        int balanceBefore = fixture.Money.Balance;
        ServiceSessionSnapshot session = fixture.Begin(
            internalActor: false,
            advertised: true);
        MoveToService(fixture.Runtime, session);
        float partialSeconds = session.Contract.serviceSeconds * 0.5f;
        fixture.Clock.Advance(partialSeconds);
        fixture.Outcomes.RejectCommits = true;
        Require(fixture.Runtime.CancelSession(
                session.SessionId,
                "qa-partial-provision"),
            "Partial service cancellation failed.");

        ServiceSessionSnapshot cancelled = fixture.RequireSession(
            session.SessionId);
        Require(cancelled.Stage == ServiceSessionStage.Cancelled
                && cancelled.ProvidedServiceSeconds > 0f
                && cancelled.ProvidedServiceSeconds
                    < cancelled.Contract.serviceSeconds
                && Math.Abs(
                    cancelled.ProvidedServiceSeconds - partialSeconds) <= 0.001f
                && !cancelled.PaymentCommitted
                && cancelled.PaymentAmount == 0
                && fixture.Money.Balance == balanceBefore
                && fixture.Money.SuccessfulCredits.Count == 0
                && cancelled.TerminalPhase
                    == ServiceSessionTerminalPhase.Prepared
                && fixture.CountTerminalOutcomes() == 0,
            "Partial provision changed payment or lost the actual service duration.");
        ServiceRoomsSaveData save = fixture.RoundTrip(
            fixture.Runtime.Capture());
        Require(save.sessions.Count == 1
                && Math.Abs(save.sessions[0].providedServiceSeconds
                    - partialSeconds) <= 0.001f,
            "Partial provision was not frozen in the terminal save owner.");
        RecordEvidence(fixture, "partial-pending-save", cancelled, save);
        fixture.Outcomes.RejectCommits = false;
        fixture.ReplaceRuntime(save, viaSection: true);
        fixture.Runtime.Tick();
        cancelled = fixture.RequireSession(session.SessionId);
        VerifySingleTerminal(
            fixture,
            cancelled,
            GameplayOutcomeStatus.Cancelled);
        GameplayOutcomeSnapshot outcome = fixture.TerminalOutcomes().Single();
        Require(Math.Abs(FindMetric(
                    outcome,
                    ServiceSessionOutcomeIds.ContractedServiceSecondsMetric)
                - cancelled.Contract.serviceSeconds) <= 0.001d
                && Math.Abs(FindMetric(
                    outcome,
                    ServiceSessionOutcomeIds.ProvidedServiceSecondsMetric)
                - cancelled.ProvidedServiceSeconds) <= 0.001d
                && FindMetric(
                    outcome,
                    ServiceSessionOutcomeIds.PaymentAmountMetric) == 0d,
            "Partial service parent lost contracted/provided duration or zero income.");
        fixture.Runtime.Tick();
        Require(fixture.CountTerminalOutcomes() == 1
                && fixture.Money.SuccessfulCredits.Count == 0,
            "Partial provision restore replay duplicated income or its terminal parent.");
        RecordEvidence(fixture, "partial-replay", cancelled);
        return new JObject
        {
            ["schema"] = "e05-service-provision-example@1",
            ["sessionId"] = cancelled.SessionId,
            ["facilityId"] = cancelled.HubId,
            ["actorId"] = cancelled.ActorId,
            ["processId"] = cancelled.ProcessId,
            ["terminalStage"] = cancelled.Stage.ToString(),
            ["previousStage"] = cancelled.PreviousStage.ToString(),
            ["contractedServiceSeconds"] = cancelled.Contract.serviceSeconds,
            ["providedServiceSeconds"] = cancelled.ProvidedServiceSeconds,
            ["paymentAmount"] = cancelled.PaymentAmount,
            ["incomeDelta"] = fixture.Money.Balance - balanceBefore,
            ["terminalPublicationKey"] = cancelled.TerminalPublicationKey,
            ["terminalPublicationId"] = cancelled.TerminalPublicationId,
            ["terminalPublicationFingerprint"] = cancelled.TerminalPublicationFingerprint,
            ["terminalParentCount"] = fixture.CountTerminalOutcomes(),
            ["restoreReplayUnchanged"] = true
        };
    }

    private static void VerifyMode(ServiceOperationMode mode)
    {
        using Fixture fixture = new("run:service-session-mode-" + mode);
        fixture.SetMode(mode);
        int before = fixture.Money.Balance;
        ServiceSessionSnapshot session = fixture.Begin(
            internalActor: false,
            advertised: true);
        MoveToService(fixture.Runtime, session);
        fixture.Clock.Advance(5f);
        Require(fixture.Runtime.TryCompleteSession(
                session.SessionId,
                out ServiceSessionSnapshot completed,
                out DomainFailure failure),
            mode + " completion failed: " + failure.Code);
        Require(completed != null
                && completed.Stage == ServiceSessionStage.Completed
                && completed.TerminalPhase
                    == ServiceSessionTerminalPhase.Acknowledged
                && completed.Contract.mode == mode
                && completed.PaymentCommitted
                && fixture.Money.Balance
                    == before + completed.Contract.price,
            mode + " completion did not atomically close payment/outcome: "
            + Describe(completed, fixture.Money.Balance, before));
        VerifySingleTerminal(fixture, completed, GameplayOutcomeStatus.Succeeded);
        ServiceRoomsSaveData acknowledgedSave = fixture.Runtime.Capture();
        Require(acknowledgedSave.sessions.Count == 0,
            mode + " acknowledged terminal leaked into save owner.");
        RecordEvidence(fixture, "acknowledged-save", completed, acknowledgedSave);
    }

    private static void VerifyColdClockRestore()
    {
        foreach (bool cancel in new[] { true, false })
        {
            using Fixture fixture = new("run:service-cold-clock-" + cancel);
            fixture.Clock.Advance(100f);
            ServiceSessionSnapshot session = fixture.Begin(false, true);
            MoveToService(fixture.Runtime, session);
            fixture.Clock.Advance(7f);
            ServiceRoomsSaveData captured = fixture.RoundTrip(fixture.Runtime.Capture());
            string before = JsonUtility.ToJson(captured);
            fixture.Clock.Restart(2f);
            fixture.ReplaceRuntime(captured, viaSection: true);
            Require(JsonUtility.ToJson(fixture.Runtime.Capture()) == before,
                "Cold-clock section restore changed the captured timeline or original session.");
            fixture.Clock.Advance(3f);
            if (cancel)
                Require(fixture.Runtime.CancelSession(session.SessionId, "cold-clock-cancel"), "Cold cancellation failed.");
            else
                Require(fixture.Runtime.TryCompleteSession(session.SessionId, out _, out _), "Cold completion failed.");
            ServiceSessionSnapshot terminal = fixture.RequireSession(session.SessionId);
            Require(terminal.StartedAt == session.StartedAt && terminal.StageStartedAt == captured.timelineTime + 3f,
                "Cold terminal reset/reversed its original service timeline.");
            VerifySingleTerminal(fixture, terminal, cancel ? GameplayOutcomeStatus.Cancelled : GameplayOutcomeStatus.Succeeded);
            fixture.Runtime.Tick();
            Require(!fixture.Runtime.CancelSession(session.SessionId, "repeated-cancel")
                && !fixture.Runtime.TryCompleteSession(session.SessionId, out _, out _)
                && fixture.CountTerminalOutcomes() == 1
                && fixture.Money.SuccessfulCredits.Count == (cancel ? 0 : 1),
                "Cold terminal retry duplicated a payment or ledger outcome.");
            RecordEvidence(fixture, "cold-clock-replay", terminal);
        }
    }

    private static void VerifyClockCandidateRollback()
    {
        using Fixture fixture = new("run:service-clock-rollback");
        fixture.Clock.Advance(50f);
        fixture.Begin(false, true);
        ServiceRoomsSaveData before = fixture.Runtime.Capture();
        string original = JsonUtility.ToJson(before);
        ServiceSessionAggregate root = fixture.AggregateStore.GetOrCreate(() => new ServiceSessionAggregate());
        ServiceRoomsSaveData later = fixture.RoundTrip(before);
        later.timelineTime += 100f;
        ServiceRoomsRestoreCandidate candidate = fixture.Runtime.PrepareRestoreCandidate(later);
        Require(JsonUtility.ToJson(fixture.Runtime.Capture()) == original,
            "Detached clock preparation changed the live root.");
        void Stage(string method) => typeof(DungeonRuntimeAggregateRootStore)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture.AggregateStore, null);
        Stage("BeginRestoreStaging");
        try
        {
            fixture.Runtime.PublishRestoreCandidate(candidate);
            Require(fixture.Runtime.Capture().timelineTime == later.timelineTime,
                "Staged candidate did not own its clock mapping.");
        }
        finally { Stage("DiscardRestoreStaging"); }
        Require(ReferenceEquals(root, fixture.AggregateStore.GetOrCreate(() => new ServiceSessionAggregate()))
            && JsonUtility.ToJson(fixture.Runtime.Capture()) == original,
            "Discarded restore changed live service state or clock anchor.");
        fixture.Clock.Advance(2f);
        Require(fixture.Runtime.Capture().timelineTime == before.timelineTime + 2f,
            "Live service clock retained a discarded candidate's offset.");
    }

    private static void VerifyMedicalOwnerSectionRestore()
    {
        // Real service runtime/section with medical callers; not physical
        // medicine consumption or a complete survival-section restore.
        foreach (bool detox in new[] { false, true })
        {
            using Fixture fixture = new("run:service-medical-section-" + detox, medical: true);
            ServiceSessionSnapshot started = fixture.Begin(false, true);
            fixture.Clock.Advance(3f);
            ServiceRoomsSaveData saved = fixture.RoundTrip(fixture.Runtime.Capture());
            var survival = new DungeonSurvivalSaveData();
            var consumables = new DungeonCharacterConsumablesSaveData
            {
                timelineTime = saved.timelineTime
            };
            if (detox)
                consumables.activeDetoxTreatmentPlans.Add(new CharacterDetoxTreatmentPlanSaveData
                {
                    operationId = SurvivalFoodRuntime.DetoxOperationPrefix + started.SessionId,
                    characterId = started.ActorId, facilityInstanceId = started.HubId,
                    phase = CharacterDetoxTreatmentPlanPhase.EffectsPublished
                });
            else
                survival.activeTreatmentPlans.Add(new SurvivalTreatmentPlanSaveData
                {
                    operationId = SurvivalFoodRuntime.TreatmentOperationPrefix + started.SessionId,
                    serviceSessionId = started.SessionId, patientId = started.ActorId,
                    facilityInstanceId = started.HubId, phase = SurvivalTreatmentPlanPhase.EffectsPublished
                });
            var ownerReport = new DungeonGameRestoreReport();
            ServiceExecutionOwnerSaveValidation.ValidateCore(saved,
                fixture.CaptureFacilityWorld(), survival, consumables, ownerReport);
            Require(ownerReport.Success, "Medical detached service join failed: " + string.Join(" | ", ownerReport.Errors));
            string before = JsonUtility.ToJson(saved);
            fixture.Clock.Restart(1f);
            fixture.ReplaceRuntime(saved, viaSection: true);
            Require(JsonUtility.ToJson(fixture.Runtime.Capture()) == before,
                "Medical service section restore changed its session or clock.");
            fixture.Clock.Advance(2f);
            Require(fixture.Runtime.TryCompleteSession(started.SessionId, out ServiceSessionSnapshot completed, out _),
                "Restored medical service could not complete.");
            VerifySingleTerminal(fixture, completed, GameplayOutcomeStatus.Succeeded);
            Require(!fixture.Runtime.TryCompleteSession(started.SessionId, out _, out _)
                && fixture.CountTerminalOutcomes() == 1 && fixture.Money.SuccessfulCredits.Count == 1,
                "Medical session repeated its payment or terminal outcome.");
            RecordEvidence(fixture, "medical-replay", completed);
        }
    }

    private static string Describe(
        ServiceSessionSnapshot session,
        int balance,
        int before) => session == null
        ? $"session=null; balance={balance}; before={before}"
        : $"stage={session.Stage}; phase={session.TerminalPhase}; "
          + $"mode={session.Contract.mode}; paid={session.PaymentCommitted}; "
          + $"price={session.Contract.price}; balance={balance}; before={before}";

    private static void VerifyInternalUnpaid()
    {
        using Fixture fixture = new("run:service-session-internal-unpaid");
        fixture.SetMode(ServiceOperationMode.Direct);
        int before = fixture.Money.Balance;
        ServiceSessionSnapshot session = fixture.Begin(
            internalActor: true,
            advertised: false);
        fixture.Clock.Advance(2f);
        Require(fixture.Runtime.TryCompleteSession(
                session.SessionId,
                out ServiceSessionSnapshot completed,
                out _),
            "Internal unpaid completion failed.");
        Require(!completed.Contract.paymentRequired
                && !completed.PaymentCommitted
                && completed.PaymentAmount == 0
                && fixture.Money.Balance == before
                && fixture.Money.SuccessfulCredits.Count == 0,
            "Internal free service published a payment.");
        VerifySingleTerminal(fixture, completed, GameplayOutcomeStatus.Succeeded);
    }

    private static void VerifyEveryStageSaveRestoreCancellation()
    {
        ServiceSessionStage[] stages =
        {
            ServiceSessionStage.Reception,
            ServiceSessionStage.Waiting,
            ServiceSessionStage.Service,
            ServiceSessionStage.Payment,
            ServiceSessionStage.Cleanup
        };
        foreach (ServiceSessionStage stage in stages)
        {
            using Fixture fixture = new(
                "run:service-session-cancel-" + stage);
            fixture.SetMode(ServiceOperationMode.Managed);
            ServiceSessionSnapshot session = fixture.Begin(
                internalActor: false,
                advertised: true);
            if (session.Stage != stage)
            {
                Require(fixture.Runtime.TrySetStage(
                        session.SessionId,
                        stage,
                        out DomainFailure stageFailure),
                    stage + " setup failed: " + stageFailure.Code);
            }
            ServiceRoomsSaveData activeSave = fixture.RoundTrip(
                fixture.Runtime.Capture());
            fixture.ReplaceRuntime(activeSave);
            fixture.Clock.Advance(1f);
            Require(fixture.Runtime.CancelSession(
                    session.SessionId,
                    "qa-cancel-" + stage),
                stage + " cancellation after restore failed.");
            ServiceSessionSnapshot cancelled = fixture.RequireSession(
                session.SessionId);
            Require(cancelled.Stage == ServiceSessionStage.Cancelled
                    && cancelled.PreviousStage == stage
                    && cancelled.TerminalPhase
                        == ServiceSessionTerminalPhase.Acknowledged
                    && cancelled.CancellationReason == "qa-cancel-" + stage,
                stage + " cancellation lost its frozen terminal facts.");
            VerifySingleTerminal(
                fixture,
                cancelled,
                GameplayOutcomeStatus.Cancelled);
        }
    }

    private static void VerifyReservationRejectionIsAtomic()
    {
        using Fixture fixture = new("run:service-session-reserve-reject");
        fixture.SetMode(ServiceOperationMode.Direct);
        ServiceSessionSnapshot session = fixture.Begin(false, false);
        int version = fixture.Runtime.Version;
        int balance = fixture.Money.Balance;
        fixture.Outcomes.RejectReservations = true;
        Require(!fixture.Runtime.TryCompleteSession(
                session.SessionId,
                out ServiceSessionSnapshot completed,
                out _)
                && completed == null,
            "Reservation rejection reported service completion.");
        ServiceSessionSnapshot unchanged = fixture.RequireSession(
            session.SessionId);
        Require(unchanged.IsActive
                && unchanged.Stage == session.Stage
                && unchanged.TerminalPhase == ServiceSessionTerminalPhase.None
                && fixture.Runtime.Version == version
                && fixture.Money.Balance == balance
                && fixture.CountTerminalOutcomes() == 0,
            "Reservation rejection mutated session, money, or ledger.");
        RecordEvidence(fixture, "reservation-rejected", unchanged);
    }

    private static void VerifyCreditRejectionKeepsPreparedPaymentOwner()
    {
        using Fixture fixture = new("run:service-session-credit-reject");
        fixture.SetMode(ServiceOperationMode.Direct);
        int before = fixture.Money.Balance;
        ServiceSessionSnapshot session = fixture.Begin(false, true);
        MoveToService(fixture.Runtime, session);
        fixture.Clock.Advance(4f);
        fixture.Money.RejectCreditOnce = true;
        Require(fixture.Runtime.TryCompleteSession(
                session.SessionId,
                out ServiceSessionSnapshot prepared,
                out _)
                && prepared != null,
            "Credit rejection did not preserve the completed terminal owner.");
        VerifyPreparedPaymentOwner(
            fixture,
            prepared,
            before,
            0,
            "Credit rejection");
        RecordEvidence(fixture, "credit-rejected", prepared);
        VerifyPreparedPaymentOwnerRetries(
            fixture,
            prepared,
            before,
            0,
            "Credit rejection");

        fixture.Money.RejectCreditOnce = false;
        VerifyRecoveredPaymentOwner(fixture, prepared, before, "Credit rejection");
    }

    private static void VerifyReceiptLookupRejectionKeepsPreparedPaymentOwner()
    {
        using Fixture fixture = new("run:service-session-receipt-query-reject");
        fixture.SetMode(ServiceOperationMode.Direct);
        int before = fixture.Money.Balance;
        ServiceSessionSnapshot session = fixture.Begin(false, true);
        MoveToService(fixture.Runtime, session);
        fixture.Clock.Advance(4f);
        fixture.Money.RejectSuccessfulTransactionLookup = true;
        Require(fixture.Runtime.TryCompleteSession(
                session.SessionId,
                out ServiceSessionSnapshot prepared,
                out _)
                && prepared != null,
            "Receipt lookup rejection did not preserve the completed terminal owner.");
        VerifyPreparedPaymentOwner(
            fixture,
            prepared,
            before + prepared.Contract.price,
            1,
            "Receipt lookup rejection");
        RecordEvidence(fixture, "receipt-lookup-rejected", prepared);
        VerifyPreparedPaymentOwnerRetries(
            fixture,
            prepared,
            before + prepared.Contract.price,
            1,
            "Receipt lookup rejection");

        fixture.Money.RejectSuccessfulTransactionLookup = false;
        VerifyRecoveredPaymentOwner(fixture, prepared, before, "Receipt lookup rejection");
    }

    private static void VerifyPreparedPaymentOwnerRetries(
        Fixture fixture,
        ServiceSessionSnapshot expectedPrepared,
        int expectedBalance,
        int expectedCredits,
        string scenario)
    {
        for (int retry = 1; retry <= 2; retry++)
        {
            fixture.Runtime.Tick();
            VerifyPreparedPaymentOwner(
                fixture,
                expectedPrepared,
                expectedBalance,
                expectedCredits,
                scenario + " tick " + retry);

            ServiceRoomsSaveData captured = fixture.Runtime.Capture();
            VerifyPreparedPaymentOwner(
                fixture,
                expectedPrepared,
                expectedBalance,
                expectedCredits,
                scenario + " capture " + retry);
            VerifyPreparedPaymentSave(
                captured,
                expectedPrepared,
                scenario + " capture " + retry);
            RecordEvidence(fixture, "prepared-save-" + retry,
                fixture.RequireSession(expectedPrepared.SessionId), captured);

            ServiceRoomsSaveData roundTripped = fixture.RoundTrip(captured);
            VerifyPreparedPaymentSave(
                roundTripped,
                expectedPrepared,
                scenario + " JSON roundtrip " + retry);
            fixture.ReplaceRuntime(roundTripped);
            VerifyPreparedPaymentOwner(
                fixture,
                expectedPrepared,
                expectedBalance,
                expectedCredits,
                scenario + " restore " + retry);
        }
    }

    private static void VerifyPreparedPaymentOwner(
        Fixture fixture,
        ServiceSessionSnapshot expectedPrepared,
        int expectedBalance,
        int expectedCredits,
        string scenario)
    {
        ServiceSessionSnapshot actual = fixture.RequireSession(
            expectedPrepared.SessionId);
        RequirePreparedPaymentIdentity(actual, expectedPrepared, scenario);
        Require(fixture.Money.Balance == expectedBalance
                && fixture.Money.SuccessfulCredits.Count == expectedCredits
                && fixture.CountTerminalOutcomes() == 0,
            scenario + " changed balance, credit count, or terminal ledger while payment remained prepared.");
        RecordEvidence(fixture, "prepared-check:" + scenario, actual);
    }

    private static void VerifyPreparedPaymentSave(
        ServiceRoomsSaveData save,
        ServiceSessionSnapshot expectedPrepared,
        string scenario)
    {
        Require(save != null
                && save.sessions != null
                && save.sessions.Count == 1
                && save.sessions[0] != null,
            scenario + " did not retain exactly one prepared payment owner.");
        RequirePreparedPaymentIdentity(
            save.sessions[0].ToSnapshot(),
            expectedPrepared,
            scenario);
    }

    private static void RequirePreparedPaymentIdentity(
        ServiceSessionSnapshot actual,
        ServiceSessionSnapshot expected,
        string scenario)
    {
        Require(actual != null
                && actual.Stage == ServiceSessionStage.Completed
                && actual.TerminalPhase == ServiceSessionTerminalPhase.Prepared
                && !actual.PaymentCommitted
                && actual.SessionId == expected.SessionId
                && actual.HubId == expected.HubId
                && actual.ActorId == expected.ActorId
                && actual.ProcessId == expected.ProcessId
                && actual.PreviousStage == expected.PreviousStage
                && actual.TerminalAbsoluteDay == expected.TerminalAbsoluteDay
                && actual.TerminalOwnerRevision == expected.TerminalOwnerRevision
                && actual.TerminalOwnerRevision
                    == ServiceSessionTerminalIdentity.OwnerRevision
                && actual.TerminalPublicationKey == expected.TerminalPublicationKey
                && actual.TerminalPublicationKey
                    == ServiceSessionOutcomeKey.ResultKeyText(actual.SessionId)
                && actual.Contract != null
                && actual.Contract.price == expected.Contract.price
                && string.IsNullOrEmpty(actual.EconomyTransactionId)
                && string.IsNullOrEmpty(actual.EconomyTransactionSourceId)
                && actual.PaymentAmount == 0
                && actual.BalanceBefore == 0
                && actual.BalanceAfter == 0
                && string.IsNullOrEmpty(actual.TerminalPublicationId)
                && string.IsNullOrEmpty(actual.TerminalPublicationFingerprint),
            scenario + " did not preserve the prepared terminal identity and empty payment receipt.");
    }

    private static void VerifyRecoveredPaymentOwner(
        Fixture fixture,
        ServiceSessionSnapshot expectedPrepared,
        int balanceBefore,
        string scenario)
    {
        int expectedAmount = expectedPrepared.Contract.price;
        int expectedBalance = balanceBefore + expectedAmount;
        string sourceId = ServiceSessionTerminalIdentity.PaymentSourceId(
            expectedPrepared.SessionId);
        fixture.Runtime.Tick();
        ServiceSessionSnapshot acknowledged = fixture.RequireSession(
            expectedPrepared.SessionId);
        Require(acknowledged.Stage == ServiceSessionStage.Completed
                && acknowledged.TerminalPhase
                    == ServiceSessionTerminalPhase.Acknowledged
                && acknowledged.PaymentCommitted
                && acknowledged.PaymentAmount == expectedAmount
                && acknowledged.BalanceBefore == balanceBefore
                && acknowledged.BalanceAfter == expectedBalance
                && acknowledged.EconomyTransactionSourceId == sourceId
                && fixture.Money.Balance == expectedBalance
                && fixture.Money.SuccessfulCredits.Count == 1,
            scenario + " did not acknowledge one exact recovered payment receipt.");
        VerifyExactPaymentReceipt(
            fixture,
            acknowledged,
            sourceId,
            expectedAmount,
            balanceBefore,
            expectedBalance,
            scenario);
        VerifySingleTerminal(
            fixture,
            acknowledged,
            GameplayOutcomeStatus.Succeeded);

        fixture.Runtime.Tick();
        ServiceRoomsSaveData acknowledgedSave = fixture.RoundTrip(
            fixture.Runtime.Capture());
        Require(acknowledgedSave.sessions.Count == 0,
            scenario + " acknowledged terminal remained in the save owner.");
        fixture.ReplaceRuntime(acknowledgedSave);
        fixture.Runtime.Tick();
        Require(fixture.Money.Balance == expectedBalance
                && fixture.Money.SuccessfulCredits.Count == 1
                && fixture.CountTerminalOutcomes() == 1,
            scenario + " post-acknowledgement replay duplicated payment or terminal parent.");
        VerifyExactPaymentReceipt(
            fixture,
            acknowledged,
            sourceId,
            expectedAmount,
            balanceBefore,
            expectedBalance,
            scenario + " post-restore");
        VerifySingleTerminal(
            fixture,
            acknowledged,
            GameplayOutcomeStatus.Succeeded);
    }

    private static void VerifyExactPaymentReceipt(
        Fixture fixture,
        ServiceSessionSnapshot session,
        string expectedSourceId,
        int expectedAmount,
        int expectedBalanceBefore,
        int expectedBalanceAfter,
        string scenario)
    {
        Require(fixture.MoneyTransactions.TryGetSuccessfulTransaction(
                    EconomyTransactionKind.GuestServiceIncome,
                    expectedSourceId,
                    out EconomyTransactionRecord receipt)
                && receipt != null
                && receipt.transactionId == session.EconomyTransactionId
                && receipt.kind == EconomyTransactionKind.GuestServiceIncome
                && receipt.sourceId == expectedSourceId
                && receipt.targetId == session.ActorId
                && receipt.amount == expectedAmount
                && receipt.balanceBefore == expectedBalanceBefore
                && receipt.balanceAfter == expectedBalanceAfter
                && receipt.succeeded,
            scenario + " payment transaction record did not match the frozen receipt.");
    }

    private static void VerifyPaymentCommittedParentFailureRestore()
    {
        using Fixture fixture = new("run:service-session-paid-restore");
        fixture.SetMode(ServiceOperationMode.Direct);
        ServiceSessionSnapshot session = fixture.Begin(false, true);
        int before = fixture.Money.Balance;
        fixture.Outcomes.RejectCommits = true;
        fixture.Clock.Advance(4f);
        Require(fixture.Runtime.TryCompleteSession(
                session.SessionId,
                out ServiceSessionSnapshot pending,
                out _),
            "Paid terminal did not preserve its applied completion.");
        Require(pending.TerminalPhase
                    == ServiceSessionTerminalPhase.PaymentCommitted
                && pending.PaymentCommitted
                && fixture.Money.Balance == before + pending.Contract.price
                && fixture.Money.SuccessfulCredits.Count == 1
                && fixture.CountTerminalOutcomes() == 0,
            "Parent commit rejection did not freeze PaymentCommitted exactly once.");
        ServiceRoomsSaveData save = fixture.RoundTrip(
            fixture.Runtime.Capture());
        Require(save.sessions.Count == 1
                && save.sessions[0].terminalPhase
                    == ServiceSessionTerminalPhase.PaymentCommitted,
            "PaymentCommitted owner was not captured while retry was rejected.");
        RecordEvidence(fixture, "payment-committed-pending-save", pending, save);
        fixture.Outcomes.RejectCommits = false;
        fixture.ReplaceRuntime(save, viaSection: true);
        fixture.Runtime.Tick();
        ServiceSessionSnapshot restored = fixture.RequireSession(
            session.SessionId);
        Require(restored.TerminalPhase
                    == ServiceSessionTerminalPhase.Acknowledged
                && fixture.Money.Balance == before + pending.Contract.price
                && fixture.Money.SuccessfulCredits.Count == 1,
            "Fresh-runtime parent retry duplicated or lost service income.");
        VerifySingleTerminal(
            fixture,
            restored,
            GameplayOutcomeStatus.Succeeded);
        fixture.Runtime.Tick();
        Require(fixture.CountTerminalOutcomes() == 1
                && fixture.Money.SuccessfulCredits.Count == 1,
            "Acknowledged paid replay duplicated money or outcome.");
        RecordEvidence(fixture, "payment-committed-replay", restored);
    }

    private static void VerifyCancelledParentFailureRestore()
    {
        using Fixture fixture = new("run:service-session-cancel-restore");
        fixture.SetMode(ServiceOperationMode.Direct);
        ServiceSessionSnapshot session = fixture.Begin(false, false);
        fixture.Outcomes.RejectCommits = true;
        Require(fixture.Runtime.CancelSession(
                session.SessionId,
                "qa-parent-retry"),
            "Cancellation did not preserve applied terminal state.");
        ServiceRoomsSaveData save = fixture.RoundTrip(
            fixture.Runtime.Capture());
        Require(save.sessions.Count == 1
                && save.sessions[0].terminalPhase
                    == ServiceSessionTerminalPhase.Prepared
                && fixture.CountTerminalOutcomes() == 0,
            "Cancelled terminal did not retain its parent outbox.");
        RecordEvidence(fixture, "cancel-pending-save", fixture.RequireSession(session.SessionId), save);
        fixture.Outcomes.RejectCommits = false;
        fixture.ReplaceRuntime(save, viaSection: true);
        fixture.Runtime.Tick();
        ServiceSessionSnapshot restored = fixture.RequireSession(
            session.SessionId);
        Require(restored.TerminalPhase
                    == ServiceSessionTerminalPhase.Acknowledged
                && restored.Stage == ServiceSessionStage.Cancelled,
            "Cancelled parent restore did not acknowledge exactly once.");
        VerifySingleTerminal(
            fixture,
            restored,
            GameplayOutcomeStatus.Cancelled);
    }

    private static void VerifyProductionIncomeRestore()
    {
        using Fixture fixture = new("run:service-session-production-income", medical: true, productionMoney: true);
        fixture.SetMode(ServiceOperationMode.Direct);
        int before = fixture.MoneyBalance;
        ServiceSessionSnapshot session = fixture.Begin(false, true);
        MoveToService(fixture.Runtime, session);
        fixture.Clock.Advance(session.Contract.serviceSeconds);
        fixture.Outcomes.RejectCommits = true;
        Require(fixture.Runtime.TryCompleteSession(session.SessionId, out ServiceSessionSnapshot pending, out _),
            "Production payment did not retain its completed owner.");
        Require(pending.TerminalPhase == ServiceSessionTerminalPhase.PaymentCommitted
                && pending.PaymentCommitted && fixture.MoneyBalance == before + pending.Contract.price
                && fixture.SuccessfulCredits.Count == 1 && fixture.CountTerminalOutcomes() == 0,
            "Production income did not precede the rejected parent exactly once.");
        ServiceRoomsSaveData serviceSave = fixture.RoundTrip(fixture.Runtime.Capture());
        ProductionFinanceSaveData financeSave = RoundTripFinance(fixture.CaptureProductionFinance());
        Require(serviceSave.sessions.Count == 1 && financeSave.ledger.records.Count == 1,
            "Production pending save lost the service or income owner.");
        Require(financeSave.ledger.records[0].transactionId == pending.EconomyTransactionId
                && financeSave.ledger.records[0].transactionId.StartsWith("economy:", StringComparison.Ordinal)
                && !financeSave.ledger.records[0].transactionId.StartsWith("economy:qa:", StringComparison.Ordinal)
                && financeSave.ledger.records[0].targetId == session.ActorId,
            "Production income was not the real ledger receipt for the exact customer.");
        RecordEvidence(fixture, "production-payment-pending-save", pending, serviceSave, financeSave);

        GameMoneyAccount oldMoney = fixture.ProductionMoney;
        fixture.RestoreProductionFinance(financeSave);
        fixture.ReplaceRuntime(serviceSave, viaSection: true);
        Require(!ReferenceEquals(oldMoney, fixture.ProductionMoney)
                && JsonUtility.ToJson(fixture.CaptureProductionFinance()) == JsonUtility.ToJson(financeSave)
                && fixture.MoneyBalance == before + pending.Contract.price && fixture.CountTerminalOutcomes() == 0,
            "Fresh production finance/session restore changed its payment or parent.");
        RecordEvidence(fixture, "production-restored-pending", fixture.RequireSession(session.SessionId), serviceSave, financeSave);
        fixture.Outcomes.RejectCommits = false;
        fixture.Runtime.Tick();
        ServiceSessionSnapshot completed = fixture.RequireSession(session.SessionId);
        VerifyExactPaymentReceipt(fixture, completed, ServiceSessionTerminalIdentity.PaymentSourceId(session.SessionId),
            completed.Contract.price, before, before + completed.Contract.price, "Production income restore");
        VerifySingleTerminal(fixture, completed, GameplayOutcomeStatus.Succeeded);
        VerifyProductionAcknowledgedReplay(fixture, completed);
    }

    private static void VerifyProductionCreditRejection()
    {
        using Fixture fixture = new("run:service-session-production-credit-overflow", medical: true,
            productionMoney: true, productionStartingMoney: int.MaxValue);
        fixture.SetMode(ServiceOperationMode.Direct);
        ServiceSessionSnapshot session = fixture.Begin(false, true);
        MoveToService(fixture.Runtime, session);
        fixture.Clock.Advance(session.Contract.serviceSeconds);
        Require(session.Contract.price > 0 && fixture.Runtime.TryCompleteSession(session.SessionId, out _, out _),
            "Production credit rejection lost its completed service owner.");
        ServiceSessionSnapshot prepared = fixture.RequireSession(session.SessionId);
        string preparedJson = JsonUtility.ToJson(ServiceRoomsSaveData.FromSnapshot(prepared));
        Require(prepared.TerminalPhase == ServiceSessionTerminalPhase.Prepared && !prepared.PaymentCommitted
                && fixture.MoneyBalance == int.MaxValue && fixture.SuccessfulCredits.Count == 0
                && fixture.CaptureProductionFinance().ledger.records.Count == 0 && fixture.CountTerminalOutcomes() == 0,
            "Overflow credit rejection changed real money, receipt or parent.");
        ServiceRoomsSaveData serviceSave = fixture.RoundTrip(fixture.Runtime.Capture());
        ProductionFinanceSaveData financeSave = RoundTripFinance(fixture.CaptureProductionFinance());
        RecordEvidence(fixture, "production-credit-rejected-save", prepared, serviceSave, financeSave);
        GameMoneyAccount oldMoney = fixture.ProductionMoney;
        fixture.RestoreProductionFinance(financeSave);
        fixture.ReplaceRuntime(serviceSave, viaSection: true);
        fixture.Runtime.Tick();
        Require(!ReferenceEquals(oldMoney, fixture.ProductionMoney)
                && JsonUtility.ToJson(ServiceRoomsSaveData.FromSnapshot(fixture.RequireSession(session.SessionId))) == preparedJson
                && JsonUtility.ToJson(fixture.CaptureProductionFinance()) == JsonUtility.ToJson(financeSave)
                && fixture.CountTerminalOutcomes() == 0,
            "Production credit rejection restore/retry changed its frozen owners.");
        RecordEvidence(fixture, "production-credit-rejected-retry", fixture.RequireSession(session.SessionId));

        // Explicit test-only preparation through the real money command. This
        // debit is a DebugAdjustment, not a purchase, narrative or training case.
        Require(fixture.ProductionMoney.TrySpendOnce(prepared.Contract.price,
                new EconomyTransactionContext(EconomyTransactionKind.DebugAdjustment,
                    "qa:e05-credit-headroom", description: "학습 제외: credit overflow 검증 복구 준비"),
                out _, out _), "Production credit headroom preparation failed.");
        int beforeCredit = fixture.MoneyBalance;
        RecordEvidence(fixture, "production-test-only-headroom", fixture.RequireSession(session.SessionId));
        fixture.Runtime.Tick();
        ServiceSessionSnapshot completed = fixture.RequireSession(session.SessionId);
        VerifyExactPaymentReceipt(fixture, completed, ServiceSessionTerminalIdentity.PaymentSourceId(session.SessionId),
            completed.Contract.price, beforeCredit, int.MaxValue, "Production overflow recovery");
        VerifySingleTerminal(fixture, completed, GameplayOutcomeStatus.Succeeded);
        VerifyProductionAcknowledgedReplay(fixture, completed);
    }

    private static void VerifyProductionAcknowledgedReplay(Fixture fixture, ServiceSessionSnapshot completed)
    {
        ServiceRoomsSaveData serviceSave = fixture.RoundTrip(fixture.Runtime.Capture());
        ProductionFinanceSaveData financeSave = RoundTripFinance(fixture.CaptureProductionFinance());
        Require(serviceSave.sessions.Count == 0 && fixture.SuccessfulCredits.Count == 1,
            "Production acknowledgement did not retire exactly one paid service.");
        RecordEvidence(fixture, "production-acknowledged-save", completed, serviceSave, financeSave);
        string parents = JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes());
        fixture.RestoreProductionFinance(financeSave);
        fixture.ReplaceRuntime(serviceSave, viaSection: true);
        fixture.Runtime.Tick();
        Require(!fixture.Runtime.TryCompleteSession(completed.SessionId, out _, out _)
                && !fixture.Runtime.CancelSession(completed.SessionId, "qa-production-replay")
                && fixture.SuccessfulCredits.Count == 1 && fixture.CountTerminalOutcomes() == 1
                && JsonUtility.ToJson(fixture.CaptureProductionFinance()) == JsonUtility.ToJson(financeSave)
                && JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()) == parents,
            "Fresh production completed restore/replay duplicated money, receipt or terminal.");
        VerifySingleTerminal(fixture, completed, GameplayOutcomeStatus.Succeeded);
        RecordEvidence(fixture, "production-completed-replay", completed, serviceSave, financeSave);
    }

    [Serializable]
    private sealed class ProductionFinanceSaveData
    {
        public GameSessionSaveData session;
        public EconomyTransactionLedgerSaveData ledger;
    }

    private static ProductionFinanceSaveData RoundTripFinance(ProductionFinanceSaveData save) =>
        JsonUtility.FromJson<ProductionFinanceSaveData>(JsonUtility.ToJson(save));

    private static void MoveToService(
        ServiceSessionRuntime runtime,
        ServiceSessionSnapshot session)
    {
        if (session.Stage == ServiceSessionStage.Service)
            return;
        Require(runtime.TrySetStage(
                session.SessionId,
                ServiceSessionStage.Service,
                out DomainFailure failure),
            "Could not advance service session to Service: " + failure.Code);
    }

    private static void VerifySingleTerminal(
        Fixture fixture,
        ServiceSessionSnapshot session,
        GameplayOutcomeStatus expectedStatus)
    {
        GameplayOutcomeSnapshot[] outcomes = fixture.TerminalOutcomes();
        Require(outcomes.Length == 1,
            "Expected one service-session parent, found " + outcomes.Length);
        GameplayOutcomeSnapshot outcome = outcomes[0];
        Require(outcome.status == expectedStatus
                && outcome.producerId
                    == ServiceSessionOutcomeKey.ProducerId
                && outcome.operationId == session.SessionId
                && outcome.ownerRevision
                    == ServiceSessionTerminalIdentity.OwnerRevision
                && outcome.participants.Count == 4
                && outcome.metrics.Count
                    == ServiceSessionOutcomeReceiptFactory.MetricCount
                && outcome.facts.Count
                    == ServiceSessionOutcomeReceiptFactory.FactCount,
            "Service-session parent lost terminal identity or structured payload.");
        Require(FindFact(outcome, ServiceSessionOutcomeIds.SessionIdFact)
                    == session.SessionId
                && FindFact(outcome, ServiceSessionOutcomeIds.ProcessIdFact)
                    == session.ProcessId
                && FindFact(outcome,
                    ServiceSessionOutcomeIds.TerminalStageFact)
                    == session.Stage.ToString()
                && Math.Abs(FindMetric(outcome,
                        ServiceSessionOutcomeIds.ContractedServiceSecondsMetric)
                    - session.Contract.serviceSeconds) <= 0.001d
                && Math.Abs(FindMetric(outcome,
                        ServiceSessionOutcomeIds.ProvidedServiceSecondsMetric)
                    - session.ProvidedServiceSeconds) <= 0.001d,
            "Service-session parent lost session/process/stage facts.");
        if (session.PaymentCommitted)
        {
            Require(FindFact(outcome,
                        ServiceSessionOutcomeIds.TransactionIdFact)
                    == session.EconomyTransactionId
                    && FindMetric(outcome,
                        ServiceSessionOutcomeIds.PaymentAmountMetric)
                    == session.PaymentAmount
                    && FindMetric(outcome,
                        ServiceSessionOutcomeIds.BalanceBeforeMetric)
                    == session.BalanceBefore
                    && FindMetric(outcome,
                        ServiceSessionOutcomeIds.BalanceAfterMetric)
                    == session.BalanceAfter,
                "Paid service parent lost its exact economy transaction.");
        }
        RecordEvidence(fixture, "terminal-verified", session);
    }

    // Observe existing DTOs only. Runtime.Capture retries pending terminals, so
    // it must never be called here merely to obtain more evidence.
    private static void RecordEvidence(Fixture fixture, string boundary,
        ServiceSessionSnapshot session = null, ServiceRoomsSaveData saved = null,
        ProductionFinanceSaveData savedFinance = null)
    {
        if (!collectingRawEvidence)
            return;
        lastRawSnapshots.Add(new JObject
        {
            ["sequence"] = lastRawSnapshots.Count,
            ["caseId"] = fixture.EvidenceCaseId,
            ["boundary"] = boundary,
            ["runtimeVersion"] = fixture.Runtime.Version,
            ["clock"] = fixture.Clock.Time,
            ["owner"] = session == null ? JValue.CreateNull()
                : JObject.Parse(JsonUtility.ToJson(ServiceRoomsSaveData.FromSnapshot(session))),
            ["savedOwner"] = saved == null ? JValue.CreateNull()
                : JObject.Parse(JsonUtility.ToJson(saved)),
            ["moneyAuthority"] = fixture.HasProductionMoney ? "production" : "controlled",
            ["moneyBalance"] = fixture.MoneyBalance,
            ["successfulCredits"] = new JArray(fixture.SuccessfulCredits
                .OrderBy(value => value.transactionId, StringComparer.Ordinal)
                .Select(value => JObject.Parse(JsonUtility.ToJson(value)))),
            ["financeOwner"] = fixture.HasProductionMoney
                ? JObject.Parse(JsonUtility.ToJson(fixture.CaptureProductionFinance())) : JValue.CreateNull(),
            ["savedFinanceOwner"] = savedFinance == null ? JValue.CreateNull()
                : JObject.Parse(JsonUtility.ToJson(savedFinance)),
            ["faults"] = new JObject
            {
                ["creditRejected"] = fixture.Money.RejectCreditOnce,
                ["receiptLookupRejected"] = fixture.Money.RejectSuccessfulTransactionLookup,
                ["reservationRejected"] = fixture.Outcomes.RejectReservations,
                ["parentCommitRejected"] = fixture.Outcomes.RejectCommits
            },
            ["ledger"] = JObject.Parse(JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()))
        });
    }

    private static string FindFact(
        GameplayOutcomeSnapshot outcome,
        GameplayOutcomeFactId id) => outcome.facts
            .Single(value => value.factId == id.Value)
            .value;

    private static double FindMetric(
        GameplayOutcomeSnapshot outcome,
        GameplayMetricId id) => outcome.metrics
            .Single(value => value.metricId == id.Value)
            .value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Actual service runtime for the controlled physical medical fixture.</summary>
    internal sealed class MedicalFixture : IDisposable
    {
        private readonly Fixture fixture;
        internal MedicalFixture(string runId, CharacterActor patient, BuildableObject hub,
            Grid grid, DungeonRuntimeAggregateRootStore roots, RestoreWorldCandidateIndex candidates)
        {
            fixture = new Fixture(runId, true, patient, hub, grid, roots, candidates);
        }
        internal ServiceSessionRuntime Runtime => fixture.Runtime;
        internal IReadOnlyList<ServiceSessionSnapshot> ActiveSessions => Runtime.ActiveSessions;
        internal int CompletedSessionCount => fixture.TerminalOutcomes()
            .Count(value => value.status == GameplayOutcomeStatus.Succeeded);
        internal int PaymentCount => fixture.Money.SuccessfulCredits.Count;
        internal ServiceRoomsSaveData Capture() => Runtime.Capture();
        internal void RejectReservations(bool reject) => fixture.Outcomes.RejectReservations = reject;
        internal void Restore(ServiceRoomsSaveData save)
        {
            fixture.Clock.Restart(1f);
            fixture.RestoreCurrentRuntime(save);
        }
        public void Dispose() => fixture.Dispose();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string runId;
        private readonly BuildingSO hubData;
        private readonly BuildableObject hub;
        private readonly FixedBuildingWorld buildingWorld;
        private readonly AcceptAllLinks links = new();
        private readonly SingleProcessCatalog processes;
        private IGameSessionStateProvider sessionState;
        private ProductionSessionState productionSession;
        private EconomyTransactionLedgerRuntime productionLedger;
        private GameMoneyAccount productionMoney;
        private readonly FixedRulesProvider rules;
        private readonly RestoreWorldCandidateIndex restoreCandidates;
        private readonly ICharacterWorldPersistenceIdentityQuery persistentCharacters;
        private readonly CharacterActor medicalPatient;
        private readonly bool ownsHub;
        private readonly bool ownsPatient;
        private readonly DungeonRuntimeAggregateRootStore sharedRoots;
        private DungeonRuntimeAggregateRootStore store;
        private int evidenceRestoreSequence;
        private readonly DomainFactDispatcher facts = new();
        private readonly IDisposable terminalObservation;

        internal Fixture(string runId, bool medical = false,
            CharacterActor externalPatient = null, BuildableObject externalHub = null,
            Grid externalGrid = null, DungeonRuntimeAggregateRootStore sharedRoots = null,
            RestoreWorldCandidateIndex sharedCandidates = null,
            bool productionMoney = false, int productionStartingMoney = 100)
        {
            this.runId = runId;
            this.sharedRoots = sharedRoots;
            sessionState = new FixedSessionState(500, externalPatient != null ? 137 : 12);
            if (productionMoney)
            {
                productionSession = new ProductionSessionState(productionStartingMoney, 12);
                sessionState = productionSession;
            }
            restoreCandidates = sharedCandidates ?? new RestoreWorldCandidateIndex();
            ownsHub = externalHub == null;
            ownsPatient = medical && externalPatient == null;
            ServiceProcessSO process = AssetDatabase.LoadAssetAtPath<
                ServiceProcessSO>(
                "Assets/Resources/SO/ServiceRooms/Processes/"
                + (medical ? "service_medical_treat.asset" : "service_bathing_wash.asset"))
                ?? throw new InvalidOperationException(
                    "Authored bathing service process is missing.");
            processes = new SingleProcessCatalog(process);
            CoreSessionRulesSO rulesAsset = AssetDatabase.LoadAssetAtPath<
                CoreSessionRulesSO>(
                "Assets/Resources/SO/Content/CoreSessionRules.asset")
                ?? throw new InvalidOperationException(
                    "Authored core-session rules are missing.");
            rules = new FixedRulesProvider(
                rulesAsset.CreateRuntimeDefinition());

            BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                medical ? "Assets/Resources/SO/Building/Medical/M01_응급처치대.asset"
                    : "Assets/Resources/SO/Building/Modular/H03_세면대.asset")
                ?? throw new InvalidOperationException(
                    "Authored bathing hub is missing.");
            if (ownsHub)
            {
                hubData = UnityEngine.Object.Instantiate(source);
                GameObject gameObject = new("Service Session Outcome Hub");
                hub = gameObject.AddComponent<BuildableObject>();
                hub.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
                CharacterAiEditorTestDependencies.Inject(hub);
                hub.Initialization(hubData, Vector2Int.zero);
            }
            else hub = externalHub;
            buildingWorld = new FixedBuildingWorld(hub);
            if (medical)
            {
                medicalPatient = externalPatient;
                if (ownsPatient)
                {
                    var actorObject = new GameObject("Service Medical Section Patient");
                    medicalPatient = (CharacterActor)typeof(SurvivalDebugScenarios)
                        .GetMethod("CreateWim028Actor", BindingFlags.NonPublic | BindingFlags.Static)!
                        .Invoke(null, new object[] { actorObject, "character:service-medical-section" });
                }
                persistentCharacters = new FixedPersistentCharacters(medicalPatient.Identity.PersistentId);
            }
            else persistentCharacters = new EmptyPersistentCharacters();
            if (sharedCandidates == null)
            {
                restoreCandidates.SetFacilityCandidate(externalGrid ?? new Grid(2, 2), new[] { hub });
                restoreCandidates.SetCharacterCandidate(
                    medicalPatient == null ? Array.Empty<CharacterActor>() : new[] { medicalPatient });
            }

            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new ServiceSessionOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new ServiceSessionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                registry,
                new GameEventBus());
            terminalObservation = facts.Subscribe<ServiceSessionTerminalFact>("qa:service.session-terminal",
                new GameplayDomainFactObserver<ServiceSessionTerminalFact, ServiceSessionOutcomeReceipt>(
                    ServiceSessionTerminalFactMapping.Capture, registry, Ledger, Recorder,
                    new GameplayDomainFactCommitGroup(Recorder, Ledger), ServiceSessionTerminalFactMapping.Reserve));
            Outcomes = new FaultingServiceFacts(facts);
            Money = new MoneyAccount(100);
            Clock = new ControlledClock(10f);
            if (productionMoney)
                RestoreProductionFinance(new ProductionFinanceSaveData
                {
                    session = productionSession.Capture(),
                    ledger = new EconomyTransactionLedgerSaveData()
                });
            ReplaceRuntime(new ServiceRoomsSaveData());
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal FaultingServiceFacts Outcomes { get; }
        internal MoneyAccount Money { get; }
        internal ControlledClock Clock { get; }
        internal ServiceSessionRuntime Runtime { get; private set; }
        internal string EvidenceCaseId => runId;
        internal bool HasProductionMoney => productionMoney != null;
        internal GameMoneyAccount ProductionMoney => productionMoney
            ?? throw new InvalidOperationException("Production money fixture is not enabled.");
        private IGameMoneyAccount MoneyAuthority => HasProductionMoney ? productionMoney : Money;
        internal IGameMoneyTransactionQuery MoneyTransactions => (IGameMoneyTransactionQuery)MoneyAuthority;
        internal int MoneyBalance => MoneyAuthority.Balance;
        internal IReadOnlyCollection<EconomyTransactionRecord> SuccessfulCredits => HasProductionMoney
            ? productionLedger.Records.Where(value => value.succeeded && value.kind == EconomyTransactionKind.GuestServiceIncome).ToArray()
            : Money.SuccessfulCredits;
        internal ProductionFinanceSaveData CaptureProductionFinance() => new()
        {
            session = productionSession.Capture(),
            ledger = productionLedger.Capture()
        };
        internal void RestoreProductionFinance(ProductionFinanceSaveData save)
        {
            productionSession = new ProductionSessionState(0, save.session.absoluteDay);
            productionSession.Restore(save.session);
            sessionState = productionSession;
            var treasury = new TreasuryEconomyAggregateStateStore(new DungeonRuntimeAggregateRootStore());
            productionLedger = new EconomyTransactionLedgerRuntime(Clock, treasury);
            var restored = new TreasuryEconomyAggregateState();
            productionLedger.PopulateRestoreState(restored, save.ledger);
            treasury.Replace(restored);
            productionMoney = new GameMoneyAccount(productionSession, productionLedger);
        }
        internal DungeonRuntimeAggregateRootStore AggregateStore => store;
        internal ModularFacilityWorldSaveData CaptureFacilityWorld() => new()
        {
            buildings = new List<ModularFacilityBuildingSaveData> { ModularFacilityBuildingSaveData.From(hub) }
        };

        internal void SetMode(ServiceOperationMode mode)
        {
            ServiceRoomsSaveData save = Runtime.Capture();
            save.hubs.RemoveAll(value => value.hubId == HubId);
            save.hubs.Add(new ServiceHubModeSaveData
            {
                hubId = HubId,
                mode = mode
            });
            ReplaceRuntime(save);
        }

        internal ServiceSessionSnapshot Begin(
            bool internalActor,
            bool advertised)
        {
            Require(Runtime.TryBeginSession(
                    new ServiceSessionRequest
                    {
                        Hub = hub,
                        Actor = medicalPatient,
                        ProcessId = processes.Process.ProcessId,
                        IsInternalActor = internalActor,
                        AdvertisedDemand = advertised
                    },
                    out ServiceSessionSnapshot session,
                    out DomainFailure failure),
                "Actual service-session entry failed: " + failure.Code);
            RecordEvidence(this, "reserved", session);
            return session;
        }

        internal ServiceSessionSnapshot RequireSession(string sessionId)
        {
            ServiceSessionAggregate aggregate = store.GetOrCreate(
                () => new ServiceSessionAggregate());
            ServiceSessionSnapshot session = null;
            Require(aggregate != null
                    && aggregate.TryGetSession(sessionId, out session),
                "Service-session owner is missing: " + sessionId);
            return session;
        }

        internal void ReplaceRuntime(ServiceRoomsSaveData save, bool viaSection = false)
        {
            Runtime?.Dispose();
            store = sharedRoots ?? new DungeonRuntimeAggregateRootStore();
            Runtime = new ServiceSessionRuntime(
                buildingWorld,
                links,
                processes,
                new ServiceSessionRuntime.Dependencies(
                    Clock,
                    MoneyAuthority,
                    AlwaysPowered.Instance,
                    AllResearch.Instance,
                    rules,
                    sessionState,
                    Outcomes,
                    Outcomes),
                store,
                restoreCandidates,
                persistentCharacters);
            if (viaSection)
            {
                var section = new ServiceRoomsSaveSection(Runtime, processes);
                var report = new DungeonGameRestoreReport();
                section.Restore(JsonUtility.ToJson(save), ServiceRoomsSaveData.CurrentVersion, report);
                Require(report.Success, "Official service section restore failed: " + string.Join(" | ", report.Errors));
            }
            else Runtime.PublishRestoreCandidate(Runtime.PrepareRestoreCandidate(save));
            if (collectingRawEvidence && lastRawSnapshots.Any(value =>
                    (string)value["caseId"] == runId))
                RecordEvidence(this, "restored-" + (++evidenceRestoreSequence),
                    save.sessions.Count == 0 ? null : RequireSession(save.sessions[0].sessionId), save);
        }

        internal void RestoreCurrentRuntime(ServiceRoomsSaveData save)
        {
            var section = new ServiceRoomsSaveSection(Runtime, processes);
            var report = new DungeonGameRestoreReport();
            section.Restore(JsonUtility.ToJson(save), ServiceRoomsSaveData.CurrentVersion, report);
            Require(report.Success, "Official medical service section restore failed: " + string.Join(" | ", report.Errors));
        }

        internal ServiceRoomsSaveData RoundTrip(ServiceRoomsSaveData save) =>
            JsonUtility.FromJson<ServiceRoomsSaveData>(
                JsonUtility.ToJson(save));

        internal GameplayOutcomeSnapshot[] TerminalOutcomes() => Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Where(value => value != null
                && value.producerId
                    == ServiceSessionOutcomeKey.ProducerId)
            .ToArray();

        internal int CountTerminalOutcomes() => TerminalOutcomes().Length;

        private string HubId => hub.RequirePersistentInstanceId().Value;

        public void Dispose()
        {
            Runtime?.Dispose();
            terminalObservation.Dispose();
            facts.Dispose();
            if (ownsPatient && medicalPatient != null)
                UnityEngine.Object.DestroyImmediate(medicalPatient.gameObject);
            if (ownsHub && hub != null)
                UnityEngine.Object.DestroyImmediate(hub.gameObject);
            if (hubData != null)
                UnityEngine.Object.DestroyImmediate(hubData);
        }
    }

    private sealed class FaultingServiceFacts : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal FaultingServiceFacts(DomainFactDispatcher inner) => this.inner = inner;
        internal bool RejectReservations { get; set; }
        internal bool RejectCommits { get; set; }
        internal bool? UnknownReply { get; set; }
        public bool TryReserve<T>(in T proposed, out DomainFactLease lease, out string failure)
        {
            if (RejectReservations)
            { lease = default; failure = "qa-service-reservation-rejected"; return false; }
            return inner.TryReserve(proposed, out lease, out failure);
        }
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T actual)
        {
            if (RejectCommits)
            { inner.Cancel(lease); return DomainFactDelivery.Reject("qa-service-publication-rejected"); }
            if (UnknownReply.HasValue)
            {
                if (UnknownReply.Value) inner.Publish(lease, actual); else inner.Cancel(lease);
                return DomainFactDelivery.Uncertain("qa-service-reply-lost");
            }
            return inner.Publish(lease, actual);
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> pending, DomainFactDelivery[] results, out string failure)
        {
            if (RejectCommits) { failure = "qa-service-publication-rejected"; return false; }
            if (!UnknownReply.HasValue) return inner.ReplayBatch(pending, results, out failure);
            if (UnknownReply.Value) inner.ReplayBatch(pending, results, out _);
            for (int index = 0; index < results.Length; index++)
                results[index] = DomainFactDelivery.Uncertain("qa-service-reply-lost");
            failure = string.Empty; return true;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> pending, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(pending, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class MoneyAccount :
        IIdempotentGameMoneyAccount,
        IGameMoneyTransactionQuery
    {
        private readonly Dictionary<string, EconomyTransactionRecord> records =
            new(StringComparer.Ordinal);
        private int nextSequence = 1;

        internal MoneyAccount(int balance) => Balance = balance;

        public int Balance { get; private set; }
        internal IReadOnlyCollection<EconomyTransactionRecord>
            SuccessfulCredits => records.Values;
        internal bool RejectCreditOnce { get; set; }
        internal bool RejectSuccessfulTransactionLookup { get; set; }

        public bool CanSpend(int amount) => Balance >= Math.Max(0, amount);

        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            int cost = Math.Max(0, amount);
            if (!CanSpend(cost))
            {
                reason = "insufficient";
                return false;
            }
            Balance -= cost;
            reason = string.Empty;
            return true;
        }

        public bool TrySpendOnce(
            int amount,
            EconomyTransactionContext context,
            out EconomyTransactionRecord receipt,
            out string reason)
        {
            receipt = null;
            reason = "unsupported";
            return false;
        }

        public bool TryCreditOnce(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (RejectCreditOnce)
            {
                reason = "qa-service-credit-rejected";
                return false;
            }
            if (records.TryGetValue(
                    context.sourceId,
                    out EconomyTransactionRecord existing))
            {
                bool exact = existing.amount == amount
                    && existing.targetId == context.targetId;
                reason = exact ? string.Empty : "mismatch";
                return exact;
            }
            int before = Balance;
            Balance = checked(Balance + amount);
            records.Add(context.sourceId, new EconomyTransactionRecord
            {
                transactionId = "economy:qa:" + nextSequence++,
                kind = context.kind,
                sourceId = context.sourceId,
                targetId = context.targetId,
                description = context.description,
                amount = amount,
                balanceBefore = before,
                balanceAfter = Balance,
                gameTime = 0f,
                succeeded = true
            });
            reason = string.Empty;
            return true;
        }

        public bool TryGetSuccessfulTransaction(
            EconomyTransactionKind kind,
            string sourceId,
            out EconomyTransactionRecord record)
        {
            if (RejectSuccessfulTransactionLookup)
            {
                record = null;
                return false;
            }
            bool found = records.TryGetValue(sourceId, out var stored)
                && stored.kind == kind;
            record = found ? stored.Clone() : null;
            return found;
        }

        public void Add(int amount) => Balance += Math.Max(0, amount);
        public void Add(int amount, EconomyTransactionContext context) =>
            Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            Balance = Math.Max(0, amount);
    }

    private sealed class ControlledClock : IGameClock
    {
        internal ControlledClock(float time) => Time = time;
        internal void Restart(float time) { Time = time; FrameCount = 0; }
        public float DeltaTime => 1f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance(float seconds)
        {
            Time += Math.Max(0f, seconds);
            FrameCount++;
        }
    }

    private sealed class ProductionSessionState : IGameSessionStateProvider
    {
        private readonly GameSessionState state;
        private readonly IGameSessionStateMutation mutation;

        internal ProductionSessionState(int money, int day)
        {
            GameData settings = ScriptableObject.CreateInstance<GameData>();
            try
            {
                state = GameSessionState.Create(settings, out mutation);
                mutation.Reset(money, day, 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        public bool TryGetSessionState(out GameSessionState gameData) { gameData = state; return true; }
        internal GameSessionSaveData Capture() => GameSessionSaveData.From(state.Capture());
        internal void Restore(GameSessionSaveData save) => mutation.Restore(save.ToSnapshot());
    }

    private sealed class FixedSessionState : IGameSessionStateProvider
    {
        internal FixedSessionState(int money, int day) =>
            State = new GameSessionState(money, day);
        internal GameSessionState State { get; }
        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = State;
            return true;
        }
    }

    private sealed class FixedBuildingWorld : IBuildingWorldQuery
    {
        internal FixedBuildingWorld(BuildableObject hub) =>
            Buildings = new[] { hub };
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class SingleProcessCatalog : IServiceProcessCatalog
    {
        internal SingleProcessCatalog(ServiceProcessSO process)
        {
            Process = process;
            All = new[] { process };
        }
        internal ServiceProcessSO Process { get; }
        public IReadOnlyList<ServiceProcessSO> All { get; }
        public bool TryGet(string processId, out ServiceProcessSO process)
        {
            bool found = string.Equals(
                processId,
                Process.ProcessId,
                StringComparison.Ordinal);
            process = found ? Process : null;
            return found;
        }
        public ServiceProcessSO Require(string processId) =>
            TryGet(processId, out var process)
                ? process
                : throw new KeyNotFoundException(processId);
    }

    private sealed class AcceptAllLinks : IServiceRoomLinkRuntime
    {
        public int Version => 1;
        public IReadOnlyList<ServiceSupportLinkSnapshot> GetLinks(
            BuildableObject hub) => Array.Empty<ServiceSupportLinkSnapshot>();
        public bool TryGetLinkForSupport(
            BuildableObject support,
            out ServiceSupportLinkSnapshot link)
        {
            link = null;
            return false;
        }
        public bool HasFeatures(
            BuildableObject hub,
            IReadOnlyList<string> requiredFeatureTags,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }
        public bool TryResolveFeature(
            BuildableObject hub,
            string featureTag,
            out BuildableObject support,
            out BuildingServiceSupportAbility ability)
        {
            support = null;
            ability = null;
            return false;
        }
    }

    private sealed class FixedRulesProvider : ICoreSessionRulesProvider
    {
        internal FixedRulesProvider(CoreSessionRulesDefinition rules) =>
            CoreSessionRules = rules;
        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    private sealed class FixedPersistentCharacters : ICharacterWorldPersistenceIdentityQuery
    {
        private readonly CharacterId[] ids;
        internal FixedPersistentCharacters(string id) => ids = new[] { new CharacterId(id) };
        public IReadOnlyCollection<CharacterId> GetPersistentCharacterIds() => ids;
        public IReadOnlyCollection<CharacterId> GetPersistentActorIds() => ids;
    }

    private sealed class EmptyPersistentCharacters :
        ICharacterWorldPersistenceIdentityQuery
    {
        public IReadOnlyCollection<CharacterId> GetPersistentCharacterIds() =>
            Array.Empty<CharacterId>();
        public IReadOnlyCollection<CharacterId> GetPersistentActorIds() =>
            Array.Empty<CharacterId>();
    }

    private sealed class AlwaysPowered : IPowerInfrastructureQuery
    {
        internal static AlwaysPowered Instance { get; } = new();
        public int Version => 1;
        public IReadOnlyList<PowerNetworkSnapshot> Networks =>
            Array.Empty<PowerNetworkSnapshot>();
        public bool IsPowered(BuildableObject building) => true;
        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }

    private sealed class AllResearch : IServiceRoomResearchQuery
    {
        internal static AllResearch Instance { get; } = new();
        public bool IsCompleted(string researchId) => true;
    }

    public class DefaultProxy : DispatchProxy
    {
        internal static T Create<T>() where T : class =>
            DispatchProxy.Create<T, DefaultProxy>();

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].IsOut
                    && !parameters[index].ParameterType.IsByRef)
                {
                    continue;
                }
                Type type = parameters[index].ParameterType.IsByRef
                    ? parameters[index].ParameterType.GetElementType()
                    : parameters[index].ParameterType;
                args[index] = type != null && type.IsValueType
                    ? Activator.CreateInstance(type)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }
}
#endif
