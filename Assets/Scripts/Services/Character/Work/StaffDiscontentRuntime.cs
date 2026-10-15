using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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
    private StaffDiscontentPublicationRuntime publications;
    private bool commandActive;
    private float nextPublicationRetry;
    private IGameCalendar calendar;
    private IDisposable operatingDayEndedSubscription;

    public StaffDiscontentState State => state;
    public StaffDiscontentRules Rules => rules;
    public long OutcomeRevision => state.OutcomeRevision;
    public long TransitionRevision => state.TransitionRevision;

    [Inject]
    public void Construct(
        ICharacterWorldQuery characterWorldQuery,
        DungeonStory.Foundation.IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterSettlementStandingQuery settlementStandings,
        IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery,
        IGameCalendar calendar)
    {
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.settlementStandings = settlementStandings
            ?? throw new ArgumentNullException(nameof(settlementStandings));
        publications = new StaffDiscontentPublicationRuntime(facts, recovery);
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        SubscribeToScopedEvents();
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        ProcessAllStaff();
    }

    public StaffDiscontentRecord ProcessStaff(CharacterActor staff, out StaffDiscontentOutcome outcome)
    {
        outcome = StaffDiscontentOutcome.None;
        if (commandActive) throw new InvalidOperationException("staff-discontent-command-active");
        commandActive = true;
        try
        {
            var pending = state.TransitionObservation;
            if (!RecoverAndNotify(out string recoveryFailure)) throw new InvalidOperationException(recoveryFailure);
            if (settlementStandings?.IsMinion(staff) == true || !StaffDiscontentService.IsTrackableStaff(staff)) return null;
            if (pending.pending && pending.fact.absoluteDay == CurrentAbsoluteDay
                && pending.fact.after.staffId == StaffDiscontentService.GetStaffId(staff))
            {
                outcome = pending.fact.transition;
                state.TryGetRecord(pending.fact.after.staffId, out var recovered);
                return recovered;
            }
            int absoluteDay = CurrentAbsoluteDay;
            Action rollback = CaptureRollback();
            var before = state.TryGetRecord(staff, out var existing) ? existing.ToSnapshot() : CreateInitialDailySnapshot(staff);
            var lifecycleBefore = staff.CurrentLifecycleState;
            IDomainFactStage stage = null;
            bool preserveApplied = false;
            try
            {
                var record = state.ProcessStaff(staff, rules, absoluteDay, out outcome);
                if (record == null || outcome == StaffDiscontentOutcome.None) return record;
                var data = new StaffDiscontentFactData
                {
                    kind = StaffDiscontentFactKind.DailyTransition, revision = state.GetNextTransitionRevision(),
                    absoluteDay = absoluteDay, transition = outcome,
                    before = StaffDiscontentFactValues.Capture(before),
                    after = StaffDiscontentFactValues.Capture(record.ToSnapshot(outcome))
                };
                if (!publications.TryStage(data, out stage, out string failure))
                    throw new InvalidOperationException("staff-discontent-transition-prepare:" + failure);
                if (outcome == StaffDiscontentOutcome.PermanentDeparture)
                    staff.Lifecycle?.SetLifecycleState(CharacterLifecycleState.Despawned);
                var delivery = publications.CommitApplied(state, data, stage);
                preserveApplied = delivery.MayBeCommitted;
                if (!preserveApplied) throw new InvalidOperationException("staff-discontent-transition-rejected:" + delivery.Detail);
                if (!delivery.Completed) Debug.LogWarning("staff-discontent-transition-publication-pending:" + delivery.Detail);
                NotifyDelivered();
                return record;
            }
            catch (Exception exception) when (IsRecoverableOutcomeException(exception))
            {
                if (!preserveApplied)
                {
                    rollback();
                    if (staff.CurrentLifecycleState != lifecycleBefore) staff.Lifecycle?.SetLifecycleState(lifecycleBefore);
                    outcome = StaffDiscontentOutcome.None;
                }
                throw new InvalidOperationException("Staff-discontent transition failed.", exception);
            }
            finally { Cancel(stage); }
        }
        finally { commandActive = false; }
    }

    public IReadOnlyList<StaffDiscontentSnapshot> CaptureSnapshots()
    { ValidateCapture(); return state.CaptureSnapshots(); }
    public StaffDiscontentObservationData ResponseObservation { get { ValidateCapture(); return state.ResponseObservation; } }
    public StaffDiscontentObservationData TransitionObservation { get { ValidateCapture(); return state.TransitionObservation; } }

    public void RestoreSnapshots(IEnumerable<StaffDiscontentSnapshot> records, long outcomeRevision = 0L,
        long transitionRevision = 0L, StaffDiscontentObservationData responseObservation = null,
        StaffDiscontentObservationData transitionObservation = null) =>
        PublishRestoreCandidate(PrepareRestoreCandidate(records, outcomeRevision, transitionRevision, responseObservation, transitionObservation));

    public StaffDiscontentRestoreCandidate PrepareRestoreCandidate(IEnumerable<StaffDiscontentSnapshot> records,
        long outcomeRevision = 0L, long transitionRevision = 0L, StaffDiscontentObservationData responseObservation = null,
        StaffDiscontentObservationData transitionObservation = null)
    {
        ValidateCapture();
        var restored = new StaffDiscontentState();
        restored.Restore(records, outcomeRevision, transitionRevision, responseObservation, transitionObservation);
        return new StaffDiscontentRestoreCandidate(restored);
    }
    public void PublishRestoreCandidate(StaffDiscontentRestoreCandidate candidate)
    {
        ValidateCapture();
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
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
            gameEventBus.Publish(new StaffRebellionGuardsAssignedFact(record.DisplayName, assignedCount));
        }

        return assignedCount;
    }

    public bool TryIsolateRebel(CharacterActor rebel, CharacterActor actor, out StaffRebellionResponseResult result) =>
        TryExecuteResponse(rebel, actor, StaffRebellionResponseType.Isolate, out result);

    public bool TryCalmStaff(CharacterActor staff, CharacterActor actor, out StaffRebellionResponseResult result) =>
        TryExecuteResponse(staff, actor, StaffRebellionResponseType.Calm, out result);

    public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender) =>
        TryExecuteResponse(rebel, defender, StaffRebellionResponseType.SuppressCommand, out _);

    private bool TryExecuteResponse(CharacterActor staff, CharacterActor actor, StaffRebellionResponseType type,
        out StaffRebellionResponseResult result)
    {
        result = FailedResponse(type, null, actor, "staff-discontent-command-active");
        if (commandActive) return false;
        commandActive = true;
        try
        {
            var pending = state.ResponseObservation;
            if (!RecoverAndNotify(out string failure))
            { result = FailedResponse(type, null, actor, failure); return false; }
            string staffId = StaffDiscontentService.GetStaffId(staff);
            if (pending.pending && pending.fact.after.staffId == staffId && pending.fact.responseType == type)
            {
                result = new StaffRebellionResponseResult(true, type, StaffDiscontentFactValues.Restore(pending.fact.after), actor, "이전 대응 결과 전달 완료");
                return true;
            }
            if (!state.TryGetRecord(staff, out var record)
                || type != StaffRebellionResponseType.Calm && !record.IsInLocalRebellion)
            { result = FailedResponse(type, null, actor, "대응할 직원 기록이나 반란 대상을 찾을 수 없습니다."); return false; }
            if (!TryCaptureResponder(actor, out string responderId, out string responderName, out failure))
            { result = FailedResponse(type, record.ToSnapshot(), actor, failure); return false; }
            var before = record.ToSnapshot();
            Action rollback = CaptureRollback();
            var moodBefore = type == StaffRebellionResponseType.Calm ? staff?.Stats?.CaptureMoodDeliveryTransactionState() : null;
            IDomainFactStage stage = null;
            bool preserveApplied = false;
            try
            {
                bool changed;
                bool moodApplied = false;
                float resolvedMoodDelta = 0f;
                if (type == StaffRebellionResponseType.Calm)
                {
                    float negotiation = actor == null ? 1f : actor.GetDetailedStatMultiplier("social:negotiation",
                        actor.Identity?.IsOwner == true ? new[] { "state:formal-status" } : Array.Empty<string>());
                    changed = record.TryCalm(staff, rules, negotiation, out failure, out moodApplied, out resolvedMoodDelta);
                }
                else
                {
                    changed = type == StaffRebellionResponseType.Isolate ? record.MarkIsolated() : record.MarkSuppressed();
                    failure = changed ? string.Empty : "현재 직원 상태에는 대응을 적용할 수 없습니다.";
                }
                if (!changed)
                { result = FailedResponse(type, before, actor, failure); return false; }
                var after = record.ToSnapshot();
                var data = new StaffDiscontentFactData
                {
                    kind = StaffDiscontentFactKind.Response, revision = state.GetNextOutcomeRevision(),
                    absoluteDay = CurrentAbsoluteDay, responseType = type,
                    responderId = responderId, responderDisplayName = responderName,
                    moodApplied = moodApplied, resolvedMoodDelta = resolvedMoodDelta,
                    before = StaffDiscontentFactValues.Capture(before), after = StaffDiscontentFactValues.Capture(after)
                };
                if (!publications.TryStage(data, out stage, out failure))
                    throw new InvalidOperationException("staff-response-prepare:" + failure);
                var delivery = publications.CommitApplied(state, data, stage);
                preserveApplied = delivery.MayBeCommitted;
                if (!preserveApplied) throw new InvalidOperationException("staff-response-rejected:" + delivery.Detail);
                NotifyDelivered();
                result = new StaffRebellionResponseResult(true, type, after, actor,
                    delivery.Completed ? type switch
                    {
                        StaffRebellionResponseType.Calm => "진정 완료",
                        StaffRebellionResponseType.Isolate => "격리 완료",
                        _ => "제압 완료"
                    } : "대응 적용됨 · 결과 전달 대기: " + delivery.Detail);
                return true;
            }
            catch (Exception exception) when (IsRecoverableOutcomeException(exception))
            {
                if (!preserveApplied)
                {
                    rollback();
                    if (moodBefore != null) staff?.Stats?.RestoreMoodDeliveryTransactionState(moodBefore, publish: false);
                }
                result = FailedResponse(type, before, actor, "staff-response-transaction:" + exception.Message);
                return false;
            }
            finally { Cancel(stage); }
        }
        finally { commandActive = false; }
    }

    private static bool TryCaptureResponder(CharacterActor actor, out string id, out string name, out string failure)
    {
        id = string.Empty; name = string.Empty; failure = string.Empty;
        if (actor == null) return true;
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId identity))
        { failure = "staff-response-responder-id-missing"; return false; }
        id = identity.Value; name = StaffDiscontentService.GetStaffDisplayName(actor, id)?.Trim() ?? string.Empty;
        if (!StaffDiscontentFactValues.Name(name)) { failure = "staff-response-responder-name-invalid"; return false; }
        return true;
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

    public bool TryRecoverPendingPublications(out string failure)
    {
        if (commandActive) { failure = "staff-discontent-command-active"; return false; }
        commandActive = true;
        try { return RecoverAndNotify(out failure); }
        finally { commandActive = false; }
    }
    private bool RecoverAndNotify(out string failure)
    {
        if (!publications.TryRecover(state, out failure)) return false;
        NotifyDelivered();
        return true;
    }
    private void NotifyDelivered()
    {
        foreach (var kind in new[] { StaffDiscontentFactKind.DailyTransition, StaffDiscontentFactKind.Response })
        {
            var fact = state.TakePublicationNotification(kind);
            if (fact == null) continue;
            // Auxiliary activity/UI notifications were already best-effort. Take once before callbacks;
            // replaying a canonical fact never repeats the producing mood/lifecycle mutation.
            try
            {
                if (kind == StaffDiscontentFactKind.DailyTransition) gameEventBus.Publish(new StaffDiscontentTransitionedFact(fact));
                else gameEventBus.Publish(new StaffRebellionResponseResolvedFact(fact));
            }
            catch (Exception exception) when (IsRecoverableOutcomeException(exception)) { Debug.LogException(exception); }
            if (kind == StaffDiscontentFactKind.DailyTransition
                && state.TryGetRecord(fact.after.staffId, out var record)
                && record.IsInLocalRebellion && !record.IsIsolated && !record.IsSuppressed)
            {
                var staff = RequireCharacterWorldQuery().Characters.FirstOrDefault(value => value != null
                    && StaffDiscontentService.GetStaffId(value) == fact.after.staffId);
                if (staff != null) DispatchAutoSuppress(staff);
            }
        }
    }
    private void Update()
    {
        if (publications == null || commandActive || !(state.HasPendingPublication || state.HasPendingNotification) || Time.unscaledTime < nextPublicationRetry) return;
        nextPublicationRetry = Time.unscaledTime + 1f;
        if (!TryRecoverPendingPublications(out string failure)) Debug.LogWarning(failure);
    }
    private Action CaptureRollback()
    {
        var records = state.CaptureSnapshots();
        long responseRevision = state.OutcomeRevision, transitionRevision = state.TransitionRevision;
        var response = state.ResponseObservation;
        var transition = state.TransitionObservation;
        return () => state.Restore(records, responseRevision, transitionRevision, response, transition);
    }
    private void ValidateCapture()
    { if (commandActive) throw new InvalidOperationException("staff-discontent-command-active"); }
    private static void Cancel(IDomainFactStage stage)
    {
        try { stage?.Cancel(); }
        catch (Exception exception) when (IsRecoverableOutcomeException(exception)) { Debug.LogWarning("staff-discontent-cancel:" + exception); }
    }
    private int CurrentAbsoluteDay => calendar.Day;
    private static bool IsRecoverableOutcomeException(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;

    private static StaffDiscontentSnapshot CreateInitialDailySnapshot(
        CharacterActor staff)
    {
        string staffId = StaffDiscontentService.GetStaffId(staff);
        return new StaffDiscontentSnapshot(
            staffId,
            StaffDiscontentService.GetStaffDisplayName(staff, staffId),
            StaffDiscontentStage.Stable,
            StaffDiscontentOutcome.None,
            StaffDiscontentService.GetMood(staff),
            0,
            permanentLoss: false,
            departed: false,
            localRebellion: false,
            ownerThreat: false,
            isolated: false,
            suppressed: false,
            lastProcessedAbsoluteDay: -1);
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
