using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;
using VContainer;

public sealed class StaffDiscontentRestoreCandidate
{
    internal StaffDiscontentRestoreCandidate(StaffDiscontentState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal StaffDiscontentState State { get; }
}

public class StaffDiscontentRuntime : MonoBehaviour
{
    [SerializeField] private StaffDiscontentRules rules = StaffDiscontentRules.CreateDefault();

    private DungeonRuntimeAggregateRootStore aggregateRootStore;

    private StaffDiscontentState state =>
        aggregateRootStore.GetOrCreate(() => new StaffDiscontentState());
    private ICharacterWorldQuery characterWorldQuery;
    private ICharacterSettlementStandingQuery settlementStandings;
    private DungeonStory.Foundation.IGameEventBus gameEventBus;
    private IStaffDiscontentOutcomeCommitter outcomes;
    private IGameClock gameClock;
    private IDisposable operatingDayEndedSubscription;

    public StaffDiscontentState State => state;
    public StaffDiscontentRules Rules => rules;
    public long OutcomeRevision => state.OutcomeRevision;

    [Inject]
    public void Construct(
        ICharacterWorldQuery characterWorldQuery,
        DungeonStory.Foundation.IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterSettlementStandingQuery settlementStandings,
        IStaffDiscontentOutcomeCommitter outcomes,
        IGameClock gameClock)
    {
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.settlementStandings = settlementStandings
            ?? throw new ArgumentNullException(nameof(settlementStandings));
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        SubscribeToScopedEvents();
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        ProcessAllStaff();
    }

    public StaffDiscontentRecord ProcessStaff(CharacterActor staff, out StaffDiscontentOutcome outcome)
    {
        if (settlementStandings?.IsMinion(staff) == true)
        {
            outcome = StaffDiscontentOutcome.None;
            return null;
        }

        StaffDiscontentRecord record = state.ProcessStaff(staff, rules, out outcome);
        if (record == null)
        {
            return null;
        }

        ApplyOutcome(staff, record, outcome);
        if (record.IsInLocalRebellion && !record.IsIsolated && !record.IsSuppressed)
        {
            DispatchAutoSuppress(staff);
        }
        return record;
    }

    public IReadOnlyList<StaffDiscontentSnapshot> CaptureSnapshots()
    {
        return state.CaptureSnapshots();
    }

    public void RestoreSnapshots(
        IEnumerable<StaffDiscontentSnapshot> savedRecords,
        long outcomeRevision = 0L)
    {
        PublishRestoreCandidate(PrepareRestoreCandidate(
            savedRecords,
            outcomeRevision));
    }

    public StaffDiscontentRestoreCandidate PrepareRestoreCandidate(
        IEnumerable<StaffDiscontentSnapshot> savedRecords,
        long outcomeRevision = 0L)
    {
        StaffDiscontentState restored = new StaffDiscontentState();
        restored.Restore(savedRecords, outcomeRevision);
        return new StaffDiscontentRestoreCandidate(restored);
    }

    public void PublishRestoreCandidate(
        StaffDiscontentRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        aggregateRootStore.Replace(candidate.State);
    }

    public void ProcessAllStaff()
    {
        IReadOnlyList<CharacterActor> actors = RequireCharacterWorldQuery().Characters;
        foreach (CharacterActor staff in actors)
        {
            if (settlementStandings?.IsFormalResident(staff) == true)
            {
                ProcessStaff(staff, out _);
            }
        }
    }

    public float GetWorkEfficiencyMultiplier(CharacterActor staff)
    {
        if (settlementStandings?.IsMinion(staff) == true
            || !StaffDiscontentService.IsTrackableStaff(staff))
        {
            return 1f;
        }

        StaffDiscontentStage stage = state.TryGetRecord(staff, out StaffDiscontentRecord record)
            ? record.Stage
            : StaffDiscontentService.EvaluateStage(StaffDiscontentService.GetMood(staff), 0, rules);
        return StaffDiscontentService.GetWorkEfficiencyMultiplier(stage, rules);
    }

