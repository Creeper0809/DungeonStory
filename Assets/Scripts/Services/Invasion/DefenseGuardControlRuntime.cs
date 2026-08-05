using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class DefenseGuardControlRuntime
{
    private readonly Dictionary<string, bool> pauseStateBeforeDefense =
        new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterActor> controlledGuards =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);

    public void Prepare(CharacterActor guard, string activity)
    {
        if (guard == null || guard.IsDead)
        {
            return;
        }

        guard.GetAbility<AbilityWork>()?.ReleaseAssignedWorkTarget();
        guard.GetAbility<AbilityMove>()?.CancelActiveMovement();
        guard.Brain?.RequestImmediateReplan(clearFailures: false);
        if (!guard.IsOwner)
        {
            string guardId = GetPersistentId(guard);
            if (!pauseStateBeforeDefense.ContainsKey(guardId))
            {
                pauseStateBeforeDefense[guardId] = guard.IsAiPaused();
            }

            controlledGuards[guardId] = guard;
        }

        guard.SetAiPaused(true);
        DefenseCombatPresentation.Ensure(guard)?.SetStatus(activity, false);
        guard.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Started,
            activity,
            actionId: "defense:dispatch",
            sentiment: -0.05f));
    }

    public void Release(CharacterActor guard, Coroutine movement, bool resumeAi)
    {
        if (guard == null)
        {
            return;
        }

        if (movement != null)
        {
            guard.StopCoroutine(movement);
        }

        guard.GetAbility<AbilityMove>()?.CancelActiveMovement();
        if (!resumeAi || guard.IsDead || guard.IsOwner)
        {
            return;
        }

        string guardId = GetPersistentId(guard);
        bool previousPause = pauseStateBeforeDefense.TryGetValue(
            guardId,
            out bool storedPause)
            && storedPause;
        pauseStateBeforeDefense.Remove(guardId);
        controlledGuards.Remove(guardId);
        guard.SetAiPaused(previousPause);
        guard.Brain?.RequestImmediateReplan(clearFailures: false);
    }

    public void ReleaseOrphans(
        bool releaseAll,
        Func<CharacterActor, bool> isAssigned)
    {
        foreach (KeyValuePair<string, CharacterActor> pair in controlledGuards.ToArray())
        {
            CharacterActor guard = pair.Value;
            if (!releaseAll && guard != null && isAssigned(guard))
            {
                continue;
            }

            bool previousPause = pauseStateBeforeDefense.TryGetValue(
                pair.Key,
                out bool storedPause)
                && storedPause;
            pauseStateBeforeDefense.Remove(pair.Key);
            controlledGuards.Remove(pair.Key);
            if (guard == null || guard.IsDead || guard.IsOwner)
            {
                continue;
            }

            guard.GetAbility<AbilityMove>()?.CancelActiveMovement();
            guard.SetAiPaused(previousPause);
            guard.Brain?.RequestImmediateReplan(clearFailures: false);
        }
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId ?? string.Empty;
    }
}
