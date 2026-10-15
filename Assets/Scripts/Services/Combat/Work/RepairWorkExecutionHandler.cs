using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IRepairWorkOutcomeRuntime repairOutcomes;

    public RepairWorkExecutionHandler(
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        IWorkAmountCalculator workAmountCalculator,
        IGameClock gameClock,
        IAutomationInfrastructureQuery automationQuery,
        IAutomationInfrastructureCommand automationCommands,
        IBuildingStructuralIntegrityRuntime structuralIntegrity,
        IDefenseFacilityRuntime defenseFacilities,
        IDefenseFacilityNetworkRuntime defenseNetwork,
        IRepairWorkOutcomeRuntime repairOutcomes)
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
        this.repairOutcomes = repairOutcomes
            ?? throw new ArgumentNullException(nameof(repairOutcomes));
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
        if (!TryBeginOutcome(
                context,
                result,
                RepairWorkOutcomeKind.GeneralDamage,
                context.Target.PersistentInstanceId.Value,
                boundsKnown: true,
                RepairWorkPrimaryUnit.Flag,
                1f,
                0f,
                out RepairWorkOutcomeHandle outcome,
                out CharacterId worker))
        {
            yield break;
        }
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
        repairOutcomes.RecordProgress(
            outcome,
            worker,
            requiredWork,
            boundsKnown: true,
            primaryAfter: 0f,
            secondaryAfter: 0f);
        if (!TryCompleteOutcome(
                context,
                result,
                outcome,
                context.Target.PersistentInstanceId.Value,
                RepairWorkPrimaryUnit.Flag,
                1f,
                0f,
                0f,
                0f,
                RepairWorkMaterialReceipt.None,
                requiredWork))
        {
            yield break;
        }
        WorkControlChangedEvent.Publish(context.Work, context.Actor,
            WorkControlTransition.RepairCompleted, BuiltInWorkTypeIds.Repair, context.Target);
    }

    private IEnumerator ExecuteStructuralRepair(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        float lastReportAt = -10f;
        float contributedWork = 0f;
        if (!structuralIntegrity.TryGet(
                context.Target,
                out BuildingStructuralIntegritySnapshot initial)
            || !TryBeginOutcome(
                context,
                result,
                RepairWorkOutcomeKind.StructuralIntegrity,
                context.Target.PersistentInstanceId.Value,
                boundsKnown: true,
                RepairWorkPrimaryUnit.Point,
                initial.CurrentHitPoints,
                0f,
                out RepairWorkOutcomeHandle outcome,
                out CharacterId worker))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }
        while (context.CanContinue
               && NeedsStructuralRepair(context.Target))
        {
            structuralIntegrity.TryGet(
                context.Target,
                out BuildingStructuralIntegritySnapshot before);
            float repairPerWork = Mathf.Max(
                0.01f,
                context.Target.BuildingData
                    .GetAbility<BuildingStructuralIntegrityAbility>()
                    ?.repairHitPointsPerWork ?? 1f);
            float remainingBefore = Mathf.Max(
                0f,
                (before.MaxHitPoints - before.CurrentHitPoints) / repairPerWork);
            float deltaWork = Mathf.Min(
                CalculateRepairWork(context),
                remainingBefore);
            if (!structuralIntegrity.TryApplyRepairWork(
                    context.Target,
                    deltaWork,
                    out bool completed,
                    out BuildingStructuralIntegritySnapshot snapshot))
            {
                result.CompletedSuccessfully = false;
                yield break;
            }

            context.RecordApprovedWork(
                deltaWork,
                Mathf.Max(0f, remainingBefore - deltaWork));
            contributedWork += deltaWork;
            repairOutcomes.RecordProgress(
                outcome,
                worker,
                deltaWork,
                boundsKnown: true,
                snapshot.CurrentHitPoints,
                0f);

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
                if (!TryCompleteOutcome(
                        context,
                        result,
                        outcome,
                        context.Target.PersistentInstanceId.Value,
                        RepairWorkPrimaryUnit.Point,
                        initial.CurrentHitPoints,
                        snapshot.CurrentHitPoints,
                        0f,
                        0f,
                        RepairWorkMaterialReceipt.None,
                        contributedWork))
                {
                    yield break;
                }
                WorkControlChangedEvent.Publish(context.Work, context.Actor,
                    WorkControlTransition.StructuralRepairCompleted,
                    BuiltInWorkTypeIds.Repair, context.Target);
                yield break;
            }

            if (context.TrySuspendAtCheckpoint())
            {
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
        DefenseFacilitySnapshot initial = snapshot;
        float contributedWork = 0f;
        if (!TryBeginOutcome(
                context,
                result,
                RepairWorkOutcomeKind.DefenseFacility,
                context.Target.PersistentInstanceId.Value,
                boundsKnown: true,
                RepairWorkPrimaryUnit.Percent,
                initial.Condition,
                DefenseStateValue(initial.OperationalState),
                out RepairWorkOutcomeHandle outcome,
                out CharacterId worker))
        {
            yield break;
        }
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
            result.Failure = jamFailure;
            yield break;
        }
        if (snapshot.OperationalState == DefenseFacilityOperationalState.Jammed)
        {
            snapshot = defenseFacilities.GetSnapshot(facility);
            repairOutcomes.RecordProgress(
                outcome,
                worker,
                0f,
                boundsKnown: true,
                snapshot.Condition,
                DefenseStateValue(snapshot.OperationalState));
        }

        while (context.CanContinue)
        {
            snapshot = defenseFacilities.GetSnapshot(facility);
            if (snapshot.Condition >= 100f)
            {
                context.Target.SetDamaged(false);
                TryCompleteOutcome(
                    context,
                    result,
                    outcome,
                    context.Target.PersistentInstanceId.Value,
                    RepairWorkPrimaryUnit.Percent,
                    initial.Condition,
                    snapshot.Condition,
                    DefenseStateValue(initial.OperationalState),
                    DefenseStateValue(snapshot.OperationalState),
                    RepairWorkMaterialReceipt.None,
                    contributedWork);
                yield break;
            }

            float conditionBefore = snapshot.Condition;
            float repair = Mathf.Min(
                100f - conditionBefore,
                Mathf.Max(
                0.01f,
                    CalculateRepairWork(context) * 2f));
            if (!defenseFacilities.TryRepair(
                    facility,
                    repair,
                    out DomainFailure repairFailure))
            {
                context.Actor?.Brain?.SetActionPhase(
                    repairFailure.Code.ToString(),
                    context.Target);
                result.CompletedSuccessfully = false;
                result.Failure = repairFailure;
                yield break;
            }


            float approvedWork = repair * 0.5f;
            context.RecordApprovedWork(
                approvedWork,
                Mathf.Max(0f, (100f - conditionBefore - repair) * 0.5f));
            contributedWork += approvedWork;
            snapshot = defenseFacilities.GetSnapshot(facility);
            repairOutcomes.RecordProgress(
                outcome,
                worker,
                approvedWork,
                boundsKnown: true,
                snapshot.Condition,
                DefenseStateValue(snapshot.OperationalState));

            if (conditionBefore + repair < 100f
                && context.TrySuspendAtCheckpoint())
            {
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
        float contributedWork = 0f;
        RepairWorkOutcomeHandle outcome = default;
        CharacterId worker = default;
        while (context.CanContinue
               && maintenanceRuntime.HasRepairWorkFor(context.Target))
        {
            CombatEquipmentRepairOrder order = maintenanceRuntime.Orders
                .Where(candidate => candidate != null
                    && candidate.facilityBuildingId
                        == context.Target.PersistentInstanceId.Value
                    && candidate.state is CombatEquipmentRepairOrderState.Ready
                        or CombatEquipmentRepairOrderState.InProgress)
                .OrderBy(candidate => candidate.orderId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (order == null)
            {
                result.CompletedSuccessfully = false;
                yield break;
            }
            if (!outcome.IsValid)
            {
                if (!TryBeginOutcome(
                        context,
                        result,
                        RepairWorkOutcomeKind.EquipmentDurability,
                        order.equipmentInstanceId,
                        boundsKnown: false,
                        RepairWorkPrimaryUnit.Ratio,
                        0f,
                        0f,
                        out outcome,
                        out worker))
                {
                    yield break;
                }
            }
            float multiplier = Mathf.Max(
                0.1f,
                context.Target.BuildingData
                    .GetAbility<BuildingEquipmentMaintenanceAbility>()
                    ?.workSpeedMultiplier ?? 1f);
            float remainingBefore = order == null
                ? 0f
                : Mathf.Max(
                    0f,
                    (order.requiredWork - order.completedWork) / multiplier);
            float deltaWork = Mathf.Min(
                CalculateRepairWork(context),
                remainingBefore);
            float completedBefore = order.completedWork;
            bool applied = maintenanceRuntime.TryApplyRepairWork(
                    context.Actor,
                    context.Target,
                    deltaWork,
                    out bool completed,
                    out string message);
            if (!maintenanceRuntime.TryGetOrder(
                    order.orderId,
                    out CombatEquipmentRepairOrder updatedOrder))
            {
                result.CompletedSuccessfully = false;
                context.Actor?.Brain?.SetActionPhase(
                    "equipment.repair.order_state_missing",
                    context.Target);
                yield break;
            }
            float acceptedWork = Mathf.Max(
                0f,
                (updatedOrder.completedWork - completedBefore) / multiplier);
            if (acceptedWork > 0f)
            {
                context.RecordApprovedWork(
                    acceptedWork,
                    Mathf.Max(0f, remainingBefore - acceptedWork));
                repairOutcomes.RecordProgress(
                    outcome,
                    worker,
                    acceptedWork,
                    boundsKnown: false,
                    0f,
                    0f);
                contributedWork += acceptedWork;
            }
            if (!applied)
            {
                result.CompletedSuccessfully = false;
                WorkControlChangedEvent.Publish(context.Work, context.Actor,
                    WorkControlTransition.EquipmentRepairRejected,
                    BuiltInWorkTypeIds.Repair, context.Target, detail: message);
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
                RepairWorkMaterialReceipt material =
                    CreateEquipmentMaterialReceipt(updatedOrder);
                if (!TryCompleteOutcome(
                        context,
                        result,
                        outcome,
                        updatedOrder.equipmentInstanceId,
                        RepairWorkPrimaryUnit.Ratio,
                        updatedOrder.repairDurabilityBefore,
                        updatedOrder.repairDurabilityAfter,
                        0f,
                        0f,
                        material,
                        contributedWork))
                {
                    yield break;
                }
                WorkControlChangedEvent.Publish(context.Work, context.Actor,
                    WorkControlTransition.EquipmentRepairCompleted,
                    BuiltInWorkTypeIds.Repair, context.Target, detail: message);
                yield break;
            }

            if (context.TrySuspendAtCheckpoint())
            {
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
        float contributedWork = 0f;
        if (!automationQuery.TryGetFacility(
                context.Target,
                out AutomationFacilitySnapshot initial)
            || !TryBeginOutcome(
                context,
                result,
                RepairWorkOutcomeKind.AutomationMaintenance,
                context.Target.PersistentInstanceId.Value,
                boundsKnown: true,
                RepairWorkPrimaryUnit.Percent,
                initial.Maintenance,
                initial.Fault,
                out RepairWorkOutcomeHandle outcome,
                out CharacterId worker))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }
        while (context.CanContinue
               && NeedsAutomationMaintenance(context.Target))
        {
            automationQuery.TryGetFacility(
                context.Target,
                out AutomationFacilitySnapshot before);
            float remainingBefore = Mathf.Max(
                Mathf.Max(0f, 85f - before.Maintenance),
                Mathf.Max(0f, before.Fault * 2f));
            float deltaWork = Mathf.Min(
                CalculateRepairWork(context),
                remainingBefore);
            InfrastructureCommandResult command = automationCommands.Maintain(
                context.Target,
                deltaWork);
            if (!command.Succeeded)
            {
                result.CompletedSuccessfully = false;
                result.Failure = command.Failure;
                result.ObserveFailureSource(() => automationQuery.Version);
                WorkControlChangedEvent.Publish(context.Work, context.Actor,
                    WorkControlTransition.AutomationMaintenanceRejected,
                    BuiltInWorkTypeIds.Repair, context.Target, detail: command.Failure.Code.ToString());
                yield break;
            }


            context.RecordApprovedWork(
                deltaWork,
                Mathf.Max(0f, remainingBefore - deltaWork));
            contributedWork += deltaWork;
            if (!automationQuery.TryGetFacility(
                    context.Target,
                    out AutomationFacilitySnapshot after))
            {
                result.CompletedSuccessfully = false;
                yield break;
            }
            repairOutcomes.RecordProgress(
                outcome,
                worker,
                deltaWork,
                boundsKnown: true,
                after.Maintenance,
                after.Fault);

            if (NeedsAutomationMaintenance(context.Target)
                && context.TrySuspendAtCheckpoint())
            {
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

        if (!automationQuery.TryGetFacility(
                context.Target,
                out AutomationFacilitySnapshot terminal)
            || !TryCompleteOutcome(
                context,
                result,
                outcome,
                context.Target.PersistentInstanceId.Value,
                RepairWorkPrimaryUnit.Percent,
                initial.Maintenance,
                terminal.Maintenance,
                initial.Fault,
                terminal.Fault,
                RepairWorkMaterialReceipt.None,
                contributedWork))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        WorkControlChangedEvent.Publish(context.Work, context.Actor,
            WorkControlTransition.AutomationMaintenanceCompleted,
            BuiltInWorkTypeIds.Repair, context.Target);
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

    private bool TryBeginOutcome(
        WorkExecutionContext context,
        WorkExecutionResult result,
        RepairWorkOutcomeKind kind,
        string targetEntityId,
        bool boundsKnown,
        RepairWorkPrimaryUnit primaryUnit,
        float primaryBefore,
        float secondaryBefore,
        out RepairWorkOutcomeHandle handle,
        out CharacterId worker)
    {
        handle = default;
        worker = default;
        if (context.Target == null
            || !context.Target.PersistentInstanceId.IsValid
            || !CharacterPersistentIdentity.TryGet(context.Actor, out worker))
        {
            result.CompletedSuccessfully = false;
            return false;
        }
        handle = repairOutcomes.BeginOrResume(
            kind,
            context.Target.PersistentInstanceId.Value,
            targetEntityId,
            kind == RepairWorkOutcomeKind.EquipmentDurability
                ? string.Empty
                : BuildingDefinitionId(context.Target),
            worker,
            AbsoluteDay(context),
            boundsKnown,
            primaryUnit,
            primaryBefore,
            secondaryBefore);
        return handle.IsValid;
    }

    private bool TryCompleteOutcome(
        WorkExecutionContext context,
        WorkExecutionResult result,
        RepairWorkOutcomeHandle handle,
        string targetEntityId,
        RepairWorkPrimaryUnit primaryUnit,
        float primaryBefore,
        float primaryAfter,
        float secondaryBefore,
        float secondaryAfter,
        in RepairWorkMaterialReceipt material,
        float contributionWork)
    {
        if (!CharacterPersistentIdentity.TryGet(context.Actor, out CharacterId completionWorker))
        {
            result.CompletedSuccessfully = false;
            return false;
        }
        bool committed = repairOutcomes.TryComplete(
            handle,
            completionWorker,
            context.CommandOrigin,
            contributionWork,
            AbsoluteDay(context),
            boundsKnown: true,
            primaryUnit,
            primaryBefore,
            primaryAfter,
            secondaryBefore,
            secondaryAfter,
            material,
            out string resultKey,
            out string failureReason);
        // The saved repair owner is authoritative even when the ledger commit
        // is temporarily pending. Never emit a conflicting generic identity.
        result.CompletionIdentityAlreadyPublished = true;
        if (!committed)
        {
            result.CompletedSuccessfully = false;
            context.Actor?.Brain?.SetActionPhase(
                failureReason,
                context.Target);
            return false;
        }
        result.SetTerminalReference(
            "repair-work-outcome",
            resultKey,
            targetEntityId,
            Mathf.Max(0f, contributionWork));
        return true;
    }

    private static RepairWorkMaterialReceipt CreateEquipmentMaterialReceipt(
        CombatEquipmentRepairOrder order)
    {
        if (order == null || string.IsNullOrEmpty(order.materialTransferOperationId))
            return RepairWorkMaterialReceipt.None;
        RepairWorkMaterialSourceSaveData[] sources =
            (order.materialTransferInputs
                ?? new List<EquipmentRepairMaterialTransferInput>())
            .Where(value => value != null)
            .Select(value => new RepairWorkMaterialSourceSaveData
            {
                stackId = value.sourceStackId,
                itemId = value.itemId,
                quantity = value.quantity
            })
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        return new RepairWorkMaterialReceipt(
            order.materialTransferOperationId,
            order.materialTransferCommitId,
            order.materialTransferMassGrams,
            sources);
    }

    private static float DefenseStateValue(
        DefenseFacilityOperationalState state) => (float)(int)state;

    private static string BuildingDefinitionId(BuildableObject target) =>
        target?.BuildingData == null
            ? string.Empty
            : "building-definition:"
                + target.BuildingData.id.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

    private static int AbsoluteDay(WorkExecutionContext context) =>
        Mathf.Max(1, context.Work?.GameCalendar?.Day ?? 1);
}
