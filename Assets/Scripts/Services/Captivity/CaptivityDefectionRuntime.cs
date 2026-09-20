using System;
using DungeonStory.Foundation;
using UnityEngine;

internal interface ICaptivityPopulationStandingTransition
{
    void Rollback();
    void Complete();
}

internal interface ICaptivityPopulationStandingPort
{
    ICaptivityPopulationStandingTransition Begin(
        CharacterActor actor,
        CharacterSettlementStanding standing);
}

internal sealed class CaptivityPopulationStandingPort :
    ICaptivityPopulationStandingPort
{
    private readonly ICharacterPopulationService population;

    public CaptivityPopulationStandingPort(ICharacterPopulationService population) =>
        this.population = population
            ?? throw new ArgumentNullException(nameof(population));

    public ICaptivityPopulationStandingTransition Begin(
        CharacterActor actor,
        CharacterSettlementStanding standing) => new Transition(
        population,
        population.BeginSettlementStandingTransition(actor, standing));

    private sealed class Transition : ICaptivityPopulationStandingTransition
    {
        private readonly ICharacterPopulationService population;
        private readonly CharacterSettlementStandingTransaction transaction;
        private bool active = true;

        public Transition(
            ICharacterPopulationService population,
            CharacterSettlementStandingTransaction transaction)
        {
            this.population = population;
            this.transaction = transaction ?? throw new ArgumentNullException(
                nameof(transaction));
        }

        public void Rollback()
        {
            if (!active)
                return;
            population.RollbackSettlementStandingTransition(transaction);
            active = false;
        }

        public void Complete()
        {
            if (!active)
                throw new InvalidOperationException(
                    "Captivity population transition is already finished.");
            population.CompleteSettlementStandingTransition(transaction);
            active = false;
        }
    }
}

