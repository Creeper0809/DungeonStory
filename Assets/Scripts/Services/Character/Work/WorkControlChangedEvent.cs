using System;
using DungeonStory.Foundation.Facts;

public enum WorkControlTransition
{
    PriorityWorkAssigned, PrioritySuppressionAssigned, SuppressionRejected,
    SuppressionMovementUnavailable, InterceptionRejected, InterceptionStarted,
    SuppressionStarted, SuppressionTargetUnreachable, SuppressionApplied,
    DutySuppressionResumed, DutyPriorityWorkResumed, DutyConditionRecovered,
    DutyOffDutyStarted, DutyExpeditionPrepared, DutyShiftEnded, DutyRoutineNeedResumed,
    SelectionDutyBlocked, SelectionUrgentDutyResumed, SelectionPriorityTargetCancelled,
    RepairCompleted, StructuralRepairCompleted, EquipmentRepairRejected,
    EquipmentRepairCompleted, AutomationMaintenanceRejected, AutomationMaintenanceCompleted,
    AssignedWorkStarted, AssignedWorkStopped, WorkAmountFinished, RestockRunFinished,
    RoutineShiftFinished
}

/// <summary>
/// A work command/duty/repair notification, captured at the owning transition.
/// It carries domain values only; activity and narrative policy belong to observers.
/// </summary>
public readonly struct WorkControlChangedEvent : ICharacterIdentityEvent
{
    public WorkControlChangedEvent(WorkControlTransition transition, CharacterId character,
        WorkTypeId workTypeId = default, string targetId = "", string targetName = "",
        int targetDefinitionId = -1, string targetRuntimeName = "", string workLabel = "",
        string detail = null, float amount = 0f, bool state = false)
    {
        if (!Enum.IsDefined(typeof(WorkControlTransition), transition) || !character.IsValid
            || !Text(targetId) || !Text(targetName) || !Text(targetRuntimeName) || !Text(workLabel)
            || detail != null && !Text(detail) || !float.IsFinite(amount) || amount < 0f)
            throw new ArgumentException("work-control-event-invalid");
        Transition = transition; Character = character; WorkTypeId = workTypeId;
        TargetId = targetId; TargetName = targetName; TargetDefinitionId = targetDefinitionId;
        TargetRuntimeName = targetRuntimeName; WorkLabel = workLabel; Detail = detail;
        Amount = amount; State = state;
    }

    public string EventId => "event:work-control-changed";
    public int AbsoluteDay => 0;
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.Autonomous;
    public WorkControlTransition Transition { get; }
    public CharacterId Character { get; }
    public WorkTypeId WorkTypeId { get; }
    public string TargetId { get; }
    public string TargetName { get; }
    public int TargetDefinitionId { get; }
    public string TargetRuntimeName { get; }
    public string WorkLabel { get; }
    public string Detail { get; }
    public float Amount { get; }
    // Actual terminal state for suppression; current off-duty state for selection.
    public bool State { get; }

    public static void PublishAssigned(CharacterActor actor,
        WorkControlTransition transition, string detail = null)
    {
        if (actor == null || !actor.TryGetAbility(out AbilityWork work)
            || work.AssignedWorkType == FacilityWorkType.None || work.assignedShop == null)
            return;
        WorkTypeId workTypeId = FacilityWorkTypeMap.TryGet(
            work.AssignedWorkType, out WorkTypeDefinition definition)
                ? definition.WorkTypeId : new WorkTypeId($"work:{(int)work.AssignedWorkType}");
        Publish(work, actor, transition, workTypeId, work.assignedShop,
            work.AssignedWorkDisplayName, detail);
    }

    public static void Publish(AbilityWork work, CharacterActor actor,
        WorkControlTransition transition, WorkTypeId workTypeId = default,
        BuildableObject target = null, string workLabel = "", string detail = null,
        float amount = 0f, bool state = false)
    {
        if (actor == null) return;
        if (work == null) throw new InvalidOperationException("work-control-owner-missing");
        work.PublishControlEvent(new WorkControlChangedEvent(transition,
            CharacterPersistentIdentity.Require(actor), workTypeId,
            target == null ? string.Empty : target.RequirePersistentInstanceId().Value,
            target == null ? string.Empty : target.BuildingData != null
                && !string.IsNullOrWhiteSpace(target.BuildingData.objectName)
                    ? target.BuildingData.objectName : target.name,
            target == null ? -1 : target.id,
            target == null ? string.Empty : target.name,
            workLabel ?? string.Empty, detail, amount, state));
    }

    public static void PublishToCharacter(AbilityWork work, CharacterActor actor,
        WorkControlTransition transition, CharacterActor target, string detail = null,
        float amount = 0f, bool state = false, bool requireTargetIdentity = true)
    {
        if (actor == null) return;
        if (work == null) throw new InvalidOperationException("work-control-owner-missing");
        string targetId = target == null ? string.Empty : requireTargetIdentity
            ? CharacterPersistentIdentity.Require(target).Value
            : target.Identity?.PersistentId ?? string.Empty;
        work.PublishControlEvent(new WorkControlChangedEvent(transition,
            CharacterPersistentIdentity.Require(actor), targetId: targetId,
            targetName: target == null ? string.Empty : target.Identity?.DisplayName ?? target.name,
            targetRuntimeName: target == null ? string.Empty : target.name,
            detail: detail, amount: amount, state: state));
    }

    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 16384, true);
}
