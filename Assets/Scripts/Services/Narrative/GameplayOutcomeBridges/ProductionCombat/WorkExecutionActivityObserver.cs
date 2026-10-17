using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied work execution notifications into the existing character
/// activity presentation. Work ownership and durable outcomes remain with the
/// producer that emitted the notification.
/// </summary>
public sealed class WorkExecutionActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable subscription;

    public WorkExecutionActivityObserver(
        IGameEventBus events,
        ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<WorkExecutionChangedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static CharacterActivityEvent Project(
        in WorkExecutionChangedEvent value) => value.Transition switch
    {
        WorkExecutionTransition.ResearchRejected => Work(
            value, CharacterActivityOutcomes.Failed, $"연구 실패: {value.Detail}",
            value.Detail, bubbleEligible: true),
        WorkExecutionTransition.ResearchProgressed => Work(
            value, CharacterActivityOutcomes.Progress, $"연구 진행: {value.WorkLabel}",
            "research-progress", value: value.Progress),
        WorkExecutionTransition.ResearchCompleted => Work(
            value, CharacterActivityOutcomes.Completed, $"연구 완료: {value.WorkLabel}",
            "blueprint-completed", value: value.Progress),
        WorkExecutionTransition.MovementUnavailable => Work(
            value,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 이동 정보 없음",
            "missing-movement",
            bubbleEligible: true),
        WorkExecutionTransition.ApproachProtectionUnavailable => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            "이동 전 환경 보호 의복을 준비할 수 없어 작업을 시작하지 못했습니다.",
            "environment-apparel-approach-blocked",
            bubbleEligible: true),
        WorkExecutionTransition.ApproachedOrderReplaced => Work(
            value,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 이동 중 작업 주문이 취소되거나 교체되었습니다.",
            "work-order-invalidated-during-approach",
            bubbleEligible: true),
        WorkExecutionTransition.WorkProtectionUnavailable => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            "환경 보호 의복을 준비할 수 없어 작업을 시작하지 못했습니다.",
            "environment-apparel-work-start-blocked",
            bubbleEligible: true),
        WorkExecutionTransition.PaidFacilityRejected => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            $"{value.WorkLabel} 중단: {value.Detail}",
            "paid-facility-order",
            bubbleEligible: true),
        WorkExecutionTransition.RoutineNeedSuspended => Work(
            value,
            CharacterActivityOutcomes.Changed,
            "생리 욕구 해결 후 재개하도록 작업 진행 상태 보존",
            "routine-need-suspended"),
        WorkExecutionTransition.TargetUnreachable => Work(
            value,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 작업 도달 실패",
            "target-unreachable",
            bubbleEligible: true),
        WorkExecutionTransition.RestockTargetUnsupported => Work(
            value,
            CharacterActivityOutcomes.Failed,
            "보충 실패: 재고를 받을 수 없는 시설",
            "target-not-restockable",
            bubbleEligible: true),
        WorkExecutionTransition.RestockPlanRejected => Work(
            value,
            CharacterActivityOutcomes.Failed,
            $"보충 실패: {value.Detail}",
            value.Detail,
            bubbleEligible: true),
        WorkExecutionTransition.RestockSourceApproach => Work(
            value,
            CharacterActivityOutcomes.Progress,
            $"보충 이동: {value.SourceName} -> {value.DestinationName}",
            "moving-to-stock"),
        WorkExecutionTransition.RestockLoadProgress => Work(
            value,
            CharacterActivityOutcomes.Progress,
            $"보충 적재: {value.ItemName} {value.Quantity}/{value.RequestedQuantity}",
            "loading-stock",
            quantity: value.Quantity),
        WorkExecutionTransition.RestockStockUnavailable => Work(
            value,
            CharacterActivityOutcomes.Failed,
            "보충 실패: 창고 재고 부족",
            "warehouse-stock-shortage",
            bubbleEligible: true),
        WorkExecutionTransition.RestockDeliveryPathUnavailable => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            "보충 실패: 상점 경로 없음",
            "shop-path-missing",
            bubbleEligible: true),
        WorkExecutionTransition.RestockFinished => Work(
            value,
            value.Quantity > 0
                ? CharacterActivityOutcomes.Completed
                : CharacterActivityOutcomes.Failed,
            value.Quantity > 0
                ? $"보충 완료: {value.DestinationName} {value.Detail}"
                : $"보충 실패: {value.Detail}",
            value.Detail,
            quantity: value.Quantity,
            bubbleEligible: value.Quantity <= 0),
        WorkExecutionTransition.ConstructionWorkforceRejected => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            $"{value.WorkLabel} 중단: {value.Detail}",
            "construction-workforce-blocked",
            bubbleEligible: true),
        WorkExecutionTransition.MaterialsPending => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            $"{value.WorkLabel} 대기: 재료가 아직 도착하지 않음",
            "waiting-for-materials",
            value: value.Progress),
        WorkExecutionTransition.WorkOrderRejected => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            $"{value.WorkLabel} 중단: {value.Detail}",
            "work-order-blocked",
            bubbleEligible: true),
        WorkExecutionTransition.ProgressAdvanced => Work(
            value,
            CharacterActivityOutcomes.Progress,
            $"{value.WorkLabel} 진행 {RoundPercent(value.Progress)}%",
            "work-progress",
            value: value.Progress),
        WorkExecutionTransition.WorkOrderCompleted => Work(
            value,
            CharacterActivityOutcomes.Completed,
            $"{value.WorkLabel} 완료",
            "work-order-completed",
            value: 1f),
        WorkExecutionTransition.PersistentProgressAdvanced => Work(
            value,
            CharacterActivityOutcomes.Progress,
            $"{value.WorkLabel} 진행 {RoundPercent(value.Progress)}%",
            "persistent-work-progress",
            value: value.Progress),
        WorkExecutionTransition.RoutineNeedInterrupted =>
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Changed,
                $"생리 욕구로 작업 중단: {value.Detail}",
                reasonCode: "routine-need-interrupt"),
        WorkExecutionTransition.EnvironmentEvacuation => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            value.Detail,
            "environment-evacuation",
            bubbleEligible: true),
        WorkExecutionTransition.EnvironmentReassignment => Work(
            value,
            CharacterActivityOutcomes.Blocked,
            value.Detail,
            "environment-reassignment",
            bubbleEligible: true),
        WorkExecutionTransition.EmergencySuspended => Work(
            value,
            CharacterActivityOutcomes.Changed,
            "비상 대응을 위해 진행 상태를 보존하고 작업을 일시중단했습니다.",
            "suspended-for-emergency"),
        WorkExecutionTransition.WorkplaceUnavailable => Work(
            value,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 작업장 없음",
            "no-workplace",
            bubbleEligible: true),
        WorkExecutionTransition.PriorityChanged => Work(
            value,
            CharacterActivityOutcomes.Changed,
            $"{value.WorkLabel} 우선순위: {value.Priority.ToDisplayText()}",
            $"priority:{(int)value.Priority}",
            value: (int)value.Priority,
            includeTarget: false),
        WorkExecutionTransition.Cancelled => Work(
            value,
            CharacterActivityOutcomes.Cancelled,
            $"작업 종료: {value.Detail}",
            value.Detail),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value.Transition,
            "Unknown work execution transition.")
    };

    private void Observe(WorkExecutionChangedEvent value)
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
                "work-execution-activity-character-missing:"
                + value.Character.Value);
        }
        actor.AddActivity(Project(value));
    }

    private static CharacterActivityEvent Work(
        in WorkExecutionChangedEvent source,
        string outcome,
        string text,
        string reason,
        float value = 0f,
        int quantity = 0,
        bool bubbleEligible = false,
        bool includeTarget = true)
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
            targetId: includeTarget ? source.TargetId : string.Empty,
            targetName: includeTarget ? source.TargetName : string.Empty,
            reasonCode: reason,
            value: value,
            quantity: quantity,
            facilityId: includeTarget ? source.TargetDefinitionId : -1,
            sentiment: Sentiment(outcome),
            narrativeEligible: true,
            bubbleEligible: bubbleEligible);
    }

    private static int RoundPercent(float value) =>
        (int)Math.Round(value * 100f, MidpointRounding.ToEven);

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
