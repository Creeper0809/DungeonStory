using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied work-control notifications into the existing character
/// activity presentation. Command, duty, selection, and repair state remain
/// owned by the publisher.
/// </summary>
public sealed class WorkControlActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable subscription;

    public WorkControlActivityObserver(
        IGameEventBus events,
        ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<WorkControlChangedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static CharacterActivityEvent Project(
        in WorkControlChangedEvent value) => value.Transition switch
    {
        WorkControlTransition.PriorityWorkAssigned => Work(
            value,
            CharacterActivityOutcomes.Changed,
            $"우선 작업 지정: {value.TargetRuntimeName} - {value.WorkLabel}",
            "priority-command"),
        WorkControlTransition.PrioritySuppressionAssigned =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Command,
                CharacterActivityOutcomes.Changed,
                $"우선 제압 지정: {value.TargetRuntimeName}",
                actionId: "command:suppress",
                targetId: value.TargetId,
                targetName: value.TargetRuntimeName),
        WorkControlTransition.SuppressionRejected =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Failed,
                $"제압 실패: {value.Detail}",
                actionId: "combat:suppress",
                targetId: value.TargetId,
                targetName: value.TargetRuntimeName,
                reasonCode: value.Detail,
                sentiment: -0.7f,
                bubbleEligible: true),
        WorkControlTransition.SuppressionMovementUnavailable =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Failed,
                "제압 실패: 이동 정보 없음",
                actionId: "combat:suppress",
                reasonCode: "missing-movement",
                sentiment: -0.7f,
                bubbleEligible: true),
        WorkControlTransition.InterceptionRejected =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Failed,
                $"저지 실패: {value.Detail ?? "방어 지휘 체계 없음"}",
                actionId: "combat:suppress",
                targetId: value.TargetId,
                targetName: value.TargetName,
                reasonCode: value.Detail,
                sentiment: -0.7f,
                bubbleEligible: true),
        WorkControlTransition.InterceptionStarted =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Started,
                $"{value.TargetName} 저지하러 이동",
                actionId: "defense:manual-intercept",
                targetId: value.TargetId,
                targetName: value.TargetName),
        WorkControlTransition.SuppressionStarted =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Started,
                $"제압 시작: {value.TargetRuntimeName}",
                actionId: "combat:suppress",
                targetId: value.TargetId,
                targetName: value.TargetRuntimeName),
        WorkControlTransition.SuppressionTargetUnreachable =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Blocked,
                "제압 실패: 도달할 수 없는 대상",
                actionId: "combat:suppress",
                targetId: value.TargetId,
                targetName: value.TargetRuntimeName,
                reasonCode: "target-unreachable",
                sentiment: -0.7f,
                bubbleEligible: true),
        WorkControlTransition.SuppressionApplied =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                value.State
                    ? CharacterActivityOutcomes.Completed
                    : CharacterActivityOutcomes.Progress,
                value.State
                    ? $"제압 완료: {value.TargetRuntimeName}"
                    : $"저항을 누르는 중: {value.TargetRuntimeName}",
                actionId: "combat:suppress",
                targetId: value.TargetId,
                targetName: value.TargetRuntimeName,
                value: value.Amount,
                sentiment: value.State ? 0.2f : -0.3f),
        WorkControlTransition.DutySuppressionResumed =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Responded,
                "비번 중 제압 명령 대응",
                actionId: "command:suppress"),
        WorkControlTransition.DutyPriorityWorkResumed =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Responded,
                "비번 중 우선 작업 대응",
                actionId: "command:priority-work"),
        WorkControlTransition.DutyConditionRecovered =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Returned,
                "근무 복귀",
                reasonCode: "condition-recovered",
                sentiment: 0.3f),
        WorkControlTransition.DutyOffDutyStarted =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Departed,
                string.IsNullOrWhiteSpace(value.Detail)
                    ? "비번 시작"
                    : $"비번 시작: {value.Detail}",
                reasonCode: value.Detail,
                sentiment: -0.2f),
        WorkControlTransition.DutyExpeditionPrepared =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Changed,
                "원정 준비: 작업 해제",
                reasonCode: "expedition-preparation"),
        WorkControlTransition.DutyShiftEnded =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Changed,
                $"근무 교대: {value.Detail}",
                reasonCode: value.Detail),
        WorkControlTransition.DutyRoutineNeedResumed => Work(
            value,
            CharacterActivityOutcomes.Returned,
            "생리 욕구를 해결해 중단했던 작업으로 복귀 준비",
            "routine-need-resume"),
        WorkControlTransition.SelectionDutyBlocked =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Blocked,
                value.State
                    ? "작업 보류: 비번"
                    : "작업 보류: 피로/기분 보호",
                reasonCode: value.State
                    ? "off-duty"
                    : "wellbeing-protection",
                sentiment: -0.2f,
                bubbleEligible: true),
        WorkControlTransition.SelectionUrgentDutyResumed =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Responded,
                "비번 중 긴급 작업 합류",
                reasonCode: "urgent-work"),
        WorkControlTransition.SelectionPriorityTargetCancelled => Work(
            value,
            CharacterActivityOutcomes.Cancelled,
            "우선 작업 취소: 대상 사용 불가",
            "target-unavailable",
            bubbleEligible: true),
        WorkControlTransition.RepairCompleted => Work(
            value,
            CharacterActivityOutcomes.Completed,
            $"수리 완료: {value.TargetRuntimeName}"),
        WorkControlTransition.StructuralRepairCompleted => Work(
            value,
            CharacterActivityOutcomes.Completed,
            $"구조 수리 완료: {value.TargetRuntimeName}",
            "structural-repair-completed",
            bubbleEligible: true),
        WorkControlTransition.EquipmentRepairRejected => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            $"장비 수리 중단: {value.Detail}",
            "equipment-repair-blocked",
            bubbleEligible: true),
        WorkControlTransition.EquipmentRepairCompleted => Work(
            value,
            CharacterActivityOutcomes.Completed,
            value.Detail,
            "equipment-repair-completed",
            bubbleEligible: true),
        WorkControlTransition.AutomationMaintenanceRejected => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            $"자동화 정비 중단: {value.Detail}",
            "automation-maintenance-blocked",
            bubbleEligible: true),
        WorkControlTransition.AutomationMaintenanceCompleted => Work(
            value,
            CharacterActivityOutcomes.Completed,
            $"자동화 정비 완료: {value.TargetRuntimeName}",
            "automation-maintenance-completed",
            bubbleEligible: true),
        WorkControlTransition.AssignedWorkStarted => Work(
            value,
            CharacterActivityOutcomes.Started,
            $"작업 시작 · {WorkContext(value)}"),
        WorkControlTransition.AssignedWorkStopped => WorkEnded(
            value,
            value.Detail),
        WorkControlTransition.WorkAmountFinished => WorkEnded(
            value,
            "작업량 완료"),
        WorkControlTransition.RestockRunFinished => WorkEnded(
            value,
            "보충 완료"),
        WorkControlTransition.RoutineShiftFinished => WorkEnded(
            value,
            $"근무 교대 · {value.Detail}"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value.Transition,
            "Unknown work-control transition.")
    };

    private void Observe(WorkControlChangedEvent value)
    {
        CharacterActor actor = world.AllCharacters.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId id)
            && id.Equals(value.Character));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "work-control-activity-character-missing:"
                + value.Character.Value);
        }

        actor.AddActivity(Project(value));
    }

    private static CharacterActivityEvent Work(
        in WorkControlChangedEvent source,
        string outcome,
        string text,
        string reason = "",
        bool bubbleEligible = false)
    {
        string workId = WorkTypeCatalog.TryGet(
                source.WorkTypeId,
                out WorkTypeDefinition definition)
            ? definition.Id
            : source.WorkTypeId.IsValid
                ? source.WorkTypeId.Value
                : "work:unknown";
        return CharacterActivityEvent.Create(
            CharacterActivityKinds.Work,
            outcome,
            text,
            actionId: workId,
            targetId: source.TargetId,
            targetName: source.TargetName,
            reasonCode: reason,
            facilityId: source.TargetDefinitionId,
            sentiment: Sentiment(outcome),
            narrativeEligible: true,
            bubbleEligible: bubbleEligible);
    }

    private static CharacterActivityEvent WorkEnded(
        in WorkControlChangedEvent source,
        string reason)
    {
        string message = $"작업 종료 · {WorkContext(source)}";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            message += $" · {reason}";
        }

        return Work(
            source,
            CharacterActivityOutcomes.Completed,
            message,
            reason);
    }

    private static string WorkContext(in WorkControlChangedEvent source) =>
        $"{source.WorkLabel} · {source.TargetName}";

    private static float Sentiment(string outcome) => outcome switch
    {
        CharacterActivityOutcomes.Completed or
            CharacterActivityOutcomes.Returned => 0.45f,
        CharacterActivityOutcomes.Failed or
            CharacterActivityOutcomes.Blocked or
            CharacterActivityOutcomes.Damaged => -0.7f,
        _ => 0f
    };
}
