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
    private readonly IAutomationInfrastructureQuery automationQuery;
    private readonly IAutomationInfrastructureCommand automationCommands;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;
    private readonly IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private readonly IDefenseFacilityRuntime defenseFacilities;
    private readonly IDefenseFacilityNetworkRuntime defenseNetwork;

    public RepairWorkExecutionHandler(
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        IWorkAmountCalculator workAmountCalculator,
        IGameClock gameClock,
        IAutomationInfrastructureQuery automationQuery,
        IAutomationInfrastructureCommand automationCommands,
        IBuildingStructuralIntegrityRuntime structuralIntegrity,
        IDefenseFacilityRuntime defenseFacilities,
        IDefenseFacilityNetworkRuntime defenseNetwork)
    {
        this.maintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        this.workAmountCalculator = workAmountCalculator
            ?? throw new ArgumentNullException(nameof(workAmountCalculator));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.automationQuery = automationQuery
            ?? throw new ArgumentNullException(nameof(automationQuery));
        this.automationCommands = automationCommands
            ?? throw new ArgumentNullException(nameof(automationCommands));
        this.structuralIntegrity = structuralIntegrity;
        this.defenseFacilities = defenseFacilities;
        this.defenseNetwork = defenseNetwork;
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
                || NeedsStructuralRepair(target)
                || NeedsDefenseFacilityMaintenance(target)
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
        if (structuralIntegrity != null
            && structuralIntegrity.TryGet(
                target,
                out BuildingStructuralIntegritySnapshot structural)
            && structural.IntegrityRatio < 1f)
        {
            urgency = Mathf.Max(
                urgency,
                Mathf.Lerp(
                    0.45f,
                    1f,
                    1f - structural.IntegrityRatio));
        }
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

        if (target is DefenseFacility defense
            && defenseFacilities != null)
        {
            DefenseFacilitySnapshot facility =
                defenseFacilities.GetSnapshot(defense);
            if (facility.OperationalState
                    == DefenseFacilityOperationalState.Jammed
                || facility.Condition < 100f)
            {
                float coverageBonus =
                    defenseNetwork?.HasMaintenanceCoverage(defense) == true
                        ? 0.15f
                        : 0f;
                urgency = Mathf.Max(
                    urgency,
                    Mathf.Clamp01(
                        0.45f
                        + (100f - facility.Condition) / 100f
                        + coverageBonus));
            }
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

        if (NeedsStructuralRepair(context.Target))
        {
            yield return ExecuteStructuralRepair(context, result);
            yield break;
        }

        if (NeedsDefenseFacilityMaintenance(context.Target))
        {
            yield return ExecuteDefenseFacilityMaintenance(context, result);
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

    private IEnumerator ExecuteStructuralRepair(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        float lastReportAt = -10f;
        while (context.CanContinue
               && NeedsStructuralRepair(context.Target))
        {
            float deltaWork = CalculateRepairWork(context);
            if (!structuralIntegrity.TryApplyRepairWork(
                    context.Target,
                    deltaWork,
                    out bool completed,
                    out BuildingStructuralIntegritySnapshot snapshot))
            {
                result.CompletedSuccessfully = false;
                yield break;
            }

            if (gameClock.Time - lastReportAt >= 0.75f)
            {
                lastReportAt = gameClock.Time;
                context.Actor?.Brain?.SetActionPhase(
                    $"구조 수리 {snapshot.CurrentHitPoints:0}/{snapshot.MaxHitPoints:0}",
                    context.Target);
            }

            if (completed)
            {
                context.Target.SetDamaged(false);
                context.Actor?.AddActivity(CharacterActivityEvent.Work(
                    FacilityWorkType.Repair,
                    CharacterActivityOutcomes.Completed,
                    $"구조 수리 완료: {context.Target.name}",
                    context.Target,
                    reasonCode: "structural-repair-completed",
                    bubbleEligible: true));
                yield break;
            }

            yield return null;
        }

        result.CompletedSuccessfully = false;
    }

    private IEnumerator ExecuteDefenseFacilityMaintenance(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (context.Target is not DefenseFacility facility
            || defenseFacilities == null)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        DefenseFacilitySnapshot snapshot =
            defenseFacilities.GetSnapshot(facility);
        if (snapshot.OperationalState
            == DefenseFacilityOperationalState.Jammed
            && !defenseFacilities.TryClearJam(
                facility,
                out DomainFailure jamFailure))
        {
            context.Actor?.Brain?.SetActionPhase(
                jamFailure.Code.ToString(),
                context.Target);
            result.CompletedSuccessfully = false;
            yield break;
        }

        while (context.CanContinue)
        {
            snapshot = defenseFacilities.GetSnapshot(facility);
            if (snapshot.Condition >= 100f)
            {
                context.Target.SetDamaged(false);
                yield break;
            }

            float repair = Mathf.Max(
                0.01f,
                CalculateRepairWork(context) * 2f);
            if (!defenseFacilities.TryRepair(
                    facility,
                    repair,
                    out DomainFailure repairFailure))
            {
                context.Actor?.Brain?.SetActionPhase(
                    repairFailure.Code.ToString(),
                    context.Target);
                result.CompletedSuccessfully = false;
                yield break;
            }

            context.Actor?.Brain?.SetActionPhase(
                $"방어시설 정비 {snapshot.Condition:0}%",
                context.Target);
            yield return null;
        }

        result.CompletedSuccessfully = false;
    }

    private bool NeedsDefenseFacilityMaintenance(
        BuildableObject target)
    {
        if (target is not DefenseFacility facility
            || defenseFacilities == null)
        {
            return false;
        }

        DefenseFacilitySnapshot snapshot =
            defenseFacilities.GetSnapshot(facility);
        return snapshot.OperationalState
                == DefenseFacilityOperationalState.Jammed
            || snapshot.Condition < 100f;
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
            InfrastructureCommandResult command = automationCommands.Maintain(
                context.Target,
                CalculateRepairWork(context));
            if (!command.Succeeded)
            {
                result.CompletedSuccessfully = false;
                context.Actor?.AddActivity(CharacterActivityEvent.Work(
                    FacilityWorkType.Repair,
                    CharacterActivityOutcomes.Blocked,
                    $"자동화 정비 중단: {command.Failure.Code}",
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

    private bool NeedsStructuralRepair(BuildableObject target)
    {
        return structuralIntegrity != null
            && structuralIntegrity.TryGet(
                target,
                out BuildingStructuralIntegritySnapshot snapshot)
            && snapshot.CurrentHitPoints < snapshot.MaxHitPoints - 0.001f;
    }

    private bool TryGetAutomationMaintenance(
        BuildableObject target,
        out AutomationFacilitySnapshot snapshot)
    {
        snapshot = null;
        return automationQuery.TryGetFacility(target, out snapshot)
            && snapshot.Mode != AutomationMode.Manual;
    }
}
