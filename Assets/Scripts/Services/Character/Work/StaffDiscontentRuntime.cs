using System;
using System.Collections.Generic;
using System.Linq;
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
    private IDisposable operatingDayEndedSubscription;

    public StaffDiscontentState State => state;
    public StaffDiscontentRules Rules => rules;

    [Inject]
    public void Construct(
        ICharacterWorldQuery characterWorldQuery,
        DungeonStory.Foundation.IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterSettlementStandingQuery settlementStandings)
    {
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.settlementStandings = settlementStandings
            ?? throw new ArgumentNullException(nameof(settlementStandings));
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

    public void RestoreSnapshots(IEnumerable<StaffDiscontentSnapshot> savedRecords)
    {
        PublishRestoreCandidate(PrepareRestoreCandidate(savedRecords));
    }

    public StaffDiscontentRestoreCandidate PrepareRestoreCandidate(
        IEnumerable<StaffDiscontentSnapshot> savedRecords)
    {
        StaffDiscontentState restored = new StaffDiscontentState();
        restored.Restore(savedRecords);
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
            StaffRebellionResponseResult result = new StaffRebellionResponseResult(
                true,
                StaffRebellionResponseType.AutoSuppress,
                record.ToSnapshot(),
                null,
                $"자동 제압 배정: {assignedCount}명");
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

        if (!record.MarkIsolated())
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Isolate, record.ToSnapshot(), actor, "격리할 수 없습니다");
            return false;
        }

        RecordSocial(rebel, CharacterActivityOutcomes.Completed, "반란 대응: 격리", "rebellion-isolated", 0.1f);
        result = new StaffRebellionResponseResult(true, StaffRebellionResponseType.Isolate, record.ToSnapshot(), actor, "격리 완료");
        gameEventBus.RaiseStaffComplaint($"{record.DisplayName}: 격리", EventAlertImportance.Medium);
        return true;
    }

    public bool TryCalmStaff(CharacterActor staff, CharacterActor actor, out StaffRebellionResponseResult result)
    {
        if (!state.TryGetRecord(staff, out StaffDiscontentRecord record))
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Calm, null, actor, "진정할 직원 기록이 없습니다");
            return false;
        }

        float negotiationMultiplier = actor == null
            ? 1f
            : actor.GetDetailedStatMultiplier(
                "social:negotiation",
                actor.Identity?.IsOwner == true
                    ? new[] { "state:formal-status" }
                    : Array.Empty<string>());
        if (!record.TryCalm(
                staff,
                rules,
                negotiationMultiplier,
                out string failureReason))
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Calm, record.ToSnapshot(), actor, failureReason);
            return false;
        }

        RecordSocial(staff, CharacterActivityOutcomes.Completed, "반란 대응: 진정", "rebellion-calmed", 0.35f);
        result = new StaffRebellionResponseResult(true, StaffRebellionResponseType.Calm, record.ToSnapshot(), actor, "진정 완료");
        gameEventBus.RaiseStaffComplaint($"{record.DisplayName}: 진정", EventAlertImportance.Low);
        return true;
    }

    public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)
    {
        if (!state.TryGetRecord(rebel, out StaffDiscontentRecord record))
        {
            return false;
        }

        if (!record.MarkSuppressed())
        {
            return false;
        }

        StaffRebellionResponseResult result = new StaffRebellionResponseResult(
            true,
            StaffRebellionResponseType.SuppressCommand,
            record.ToSnapshot(),
            defender,
            "제압 완료");
        gameEventBus.RaiseStaffComplaint($"{record.DisplayName}: 제압 완료", EventAlertImportance.Medium);
        return true;
    }

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