    public bool ShouldBlockWork(CharacterActor staff, out string reason)
    {
        reason = string.Empty;
        if (settlementStandings?.IsMinion(staff) == true
            || !StaffDiscontentService.IsTrackableStaff(staff))
        {
            return false;
        }

        StaffDiscontentStage stage = state.TryGetRecord(staff, out StaffDiscontentRecord record)
            ? record.Stage
            : StaffDiscontentService.EvaluateStage(StaffDiscontentService.GetMood(staff), 0, rules);
        if (!StaffDiscontentService.ShouldBlockWork(stage))
        {
            return false;
        }

        reason = StaffDiscontentService.GetBlockReason(stage);
        return true;
    }

    public bool IsRebellionTarget(CharacterActor target)
    {
        return settlementStandings?.IsMinion(target) != true
            && state.TryGetRecord(target, out StaffDiscontentRecord record)
            && record.IsInLocalRebellion
            && !record.IsDeparted
            && !record.IsSuppressed;
    }

    public int DispatchAutoSuppress(CharacterActor rebel)
    {
        if (!IsRebellionTarget(rebel))
        {
            return 0;
        }

        IReadOnlyList<CharacterActor> characters = RequireCharacterWorldQuery().Characters;
        int assignedCount = 0;
        foreach (CharacterActor candidate in characters)
        {
            if (candidate == null
                || candidate == rebel
                || (candidate.Stats != null && candidate.Stats.IsDead)
                || !candidate.TryGetAbility(out AbilityWork work)
                || work.HasPrioritySuppressTarget
                || !work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard))
            {
                continue;
            }

            if (!WorkCommandResolver.TryResolveSuppressCommand(candidate, rebel, IsRebellionTarget, out _))
            {
                continue;
            }

            if (!work.TrySetPrioritySuppressTarget(rebel, null, out _))
            {
                continue;
            }

            assignedCount++;
        }

        if (assignedCount > 0 && state.TryGetRecord(rebel, out StaffDiscontentRecord record))
        {
            gameEventBus.RaiseStaffComplaint(
                $"{record.DisplayName}: 자동 제압 {assignedCount}명 배정",
                EventAlertImportance.Medium);
        }

        return assignedCount;
    }

