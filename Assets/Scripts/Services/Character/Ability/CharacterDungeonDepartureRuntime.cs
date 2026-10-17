using System;

internal sealed class CharacterDungeonDepartureRuntime
{
    private const string CaptiveOwnedExitFailureReason =
        "exit-dungeon-captive-owned";
    private const string ExitCaptivityAuthorityMissingFailureReason =
        "exit-dungeon-captivity-authority-missing";
    private const string ExitCharacterIdentityMissingFailureReason =
        "exit-dungeon-character-identity-missing";
    private readonly CharacterMovementOperationRuntime movement;
    private ICaptivityRuntime captivityRuntime;
    private ICharacterVisitorExitOutcomeRuntime visitorExitOutcomes;
    internal CharacterDungeonDepartureRuntime(CharacterMovementOperationRuntime movement)
    {
        this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
    }
    internal void ConstructExitDungeonAdmission(ICaptivityRuntime captivityRuntime)
    {
        this.captivityRuntime = captivityRuntime
            ?? throw new ArgumentNullException(nameof(captivityRuntime));
    }

    internal void ConstructVisitorExitOutcome(
        ICharacterVisitorExitOutcomeRuntime visitorExitOutcomes)
    {
        this.visitorExitOutcomes = visitorExitOutcomes
            ?? throw new ArgumentNullException(nameof(visitorExitOutcomes));
    }

    internal bool CanStartExitDungeon(CharacterActor actor, out AIActionFailure failure)
    {
        string persistentId = actor?.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Unsupported,
                ExitCharacterIdentityMissingFailureReason);
            return false;
        }

        if (captivityRuntime == null)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Unsupported,
                ExitCaptivityAuthorityMissingFailureReason);
            return false;
        }

        if (captivityRuntime.IsCaptive(persistentId))
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.CannotStart,
                CaptiveOwnedExitFailureReason);
            return false;
        }

        failure = AIActionFailure.None;
        return true;
    }

    internal void FailExitAction(CharacterActor actor,
        AIAction expectedAction,
        AIActionFailureKind kind,
        string detail)
    {
        if (expectedAction == null || actor?.Brain == null)
        {
            return;
        }
        actor.Brain.ReportRuntimeActionFailure(
            AIActionFailure.Create(kind, detail, expectedAction.destination),
            requestImmediateReplan: false);
        actor.Brain.EndExpectedAction(
            expectedAction,
            CharacterAiActionTerminalKind.Failed,
            clearFailures: false);
    }

    internal void RejectExitDungeonAdmission(CharacterActor actor,
        AIAction expectedAction,
        AIActionFailure failure)
    {
        if (!failure.HasFailure || actor?.Brain == null)
        {
            return;
        }

        actor.Brain.ReportRuntimeActionFailure(
            failure,
            requestImmediateReplan: false);
        if (expectedAction != null)
        {
            actor.Brain.EndExpectedAction(
                expectedAction,
                CharacterAiActionTerminalKind.Failed,
                clearFailures: false);
        }
    }

    internal bool TryBeginExit(CharacterActor actor, AIAction expectedAction, bool allowWorker)
    {
        if (!CanStartExitDungeon(actor, out AIActionFailure admissionFailure))
        {
            RejectExitDungeonAdmission(actor, expectedAction, admissionFailure);
            return false;
        }

        if (!allowWorker
            && CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
            && !work.IsOffDuty)
        {
            actor.Brain?.ClearPathSearchCache();
            return false;
        }

        if (!allowWorker && actor.characterType == CharacterType.Customer)
        {
            ICharacterVisitorExitOutcomeRuntime exitOutcomes =
                visitorExitOutcomes
                ?? throw new InvalidOperationException(
                    "AbilityMove requires "
                    + $"{nameof(ICharacterVisitorExitOutcomeRuntime)} injection.");
            if (!exitOutcomes.BeginExit(
                    actor,
                    "ai.exit-dungeon",
                    out string beginFailure))
            {
                FailExitAction(
                    actor, expectedAction,
                    AIActionFailureKind.Unsupported,
                    beginFailure);
                return false;
            }
        }

        return true;
    }
    internal void RecoverFailedExit(CharacterActor actor, AIAction expectedAction,
        AIActionFailureKind kind, VisitorDepartureTerminalKind terminal, string reason)
    {
        if (actor != null && actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon)
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        movement.ClearSystemDestination();
        visitorExitOutcomes?.TryFailExit(actor, terminal, reason);
        FailExitAction(actor, expectedAction, kind, reason);
    }
    internal void RejectFinalAdmission(CharacterActor actor, AIAction expectedAction, AIActionFailure failure)
    {
        if (actor != null && actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon)
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        movement.ClearSystemDestination();
        visitorExitOutcomes?.TryFailExit(actor, VisitorDepartureTerminalKind.CaptiveBlocked, failure.Reason);
        RejectExitDungeonAdmission(actor, expectedAction, failure);
    }
    internal void FailPathStarvation(CharacterActor actor, AIAction expectedAction, int pathSearchDeferrals)
    {
        FailExitAction(actor, expectedAction, AIActionFailureKind.PathSearchStarved,
            $"exit path search remained deferred for {pathSearchDeferrals} attempts");
        visitorExitOutcomes?.TryFailExit(actor, VisitorDepartureTerminalKind.PathUnavailable,
            "exit-dungeon-path-search-starved");
        movement.ClearSystemDestination();
    }
    internal bool AbortFailedDoorExit(CharacterActor actor, AIAction expectedAction, GridMoveFailureReason lastFailure)
    {
        if (lastFailure == GridMoveFailureReason.None) return false;
        if (actor != null && actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon)
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        movement.ClearSystemDestination();
        visitorExitOutcomes?.TryFailExit(
            actor,
            VisitorDepartureTerminalKind.DoorPassageFailed,
            "exit-dungeon-world-passage-" + lastFailure);
        FailExitAction(actor, expectedAction, AIActionFailureKind.NoPath, "exit-dungeon-world-passage-" + lastFailure);
        return true;
    }
}
