using System;
using System.Collections;
using DungeonStory.Foundation;
using static DefenseRangedSupportAccess;
using UnityEngine;

internal sealed class DefenseEngagementCombatRuntime
{
    private readonly IGridSystemProvider gridProvider;
    private readonly IDefenseCombatExecutor combatExecutor;
    private readonly IDefenseTacticalCoordinator tacticalCoordinator;
    private readonly IDefenseEngagementStore store;
    private readonly IGameEventBus events;
    private readonly IGameClock clock;
    private readonly DefenseGuardControlRuntime guardControl;

    public DefenseEngagementCombatRuntime(
        DefenseEngagementWorldServices world,
        DefenseEngagementCombatServices combat,
        DefenseGuardControlRuntime guardControl)
    {
        DefenseEngagementWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        DefenseEngagementCombatServices requiredCombat = combat
            ?? throw new ArgumentNullException(nameof(combat));
        gridProvider = requiredWorld.Grid;
        events = requiredWorld.Events;
        clock = requiredWorld.Clock;
        combatExecutor = requiredCombat.Executor;
        tacticalCoordinator = requiredCombat.Tactics;
        store = requiredCombat.Store;
        this.guardControl = guardControl
            ?? throw new ArgumentNullException(nameof(guardControl));
    }

    public void Begin(DefenseEngagement engagement)
    {
        if (engagement == null
            || engagement.State == DefenseEngagementState.Engaged
            || engagement.LeadGuard == null
            || engagement.LeadGuard.IsDead
            || engagement.IntruderActor == null
            || engagement.IntruderActor.IsDead
            || engagement.LeadGuard.GetNowXY() != engagement.GuardCell
            || engagement.IntruderActor.GetNowXY() != engagement.IntruderStopCell)
        {
            return;
        }

        engagement.State = DefenseEngagementState.Engaged;
        engagement.StatusText = engagement.IsOwnerFinalDefense
            ? "Owner final defense"
            : "Engaged";
        engagement.NextGuardAttackAt = clock.Time
            + combatExecutor.GetAttackInterval(engagement.LeadGuard, 1f);
        engagement.NextIntruderAttackAt = clock.Time
            + combatExecutor.GetAttackInterval(
                engagement.IntruderActor,
                engagement.Intruder.AttackSpeedMultiplier);
        FaceOpponents(engagement.LeadGuard, engagement.IntruderActor);
        SetCombatPresentation(engagement, true);
        SetStatus(engagement.ReserveGuard, "Waiting", false);
        engagement.Intruder.SetEngagementState(true, engagement.IntruderStopCell);
        engagement.LeadGuard.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Started,
            $"Defend against {engagement.IntruderActor.Identity?.DisplayName ?? "intruder"}",
            actionId: "defense:engagement",
            targetId: GetPersistentId(engagement.IntruderActor),
            targetName: engagement.IntruderActor.Identity?.DisplayName
                ?? engagement.IntruderActor.name,
            sentiment: -0.1f,
            bubbleEligible: true));
        TriggerPassives(
            engagement.LeadGuard,
            CharacterSkillTrigger.BattleStarted,
            engagement,
            engagement.IntruderActor,
            "guard");
        TriggerPassives(
            engagement.IntruderActor,
            CharacterSkillTrigger.BattleStarted,
            engagement,
            engagement.LeadGuard,
            "intruder");
    }

    public void TickExchange(
        DefenseEngagement engagement,
        DefenseGuardMovementStarter startMovement)
    {
        if (clock.Time >= engagement.NextGuardAttackAt)
        {
            DefenseCombatExecutionResult guardAttack = combatExecutor.ExecuteMelee(
                engagement,
                engagement.LeadGuard,
                engagement.IntruderActor,
                1f,
                attackerIsGuard: true);
            if (guardAttack.DefenderDefeated
                || engagement.IntruderActor == null
                || engagement.IntruderActor.IsDead)
            {
                ResolveIntruderDefeated(engagement);
                return;
            }

            engagement.NextGuardAttackAt = clock.Time
                + combatExecutor.GetAttackInterval(engagement.LeadGuard, 1f);
        }

        if (clock.Time < engagement.NextIntruderAttackAt)
        {
            return;
        }

        DefenseCombatExecutionResult intruderAttack = combatExecutor.ExecuteMelee(
            engagement,
            engagement.IntruderActor,
            engagement.LeadGuard,
            engagement.Intruder.MeleeDamageMultiplier,
            attackerIsGuard: false);
        if (intruderAttack.DefenderDefeated
            || engagement.LeadGuard == null
            || engagement.LeadGuard.IsDead)
        {
            HandleLeadLost(engagement, "Lead guard down", startMovement);
            return;
        }

        engagement.NextIntruderAttackAt = clock.Time
            + combatExecutor.GetAttackInterval(
                engagement.IntruderActor,
                engagement.Intruder.AttackSpeedMultiplier);
    }

    public void BeginGuardSwitch(DefenseEngagement engagement)
    {
        if (engagement == null
            || engagement.State == DefenseEngagementState.Switching
            || engagement.LeadGuard == null
            || engagement.ReserveGuard == null
            || !engagement.ReserveArrived)
        {
            return;
        }

        engagement.State = DefenseEngagementState.Switching;
        engagement.StatusText = "Switching guards";
        SetStatus(engagement.LeadGuard, string.Empty, true);
        SetStatus(engagement.ReserveGuard, "Switching", true);
        engagement.LeadGuard.StartCoroutine(RunGuardSwitch(engagement));
    }

    public void HandleLeadLost(
        DefenseEngagement engagement,
        string reason,
        DefenseGuardMovementStarter startMovement)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        SetEngaged(engagement.LeadGuard, false);
        guardControl.Release(engagement.LeadGuard, engagement.LeadMovement, true);
        if (engagement.ReserveGuard != null
            && !engagement.ReserveGuard.IsDead
            && engagement.ReserveArrived
            && startMovement != null)
        {
            CharacterActor promoted = engagement.ReserveGuard;
            engagement.LeadGuard = promoted;
            engagement.ReserveGuard = null;
            engagement.ReserveArrived = false;
            engagement.ReserveMovement = null;
            engagement.State = DefenseEngagementState.Switching;
            engagement.StatusText = "Promoting reserve guard";
            SetStatus(promoted, "Promoting", true);
            startMovement(
                gridProvider.Grid,
                engagement,
                promoted,
                engagement.GuardCell,
                false,
                null);
            return;
        }

        CollapseFront(engagement, reason);
    }

    public void CollapseFront(DefenseEngagement engagement, string reason)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        engagement.State = DefenseEngagementState.FrontCollapsed;
        engagement.StatusText = reason;
        engagement.Intruder?.SetFrontBrokenState();
        events.Publish(new DefenseFrontCollapsedEvent(engagement, reason));
        CharacterActor releasedLead = engagement.LeadGuard;
        CharacterActor releasedReserve = engagement.ReserveGuard;
        SetCombatPresentation(engagement, false);
        guardControl.Release(engagement.LeadGuard, engagement.LeadMovement, true);
        guardControl.Release(engagement.ReserveGuard, engagement.ReserveMovement, true);
        ShowRetreat(releasedLead);
        ShowRetreat(releasedReserve);
        engagement.LeadGuard = null;
        engagement.ReserveGuard = null;
        engagement.State = DefenseEngagementState.Completed;
        store.Remove(engagement);
    }

    public void ResolveOwnerDefeated(DefenseEngagement engagement)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        CharacterActor owner = engagement.LeadGuard;
        InvasionIntruderRuntime intruder = engagement.Intruder;
        Complete(engagement, false);
        intruder?.ResolveDefenseFailed(owner);
    }

    public void ResolveIntruderDefeated(DefenseEngagement engagement)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        CharacterActor victor = engagement.LeadGuard;
        combatExecutor.AwardEncounterCompletion(
            engagement,
            engagement.LeadGuard,
            BuiltInCharacterProficiencyIds.MeleeCombat);
        combatExecutor.AwardEncounterCompletion(
            engagement,
            engagement.RangedGuard,
            BuiltInCharacterProficiencyIds.RangedCombat);
        combatExecutor.AwardEncounterCompletion(
            engagement,
            engagement.SecondaryRangedGuard,
            BuiltInCharacterProficiencyIds.RangedCombat);
        if (victor != null && !victor.IsDead)
        {
            TriggerPassives(
                victor,
                CharacterSkillTrigger.EnemyDefeated,
                engagement,
                engagement.IntruderActor,
                "victory");
            TriggerPassives(
                victor,
                CharacterSkillTrigger.BattleCompleted,
                engagement,
                engagement.IntruderActor,
                "complete");
            victor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Completed,
                "Intruder defense completed",
                actionId: "defense:engagement",
                targetName: engagement.IntruderActor?.Identity?.DisplayName ?? "intruder",
                value: engagement.ExchangeCount,
                sentiment: 0.7f,
                bubbleEligible: true));
        }

        InvasionIntruderRuntime intruder = engagement.Intruder;
        Complete(engagement, false);
        intruder?.ResolveSuppressedBy(victor);
    }

    public void Complete(DefenseEngagement engagement, bool releaseIntruder)
    {
        if (engagement == null)
        {
            return;
        }

        engagement.State = DefenseEngagementState.Completed;
        SetCombatPresentation(engagement, false);
        SetEngaged(engagement.RangedGuard, false);
        SetEngaged(engagement.SecondaryRangedGuard, false);
        guardControl.Release(engagement.LeadGuard, engagement.LeadMovement, true);
        guardControl.Release(engagement.ReserveGuard, engagement.ReserveMovement, true);
        guardControl.Release(engagement.RangedGuard, engagement.RangedMovement, true);
        guardControl.Release(
            engagement.SecondaryRangedGuard,
            engagement.SecondaryRangedMovement,
            true);
        tacticalCoordinator.Release(GetPersistentId(engagement.RangedGuard));
        tacticalCoordinator.Release(GetPersistentId(engagement.SecondaryRangedGuard));
        if (releaseIntruder)
        {
            engagement.Intruder?.SetEngagementState(false);
        }

        store.Remove(engagement);
    }

    public void ReleaseRangedGuard(
        DefenseEngagement engagement,
        string reason,
        bool secondary)
    {
        if (engagement == null)
        {
            return;
        }

        CharacterActor guard = GetRangedGuard(engagement, secondary);
        tacticalCoordinator.Release(GetPersistentId(guard));
        SetEngaged(guard, false);
        guardControl.Release(guard, GetRangedMovement(engagement, secondary), true);
        SetRangedGuard(engagement, secondary, null);
        SetRangedMovement(engagement, secondary, null);
        SetRangedArrived(engagement, secondary, false);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            engagement.StatusText = reason;
        }
    }

    public void MarkRetreated(CharacterActor guard)
    {
        string id = GetPersistentId(guard);
        if (!string.IsNullOrWhiteSpace(id))
        {
            store.MarkRetreated(id);
        }
    }

    private IEnumerator RunGuardSwitch(DefenseEngagement engagement)
    {
        CharacterActor oldLead = engagement.LeadGuard;
        CharacterActor newLead = engagement.ReserveGuard;
        Vector3 oldStart = oldLead.transform.position;
        Vector3 newStart = newLead.transform.position;
        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration
            && oldLead != null
            && !oldLead.IsDead
            && newLead != null
            && !newLead.IsDead)
        {
            float t = elapsed / duration;
            oldLead.transform.position = Vector3.Lerp(oldStart, newStart, t);
            newLead.transform.position = Vector3.Lerp(newStart, oldStart, t);
            elapsed += clock.DeltaTime;
            yield return null;
        }

        if (newLead == null || newLead.IsDead)
        {
            CollapseFront(engagement, "Reserve guard down");
            yield break;
        }

        oldLead.transform.position = newStart;
        newLead.transform.position = oldStart;
        SetEngaged(oldLead, false);
        MarkRetreated(oldLead);
        guardControl.Release(oldLead, null, true);
        DefenseCombatPresentation.Ensure(oldLead)?.ShowTemporaryStatus("Retreating", 1.5f);
        engagement.LeadGuard = newLead;
        engagement.ReserveGuard = null;
        engagement.LeadArrived = true;
        engagement.ReserveArrived = false;
        engagement.ReserveMovement = null;
        engagement.State = DefenseEngagementState.Engaged;
        engagement.StatusText = "Guard switch complete";
        engagement.NextGuardAttackAt = clock.Time + 0.15f;
        FaceOpponents(newLead, engagement.IntruderActor);
        SetCombatPresentation(engagement, true);
    }

    private static void TriggerPassives(
        CharacterActor actor,
        CharacterSkillTrigger trigger,
        DefenseEngagement engagement,
        CharacterActor counterpart,
        string eventId)
    {
        if (actor == null)
        {
            return;
        }

        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(
            new CharacterSkillExecutionContext(
                actor,
                trigger,
                $"defense:{eventId}:{engagement?.Id}",
                targetActor: counterpart));
    }

    private static void FaceOpponents(CharacterActor first, CharacterActor second)
    {
        if (first == null || second == null)
        {
            return;
        }

        first.Flip(second.GetNowXY().x >= first.GetNowXY().x
            ? CharacterFacing.RIGHT
            : CharacterFacing.LEFT);
        second.Flip(first.GetNowXY().x >= second.GetNowXY().x
            ? CharacterFacing.RIGHT
            : CharacterFacing.LEFT);
    }

    private static void SetCombatPresentation(DefenseEngagement engagement, bool engaged)
    {
        if (engagement == null)
        {
            return;
        }

        SetStatus(engagement.LeadGuard, engaged ? "Engaged" : string.Empty, engaged);
        SetStatus(engagement.IntruderActor, string.Empty, engaged);
        if (!engaged)
        {
            SetEngaged(engagement.ReserveGuard, false);
        }
    }

    private static void SetStatus(CharacterActor actor, string status, bool engaged)
    {
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(status, engaged);
    }

    private static void SetEngaged(CharacterActor actor, bool engaged)
    {
        DefenseCombatPresentation.Ensure(actor)?.SetEngaged(engaged);
    }

    private static void ShowRetreat(CharacterActor actor)
    {
        if (actor != null && !actor.IsDead)
        {
            DefenseCombatPresentation.Ensure(actor)?.ShowTemporaryStatus("Retreating", 1.5f);
        }
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId ?? string.Empty;
    }
}
