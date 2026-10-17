using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>
/// Owns the per-run accident latch and the authoritative damage/fact transaction.
/// The durable accident journal stays in the existing work-order observation owner.
/// </summary>
internal sealed class WorkAccidentExecutionRuntime
{
    private const float BaseAccidentHazardPerApprovedWorkUnit = 0.001f;
    private const float WorkAccidentDamage = 2f;
    private readonly AbilityWork work;
    private readonly IWorkOrderRuntime workOrderRuntime;
    private readonly IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private readonly IGameCalendar calendar;
    private readonly IRandomStream workAccidentRandom;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly ICharacterPerformanceQuery performance;
    private readonly CharacterWorkPerformanceContextResolver performanceContext;
    private readonly IAnatomyHealthRuntime anatomyHealth;
    private readonly ICharacterBodyHealthMutationTransaction bodyHealthMutation;
    private readonly IDomainFactPublisher accidentFacts;
    private readonly WorkAccidentObservationRuntime workAccidentObservations;

    internal WorkAccidentExecutionRuntime(AbilityWork work, WorkTaskExecutionDependencies execution,
        IGameCalendar calendar, IRandomStream workAccidentRandom, CharacterIdentityEventPublisher identityEvents,
        ICharacterPerformanceQuery performance, CharacterWorkPerformanceContextResolver performanceContext,
        IAnatomyHealthRuntime anatomyHealth, ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        IDomainFactPublisher accidentFacts, WorkAccidentObservationRuntime workAccidentObservations)
    {
        this.work = work;
        workOrderRuntime = execution.WorkOrderRuntime;
        structuralIntegrity = execution.StructuralIntegrity;
        this.calendar = calendar;
        this.workAccidentRandom = workAccidentRandom;
        this.identityEvents = identityEvents;
        this.performance = performance;
        this.performanceContext = performanceContext;
        this.anatomyHealth = anatomyHealth;
        this.bodyHealthMutation = bodyHealthMutation;
        this.accidentFacts = accidentFacts;
        this.workAccidentObservations = workAccidentObservations;
    }

    internal bool Occurred { get; private set; }
    internal string LastFailure => workAccidentObservations.LastFailure;
    internal void BeginRun() => Occurred = false;

