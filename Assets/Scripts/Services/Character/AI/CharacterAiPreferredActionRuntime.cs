using System;
using UnityEngine;
using DungeonStory.Foundation;

internal sealed class CharacterAiPreferredActionRuntime
{
    private IGameClock gameClock;
    private float Now => gameClock != null ? gameClock.Time : 0f;
    internal void BindClock(IGameClock clock) => gameClock = clock;
    private AIActionSet preferredNextActionSet;
    private float preferredNextActionUntil = float.NegativeInfinity;
    private WorkTypeId preferredWorkTypeId;
    private float preferredWorkTypeUntil = float.NegativeInfinity;
    private bool preferredActionDeferredPending;
    private long preferredActionDeferredCount;
    private long preferredActionDeferredFallbackSuppressionCount;
    private long preferredActionCommitCount;
    private AIActionFailure lastPreferredActionDeferredFailure;
    private AIActionFailure firstPreferredActionHardFailure;
    private CharacterAiPreferredActionFailureSource
        firstPreferredActionHardFailureSource;
    private long preferredActionHardFailureCount;
    private bool preferredActionHardFailureDecisionPending;
    private Predicate<AIActionSet> preferredActionMatcher;
    private CharacterAiPreferredActionDisposition preferredActionDisposition;
    private CharacterAiBranch preferredActionDispositionBranch;
    private long preferredActionDispositionRevision;

    internal bool IsActionPreferredForNextDecision<TActionSet>()
        where TActionSet : AIActionSet
    {
        return preferredNextActionSet is TActionSet
            && Now <= preferredNextActionUntil;
    }

    internal bool IsBranchPreferredForNextDecision(
        CharacterAiBranch branch)
    {
        return branch != CharacterAiBranch.None
            && preferredNextActionSet != null
            && Now <= preferredNextActionUntil
            && preferredNextActionSet.Branch == branch;
    }

    internal bool IsRoutineGroupPreferredForNextDecision(
        CharacterAiBranch routineGroup)
    {
        CharacterAiBranch preferredBranch = preferredNextActionSet != null
            && Now <= preferredNextActionUntil
                ? preferredNextActionSet.Branch
                : CharacterAiBranch.None;
        return routineGroup switch
        {
            CharacterAiBranch.DutyWork =>
                preferredBranch == CharacterAiBranch.Work,
            CharacterAiBranch.SurvivalNeeds =>
                preferredBranch == CharacterAiBranch.ExitDungeon
                || preferredBranch == CharacterAiBranch.Eat
                || preferredBranch == CharacterAiBranch.Drink
                || preferredBranch == CharacterAiBranch.Rest
                || preferredBranch == CharacterAiBranch.Toilet
                || preferredBranch == CharacterAiBranch.Hygiene,
            CharacterAiBranch.LeisureVisit =>
                preferredBranch == CharacterAiBranch.LeisureVisit
                || preferredBranch == CharacterAiBranch.Shopping
                || preferredBranch == CharacterAiBranch.LookAround,
            CharacterAiBranch.Idle =>
                preferredBranch == CharacterAiBranch.Wait,
            _ => false
        };
    }

    internal bool HasPreferredActionMatching(
        Predicate<AIActionSet> actionGroupMatcher)
    {
        return actionGroupMatcher != null
            && preferredNextActionSet != null
            && Now <= preferredNextActionUntil
            && actionGroupMatcher(preferredNextActionSet);
    }

    internal Predicate<AIActionSet> GetPreferredActionMatcher()
    {
        return preferredActionMatcher ??= MatchesPreferredActionSet;
    }

    private bool MatchesPreferredActionSet(AIActionSet actionSet)
    {
        return actionSet != null
            && preferredNextActionSet != null
            && Now <= preferredNextActionUntil
            && ReferenceEquals(actionSet, preferredNextActionSet);
    }

    internal bool TryRetainPreferredBranchDeferred(
        CharacterAiBranch branch,
        AIActionFailure failure)
    {
        if (!IsBranchPreferredForNextDecision(branch))
        {
            return false;
        }

        if (!failure.IsDeferred)
        {
            return false;
        }

        preferredActionDeferredPending = true;
        preferredActionDeferredCount = checked(
            preferredActionDeferredCount + 1L);
        lastPreferredActionDeferredFailure = failure;
        RecordPreferredActionDisposition(
            CharacterAiPreferredActionDisposition.Deferred,
            branch);
        return true;
    }

    internal bool RetirePreferredBranchAfterHardFailure(
        CharacterAiBranch branch,
        AIActionFailure failure,
        CharacterAiPreferredActionFailureSource source)
    {
        if (!failure.HasFailure
            || failure.IsDeferred
            || !IsBranchPreferredForNextDecision(branch))
        {
            return false;
        }

        preferredActionHardFailureCount = checked(
            preferredActionHardFailureCount + 1L);
        if (!firstPreferredActionHardFailure.HasFailure)
        {
            firstPreferredActionHardFailure = failure;
            firstPreferredActionHardFailureSource = source;
        }
        preferredActionHardFailureDecisionPending = true;

        RecordPreferredActionDisposition(
            CharacterAiPreferredActionDisposition.None,
            branch);
        ClearPreferredAction();
        return true;
    }

