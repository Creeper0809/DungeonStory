using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
    private readonly ICharacterBodyHealthMutationTransaction bodyHealthMutations;
    private readonly CaptivityInteractionOutcomeRuntime outcomes;
    private readonly IGameClock gameClock;
    private readonly ICaptivityInteractionMaterialRuntime materials;
    private readonly TryGetCaptiveHousing tryGetHousing;

    public CaptivityInteractionRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        CaptivityInteractionRegistry interactions,
        CaptivityInterrogationInformationRuntime interrogationInformation,
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthMutationTransaction bodyHealthMutations,
        CaptivityInteractionOutcomeRuntime outcomes,
        IGameClock gameClock,
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
        this.bodyHealthMutations = bodyHealthMutations
            ?? throw new ArgumentNullException(nameof(bodyHealthMutations));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
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

        if (state.interrogationTerminal == null
            || state.interactionTerminal == null)
        {
            throw new InvalidOperationException(
                $"Captive '{state.captiveId}' is missing interaction terminal state.");
        }
        if (state.interactionTerminal.HasOutcome)
        {
            failureReason = "이전 포로 상호작용 결과를 마무리하는 중입니다.";
            return false;
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
        if (state.interactionAttemptSequence == int.MaxValue)
        {
            failureReason = "포로 상호작용 시도 번호가 한계에 도달했습니다.";
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
        state.interactionAttemptSequence++;
        state.currentInteractionAttemptId = state.interactionAttemptSequence;
        state.interactionTerminal = new CaptivityInteractionTerminalState();
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

        if (state.currentInteractionAttemptId == 0)
        {
            if (state.interactionAttemptSequence == int.MaxValue)
            {
                status = "포로 상호작용 시도 번호가 한계에 도달했습니다.";
                return false;
            }
            state.interactionAttemptSequence++;
            state.currentInteractionAttemptId = state.interactionAttemptSequence;
            state.interactionTerminal ??= new CaptivityInteractionTerminalState();
        }

        if (state.interactionTerminal?.HasOutcome == true)
        {
            TryCompleteFrozenOutcome(state, subject, out status);
            return true;
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
        state.interactionTerminal = FreezeResult(
            state,
            subject,
            warden,
            housing,
            handler,
            result);
        TryCompleteFrozenOutcome(state, subject, out status);
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

        if (state.interactionTerminal?.HasOutcome == true)
        {
            CharacterActor subject = actorRuntime.Find(state.captiveId);
            string completionStatus = string.Empty;
            if (subject == null
                || !TryCompleteFrozenOutcome(
                    state,
                    subject,
                    out completionStatus))
            {
                state.lastResult = string.IsNullOrWhiteSpace(completionStatus)
                    ? "사망 전 포로 상호작용 결과 전달을 재시도합니다."
                    : completionStatus;
                return;
            }
        }

        ReleaseMaterials(state);
        ClearInteractionState(state);
    }

    public void RetryPendingOutcomes(
        IEnumerable<CaptiveState> states)
    {
        foreach (CaptiveState state in states ?? Array.Empty<CaptiveState>())
        {
            if (state?.interactionTerminal?.HasOutcome == true)
            {
                CharacterActor subject = actorRuntime.Find(state.captiveId);
                if (subject != null)
                    TryCompleteFrozenOutcome(state, subject, out _);
                continue;
            }

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

    private CaptivityInteractionTerminalState FreezeResult(
        CaptiveState state,
        CharacterActor subject,
        CharacterActor warden,
        BuildableObject housing,
        ICaptivityInteractionHandler handler,
        CaptivityInteractionResult result)
    {
        if (state == null
            || subject == null
            || warden == null
            || housing == null
            || handler == null
            || state.currentInteractionAttemptId <= 0)
        {
            throw new InvalidOperationException(
                "A complete captivity interaction owner context is required.");
        }
        string wardenId = CaptivityActorAccess.RequireCharacterId(
            warden.Identity?.PersistentId);
        if (!string.Equals(
                wardenId,
                state.reservedWardenId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The completing warden does not own this interaction attempt.");
        }
        BuildingInstanceId facilityId = housing.RequirePersistentInstanceId();
        float willAfter = result.Success
            ? ClampStat(state.will + result.WillDelta)
            : state.will;
        float fearAfter = result.Success
            ? ClampStat(state.fear + result.FearDelta)
            : state.fear;
        float trustAfter = result.Success
            ? ClampStat(state.trust + result.TrustDelta)
            : state.trust;
        float grudgeAfter = result.Success
            ? ClampStat(state.grudge + result.GrudgeDelta)
            : state.grudge;
        float corruptionAfter = result.Success
            ? ClampStat(state.corruption + result.CorruptionDelta)
            : state.corruption;
        float bodyDamage = 0f;
        float bodyBefore = 0f;
        float bodyAfter = 0f;
        float bodyMaximum = 0f;
        if (result.Success && handler.BodyDamage.HasDamage)
        {
            CharacterVitalsSnapshot vitals = bodyHealthQuery.GetVitals(subject);
            CaptivityBodyDamageProjection damage = handler.BodyDamage.Project(
                vitals.CurrentHealth,
                vitals.MaximumHealth);
            bodyDamage = damage.DamageAmount;
            bodyBefore = damage.CurrentHealth;
            bodyAfter = damage.ExpectedHealth;
            bodyMaximum = damage.MaximumHealth;
        }
        string outputItemId = result.Success
            ? result.OutputItemId?.Trim() ?? string.Empty
            : string.Empty;
        int outputAmount = result.Success && outputItemId.Length > 0
            ? result.OutputAmount
            : 0;
        string facilityDisplay = housing.BuildingData?.objectName?.Trim()
            ?? string.Empty;
        if (facilityDisplay.Length == 0)
            facilityDisplay = housing.name;
        string message = result.Message?.Trim() ?? string.Empty;
        if (message.Length == 0)
        {
            message = handler.DisplayName
                + (result.Success ? " 완료" : " 실패");
        }
        return new CaptivityInteractionTerminalState
        {
            attemptId = state.currentInteractionAttemptId,
            interactionId = handler.InteractionId,
            interactionKind = handler.Kind,
            interactionDisplayName = handler.DisplayName,
            wardenId = wardenId,
            wardenDisplayName = warden.Identity?.DisplayName?.Trim()
                ?? warden.name,
            facilityId = facilityId.Value,
            facilityDisplayName = facilityDisplay,
            resultGridX = state.housingPosition.x,
            resultGridY = state.housingPosition.y,
            success = result.Success,
            message = message,
            willBefore = state.will,
            willAfter = willAfter,
            fearBefore = state.fear,
            fearAfter = fearAfter,
            trustBefore = state.trust,
            trustAfter = trustAfter,
            grudgeBefore = state.grudge,
            grudgeAfter = grudgeAfter,
            corruptionBefore = state.corruption,
            corruptionAfter = corruptionAfter,
            outputItemId = outputItemId,
            outputAmount = outputAmount,
            outputOperationId = outputAmount > 0
                ? CaptivityInteractionAttemptIdentity.FormatOutputOperationId(
                    state.captiveId,
                    state.currentInteractionAttemptId)
                : string.Empty,
            bodyDamageAmount = bodyDamage,
            bodyHealthBefore = bodyBefore,
            bodyHealthAfter = bodyAfter,
            bodyMaximumHealth = bodyMaximum
        };
    }

    private bool TryCompleteFrozenOutcome(
        CaptiveState state,
        CharacterActor subject,
        out string status)
    {
        status = string.Empty;
        CaptivityInteractionTerminalState terminal = state?.interactionTerminal;
        if (state == null
            || subject == null
            || terminal?.HasOutcome != true
            || state.currentInteractionAttemptId != terminal.attemptId
            || !string.Equals(
                state.currentInteractionId,
                terminal.interactionId,
                StringComparison.Ordinal))
        {
            status = "동결된 포로 상호작용 결과가 현재 작업과 일치하지 않습니다.";
            return false;
        }

        if (!terminal.HasCommittedOutcome
            && !TryCommitFrozenOutcome(state, subject, terminal, out status))
        {
            state.lastResult = status;
            return false;
        }
        terminal = state.interactionTerminal;
        if (terminal?.HasCommittedOutcome != true)
        {
            status = "포로 상호작용 결과의 원장 커밋 상태가 사라졌습니다.";
            state.lastResult = status;
            return false;
        }

        if (!outcomes.TryEnsureOutput(state, out string outputFailure))
        {
            status = "포로 상호작용 결과는 확정되었지만 출력 전달을 재시도합니다: "
                + outputFailure;
            state.lastResult = status;
            return false;
        }

        if (terminal.bodyObserversPending)
        {
            terminal.bodyObserversPending = false;
            bodyHealthMutations.CompletePreparedAggregateDamage(
                subject,
                new CharacterPreparedAggregateDamageReceipt(
                    new CharacterId(state.captiveId),
                    terminal.bodyDamageAmount,
                    terminal.bodyHealthBefore,
                    terminal.bodyHealthAfter,
                    terminal.bodyMaximumHealth,
                    CharacterDeathCauseCode.Unknown,
                    $"captivity-interaction:{terminal.interactionId}",
                    allowDeath: true));
        }

        if (subject.IsDead || state.status == CaptivityStatus.Dead)
        {
            status = terminal.message;
            return true;
        }
        if (terminal.interactionKind == CaptiveInteractionKind.Interrogation
            && terminal.success)
        {
            TryCompleteFrozenInterrogation(state, out status);
            return true;
        }

        try
        {
            string message = terminal.message;
            Clear(state);
            state.status = CaptivityStatus.Confined;
            state.lastResult = message;
            status = message;
            return true;
        }
        catch (Exception exception)
        {
            status = terminal.message + " · 작업 종결 재시도 대기 ("
                + exception.Message + ")";
            state.lastResult = status;
            return false;
        }
    }

    private bool TryCommitFrozenOutcome(
        CaptiveState state,
        CharacterActor subject,
        CaptivityInteractionTerminalState terminal,
        out string status)
    {
        status = string.Empty;
        if (terminal.bodyDamageAmount > 0f)
        {
            CharacterVitalsSnapshot current = bodyHealthQuery.GetVitals(subject);
            if (!Approximately(current.CurrentHealth, terminal.bodyHealthBefore)
                || !Approximately(current.MaximumHealth, terminal.bodyMaximumHealth))
            {
                status = "동결 후 포로의 신체 상태가 바뀌어 결과를 커밋할 수 없습니다.";
                return false;
            }
        }
        if (!outcomes.TryPrepare(
                state,
                CurrentAbsoluteDay,
                out PreparedCaptivityInteractionOutcome prepared,
                out long ownerRevision,
                out string prepareFailure))
        {
            status = "포로 상호작용 결과 원장 준비를 재시도합니다: "
                + prepareFailure;
            return false;
        }

        string stateSnapshot = CaptivityStateTransitionRules
            .CaptureStateSnapshot(state);
        CharacterBodyHealthMutationSnapshot bodySnapshot = default;
        bool bodyStaged = false;
        try
        {
            state.will = terminal.willAfter;
            state.fear = terminal.fearAfter;
            state.trust = terminal.trustAfter;
            state.grudge = terminal.grudgeAfter;
            state.corruption = terminal.corruptionAfter;
            state.lastResult = terminal.message;
            if (terminal.success)
                actors.Recalculate(state);

            if (terminal.bodyDamageAmount > 0f)
            {
                bodySnapshot = bodyHealthMutations.CaptureCombatMutation(subject);
                CharacterPreparedAggregateDamageReceipt damage =
                    bodyHealthMutations.ApplyPreparedAggregateDamage(
                        subject,
                        terminal.bodyDamageAmount,
                        CharacterDeathCauseCode.Unknown,
                        $"captivity-interaction:{terminal.interactionId}",
                        allowDeath: true);
                bodyStaged = true;
                if (!damage.IsValid
                    || !Approximately(
                        damage.PreviousHealth,
                        terminal.bodyHealthBefore)
                    || !Approximately(
                        damage.ResultingHealth,
                        terminal.bodyHealthAfter)
                    || !Approximately(
                        damage.MaximumHealth,
                        terminal.bodyMaximumHealth))
                {
                    throw new InvalidOperationException(
                        "Prepared captivity body damage drifted from its frozen receipt.");
                }
            }

            terminal.outcomeRevision = ownerRevision;
            terminal.bodyObserversPending = terminal.bodyDamageAmount > 0f;
            state.interactionOutcomeRevision = ownerRevision;
        }
        catch (Exception exception)
        {
            outcomes.Cancel(prepared);
            if (bodyStaged)
            {
                bodyHealthMutations.RestoreCombatMutation(
                    subject,
                    bodySnapshot,
                    "captivity-interaction-stage-rollback");
            }
            CaptivityStateTransitionRules.RestoreStateSnapshot(
                stateSnapshot,
                state);
            status = "포로 상호작용 결과를 적용하지 못했습니다: "
                + exception.Message;
            return false;
        }

        OwnerOutcomeCommitResult commit = outcomes.Commit(prepared);
        if (commit.DurablyCommitted)
            return true;

        if (bodyStaged)
        {
            bodyHealthMutations.RestoreCombatMutation(
                subject,
                bodySnapshot,
                "captivity-interaction-outcome-rejected");
        }
        CaptivityStateTransitionRules.RestoreStateSnapshot(stateSnapshot, state);
        status = commit.DetailCode.Length == 0
            ? "포로 상호작용 결과 원장 커밋이 거절되었습니다."
            : commit.DetailCode;
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
        state.currentInteractionAttemptId = 0;
        state.interactionTerminal = new CaptivityInteractionTerminalState();
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

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.001f;
}