    internal bool TryTriggerWorkAccident(
        CharacterActor actor,
        float approvedWork,
        WorkTypeId approvedWorkTypeId,
        int currentRunId)
    {
        if (Occurred
            || actor == null
            || workAccidentRandom == null
            || approvedWork <= 0f)
        {
            return false;
        }

        if (!approvedWorkTypeId.IsValid)
            throw new InvalidOperationException(
                "Work accident execution requires the approved work type captured before progress commit.");
        if (!ReferenceEquals(actor, work.WorkerActor)
            || approvedWorkTypeId != work.AssignedWorkTypeId)
            throw new InvalidOperationException(
                "Work accident execution requires approved work from the exact assigned actor and work type.");
        if (performance == null || performanceContext == null)
            throw new InvalidOperationException(
                "Work accident execution requires the character performance query.");
        if (structuralIntegrity == null)
            throw new InvalidOperationException(
                "Work accident execution requires the building structural integrity runtime.");
        BuildableObject assignedTarget = work.assignedShop;
        if (!performanceContext.TryResolve(
                actor,
                assignedTarget,
                approvedWorkTypeId,
                out ProficiencyWorkProfile profile,
                out string failureReason))
            throw new InvalidOperationException(failureReason);
        CharacterPerformanceSnapshot accident = performance.EvaluateWork(
            actor,
            approvedWorkTypeId,
            CharacterPerformanceResultChannel.AccidentRisk,
            performanceContext.BuildEvaluationContext(
                profile,
                actor.Stats.BuildWorkEffectContext(approvedWorkTypeId)));
        if (!accident.IsApplicable)
            throw new InvalidOperationException(
                accident.Failure?.Message
                ?? $"Work accident performance '{approvedWorkTypeId.Value}' is unavailable.");
        BuildingStructuralIntegritySnapshot structuralSnapshot = default;
        bool hasFacilityIntegrity = assignedTarget != null
            && structuralIntegrity.TryGet(
                assignedTarget,
                out structuralSnapshot)
            && ReferenceEquals(structuralSnapshot.Building, assignedTarget);
        WorkAccidentRiskContext riskContext =
            performanceContext.ResolveAccidentRiskContext(
                actor,
                approvedWorkTypeId,
                hasFacilityIntegrity,
                hasFacilityIntegrity
                    ? structuralSnapshot.IntegrityRatio
                    : 1f);
        float performanceMultiplier = Mathf.Max(0f, accident.Value);
        float accidentMultiplier = performanceMultiplier
            * riskContext.CombinedMultiplier;
        float chance = 1f - Mathf.Exp(
            -BaseAccidentHazardPerApprovedWorkUnit
            * approvedWork
            * accidentMultiplier);
        string actorId = CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId characterId)
            ? characterId.Value
            : "unavailable";
        string targetId = assignedTarget != null
            && assignedTarget.PersistentInstanceId.IsValid
            ? assignedTarget.PersistentInstanceId.Value
            : assignedTarget != null
                ? $"runtime:{assignedTarget.GetInstanceID()}"
                : "none";
        CharacterPerformanceExecutionTrace.Record(
            accident.FormulaId,
            "WorkTaskExecutor.TryTriggerWorkAccident",
            approvedWork,
            chance,
            FormattableString.Invariant(
                $"actor={actorId};workType={approvedWorkTypeId.Value};target={targetId};run={currentRunId};performanceMultiplier={performanceMultiplier:0.######};{riskContext.ObservationDetail};combinedMultiplier={accidentMultiplier:0.######}"));
        if (!workAccidentRandom.Chance(Mathf.Clamp01(chance)))
            return false;

        if (identityEvents == null)
            throw new InvalidOperationException(
                "Work accident execution requires the typed identity-event publisher before damage can commit.");
        CharacterId committedWorkerId = CharacterPersistentIdentity.Require(actor);
        if (assignedTarget == null
            || !assignedTarget.PersistentInstanceId.IsValid)
            throw new InvalidOperationException(
                "Work accident execution requires the exact persistent work facility before damage can commit.");
        if (workOrderRuntime is not IWorkAccidentOperationIdAuthority
                operationIds)
        {
            throw new InvalidOperationException(
                "Work accident execution requires the persistent work-operation ID authority before damage can commit.");
        }
        workAccidentObservations.RequireReady(committedWorkerId.Value);
        string workOperationId = operationIds.AllocateWorkAccidentOperationId(
            committedWorkerId,
            approvedWorkTypeId,
            assignedTarget.PersistentInstanceId);
        if (string.IsNullOrWhiteSpace(workOperationId))
        {
            throw new InvalidOperationException(
                "Work accident execution received an empty persistent work-operation ID before damage could commit.");
        }

