using System;
using System.Collections;
using System.Collections.Generic;

public sealed class SurgeryWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids =
    {
        BuiltInWorkTypeIds.Surgery
    };

    private readonly ISurgeryRuntime surgery;
    private readonly ISurgicalProcedureCatalog procedures;
    private readonly ISurgicalFacilityQuery facilities;

    public SurgeryWorkExecutionHandler(
        ISurgeryRuntime surgery,
        ISurgicalProcedureCatalog procedures,
        ISurgicalFacilityQuery facilities)
    {
        this.surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
        this.procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        if (!surgery.TryGetWorkFor(target, out SurgeryOrder order))
        {
            reason = "진행할 수술이 없습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.preferredDoctorId)
            && !string.Equals(
                order.preferredDoctorId,
                actor?.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            reason = "지정된 의사만 집도할 수 있습니다.";
            return false;
        }

        return true;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return surgery.HasWorkFor(target) ? 0.98f : 0f;
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (!surgery.TryReserveWork(
                context.Target,
                context.Actor,
                out SurgeryOrder order,
                out string failure))
        {
            result.CompletedSuccessfully = false;
            context.Actor?.Brain?.SetActionPhase(failure, context.Target);
            yield break;
        }

        bool applied = true;
        bool operationCompleted = false;
        float facilitySpeed = 1f;
        if (procedures.TryGet(
                order.procedureId,
                out SurgicalProcedureSO procedure))
        {
            SurgicalFacilitySnapshot snapshot = facilities.Evaluate(
                context.Target,
                procedure.RequiredFacilityTags);
            facilitySpeed = snapshot.IsAvailable
                ? snapshot.SpeedMultiplier
                : 1f;
        }

        yield return context.ExecutePersistentWorkAmount(
            order.requiredWork,
            order.completedWork,
            "수술",
            delta =>
            {
                bool succeeded = surgery.ApplyWork(
                    order.orderId,
                    context.Actor,
                    delta,
                    out bool completed,
                    out string reason);
                applied &= succeeded;
                operationCompleted |= completed;
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    context.Actor?.Brain?.SetActionPhase(reason, context.Target);
                }
                else if (surgery.TryGetOrder(order.orderId, out SurgeryOrder current))
                {
                    context.Actor?.Brain?.SetActionPhase(
                        $"{current.status} {current.Progress01 * 100f:0}%",
                        context.Target);
                }

                return succeeded;
            },
            facilitySpeed);

        if (!operationCompleted)
        {
            surgery.ReleaseDoctor(
                order.orderId,
                context.Actor,
                applied ? "작업 전환" : "수술 진행 실패");
        }

        result.CompletedSuccessfully = applied && operationCompleted;
        result.CompletionEffectsAlreadyApplied = operationCompleted;
    }
}
