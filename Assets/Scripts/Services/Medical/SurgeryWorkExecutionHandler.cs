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

    private readonly ISurgeryQuery surgery;
    private readonly ISurgeryWorkCommand commands;
    private readonly ISurgicalProcedureCatalog procedures;
    private readonly ISurgicalFacilityQuery facilities;

    public SurgeryWorkExecutionHandler(
        ISurgeryQuery surgery,
        ISurgeryWorkCommand commands,
        ISurgicalProcedureCatalog procedures,
        ISurgicalFacilityQuery facilities)
    {
        this.surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
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
            reason = FailureCode.SurgeryOrderMissing.ToString();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.preferredDoctorId)
            && !string.Equals(
                order.preferredDoctorId,
                actor?.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            reason = FailureCode.SurgeryPreferredDoctorOnly.ToString();
            return false;
        }

        if (!surgery.CanOperate(
                order,
                actor,
                out DomainFailure failure))
        {
            reason = failure.Code.ToString();
            return false;
        }

        return true;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        if (!surgery.TryGetWorkFor(target, out SurgeryOrder order)
            || !procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure))
        {
            return 0f;
        }

        return procedure.Urgency switch
        {
            MedicalProcedureUrgency.Emergency => 1f,
            MedicalProcedureUrgency.Required => 0.98f,
            MedicalProcedureUrgency.Elective => 0.78f,
            MedicalProcedureUrgency.Maintenance => 0.62f,
            _ => 0.75f
        };
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (!commands.TryReserveWork(
                context.Target,
                context.Actor,
                out SurgeryOrder order,
                out DomainFailure failure))
        {
            result.CompletedSuccessfully = false;
            context.Actor?.Brain?.SetActionPhase(
                failure.Code.ToString(),
                context.Target);
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
            BuiltInWorkTypeIds.Surgery.Value,
            delta =>
            {
                bool succeeded = commands.ApplyWork(
                    order.orderId,
                    context.Actor,
                    delta,
                    out bool completed,
                    out DomainFailure failure);
                applied &= succeeded;
                operationCompleted |= completed;
                if (failure.IsFailure)
                {
                    context.Actor?.Brain?.SetActionPhase(
                        failure.Code.ToString(),
                        context.Target);
                }
                else if (surgery.TryGetOrder(order.orderId, out SurgeryOrder current))
                {
                    context.Actor?.Brain?.SetActionPhase(
                        $"{current.statusData?.code.ToString() ?? SurgeryStatusCode.None.ToString()} "
                        + $"{current.Progress01 * 100f:0}%",
                        context.Target);
                }

                return succeeded;
            },
            facilitySpeed);

        if (!operationCompleted)
        {
            commands.ReleaseDoctor(
                order.orderId,
                context.Actor);
        }

        result.CompletedSuccessfully = applied && operationCompleted;
        result.CompletionEffectsAlreadyApplied = operationCompleted;
    }
}
