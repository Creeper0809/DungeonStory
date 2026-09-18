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
    private readonly CaptivityInterrogationInformationRuntime
        interrogationInformation;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly ICaptivityInteractionMaterialRuntime materials;
    private readonly TryGetCaptiveHousing tryGetHousing;

    public CaptivityInteractionRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        CaptivityInteractionRegistry interactions,
        CaptivityInterrogationInformationRuntime interrogationInformation,
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        IWorldItemStackRuntime itemRuntime,
        ICaptivityInteractionMaterialRuntime materials,
        TryGetCaptiveHousing tryGetHousing)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        this.interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        this.interrogationInformation = interrogationInformation
            ?? throw new ArgumentNullException(nameof(interrogationInformation));
        this.bodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.bodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
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

        if (state.interrogationTerminal == null)
        {
            throw new InvalidOperationException(
                $"Captive '{state.captiveId}' is missing interrogation terminal state.");
        }
        if (state.interrogationTerminal.HasPendingPublication)
        {
            failureReason = "이전 심문 결과를 전달하는 중입니다.";
            return false;
        }
        if (handler.Kind == CaptiveInteractionKind.Interrogation
            && state.interrogationAttemptSequence == int.MaxValue)
        {
            failureReason = "포로 심문 시도 번호가 한계에 도달했습니다.";
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
        state.currentInterrogationAttemptId = 0;
        if (handler.Kind == CaptiveInteractionKind.Interrogation)
        {
            state.interrogationAttemptSequence++;
            state.currentInterrogationAttemptId =
                state.interrogationAttemptSequence;
        }
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

        if (IsFrozenCurrentInterrogation(state))
        {
            TryCompleteFrozenInterrogation(state, out status);
            return true;
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

        CaptivityInterrogationTerminalState interrogationOutcome =
            handler.Kind == CaptiveInteractionKind.Interrogation
                ? interrogationInformation.FreezeOutcome(
                    state,
                    actors.States)
                : null;
        CaptivityInteractionResult result = handler.Execute(context);
        if (interrogationOutcome != null && result.Success)
        {
            // Persist the decision before applying any one-shot captive effects.
            // If a later publication callback throws, the next advance/tick can
            // only retry this frozen outcome and cannot execute the handler again.
            state.interrogationTerminal = interrogationOutcome;
        }
        bool subjectDied = ApplyResult(
            state,
            subject,
            handler,
            result);
        status = state.lastResult;
        if (subjectDied)
        {
            return true;
        }
        if (interrogationOutcome != null && result.Success)
        {
            TryCompleteFrozenInterrogation(state, out status);
            return true;
        }
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

    public void HandleSubjectDeath(CaptiveState state)
    {
        if (state == null)
        {
            return;
        }

        ReleaseMaterials(state);
        ClearInteractionState(state);
    }

    public void RetryPendingInterrogationPublications(
        IEnumerable<CaptiveState> states)
    {
        foreach (CaptiveState state in states ?? Array.Empty<CaptiveState>())
        {
            CaptivityInterrogationTerminalState terminal =
                state?.interrogationTerminal;
            if (terminal == null
                || !terminal.HasOutcome
                || (!terminal.HasPendingPublication
                    && !IsFrozenCurrentInterrogation(state)))
            {
                continue;
            }

            TryCompleteFrozenInterrogation(state, out _);
        }
    }

    private bool ApplyResult(
        CaptiveState state,
        CharacterActor subject,
        ICaptivityInteractionHandler handler,
        CaptivityInteractionResult result)
    {
        if (!result.Success)
        {
            state.lastResult = result.Message;
            return false;
        }

        state.will = ClampStat(state.will + result.WillDelta);
        state.fear = ClampStat(state.fear + result.FearDelta);
        state.trust = ClampStat(state.trust + result.TrustDelta);
        state.grudge = ClampStat(state.grudge + result.GrudgeDelta);
        state.corruption = ClampStat(state.corruption + result.CorruptionDelta);
        state.lastResult = result.Message;

        // Successful extraction output is settled before the body command. If
        // that command kills the subject synchronously, the authored result is
        // still published exactly once while all later captive-state work stops.
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

        if (handler.BodyDamage.HasDamage)
        {
            CharacterVitalsSnapshot vitals = bodyHealthQuery.GetVitals(subject);
            CaptivityBodyDamageProjection damage = handler.BodyDamage.Project(
                vitals.CurrentHealth,
                vitals.MaximumHealth);
            bodyHealthCommands.ApplyLegacyDamageWithCause(
                subject,
                damage.DamageAmount,
                CharacterDeathCauseCode.Unknown,
                $"captivity-interaction:{handler.InteractionId}",
                allowDeath: true);
            if (subject.IsDead || state.status == CaptivityStatus.Dead)
            {
                return true;
            }
        }

        actors.Recalculate(state);
        return false;
    }

    private void Clear(CaptiveState state)
    {
        ReleaseMaterials(state);
        ClearInteractionState(state);
    }

    private static void ClearInteractionState(CaptiveState state)
    {
        state.reservedWardenId = string.Empty;
        state.currentInteractionId = string.Empty;
        state.currentInterrogationAttemptId = 0;
        state.interactionMaterialDestinationId = string.Empty;
        state.interactionMaterialsConsumed = false;
        state.completedInteractionWork = 0f;
        state.requiredInteractionWork = 0f;
    }

    private bool TryCompleteFrozenInterrogation(
        CaptiveState state,
        out string status)
    {
        CaptivityInterrogationTerminalState terminal =
            state?.interrogationTerminal;
        string resultTitle = CaptivityInterrogationInformationRuntime
            .GetResultTitle(terminal);
        if (!interrogationInformation.TryPublish(
                state?.captiveId,
                terminal,
                out string publicationFailure))
        {
            status = resultTitle + " · 결과 전달 재시도 대기";
            state.lastResult = status;
            if (!string.IsNullOrWhiteSpace(publicationFailure))
            {
                status += $" ({publicationFailure})";
            }
            return false;
        }

        state.lastResult = resultTitle;
        if (!IsFrozenCurrentInterrogation(state))
        {
            status = state.lastResult;
            return true;
        }

        try
        {
            Clear(state);
            state.status = CaptivityStatus.Confined;
            status = state.lastResult;
            return true;
        }
        catch (Exception exception)
        {
            status = resultTitle + " · 작업 종결 재시도 대기"
                + $" ({exception.Message})";
            state.lastResult = status;
            return false;
        }
    }

    private static bool IsFrozenCurrentInterrogation(CaptiveState state)
    {
        return state != null
            && state.status == CaptivityStatus.Interaction
            && string.Equals(
                state.currentInteractionId,
                CaptivityInterrogationAttemptIdentity.InteractionId,
                StringComparison.Ordinal)
            && state.currentInterrogationAttemptId > 0
            && state.interrogationTerminal?.attemptId
                == state.currentInterrogationAttemptId;
    }

    private static float ClampStat(float value)
    {
        return Mathf.Clamp(value, 0f, 100f);
    }
}
