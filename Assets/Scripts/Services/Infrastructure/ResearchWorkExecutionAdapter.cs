using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Work;
using UnityEngine;

public sealed class ResearchWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider
{
    private readonly DefaultResearchWorkRuntimePort runtime;
    private readonly DungeonStory.Work.ResearchWorkExecutionHandler core;
    private readonly IBlueprintResearchWorkforcePolicyQuery workforcePolicy;
    private readonly IProjectWorkforceRuntime projectWorkforce;

    public ResearchWorkExecutionHandler(IBlueprintResearchWorkService researchWorkService)
        : this(
            researchWorkService,
            (IEquipmentPhysicalItemGateway)
                UnavailableEquipmentPhysicalItemGateway.Instance,
            new SingleResearchWorkforcePolicyQuery(),
            new ProjectWorkforceRuntime(),
            null,
            null,
            null,
            null,
            null)
    {
    }

    public ResearchWorkExecutionHandler(
        IBlueprintResearchWorkService researchWorkService,
        IWorldItemStackRuntime items,
        IBlueprintResearchWorkforcePolicyQuery workforcePolicy,
        IProjectWorkforceRuntime projectWorkforce)
        : this(
            researchWorkService,
            (IEquipmentPhysicalItemGateway)items,
            workforcePolicy,
            projectWorkforce,
            null,
            null,
            null,
            null,
            null)
    {
    }

    [VContainer.Inject]
    public ResearchWorkExecutionHandler(
        IBlueprintResearchWorkService researchWorkService,
        IWorldItemStackRuntime items,
        IBlueprintResearchWorkforcePolicyQuery workforcePolicy,
        IProjectWorkforceRuntime projectWorkforce,
        IResearchDurableEquipmentWorkPolicyQuery equipmentWorkPolicies,
        IDurableFacilityEquipmentPolicyQuery equipmentPolicies,
        IDurableFacilityEquipmentSlotCommand equipmentSlots,
        IDurableFacilityEquipmentSlotQuery equipmentSlotQuery,
        IDurableFacilityEquipmentUseCommand equipmentUse)
        : this(
            researchWorkService,
            (IEquipmentPhysicalItemGateway)items,
            workforcePolicy,
            projectWorkforce,
            equipmentWorkPolicies,
            equipmentPolicies,
            equipmentSlots,
            equipmentSlotQuery,
            equipmentUse)
    {
    }