internal sealed class CaptivityDefectionRuntime
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityActorRuntimeLookup actorRuntime;
    private readonly IRandomStream random;
    private readonly IDoorAccessSubjectRegistry doorSubjectRegistry;
    private readonly ICaptivityPopulationStandingPort population;
    private readonly IEmploymentStandingCommand employmentStanding;
    private readonly IGameClock gameClock;
    private readonly CaptiveEscapeOutcomeRuntime outcomes;

    public CaptivityDefectionRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        IRandomStream random,
        IDoorAccessSubjectRegistry doorSubjectRegistry,
        ICaptivityPopulationStandingPort population,
        IEmploymentStandingCommand employmentStanding,
        IGameClock gameClock,
        CaptiveEscapeOutcomeRuntime outcomes)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        this.doorSubjectRegistry = doorSubjectRegistry
            ?? throw new ArgumentNullException(nameof(doorSubjectRegistry));
        this.population = population ?? throw new ArgumentNullException(nameof(population));
        this.employmentStanding = employmentStanding
            ?? throw new ArgumentNullException(nameof(employmentStanding));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public bool TryTriggerBetrayal(
        string captiveId,
        string trigger,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = actors.FindState(captiveId);
        CharacterActor actor = actorRuntime.Find(captiveId);
        if (state == null || actor == null || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }
        if (!state.falseCompliance)
        {
            failureReason = "거짓 복종 상태가 아닙니다.";
            return false;
        }
        if (state.status is CaptivityStatus.Escorting
            or CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort
            or CaptivityStatus.Interaction
            or CaptivityStatus.EscapeAttempt)
        {
            failureReason = "현재 상태에서는 배신 행동을 시작할 수 없습니다.";
            return false;
        }

        float betrayalChance = Mathf.Clamp01(
            0.25f
            + state.grudge * 0.005f
            + state.escapeRisk * 0.003f);
        if (!random.Chance(betrayalChance))
        {
            failureReason = "배신할 기회를 엿보고 있습니다.";
            return false;
        }

        string normalizedTrigger = string.IsNullOrWhiteSpace(trigger)
            ? "기회 포착"
            : trigger.Trim();
        CaptivityStatus previousStatus = state.status;
        float resultingPressure = ClampStat(
            state.retaliationPressure + 20f + state.grudge * 0.25f);
        if (!outcomes.TryPrepare(
                state,
                CaptiveEscapeOutcomeKind.FalseComplianceBetrayal,
                normalizedTrigger,
                previousStatus,
                resultingPressure,
                CurrentAbsoluteDay,
                out PreparedCaptiveEscapeOutcome prepared,
                out long ownerRevision,
                out normalizedTrigger,
                out failureReason))
        {
            return false;
        }

        string stateSnapshot =
            CaptivityStateTransitionRules.CaptureStateSnapshot(state);
        CharacterType previousActorType = actor.characterType;
        CharacterLifecycleState previousLifecycle = actor.CurrentLifecycleState;
        bool previousAiPaused = actor.IsAiPaused();
        bool previousDoorCaptive = state.IsInCustody;
        try
        {
            state.status = CaptivityStatus.Escaped;
            state.restrained = false;
            state.betrayalTrigger = normalizedTrigger;
            state.retaliationPressure = resultingPressure;
            state.lastResult = $"{normalizedTrigger} 중 복종을 깨고 배신";
            state.escapeOutcomeRevision = ownerRevision;
            actor.characterType = CharacterType.Intruder;
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.SetAiPaused(false);
            doorSubjectRegistry.SetCaptive(state.captiveId, false);
        }
        catch (Exception exception)
        {
            outcomes.Cancel(prepared);
            RestoreBetrayal(
                state,
                stateSnapshot,
                actor,
                previousActorType,
                previousLifecycle,
                previousAiPaused,
                previousDoorCaptive);
            failureReason = "포로 배신 상태를 적용하지 못했습니다: "
                + exception.Message;
            return false;
        }

        OwnerOutcomeCommitResult commit = outcomes.Commit(prepared);
        if (!commit.DurablyCommitted)
        {
            RestoreBetrayal(
                state,
                stateSnapshot,
                actor,
                previousActorType,
                previousLifecycle,
                previousAiPaused,
                previousDoorCaptive);
            failureReason = commit.DetailCode.Length == 0
                ? "포로 배신 원장 커밋이 거절되었습니다."
                : commit.DetailCode;
            return false;
        }

        RequestReplan(actor, "포로 배신");
        if (!prepared.IsReplay)
            outcomes.NotifyCommitted(state, normalizedTrigger, betrayal: true);
        return true;
    }

    public bool TryBreakMinionControl(
        string minionId,
        string reason,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = actors.FindState(minionId);
        CharacterActor actor = actorRuntime.Find(minionId);
        if (state?.IsMinion != true || actor == null)
        {
            failureReason = "통제 이탈 대상인 하수인을 찾을 수 없습니다.";
            return false;
        }

        string normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "통제에서 벗어남"
            : reason.Trim();
        if (!outcomes.TryPrepare(
                state,
                CaptiveEscapeOutcomeKind.MinionControlBreak,
                normalizedReason,
                state.status,
                state.retaliationPressure,
                CurrentAbsoluteDay,
                out PreparedCaptiveEscapeOutcome prepared,
                out long ownerRevision,
                out normalizedReason,
                out failureReason))
        {
            return false;
        }

        string captivityStateSnapshot =
            CaptivityStateTransitionRules.CaptureStateSnapshot(state);
        CharacterType previousActorType = actor.characterType;
        CharacterType previousIdentityType = actor.Identity?.CharacterType
            ?? previousActorType;
        CharacterLifecycleState previousLifecycle = actor.CurrentLifecycleState;
        bool previousAiPaused = actor.IsAiPaused();
        bool previousDoorCaptive = state.IsInCustody;
        EmploymentStandingState employmentSnapshot =
            employmentStanding.CaptureStandingState(state.captiveId);
        ICaptivityPopulationStandingTransition populationTransition = null;
        try
        {
            populationTransition = population.Begin(
                actor,
                CharacterSettlementStanding.PreparedCandidate);
            employmentStanding.ApplyStanding(
                state.captiveId,
                CharacterSettlementStanding.PreparedCandidate);
            actor.characterType = CharacterType.Intruder;
            actor.Identity?.SetCharacterType(CharacterType.Intruder);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.SetAiPaused(false);
            doorSubjectRegistry.SetCaptive(state.captiveId, false);
            CaptivityStateTransitionRules.ClearRehabilitationState(state);
            state.status = CaptivityStatus.Escaped;
            state.lastResult = normalizedReason;
            state.betrayalTrigger = normalizedReason;
            state.escapeOutcomeRevision = ownerRevision;
        }
        catch (Exception exception)
        {
            outcomes.Cancel(prepared);
            RestoreMinionControlBreak(
                state,
                captivityStateSnapshot,
                actor,
                previousActorType,
                previousIdentityType,
                previousLifecycle,
                previousAiPaused,
                previousDoorCaptive,
                employmentSnapshot,
                populationTransition);
            failureReason = "통제 이탈 상태를 적용하지 못했습니다: "
                + exception.Message;
            return false;
        }

        OwnerOutcomeCommitResult commit = outcomes.Commit(prepared);
        if (!commit.DurablyCommitted)
        {
            RestoreMinionControlBreak(
                state,
                captivityStateSnapshot,
                actor,
                previousActorType,
                previousIdentityType,
                previousLifecycle,
                previousAiPaused,
                previousDoorCaptive,
                employmentSnapshot,
                populationTransition);
            failureReason = commit.DetailCode.Length == 0
                ? "하수인 통제 이탈 원장 커밋이 거절되었습니다."
                : commit.DetailCode;
            return false;
        }

        populationTransition.Complete();
        RequestReplan(actor, "하수인 통제 이탈");
        if (!prepared.IsReplay)
            outcomes.NotifyCommitted(state, normalizedReason, betrayal: true);
        return true;
    }

    private void RestoreBetrayal(
        CaptiveState state,
        string stateSnapshot,
        CharacterActor actor,
        CharacterType previousActorType,
        CharacterLifecycleState previousLifecycle,
        bool previousAiPaused,
        bool previousDoorCaptive)
    {
        CaptivityStateTransitionRules.RestoreStateSnapshot(stateSnapshot, state);
        actor.characterType = previousActorType;
        actor.SetLifecycleState(previousLifecycle);
        actor.SetAiPaused(previousAiPaused);
        doorSubjectRegistry.SetCaptive(state.captiveId, previousDoorCaptive);
    }

    private void RestoreMinionControlBreak(
        CaptiveState state,
        string captivityStateSnapshot,
        CharacterActor actor,
        CharacterType previousActorType,
        CharacterType previousIdentityType,
        CharacterLifecycleState previousLifecycle,
        bool previousAiPaused,
        bool previousDoorCaptive,
        EmploymentStandingState employmentSnapshot,
        ICaptivityPopulationStandingTransition populationTransition)
    {
        CaptivityStateTransitionRules.RestoreStateSnapshot(
            captivityStateSnapshot,
            state);
        employmentStanding.RestoreStandingState(employmentSnapshot);
        populationTransition?.Rollback();
        actor.characterType = previousActorType;
        actor.Identity?.SetCharacterType(previousIdentityType);
        actor.SetLifecycleState(previousLifecycle);
        actor.SetAiPaused(previousAiPaused);
        doorSubjectRegistry.SetCaptive(state.captiveId, previousDoorCaptive);
    }

    private static void RequestReplan(CharacterActor actor, string outcome)
    {
        try
        {
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"확정된 {outcome}의 AI 재계획 관찰자가 실패했습니다: "
                + exception);
        }
    }

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private static float ClampStat(float value) => Mathf.Clamp(value, 0f, 100f);
}