        AnatomyNodeHealthState[] eligibleNodes = anatomyHealth
            .GetAnatomySnapshot(actor)
            .Nodes
            .Where(value => value != null
                && !value.missing
                && value.currentHealth > 0f)
            .OrderBy(value => value.nodeId, StringComparer.Ordinal)
            .ToArray();
        if (eligibleNodes.Length == 0)
            throw new InvalidOperationException(
                $"Work accident cannot resolve an anatomy node for '{actor.name}'.");
        AnatomyNodeHealthState injured = eligibleNodes[
            workAccidentRandom.NextInt(0, eligibleNodes.Length)];
        float nodeHealthBefore = injured.currentHealth;
        float expectedAppliedDamage = Mathf.Min(nodeHealthBefore, WorkAccidentDamage);
        string workerDisplayName = actor.Identity?.DisplayName?.Trim() ?? string.Empty;
        string facilityDisplayName = assignedTarget.BuildingData?.objectName?.Trim()
            ?? string.Empty;
        if (workerDisplayName.Length == 0 || facilityDisplayName.Length == 0)
            throw new InvalidOperationException(
                "Work accident requires immutable worker and facility display snapshots.");
        var snapshot = new WorkAccidentFactSaveData
        {
            operationId = workOperationId, workerId = committedWorkerId.Value,
            workerDisplayName = workerDisplayName, facilityId = assignedTarget.PersistentInstanceId.Value,
            facilityDisplayName = facilityDisplayName, workTypeId = approvedWorkTypeId.Value,
            anatomyNodeId = injured.nodeId, appliedDamage = expectedAppliedDamage,
            observedCause = riskContext.ObservationDetail, accidentMultiplier = accidentMultiplier,
            x = assignedTarget.centerPos.x, y = assignedTarget.centerPos.y,
            absoluteDay = calendar?.Day ?? 0, commandOrigin = (int)ResolveCommandOrigin()
        };
        WorkAccidentObservationRuntime.CaptureFirePlan(snapshot, assignedTarget);
        var proposed = new WorkAccidentFact(snapshot);
        if (!accidentFacts.TryReserve(proposed, out var lease, out string failure))
            throw new InvalidOperationException("work-accident-observer-reservation-failed:" + failure);
        CharacterBodyHealthMutationSnapshot bodyBefore = default;
        bool capturedBody = false;
        bool appliedRecorded = false;
        bool mayBeCommitted = false;
        try
        {
            bodyBefore = bodyHealthMutation.CaptureCombatMutation(actor);
            capturedBody = true;
            if (!anatomyHealth.TryDamageNode(actor, injured.nodeId, WorkAccidentDamage,
                    bleeding: 0f, reason: "work-accident"))
                throw new InvalidOperationException("work-accident-damage-failed:" + injured.nodeId);
            var after = anatomyHealth.GetAnatomySnapshot(actor).Nodes.FirstOrDefault(value =>
                value != null && string.Equals(value.nodeId, injured.nodeId, StringComparison.Ordinal));
            float appliedDamage = after == null ? 0f : Mathf.Max(0f, nodeHealthBefore - after.currentHealth);
            if (appliedDamage <= 0f || !Mathf.Approximately(appliedDamage, expectedAppliedDamage))
                throw new InvalidOperationException("work-accident-authoritative-damage-mismatch");
            snapshot.appliedDamage = appliedDamage;
            var actual = new WorkAccidentFact(snapshot);
            workAccidentObservations.RecordApplied(actual, lease.PublicationKey);
            appliedRecorded = true;
            var delivery = accidentFacts.Publish(lease, actual);
            mayBeCommitted = delivery.MayBeCommitted;
            if (!mayBeCommitted)
                throw new InvalidOperationException("work-accident-observation-rejected:" + delivery.Detail);
            // No rollback after an ambiguous publication; the frozen fact survives
            // both executor lifetime and whole-world restore in work.orders.
            Occurred = true;
            work.isWorking = false;
            workAccidentObservations.MarkDelivery(workOperationId, delivery);
        }
        catch
        {
            if (!mayBeCommitted && capturedBody)
            {
                void Rollback() => bodyHealthMutation.RestoreCombatMutation(
                    actor, bodyBefore, "work-accident-observation-rollback");
                try
                {
                    if (appliedRecorded) workAccidentObservations.RollbackRejected(workOperationId, Rollback);
                    else Rollback();
                }
                catch
                {
                    Occurred = true;
                    work.isWorking = false;
                    throw;
                }
            }
            throw;
        }
        finally
        {
            try { accidentFacts.Cancel(lease); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            { Debug.LogError("work-accident-reservation-cleanup-failed:" + exception.Message); }
        }
        workAccidentObservations.CompleteFollowups(workOperationId);
        return true;
    }

    private CharacterCommandOrigin ResolveCommandOrigin() =>
        work.PriorityWorkTarget != null
        && work.assignedShop == work.PriorityWorkTarget
            ? CharacterCommandOrigin.DirectPlayerOrder
            : CharacterCommandOrigin.Autonomous;
}