    private ResearchWorkExecutionHandler(
        IBlueprintResearchWorkService researchWorkService,
        IEquipmentPhysicalItemGateway items,
        IBlueprintResearchWorkforcePolicyQuery workforcePolicy,
        IProjectWorkforceRuntime projectWorkforce,
        IResearchDurableEquipmentWorkPolicyQuery equipmentWorkPolicies,
        IDurableFacilityEquipmentPolicyQuery equipmentPolicies,
        IDurableFacilityEquipmentSlotCommand equipmentSlots,
        IDurableFacilityEquipmentSlotQuery equipmentSlotQuery,
        IDurableFacilityEquipmentUseCommand equipmentUse)
    {
        runtime = new DefaultResearchWorkRuntimePort(
            researchWorkService
                ?? throw new ArgumentNullException(nameof(researchWorkService)),
            items ?? throw new ArgumentNullException(nameof(items)),
            equipmentWorkPolicies,
            equipmentPolicies,
            equipmentSlots,
            equipmentSlotQuery,
            equipmentUse);
        core = new DungeonStory.Work.ResearchWorkExecutionHandler(runtime);
        this.workforcePolicy = workforcePolicy
            ?? throw new ArgumentNullException(nameof(workforcePolicy));
        this.projectWorkforce = projectWorkforce
            ?? throw new ArgumentNullException(nameof(projectWorkforce));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => core.WorkTypeIds;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        if (target == null
            || !core.IsAvailable(
                workTypeId,
                runtime.CaptureFacility(target),
                out reason))
        {
            return false;
        }

        if (!workforcePolicy.TryGetWorkforcePolicy(
                target,
                out string projectId,
                out int maximumResearchers)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            reason = "연구 프로젝트 또는 연구자 ID를 확인할 수 없습니다.";
            return false;
        }

        bool available = projectWorkforce.CanJoin(
            projectId,
            characterId.Value,
            maximumResearchers);
        reason = available ? string.Empty : "연구 프로젝트의 동시 연구자 슬롯이 가득 찼습니다.";
        return available;
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (!workforcePolicy.TryGetWorkforcePolicy(
                context.Target,
                out string projectId,
                out int maximumResearchers)
            || !CharacterPersistentIdentity.TryGet(
                context.Actor,
                out CharacterId characterId))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        ProjectScale scale = ResolveResearchScale(maximumResearchers);
        if (!projectWorkforce.TryJoin(
                projectId,
                characterId.Value,
                scale,
                maximumResearchers,
                out ProjectWorkerLease workforceLease,
                out _))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        using (workforceLease)
        {
        ResearchWorkerHandle worker = runtime.CaptureWorker(context.Actor);
        ResearchFacilityHandle facility = runtime.CaptureFacility(context.Target);
        ResearchWorkPlan plan = core.CreatePlan(facility);
        yield return context.ExecuteWorkAmount(plan.RequiredWork, "연구");
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        float contribution = projectWorkforce.GetContributionMultiplier(
            projectId,
            characterId.Value);
        ResearchWorkProgressResult work = core.ApplyApprovedWork(
            worker,
            facility,
            plan.RequiredWork * contribution);
        result.CompletedSuccessfully = work.Succeeded;
        if (!work.Succeeded)
        {
            context.Actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Research,
                CharacterActivityOutcomes.Failed,
                $"연구 실패: {work.FailureCode}",
                context.Target,
                reasonCode: work.FailureCode,
                bubbleEligible: true));
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Research,
            work.Completed
                ? CharacterActivityOutcomes.Completed
                : CharacterActivityOutcomes.Progress,
            work.Completed
                ? $"연구 완료: {work.Label}"
                : $"연구 진행: {work.Label}",
            context.Target,
            reasonCode: work.Completed ? "blueprint-completed" : "research-progress",
            value: work.ProgressRatio));
        }
    }

    private static ProjectScale ResolveResearchScale(int maximumResearchers) =>
        maximumResearchers switch
        {
            1 => ProjectScale.StandardResearch,
            2 => ProjectScale.CollaborativeResearch,
            4 => ProjectScale.MajorResearch,
            _ => throw new InvalidOperationException(
                $"Research maximum {maximumResearchers} must be 1, 2 or 4.")
        };

    private sealed class SingleResearchWorkforcePolicyQuery :
        IBlueprintResearchWorkforcePolicyQuery
    {
        public bool TryGetWorkforcePolicy(
            BuildableObject facility,
            out string projectId,
            out int maximumResearchers)
        {
            projectId = facility != null
                ? $"research:test:{facility.RequirePersistentInstanceId().Value}"
                : string.Empty;
            maximumResearchers = facility != null ? 1 : 0;
            return facility != null;
        }
    }
}

public sealed class DefaultResearchWorkRuntimePort : IResearchWorkRuntimePort
{
    private readonly IBlueprintResearchWorkService service;
    private readonly IResearchDurableEquipmentWorkPolicyQuery
        equipmentWorkPolicies;
    private readonly IDurableFacilityEquipmentPolicyQuery equipmentPolicies;
    private readonly IDurableFacilityEquipmentSlotCommand equipmentSlots;
    private readonly IDurableFacilityEquipmentSlotQuery equipmentSlotQuery;
    private readonly IDurableFacilityEquipmentUseCommand equipmentUse;
    private readonly bool equipmentEnabled;

    public DefaultResearchWorkRuntimePort(
        IBlueprintResearchWorkService service,
        IEquipmentPhysicalItemGateway items)
        : this(service, items, null, null, null, null, null)
    {
    }

