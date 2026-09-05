using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal delegate bool TryGetCaptiveHousing(
    string captiveId,
    out BuildableObject housing);

internal sealed class CaptivityInteractionRuntime
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityActorRuntimeLookup actorRuntime;
    private readonly CaptivityInteractionRegistry interactions;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly ICaptivityInteractionMaterialRuntime materials;
    private readonly TryGetCaptiveHousing tryGetHousing;

    public CaptivityInteractionRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        CaptivityInteractionRegistry interactions,
        IWorldItemStackRuntime itemRuntime,
        ICaptivityInteractionMaterialRuntime materials,
        TryGetCaptiveHousing tryGetHousing)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        this.interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
        this.tryGetHousing = tryGetHousing ?? throw new ArgumentNullException(nameof(tryGetHousing));
    }

    public bool TryStart(
        string captiveId,
        string interactionId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = actors.FindState(captiveId);
        CharacterActor subject = actorRuntime.Find(captiveId);
        if (state == null
            || subject == null
            || !interactions.TryGet(interactionId, out ICaptivityInteractionHandler handler))
        {
            failureReason = "포로 또는 상호작용을 찾을 수 없습니다.";
            return false;
        }

        Vector2Int interactionPosition = facility != null
            ? facility.centerPos
            : state.housingPosition;
        CaptivityInteractionContext context = new CaptivityInteractionContext(
            state,
            subject != null && !subject.IsDead,
            warden != null
                && !warden.IsDead
                && warden.CurrentLifecycleState == CharacterLifecycleState.Active,
            facility?.BuildingData.GetCaptiveHousingAbility()?.IsValid == true,
            interactionPosition);
        if (!handler.CanExecute(context, out failureReason))
        {
            return false;
        }

        if (!materials.TryOpenAndRequest(
                state,
                handler,
                facility,
                out string materialDestinationId,
                out failureReason))
        {
            return false;
        }

        state.status = CaptivityStatus.Interaction;
        subject.SetAiPaused(true);
        state.reservedWardenId = CaptivityActorAccess.RequireCharacterId(
            warden?.Identity?.PersistentId);
        state.currentInteractionId = handler.InteractionId;
        state.interactionMaterialDestinationId = materialDestinationId;
        state.interactionMaterialsConsumed = handler.MaterialRequirements.Count == 0;
        state.completedInteractionWork = 0f;
        state.requiredInteractionWork = Mathf.Max(1f, handler.RequiredWork);
        AIBrain wardenBrain = warden?.Brain;
        if (wardenBrain == null)
        {
            state.status = CaptivityStatus.Confined;
            Clear(state);
            failureReason =
                "The assigned warden has no AI brain.";
            return false;
        }

        // Wake the selected actor, but do not force Warden work before its
        // physical inputs arrive. A one-worker settlement must remain free to
        // haul the reserved interaction input first.
        wardenBrain.RequestImmediateReplan(clearFailures: false);
        state.lastResult = $"{handler.DisplayName} 준비";
        return true;
    }

    public bool Advance(
        string captiveId,
        CharacterActor warden,
        float workAmount,
        out string status)
    {
        status = string.Empty;
        CaptiveState state = actors.FindState(captiveId);
        CharacterActor subject = actorRuntime.Find(captiveId);
        if (state == null
            || subject == null
            || state.status != CaptivityStatus.Interaction
            || !string.Equals(
                state.reservedWardenId,
                CaptivityActorAccess.RequireCharacterId(
                    warden?.Identity?.PersistentId),
                StringComparison.Ordinal)
            || !interactions.TryGet(
                state.currentInteractionId,
                out ICaptivityInteractionHandler handler))
        {
            status = "유효한 관리 작업이 아닙니다.";
            return false;
        }

        if (!state.interactionMaterialsConsumed)
        {
            if (!materials.TryCommitSink(
                    state,
                    handler,
                    out string materialReason))
            {
                status = string.IsNullOrWhiteSpace(materialReason)
                    ? "관리 작업 재료 운반 대기"
                    : $"재료 운반 대기 · {materialReason}";
                return false;
            }

            state.interactionMaterialsConsumed = true;
        }

        state.completedInteractionWork = Mathf.Min(
            state.requiredInteractionWork,
            state.completedInteractionWork + Mathf.Max(0f, workAmount));
        if (state.completedInteractionWork + 0.001f < state.requiredInteractionWork)
        {
            status = $"{handler.DisplayName} "
                + $"{Mathf.RoundToInt(state.completedInteractionWork / state.requiredInteractionWork * 100f)}%";
            return true;
        }

        tryGetHousing(state.captiveId, out BuildableObject housing);
        CaptivityInteractionContext context = new CaptivityInteractionContext(
            state,
            subject != null && !subject.IsDead,
            warden != null
                && !warden.IsDead
                && warden.CurrentLifecycleState == CharacterLifecycleState.Active,
            housing?.BuildingData.GetCaptiveHousingAbility()?.IsValid == true,
            state.housingPosition);
        if (!handler.CanExecute(context, out status))
        {
            state.status = CaptivityStatus.Confined;
            Clear(state);
            return false;
        }

        ApplyResult(state, handler.Execute(context));
        status = state.lastResult;
        state.status = CaptivityStatus.Confined;
        Clear(state);
        return true;
    }

    public bool IsReady(string captiveId, out string reason)
    {
        reason = string.Empty;
        CaptiveState state = actors.FindState(captiveId);
        if (state == null
            || state.status != CaptivityStatus.Interaction
            || !interactions.TryGet(
                state.currentInteractionId,
                out ICaptivityInteractionHandler handler))
        {
            reason = "No active captive interaction was found.";
            return false;
        }

        if (state.interactionMaterialsConsumed
            || handler.MaterialRequirements.Count == 0)
        {
            return true;
        }

        return materials.IsReady(state, handler, out reason);
    }

    public void ReleaseMaterials(CaptiveState state)
    {
        if (state == null
            || string.IsNullOrWhiteSpace(state.interactionMaterialDestinationId))
        {
            return;
        }
        if (!materials.TryClose(
                state,
                "captivity-interaction-terminal",
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Captivity interaction material authority could not close: "
                + failureReason);
        }
    }

    private void ApplyResult(
        CaptiveState state,
        CaptivityInteractionResult result)
    {
        if (!result.Success)
        {
            state.lastResult = result.Message;
            return;
        }

        state.will = ClampStat(state.will + result.WillDelta);
        state.fear = ClampStat(state.fear + result.FearDelta);
        state.trust = ClampStat(state.trust + result.TrustDelta);
        state.grudge = ClampStat(state.grudge + result.GrudgeDelta);
        state.corruption = ClampStat(state.corruption + result.CorruptionDelta);
        state.health = ClampStat(state.health + result.HealthDelta);
        state.lastResult = result.Message;
        if (!string.IsNullOrWhiteSpace(result.OutputItemId)
            && result.OutputAmount > 0)
        {
            itemRuntime.SpawnItemAt(
                result.OutputItemId,
                result.OutputAmount,
                state.housingPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out _);
        }

        actors.Recalculate(state);
    }

    private void Clear(CaptiveState state)
    {
        ReleaseMaterials(state);
        state.reservedWardenId = string.Empty;
        state.currentInteractionId = string.Empty;
        state.interactionMaterialDestinationId = string.Empty;
        state.interactionMaterialsConsumed = false;
        state.completedInteractionWork = 0f;
        state.requiredInteractionWork = 0f;
    }

    private static float ClampStat(float value)
    {
        return Mathf.Clamp(value, 0f, 100f);
    }
}
