using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class CaptivityStateRuntime
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityPolicyRuntime policies;
    private readonly CaptivityInteractionRuntime interactions;
    private readonly ICaptivityEscortRestoreLifecycle escortRestore;
    private readonly IDoorAccessSubjectRegistry doorSubjects;

    private int captureSequence
    {
        get => actors.CaptureSequence;
        set => actors.CaptureSequence = value;
    }

    public CaptivityStateRuntime(
        CaptivityActorAccess actors,
        CaptivityPolicyRuntime policies,
        CaptivityInteractionRuntime interactions,
        ICaptivityEscortRestoreLifecycle escortRestore,
        IDoorAccessSubjectRegistry doorSubjects)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        this.escortRestore = escortRestore
            ?? throw new ArgumentNullException(nameof(escortRestore));
        this.doorSubjects = doorSubjects ?? throw new ArgumentNullException(nameof(doorSubjects));
    }

    public void OnCharacterDowned(CharacterActor actor)
    {
        if (!IsEligibleDownedIntruder(actor))
        {
            return;
        }

        actor.SetLifecycleState(CharacterLifecycleState.Downed);
        EnsureCandidate(actor);
    }

    public void OnCharacterRecovered(CharacterActor actor)
    {
        CaptiveState state = actors.FindState(
            CaptivityActorAccess.RequireCharacterId(
                actor?.Identity?.PersistentId));
        if (state == null || !state.IsActive)
        {
            return;
        }

        if (state.status is CaptivityStatus.Confined
            or CaptivityStatus.Labor
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer)
        {
            actor.SetAiPaused(state.status != CaptivityStatus.Labor);
            actor.SetLifecycleState(
                state.status == CaptivityStatus.Labor
                    ? CharacterLifecycleState.Active
                    : CharacterLifecycleState.Downed);
        }
    }

    public void OnCharacterDeath(CharacterDeathEvent gameEvent)
    {
        CaptiveState state = actors.FindState(gameEvent.CharacterId.Value);
        if (state == null)
        {
            return;
        }

        state.status = CaptivityStatus.Dead;
        state.lastResult = "수용 중 사망";
        interactions.ReleaseMaterials(state);
        doorSubjects.SetCaptive(state.captiveId, false);
        escortRestore.RestoreCaptiveParent(state.captiveId);
    }

    public CaptiveState EnsureCandidate(CharacterActor actor)
    {
        string id = CaptivityActorAccess.RequireCharacterId(
            actor?.Identity?.PersistentId);
        CaptiveState existing = actors.FindState(id);
        if (existing != null)
        {
            return existing;
        }

        CaptivePolicyData defaultPolicy = policies.DefaultPolicy;
        CaptiveState created = new CaptiveState
        {
            captiveId = id,
            displayName = actor.Identity?.DisplayName ?? actor.name,
            speciesTag = actor.SpeciesTag,
            status = CaptivityStatus.AwaitingCapture,
            capturePosition = actor.GetNowXY(),
            policyId = defaultPolicy.policyId,
            laborPermissions = defaultPolicy.allowedLabor,
            health = EstimateHealth(actor),
            lastResult = "포획 가능"
        };
        captureSequence++;
        actors.AddState(created);
        doorSubjects.SetCaptive(id, true);
        return created;
    }

    public CaptivitySaveData Capture()
    {
        return new CaptivitySaveData
        {
            version = CaptivitySaveData.CurrentVersion,
            captureSequence = captureSequence,
            policySequence = policies.Sequence,
            captives = actors.States.Select(state => state.Clone()).ToList(),
            policies = policies.CapturePolicies()
        };
    }

    private static bool IsEligibleDownedIntruder(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.characterType == CharacterType.Intruder
            && actor.CurrentLifecycleState == CharacterLifecycleState.Downed;
    }

    private static float EstimateHealth(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            return 0f;
        }

        return Mathf.Clamp(
            actor.Stats.CurrentHealth / Mathf.Max(1f, actor.Stats.MaxHealth) * 100f,
            0f,
            100f);
    }
}