    public bool TryIsolateRebel(CharacterActor rebel, CharacterActor actor, out StaffRebellionResponseResult result)
    {
        if (!state.TryGetRecord(rebel, out StaffDiscontentRecord record)
            || !record.IsInLocalRebellion)
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Isolate, null, actor, "격리할 반란 대상이 없습니다");
            return false;
        }

        StaffDiscontentSnapshot before = record.ToSnapshot();
        IReadOnlyList<StaffDiscontentSnapshot> ownerBefore = state.CaptureSnapshots();
        long revisionBefore = state.OutcomeRevision;
        if (!TryReserveResponse(
                actor,
                out CharacterId responder,
                out KoreanNameSnapshot responderName,
                out long ownerRevision,
                out int absoluteDay,
                out ReservedStaffDiscontentOutcome reserved,
                out string reserveFailure))
        {
            result = FailedResponse(
                StaffRebellionResponseType.Isolate,
                before,
                actor,
                "격리 결과를 기록할 수 없습니다: " + reserveFailure);
            return false;
        }
        if (!record.MarkIsolated())
        {
            outcomes.Cancel(reserved);
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Isolate, record.ToSnapshot(), actor, "격리할 수 없습니다");
            return false;
        }

        StaffDiscontentSnapshot after = record.ToSnapshot();
        if (!TryCommitResponse(
                StaffRebellionResponseType.Isolate,
                before,
                after,
                responder,
                responderName,
                ownerRevision,
                absoluteDay,
                reserved,
                out string failureReason))
        {
            state.Restore(ownerBefore, revisionBefore);
            result = FailedResponse(
                StaffRebellionResponseType.Isolate,
                before,
                actor,
                "격리 결과 커밋 실패: " + failureReason);
            return false;
        }

        PublishCommittedResponseObservers(
            rebel,
            after,
            StaffRebellionResponseType.Isolate);
        result = new StaffRebellionResponseResult(
            true,
            StaffRebellionResponseType.Isolate,
            after,
            actor,
            "격리 완료");
        return true;
    }

    public bool TryCalmStaff(CharacterActor staff, CharacterActor actor, out StaffRebellionResponseResult result)
    {
        if (!state.TryGetRecord(staff, out StaffDiscontentRecord record))
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Calm, null, actor, "진정할 직원 기록이 없습니다");
            return false;
        }

        StaffDiscontentSnapshot before = record.ToSnapshot();
        IReadOnlyList<StaffDiscontentSnapshot> ownerBefore = state.CaptureSnapshots();
        long revisionBefore = state.OutcomeRevision;
        if (!TryReserveResponse(
                actor,
                out CharacterId responder,
                out KoreanNameSnapshot responderName,
                out long ownerRevision,
                out int absoluteDay,
                out ReservedStaffDiscontentOutcome reserved,
                out string reserveFailure))
        {
            result = FailedResponse(
                StaffRebellionResponseType.Calm,
                before,
                actor,
                "진정 결과를 기록할 수 없습니다: " + reserveFailure);
            return false;
        }

        float negotiationMultiplier = actor == null
            ? 1f
            : actor.GetDetailedStatMultiplier(
                "social:negotiation",
                actor.Identity?.IsOwner == true
                    ? new[] { "state:formal-status" }
                    : Array.Empty<string>());
        CharacterMoodDeliveryTransactionSnapshot moodBefore =
            staff?.Stats?.CaptureMoodDeliveryTransactionState();
        if (!record.TryCalm(
                staff,
                rules,
                negotiationMultiplier,
                out string failureReason))
        {
            outcomes.Cancel(reserved);
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Calm, record.ToSnapshot(), actor, failureReason);
            return false;
        }

        StaffDiscontentSnapshot after = record.ToSnapshot();
        if (!TryCommitResponse(
                StaffRebellionResponseType.Calm,
                before,
                after,
                responder,
                responderName,
                ownerRevision,
                absoluteDay,
                reserved,
                out failureReason))
        {
            state.Restore(ownerBefore, revisionBefore);
            if (moodBefore != null)
                staff?.Stats?.RestoreMoodDeliveryTransactionState(moodBefore);
            result = FailedResponse(
                StaffRebellionResponseType.Calm,
                before,
                actor,
                "진정 결과 커밋 실패: " + failureReason);
            return false;
        }

        PublishCommittedResponseObservers(
            staff,
            after,
            StaffRebellionResponseType.Calm);
        result = new StaffRebellionResponseResult(
            true,
            StaffRebellionResponseType.Calm,
            after,
            actor,
            "진정 완료");
        return true;
    }

    public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)
    {
        if (!state.TryGetRecord(rebel, out StaffDiscontentRecord record))
        {
            return false;
        }

        StaffDiscontentSnapshot before = record.ToSnapshot();
        IReadOnlyList<StaffDiscontentSnapshot> ownerBefore = state.CaptureSnapshots();
        long revisionBefore = state.OutcomeRevision;
        if (!TryReserveResponse(
                defender,
                out CharacterId responder,
                out KoreanNameSnapshot responderName,
                out long ownerRevision,
                out int absoluteDay,
                out ReservedStaffDiscontentOutcome reserved,
                out string reserveFailure))
        {
            Debug.LogError(
                "Staff suppression result reservation failed: "
                + reserveFailure);
            return false;
        }
        if (!record.MarkSuppressed())
        {
            outcomes.Cancel(reserved);
            return false;
        }

        StaffDiscontentSnapshot after = record.ToSnapshot();
        if (!TryCommitResponse(
                StaffRebellionResponseType.SuppressCommand,
                before,
                after,
                responder,
                responderName,
                ownerRevision,
                absoluteDay,
                reserved,
                out string failureReason))
        {
            state.Restore(ownerBefore, revisionBefore);
            Debug.LogError(
                "Staff suppression result commit failed: "
                + failureReason);
            return false;
        }

        PublishCommittedResponseObservers(
            rebel,
            after,
            StaffRebellionResponseType.SuppressCommand);
        return true;
    }

    private bool TryReserveResponse(
        CharacterActor responderActor,
        out CharacterId responder,
        out KoreanNameSnapshot responderName,
        out long ownerRevision,
        out int absoluteDay,
        out ReservedStaffDiscontentOutcome reserved,
        out string failureReason)
    {
        responder = default;
        responderName = default;
        ownerRevision = 0L;
        absoluteDay = CurrentAbsoluteDay;
        reserved = default;
        failureReason = string.Empty;
        if (!TryCaptureResponder(
                responderActor,
                out responder,
                out responderName,
                out failureReason))
        {
            return false;
        }

        try
        {
            ownerRevision = state.GetNextOutcomeRevision();
        }
        catch (Exception exception) when (IsRecoverableOutcomeException(exception))
        {
            failureReason = "staff-response-revision-invalid:"
                + exception.Message;
            return false;
        }

        return outcomes.TryReserve(
            ownerRevision,
            absoluteDay,
            responder.IsValid,
            out reserved,
            out failureReason);
    }

    private bool TryCommitResponse(
        StaffRebellionResponseType responseType,
        StaffDiscontentSnapshot before,
        StaffDiscontentSnapshot after,
        CharacterId responder,
        KoreanNameSnapshot responderName,
        long ownerRevision,
        int absoluteDay,
        in ReservedStaffDiscontentOutcome reserved,
        out string failureReason)
    {
        PreparedOwnerOutcome prepared = default;
        try
        {
            StaffDiscontentOutcomeReceipt receipt = new(
                ownerRevision,
                absoluteDay,
                responseType,
                before,
                after,
                responder,
                responderName);
            if (!outcomes.TryWrite(
                    receipt,
                    reserved,
                    out prepared,
                    out failureReason))
            {
                return false;
            }

            state.AdvanceOutcomeRevision(ownerRevision);
            OwnerOutcomeCommitResult committed = outcomes.Commit(
                prepared,
                ownerRevision);
            if (!committed.DurablyCommitted)
            {
                outcomes.Cancel(prepared);
                failureReason = committed.DetailCode;
                return false;
            }

            failureReason = committed.DetailCode;
            return true;
        }
        catch (Exception exception) when (IsRecoverableOutcomeException(exception))
        {
            OwnerOutcomeCommitResult reconciled = outcomes.Reconcile(
                reserved.ResultKey);
            if (reconciled.DurablyCommitted)
            {
                failureReason = reconciled.DetailCode;
                return true;
            }

            outcomes.Cancel(prepared);
            outcomes.Cancel(reserved);
            failureReason = "staff-response-transaction-fault:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private static bool TryCaptureResponder(
        CharacterActor actor,
        out CharacterId responder,
        out KoreanNameSnapshot responderName,
        out string failureReason)
    {
        responder = default;
        responderName = default;
        failureReason = string.Empty;
        if (actor == null)
            return true;
        if (!CharacterPersistentIdentity.TryGet(actor, out responder))
        {
            failureReason = "staff-response-responder-id-missing";
            return false;
        }

        try
        {
            responderName = StaffDiscontentOutcomeReceipt.CaptureName(
                responder.Value,
                StaffDiscontentService.GetStaffDisplayName(
                    actor,
                    responder.Value));
            return true;
        }
        catch (Exception exception) when (IsRecoverableOutcomeException(exception))
        {
            failureReason = "staff-response-responder-name-invalid:"
                + exception.Message;
            return false;
        }
    }

    private static StaffRebellionResponseResult FailedResponse(
        StaffRebellionResponseType responseType,
        StaffDiscontentSnapshot snapshot,
        CharacterActor actor,
        string message) => new(
        false,
        responseType,
        snapshot,
        actor,
        message);

    private void PublishCommittedResponseObservers(
        CharacterActor staff,
        StaffDiscontentSnapshot snapshot,
        StaffRebellionResponseType responseType)
    {
        try
        {
            switch (responseType)
            {
                case StaffRebellionResponseType.Isolate:
                    RecordSocial(
                        staff,
                        CharacterActivityOutcomes.Completed,
                        "반란 대응: 격리",
                        "rebellion-isolated",
                        0.1f);
                    break;
                case StaffRebellionResponseType.Calm:
                    RecordSocial(
                        staff,
                        CharacterActivityOutcomes.Completed,
                        "반란 대응: 진정",
                        "rebellion-calmed",
                        0.35f);
                    break;
            }
        }
        catch (Exception exception) when (IsRecoverableObserverException(exception))
        {
            Debug.LogException(exception);
        }

        try
        {
            EventAlertImportance importance = responseType ==
                StaffRebellionResponseType.Calm
                ? EventAlertImportance.Low
                : EventAlertImportance.Medium;
            string label = responseType switch
            {
                StaffRebellionResponseType.Calm => "진정",
                StaffRebellionResponseType.Isolate => "격리",
                _ => "제압 완료"
            };
            gameEventBus.RaiseStaffComplaint(
                $"{snapshot.displayName}: {label}",
                importance);
        }
        catch (Exception exception) when (IsRecoverableObserverException(exception))
        {
            Debug.LogException(exception);
        }
    }

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private static bool IsRecoverableOutcomeException(Exception exception) =>
        exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException;

    private static bool IsRecoverableObserverException(Exception exception) =>
        IsRecoverableOutcomeException(exception);

    private void ApplyOutcome(CharacterActor staff, StaffDiscontentRecord record, StaffDiscontentOutcome outcome)
    {
        if (outcome == StaffDiscontentOutcome.None)
        {
            return;
        }

        StaffDiscontentSnapshot snapshot = record.ToSnapshot(outcome);

        switch (outcome)
        {
            case StaffDiscontentOutcome.Warning:
                RecordSocial(staff, CharacterActivityOutcomes.Changed, "직원 불만: 만족도 낮음", "low-satisfaction", -0.45f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 만족도 낮음", EventAlertImportance.Low);
                break;
            case StaffDiscontentOutcome.EfficiencyPenalty:
                RecordSocial(staff, CharacterActivityOutcomes.Changed, "직원 불만: 효율 저하", "efficiency-penalty", -0.55f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 효율 저하", EventAlertImportance.Medium);
                break;
            case StaffDiscontentOutcome.WorkDisruption:
                RecordSocial(staff, CharacterActivityOutcomes.Blocked, "직원 불만: 태업/결근", "work-disruption", -0.7f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 태업/결근", EventAlertImportance.Medium);
                break;
            case StaffDiscontentOutcome.PermanentDeparture:
                RecordSocial(staff, CharacterActivityOutcomes.Departed, "직원 이탈: 영구 손실", "permanent-departure", -1f);
                staff?.Lifecycle?.SetLifecycleState(CharacterLifecycleState.Despawned);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 이탈", EventAlertImportance.High);
                break;
            case StaffDiscontentOutcome.LocalRebellion:
                RecordSocial(staff, CharacterActivityOutcomes.Started, "국지 반란: 주변 피해 시작", "local-rebellion", -1f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 국지 반란", EventAlertImportance.High);
                DispatchAutoSuppress(staff);
                break;
            case StaffDiscontentOutcome.OwnerThreat:
                RecordSocial(staff, CharacterActivityOutcomes.Changed, "반란 확산: 사장 위협", "owner-threat", -1f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 반란 확산", EventAlertImportance.High);
                break;
        }
    }

    private static void RecordSocial(
        CharacterActor actor,
        string outcomeId,
        string factText,
        string reasonCode,
        float sentiment)
    {
        actor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Social,
            outcomeId,
            factText,
            actionId: "staff-discontent",
            reasonCode: reasonCode,
            sentiment: sentiment,
            bubbleEligible: true));
    }

    private ICharacterWorldQuery RequireCharacterWorldQuery()
    {
        if (characterWorldQuery == null)
        {
            throw new InvalidOperationException(
                $"{nameof(StaffDiscontentRuntime)} requires {nameof(ICharacterWorldQuery)} injection.");
        }

        return characterWorldQuery;
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        operatingDayEndedSubscription?.Dispose();
        operatingDayEndedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        operatingDayEndedSubscription ??=
            gameEventBus.Subscribe<OperatingDayEndedEvent>(OnTriggerEvent);
    }
}