    public DefaultResearchWorkRuntimePort(
        IBlueprintResearchWorkService service,
        IEquipmentPhysicalItemGateway items,
        IResearchDurableEquipmentWorkPolicyQuery equipmentWorkPolicies,
        IDurableFacilityEquipmentPolicyQuery equipmentPolicies,
        IDurableFacilityEquipmentSlotCommand equipmentSlots,
        IDurableFacilityEquipmentSlotQuery equipmentSlotQuery,
        IDurableFacilityEquipmentUseCommand equipmentUse)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        _ = items ?? throw new ArgumentNullException(nameof(items));
        bool anyEquipmentDependency = equipmentWorkPolicies != null
            || equipmentPolicies != null
            || equipmentSlots != null
            || equipmentSlotQuery != null
            || equipmentUse != null;
        bool allEquipmentDependencies = equipmentWorkPolicies != null
            && equipmentPolicies != null
            && equipmentSlots != null
            && equipmentSlotQuery != null
            && equipmentUse != null;
        if (anyEquipmentDependency && !allEquipmentDependencies)
        {
            throw new ArgumentException(
                "Research durable-equipment runtime dependencies must be all present or all absent.");
        }
        this.equipmentWorkPolicies = equipmentWorkPolicies;
        this.equipmentPolicies = equipmentPolicies;
        this.equipmentSlots = equipmentSlots;
        this.equipmentSlotQuery = equipmentSlotQuery;
        this.equipmentUse = equipmentUse;
        equipmentEnabled = allEquipmentDependencies;
    }

    public ResearchWorkerHandle CaptureWorker(object runtimeWorker)
    {
        CharacterActor actor = runtimeWorker as CharacterActor
            ?? throw new InvalidOperationException("Research work requires a CharacterActor.");
        CharacterId id = actor.BuildingCharacterId;
        return new ResearchWorkerHandle(actor, id);
    }

    public ResearchFacilityHandle CaptureFacility(object runtimeFacility)
    {
        BuildableObject facility = runtimeFacility as BuildableObject
            ?? throw new InvalidOperationException("Research work requires a BuildableObject.");
        return new ResearchFacilityHandle(
            facility,
            facility.RequirePersistentInstanceId());
    }

    public bool HasResearchWork(ResearchFacilityHandle facility) =>
        service.HasResearchWorkFor(RequireFacility(facility));

    public ResearchWorkPlan CreatePlan(ResearchFacilityHandle facility)
    {
        BuildableObject target = RequireFacility(facility);
        float requiredWork = target.BuildingData != null
            ? Mathf.Max(
                0.1f,
                target.BuildingData.GetRequiredWork(BuiltInWorkTypeIds.Research))
            : 1f;
        return new ResearchWorkPlan(requiredWork, "연구");
    }

    public ResearchWorkProgressResult ApplyApprovedWork(
        ResearchWorkerHandle worker,
        ResearchFacilityHandle facility,
        float approvedWorkUnits)
    {
        BuildableObject target = RequireFacility(facility);
        CharacterActor researcher = RequireWorker(worker);
        float appliedWorkUnits = Math.Max(0f, approvedWorkUnits);
        BlueprintResearchWorkResult result;
        if (!equipmentEnabled)
        {
            result = service.ApplyApprovedResearchWork(
                researcher,
                target,
                appliedWorkUnits);
        }
        else if (!TryApplyRegisteredEquipmentWork(
                     researcher,
                     target,
                     appliedWorkUnits,
                     out result,
                     out string equipmentFailure))
        {
            return new ResearchWorkProgressResult(
                false,
                false,
                0f,
                equipmentFailure,
                equipmentFailure);
        }
        string label = result.Blueprint != null
            ? result.Blueprint.DisplayName
            : result.Message;
        return new ResearchWorkProgressResult(
            result.Success,
            result.Completed,
            result.ProgressRatio,
            label,
            result.Success ? string.Empty : result.Message);
    }

    private bool TryApplyRegisteredEquipmentWork(
        CharacterActor researcher,
        BuildableObject target,
        float approvedWorkUnits,
        out BlueprintResearchWorkResult result,
        out string failureReason)
    {
        result = default;
        failureReason = string.Empty;
        if (!equipmentWorkPolicies.TryResolve(
                target,
                out ResearchDurableEquipmentWorkPolicy workPolicy,
                out failureReason))
        {
            return false;
        }
        if (!equipmentPolicies.TryGetPolicy(
                workPolicy.EquipmentPolicyId,
                out DurableFacilityEquipmentPolicy equipmentPolicy))
        {
            failureReason = "research-durable-equipment-policy-unregistered:"
                + workPolicy.EquipmentPolicyId;
            return false;
        }
        if (!string.Equals(
                equipmentPolicy.UsabilityPolicyKind,
                workPolicy.WearPolicyKind,
                StringComparison.Ordinal))
        {
            failureReason =
                "research-durable-equipment-wear-policy-mismatch";
            return false;
        }
        BuildingInstanceId facilityId = target.RequirePersistentInstanceId();
        DurableFacilityEquipmentAssignment assignment =
            equipmentPolicy.CreateAssignment(
                facilityId.Value,
                facilityId,
                target.centerPos);
        DurableFacilityEquipmentSlotResult reconciled =
            equipmentSlots.TryReconcile(assignment);
        if (!reconciled.Succeeded)
        {
            failureReason = Canonical(reconciled.FailureReason)
                ? reconciled.FailureReason
                : "research-durable-equipment-reconcile-failed";
            return false;
        }

        DurableFacilityEquipmentSlotResult supplied =
            equipmentSlots.TryEnsureSupply(assignment.Key);
        if (supplied.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            failureReason = Canonical(supplied.FailureReason)
                ? supplied.FailureReason
                : "research-durable-equipment-supply-conflict";
            return false;
        }
        if (!equipmentSlotQuery.TryCapture(
                assignment.Key,
                out DurableFacilityEquipmentSlotSnapshot slot))
        {
            failureReason = "research-durable-equipment-slot-missing";
            return false;
        }
        if (!slot.SupplyReady)
        {
            result = service.ApplyApprovedResearchWork(
                researcher,
                target,
                approvedWorkUnits);
            return true;
        }

        ResearchEquipmentEffectCommit effect = new(
            service,
            researcher,
            target,
            workPolicy,
            approvedWorkUnits);
        double wearAmount = checked(
            (double)approvedWorkUnits * workPolicy.WearPerApprovedWorkUnit);
        DurableFacilityEquipmentUseResult use =
            equipmentUse.TryApplyWearAndEffect(
                assignment.Key,
                workPolicy.RequirementId,
                wearAmount,
                effect);
        if (!use.Succeeded)
        {
            failureReason = Canonical(use.FailureReason)
                ? use.FailureReason
                : "research-durable-equipment-use-failed";
            return false;
        }
        result = effect.Result;
        if (!result.Success)
        {
            failureReason = "research-durable-equipment-effect-result-missing";
            return false;
        }
        return true;
    }

    private sealed class ResearchEquipmentEffectCommit :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly IBlueprintResearchWorkService service;
        private readonly CharacterActor researcher;
        private readonly BuildableObject facility;
        private readonly ResearchDurableEquipmentWorkPolicy policy;
        private readonly float approvedWorkUnits;

        internal ResearchEquipmentEffectCommit(
            IBlueprintResearchWorkService service,
            CharacterActor researcher,
            BuildableObject facility,
            ResearchDurableEquipmentWorkPolicy policy,
            float approvedWorkUnits)
        {
            this.service = service;
            this.researcher = researcher;
            this.facility = facility;
            this.policy = policy;
            this.approvedWorkUnits = approvedWorkUnits;
        }

        public string EffectKind => policy.EffectKind;
        internal BlueprintResearchWorkResult Result { get; private set; }

        public bool TryPreflight(
            DurableFacilityEquipmentSlotSnapshot slot,
            DurableFacilityEquipmentRequirement requirement,
            DurableFacilityEquipmentUseSubject subject,
            double wearAmount,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (slot == null
                || requirement == null
                || subject == null
                || !string.Equals(
                    slot.PolicyId,
                    policy.EquipmentPolicyId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    requirement.RequirementId,
                    policy.RequirementId,
                    StringComparison.Ordinal)
                || researcher == null
                || facility == null
                || approvedWorkUnits <= 0f
                || !service.HasResearchWorkFor(facility))
            {
                failureReason = "research-durable-equipment-effect-preflight-rejected";
                return false;
            }
            return true;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            float boosted = checked(
                approvedWorkUnits * (float)policy.EffectMultiplier);
            Result = service.ApplyApprovedResearchWork(
                researcher,
                facility,
                boosted);
            if (!Result.Success)
            {
                failureReason = Canonical(Result.Message)
                    ? Result.Message
                    : "research-durable-equipment-effect-rejected";
                return false;
            }
            return true;
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static CharacterActor RequireWorker(ResearchWorkerHandle handle) =>
        handle?.RuntimeObject as CharacterActor
        ?? throw new InvalidOperationException("Research worker handle is invalid.");

    private static BuildableObject RequireFacility(ResearchFacilityHandle handle) =>
        handle?.RuntimeObject as BuildableObject
        ?? throw new InvalidOperationException("Research facility handle is invalid.");
}
