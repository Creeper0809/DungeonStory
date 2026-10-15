using System;
using DungeonStory.Foundation.Facts;

public enum WorkExecutionTransition
{
    MovementUnavailable, ApproachProtectionUnavailable, ApproachedOrderReplaced,
    WorkProtectionUnavailable, PaidFacilityRejected, RoutineNeedSuspended,
    TargetUnreachable, RestockTargetUnsupported, RestockPlanRejected,
    RestockSourceApproach, RestockLoadProgress, RestockStockUnavailable,
    RestockDeliveryPathUnavailable, RestockFinished, ConstructionWorkforceRejected,
    MaterialsPending, WorkOrderRejected, ProgressAdvanced, WorkOrderCompleted,
    PersistentProgressAdvanced, RoutineNeedInterrupted, EnvironmentEvacuation,
    EnvironmentReassignment, EmergencySuspended, WorkplaceUnavailable,
    PriorityChanged, Cancelled, ResearchRejected, ResearchProgressed, ResearchCompleted
}

// A synchronous notification of the work owner's decision or applied progress.
// It does not choose a log kind/outcome, UI text, memory importance or bubble.
// Durable work effects and completion delivery remain with their existing owners.
public readonly struct WorkExecutionChangedEvent : ICharacterIdentityEvent
{
    public WorkExecutionChangedEvent(WorkExecutionTransition transition, CharacterId character,
        WorkTypeId workTypeId, string targetId = "", string targetName = "", int targetDefinitionId = -1,
        string workLabel = "", string detail = "", string sourceName = "", string itemName = "",
        string destinationName = "", float progress = 0f, int quantity = 0, int requestedQuantity = 0,
        WorkPriorityLevel priority = WorkPriorityLevel.Off, int absoluteDay = 0,
        CharacterCommandOrigin origin = CharacterCommandOrigin.Autonomous)
    {
        if (!Enum.IsDefined(typeof(WorkExecutionTransition), transition) || !character.IsValid
            || transition != WorkExecutionTransition.RoutineNeedInterrupted && !workTypeId.IsValid
            || !Text(targetId) || !Text(targetName) || !Text(workLabel) || !Text(detail)
            || !Text(sourceName) || !Text(itemName) || !Text(destinationName)
            || !float.IsFinite(progress) || progress < 0f || progress > 1f
            || quantity < 0 || requestedQuantity < 0 || absoluteDay < 0
            || !Enum.IsDefined(typeof(WorkPriorityLevel), priority)
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), origin))
            throw new ArgumentException("work-execution-event-invalid");
        Transition = transition; Character = character; WorkTypeId = workTypeId;
        TargetId = targetId; TargetName = targetName; TargetDefinitionId = targetDefinitionId;
        WorkLabel = workLabel; Detail = detail; SourceName = sourceName; ItemName = itemName;
        DestinationName = destinationName; Progress = progress; Quantity = quantity;
        RequestedQuantity = requestedQuantity; Priority = priority; AbsoluteDay = absoluteDay; Origin = origin;
    }
    public string EventId => "event:work-execution-changed";
    public WorkExecutionTransition Transition { get; }
    public CharacterId Character { get; }
    public WorkTypeId WorkTypeId { get; }
    public string TargetId { get; }
    public string TargetName { get; }
    public int TargetDefinitionId { get; }
    public string WorkLabel { get; }
    public string Detail { get; }
    public string SourceName { get; }
    public string ItemName { get; }
    public string DestinationName { get; }
    public float Progress { get; }
    public int Quantity { get; }
    public int RequestedQuantity { get; }
    public WorkPriorityLevel Priority { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin { get; }

    public static void Publish(CharacterIdentityEventPublisher events, CharacterActor actor,
        WorkExecutionTransition transition, FacilityWorkType workType, BuildableObject target = null,
        string workLabel = "", string detail = "", string sourceName = "", string itemName = "",
        string destinationName = "", float progress = 0f, int quantity = 0, int requestedQuantity = 0,
        WorkPriorityLevel priority = WorkPriorityLevel.Off, int absoluteDay = 0,
        CharacterCommandOrigin origin = CharacterCommandOrigin.Autonomous)
    {
        WorkTypeId workTypeId = FacilityWorkTypeMap.TryGet(workType, out var definition)
            ? definition.WorkTypeId : new WorkTypeId($"work:{(int)workType}");
        Publish(events, actor, transition, workTypeId, target, workLabel, detail,
            sourceName, itemName, destinationName, progress, quantity, requestedQuantity, priority, absoluteDay, origin);
    }

    public static void Publish(CharacterIdentityEventPublisher events, CharacterActor actor,
        WorkExecutionTransition transition, WorkTypeId workTypeId, BuildableObject target = null,
        string workLabel = "", string detail = "", string sourceName = "", string itemName = "",
        string destinationName = "", float progress = 0f, int quantity = 0, int requestedQuantity = 0,
        WorkPriorityLevel priority = WorkPriorityLevel.Off, int absoluteDay = 0,
        CharacterCommandOrigin origin = CharacterCommandOrigin.Autonomous)
    {
        if (actor == null) return;
        if (events == null) throw new InvalidOperationException("work-execution-event-publisher-missing");
        events.Publish(new WorkExecutionChangedEvent(transition, CharacterPersistentIdentity.Require(actor), workTypeId,
            target == null ? string.Empty : target.RequirePersistentInstanceId().Value,
            target == null ? string.Empty : target.BuildingData != null && !string.IsNullOrWhiteSpace(target.BuildingData.objectName)
                ? target.BuildingData.objectName : target.name,
            target == null ? -1 : target.id,
            workLabel ?? string.Empty, detail ?? string.Empty, sourceName ?? string.Empty,
            itemName ?? string.Empty, destinationName ?? string.Empty,
            progress, quantity, requestedQuantity, priority, absoluteDay, origin));
    }

    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 16384, allowEmpty: true);
}
