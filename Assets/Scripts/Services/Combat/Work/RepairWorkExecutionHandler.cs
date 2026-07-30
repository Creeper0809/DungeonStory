using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class RepairWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Repair };
    private readonly ICombatEquipmentMaintenanceRuntime maintenanceRuntime;
    private readonly IAutomationRuntime automationRuntime;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;

    public RepairWorkExecutionHandler(
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        IWorkAmountCalculator workAmountCalculator,
        IGameClock gameClock,
        IAutomationRuntime automationRuntime = null)
    {
        this.maintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        this.workAmountCalculator = workAmountCalculator
            ?? throw new ArgumentNullException(nameof(workAmountCalculator));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.automationRuntime = automationRuntime;
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        return target != null
            && (target.IsDamaged
                || maintenanceRuntime.HasRepairWorkFor(target)
                || NeedsAutomationMaintenance(target));
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        if (target == null)
        {
            return 0f;
        }

        float urgency = maintenanceRuntime.HasRepairWorkFor(target)
            ? maintenanceRuntime.GetRepairUrgency(target)
            : target.IsDamaged ? 0.55f : 0f;
        if (TryGetAutomationMaintenance(
                target,
                out AutomationFacilitySnapshot snapshot))
        {
            float conditionUrgency = Mathf.Max(
                (85f - snapshot.Maintenance) / 85f,
                snapshot.Fault / 100f);
            urgency = Mathf.Max(
                urgency,
                Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(conditionUrgency)));
        }

        return urgency;
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (context.Target == null)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        if (maintenanceRuntime.HasRepairWorkFor(context.Target))
        {
            yield return ExecuteEquipmentRepair(context, result);
            yield break;
        }

        if (NeedsAutomationMaintenance(context.Target))
        {
            yield return ExecuteAutomationMaintenance(context, result);
            yield break;
        }

        float requiredWork = Mathf.Max(
            0.1f,
            context.Target.BuildingData.GetRequiredWork(
                BuiltInWorkTypeIds.Repair));
        float repairMultiplier =
            CharacterSkillRuntimeEffects.GetRepairSpeedMultiplier(
                context.Actor);
        yield return context.ExecuteWorkAmount(
            requiredWork,
            "수리",
            repairMultiplier);
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        context.Target.SetDamaged(false);
        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Repair,
            CharacterActivityOutcomes.Completed,
            $"수리 완료: {context.Target.name}",
            context.Target));
    }

    private IEnumerator ExecuteEquipmentRepair(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        float lastReportAt = -10f;
        while (context.CanContinue
               && maintenanceRuntime.HasRepairWorkFor(context.Target))
        {
            float deltaWork = CalculateRepairWork(context);
            if (!maintenanceRuntime.TryApplyRepairWork(
                    context.Actor,
                    context.Target,
                    deltaWork,
                    out bool completed,
                    out string message))
            {
                result.CompletedSuccessfully = false;
                context.Actor?.AddActivity(CharacterActivityEvent.Work(
                    FacilityWorkType.Repair,
                    CharacterActivityOutcomes.Blocked,
                    $"장비 수리 중단: {message}",
                    context.Target,
                    reasonCode: "equipment-repair-blocked",
                    bubbleEligible: true));
                yield break;
            }

            if (gameClock.Time - lastReportAt >= 0.75f)
            {
                lastReportAt = gameClock.Time;
                context.Actor?.Brain?.SetActionPhase(
                    message,
                    context.Target);
            }

            if (completed)
            {
                context.Actor?.AddActivity(CharacterActivityEvent.Work(
                    FacilityWorkType.Repair,
                    CharacterActivityOutcomes.Completed,
                    message,
                    context.Target,
                    reasonCode: "equipment-repair-completed",
                    bubbleEligible: true));
                yield break;
            }

            yield return null;
        }

        result.CompletedSuccessfully = false;
    }

    private IEnumerator ExecuteAutomationMaintenance(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        float lastReportAt = -10f;
        while (context.CanContinue
               && NeedsAutomationMaintenance(context.Target))
        {
            InfrastructureCommandResult command = automationRuntime.Maintain(
                context.Target,
                CalculateRepairWork(context));
            if (!command.Succeeded)
            {
                result.CompletedSuccessfully = false;
                context.Actor?.AddActivity(CharacterActivityEvent.Work(
                    FacilityWorkType.Repair,
                    CharacterActivityOutcomes.Blocked,
                    $"자동화 정비 중단: {command.Message}",
                    context.Target,
                    reasonCode: "automation-maintenance-blocked",
                    bubbleEligible: true));
                yield break;
            }

            if (gameClock.Time - lastReportAt >= 0.75f)
            {
                lastReportAt = gameClock.Time;
                context.Actor?.Brain?.SetActionPhase(
                    "자동화 설비 정비 중",
                    context.Target);
            }

            yield return null;
        }

        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Repair,
            CharacterActivityOutcomes.Completed,
            $"자동화 정비 완료: {context.Target.name}",
            context.Target,
            reasonCode: "automation-maintenance-completed",
            bubbleEligible: true));
    }

    private float CalculateRepairWork(WorkExecutionContext context)
    {
        return workAmountCalculator.CalculateWorkPerSecond(
                context.Actor,
                context.Target,
                context.WorkTypeId,
                context.Work.GetWorkEnvironmentDurationMultiplier(
                    context.WorkTypeId))
            * CharacterSkillRuntimeEffects.GetRepairSpeedMultiplier(
                context.Actor)
            * gameClock.DeltaTime;
    }

    private bool NeedsAutomationMaintenance(BuildableObject target)
    {
        return TryGetAutomationMaintenance(
            target,
            out AutomationFacilitySnapshot snapshot)
            && (snapshot.Maintenance < 85f || snapshot.Fault > 0.01f);
    }

    private bool TryGetAutomationMaintenance(
        BuildableObject target,
        out AutomationFacilitySnapshot snapshot)
    {
        snapshot = null;
        return automationRuntime != null
            && automationRuntime.TryGetFacility(target, out snapshot)
            && snapshot.Mode != AutomationMode.Manual;
    }
}
