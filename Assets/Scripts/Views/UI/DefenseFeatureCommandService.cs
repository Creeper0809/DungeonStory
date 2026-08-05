using System;
using System.Linq;
using UnityEngine;

public sealed class DefenseFeatureCommandService : IDefenseFeatureCommandService
{
    private readonly IDefenseResponsePolicyRuntime policyRuntime;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IDefenseFacilityRuntime defenseFacilities;
    private readonly IDefenseUiTextQuery text;

    public DefenseFeatureCommandService(
        IDefenseResponsePolicyRuntime policyRuntime,
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld,
        IDefenseFacilityRuntime defenseFacilities,
        IDefenseUiTextQuery text)
    {
        this.policyRuntime = policyRuntime
            ?? throw new ArgumentNullException(nameof(policyRuntime));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.defenseFacilities = defenseFacilities
            ?? throw new ArgumentNullException(nameof(defenseFacilities));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public DefenseFeatureCommandResult ToggleAutoResponse(string policyId)
    {
        return Update(policyId, policy => policy.autoRespond = !policy.autoRespond);
    }

    public DefenseFeatureCommandResult StepMinimumDispatchHealth(string policyId)
    {
        return Update(
            policyId,
            policy => policy.minimumDispatchHealthRatio =
                StepRatio(policy.minimumDispatchHealthRatio));
    }

    public DefenseFeatureCommandResult StepRetreatHealth(string policyId)
    {
        return Update(
            policyId,
            policy => policy.retreatHealthRatio = StepRatio(policy.retreatHealthRatio));
    }

    public DefenseFeatureCommandResult ToggleHoldWithoutReplacement(string policyId)
    {
        return Update(
            policyId,
            policy => policy.holdWithoutReplacement = !policy.holdWithoutReplacement);
    }

    public DefenseFeatureCommandResult StepRejoinHealth(string policyId)
    {
        return Update(
            policyId,
            policy => policy.rejoinHealthRatio = StepRatio(policy.rejoinHealthRatio));
    }

    public DefenseFeatureCommandResult CreatePolicy()
    {
        bool succeeded = policyRuntime.TryCreatePolicy(
            text.Get("PolicyDefaultName", policyRuntime.Policies.Count + 1),
            out DefenseResponsePolicyData created);
        return new DefenseFeatureCommandResult(
            succeeded,
            text.Get(
                succeeded ? "PolicyCreated" : "PolicyCreateFailed",
                created?.displayName ?? string.Empty),
            created?.id);
    }

    public DefenseFeatureCommandResult DuplicatePolicy(string policyId)
    {
        DefenseResponsePolicyData source = FindPolicy(policyId);
        DefenseResponsePolicyData duplicate = null;
        bool succeeded = source != null
            && policyRuntime.TryDuplicatePolicy(
                source.id,
                text.Get("PolicyCopyName", source.displayName),
                out duplicate);
        return new DefenseFeatureCommandResult(
            succeeded,
            text.Get(
                succeeded ? "PolicyDuplicated" : "PolicyDuplicateFailed",
                duplicate?.displayName ?? string.Empty),
            succeeded ? duplicate.id : string.Empty);
    }

    public DefenseFeatureCommandResult DeletePolicy(string policyId)
    {
        bool succeeded = policyRuntime.TryDeletePolicy(policyId, reassignToStandard: true);
        return new DefenseFeatureCommandResult(
            succeeded,
            text.Get(succeeded ? "PolicyDeleted" : "PolicyDeleteFailed"),
            succeeded ? DefenseResponsePolicyRuntime.StandardPolicyId : policyId);
    }

    public DefenseFeatureCommandResult AssignPolicy(int actorRuntimeId, string policyId)
    {
        CharacterActor actor = characterWorld.Characters.FirstOrDefault(candidate =>
            candidate != null && candidate.GetInstanceID() == actorRuntimeId);
        if (actor == null)
        {
            return new DefenseFeatureCommandResult(
                false,
                text.Get("GuardAssignmentTargetMissing"));
        }

        bool succeeded = policyRuntime.AssignPolicy(actor, policyId);
        return new DefenseFeatureCommandResult(
            succeeded,
            text.Get(
                succeeded ? "GuardPolicyAssigned" : "GuardPolicyAssignmentFailed",
                actor.Identity?.DisplayName ?? actor.name));
    }

    public DefenseFeatureCommandResult CycleFacilityArmingPolicy(
        int facilityRuntimeId)
    {
        DefenseFacility facility = FindFacility(facilityRuntimeId);
        if (facility == null)
        {
            return new DefenseFeatureCommandResult(
                false,
                text.Get("FacilityMissing"));
        }

        DefenseArmingPolicy current =
            defenseFacilities.GetSnapshot(facility).ArmingPolicy;
        DefenseArmingPolicy next =
            (DefenseArmingPolicy)(((int)current + 1) % 4);
        bool succeeded = defenseFacilities.SetArmingPolicy(facility, next);
        return new DefenseFeatureCommandResult(
            succeeded,
            text.Get(
                succeeded ? "FacilityArmingPolicyChanged" : "FacilityArmingPolicyChangeFailed",
                text.Get("ArmingPolicy." + current),
                text.Get("ArmingPolicy." + next)));
    }

    public DefenseFeatureCommandResult RequestFacilityService(
        int facilityRuntimeId)
    {
        DefenseFacility facility = FindFacility(facilityRuntimeId);
        if (facility == null)
        {
            return new DefenseFeatureCommandResult(
                false,
                text.Get("FacilityMissing"));
        }

        DefenseFacilitySnapshot snapshot =
            defenseFacilities.GetSnapshot(facility);
        bool succeeded;
        DomainFailure failure;
        if (snapshot.OperationalState == DefenseFacilityOperationalState.Jammed)
        {
            succeeded = defenseFacilities.TryClearJam(
                facility,
                out failure);
        }
        else
        {
            succeeded = defenseFacilities.TryRequestReload(
                facility,
                out failure);
        }

        return new DefenseFeatureCommandResult(
            succeeded,
            succeeded
                ? text.Get("FacilityServiceRequested")
                : text.Get(failure));
    }

    private DefenseFacility FindFacility(int runtimeId)
    {
        return buildingWorld.Buildings
            .OfType<DefenseFacility>()
            .FirstOrDefault(facility =>
                facility != null
                && facility.GetInstanceID() == runtimeId);
    }

    private DefenseFeatureCommandResult Update(
        string policyId,
        Action<DefenseResponsePolicyData> mutate)
    {
        DefenseResponsePolicyData source = FindPolicy(policyId);
        if (source == null)
        {
            return new DefenseFeatureCommandResult(
                false,
                text.Get("PolicyMissing"));
        }

        DefenseResponsePolicyData edited = source.Clone();
        mutate(edited);
        bool succeeded = policyRuntime.TryUpdatePolicy(edited);
        return new DefenseFeatureCommandResult(
            succeeded,
            text.Get(
                succeeded ? "PolicyUpdated" : "PolicyUpdateFailed",
                edited.displayName));
    }

    private DefenseResponsePolicyData FindPolicy(string policyId)
    {
        return policyRuntime.Policies.FirstOrDefault(policy =>
            policy != null
            && string.Equals(policy.id, policyId, StringComparison.Ordinal));
    }

    private static float StepRatio(float current)
    {
        float next = Mathf.Round((Mathf.Clamp01(current) + 0.05f) * 20f) / 20f;
        return next > 1f ? 0f : next;
    }
}