    internal bool TryConsumePreferredActionHardFailureDecision(
        out AIActionFailure failure,
        out CharacterAiPreferredActionFailureSource source)
    {
        failure = firstPreferredActionHardFailure;
        source = firstPreferredActionHardFailureSource;
        if (!preferredActionHardFailureDecisionPending)
        {
            return false;
        }

        preferredActionHardFailureDecisionPending = false;
        return true;
    }

    private void RecordPreferredActionDisposition(
        CharacterAiPreferredActionDisposition disposition,
        CharacterAiBranch branch)
    {
        preferredActionDisposition = disposition;
        preferredActionDispositionBranch = branch;
        preferredActionDispositionRevision = checked(
            preferredActionDispositionRevision + 1L);
    }

    internal bool TryGetPreferredWorkType(out WorkTypeId workTypeId)
    {
        workTypeId = preferredWorkTypeId;
        if (!workTypeId.IsValid)
        {
            return false;
        }

        if (Now <= preferredWorkTypeUntil)
        {
            return true;
        }

        ClearPreferredWorkType();
        workTypeId = default;
        return false;
    }

    internal void ConsumePreferredWorkType(WorkTypeId workTypeId)
    {
        if (preferredWorkTypeId == workTypeId)
        {
            ClearPreferredWorkType();
        }
    }

    internal void ClearPreferredAction(bool preserveWorkType = false)
    {
        preferredActionDeferredPending = false;
        preferredNextActionSet = null;
        preferredNextActionUntil = float.NegativeInfinity;
        if (!preserveWorkType)
        {
            ClearPreferredWorkType();
        }
    }

    private void ClearPreferredWorkType()
    {
        preferredWorkTypeId = default;
        preferredWorkTypeUntil = float.NegativeInfinity;
    }

    internal void SetPreferredAction(AIActionSet actionSet, float persistenceSeconds)
    {
        ClearPreferredWorkType();
        preferredActionDeferredPending = false;
        lastPreferredActionDeferredFailure = AIActionFailure.None;
        firstPreferredActionHardFailure = AIActionFailure.None;
        firstPreferredActionHardFailureSource =
            CharacterAiPreferredActionFailureSource.None;
        preferredActionHardFailureDecisionPending = false;
        preferredNextActionSet = actionSet;
        preferredNextActionUntil = preferredNextActionSet != null
            ? Now + Mathf.Max(2f, persistenceSeconds)
            : float.NegativeInfinity;
        RecordPreferredActionDisposition(
            CharacterAiPreferredActionDisposition.None,
            preferredNextActionSet?.Branch ?? CharacterAiBranch.None);
    }

    internal void PreferWorkType(WorkTypeId workTypeId, float persistenceSeconds)
    {
        preferredWorkTypeId = workTypeId;
        preferredWorkTypeUntil = Now + Mathf.Max(2f, persistenceSeconds);
    }

    internal bool IsDeferred => preferredActionDeferredPending && preferredNextActionSet != null && Now <= preferredNextActionUntil;
    internal bool HasPreferredWorkType => preferredWorkTypeId.IsValid && Now <= preferredWorkTypeUntil;
    internal void ClearDeferredPending() => preferredActionDeferredPending = false;
    internal void RecordDeferredFallbackSuppression() => preferredActionDeferredFallbackSuppressionCount = checked(preferredActionDeferredFallbackSuppressionCount + 1L);
    internal void ExpireDefinitionForScoring()
    {
        preferredNextActionSet = null;
        preferredNextActionUntil = float.NegativeInfinity;
    }

    internal void RecordCandidateCommitted(CharacterAiBranch branch)
    {
        preferredActionCommitCount = checked(preferredActionCommitCount + 1L);
        RecordPreferredActionDisposition(CharacterAiPreferredActionDisposition.Selected, branch);
    }

    internal void RecordCommitted(CharacterAiBranch branch)
    {
        preferredActionDeferredPending = false;
        preferredActionCommitCount = checked(preferredActionCommitCount + 1L);
        RecordPreferredActionDisposition(CharacterAiPreferredActionDisposition.Selected, branch);
    }

    internal AIActionSet PreferredNextActionSet => preferredNextActionSet;
    internal float PreferredNextActionUntil => preferredNextActionUntil;
    internal long PreferredActionDeferredCount => preferredActionDeferredCount;
    internal long PreferredActionDeferredFallbackSuppressionCount => preferredActionDeferredFallbackSuppressionCount;
    internal long PreferredActionCommitCount => preferredActionCommitCount;
    internal AIActionFailure LastPreferredActionDeferredFailure => lastPreferredActionDeferredFailure;
    internal AIActionFailure FirstPreferredActionHardFailure => firstPreferredActionHardFailure;
    internal CharacterAiPreferredActionFailureSource FirstPreferredActionHardFailureSource => firstPreferredActionHardFailureSource;
    internal long PreferredActionHardFailureCount => preferredActionHardFailureCount;
    internal bool PreferredActionHardFailureDecisionPending => preferredActionHardFailureDecisionPending;
    internal CharacterAiPreferredActionDisposition PreferredActionDisposition => preferredActionDisposition;
    internal CharacterAiBranch PreferredActionDispositionBranch => preferredActionDispositionBranch;
    internal long PreferredActionDispositionRevision => preferredActionDispositionRevision;
}
