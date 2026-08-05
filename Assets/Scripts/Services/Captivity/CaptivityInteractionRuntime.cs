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
    private readonly TryGetCaptiveHousing tryGetHousing;

    public CaptivityInteractionRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        CaptivityInteractionRegistry interactions,
        IWorldItemStackRuntime itemRuntime,
        TryGetCaptiveHousing tryGetHousing)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        this.interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
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

        string materialDestinationId =
            $"captivity-interaction:{state.captiveId}:{handler.InteractionId}";
        foreach (KeyValuePair<StockCategory, int> cost in
                 handler.MaterialRequirements.Where(item => item.Value > 0))
        {
            if (itemRuntime.TryRequestFacilityDelivery(
                    cost.Key,
                    cost.Value,
                    interactionPosition,
                    materialDestinationId,
                    out int requested,
                    out string deliveryReason)
                && requested >= cost.Value)
            {
                continue;
            }

            itemRuntime.ReleaseStacksByDestination(
                materialDestinationId,
                interactionPosition);
            failureReason = string.IsNullOrWhiteSpace(deliveryReason)
                ? $"{cost.Key} 재료를 충분히 예약할 수 없습니다."
                : deliveryReason;
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
            if (!itemRuntime.TryConsumeFacilityBuffer(
                    state.interactionMaterialDestinationId,
                    handler.MaterialRequirements,
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

    public void ReleaseMaterials(CaptiveState state)
    {
        if (state == null
            || state.interactionMaterialsConsumed
            || string.IsNullOrWhiteSpace(state.interactionMaterialDestinationId))
        {
            return;
        }

        itemRuntime.ReleaseStacksByDestination(
            state.interactionMaterialDestinationId,
            state.housingPosition);
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
